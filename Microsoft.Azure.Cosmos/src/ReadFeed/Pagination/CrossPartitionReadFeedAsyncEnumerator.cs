// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal sealed class CrossPartitionReadFeedAsyncEnumerator : IAsyncEnumerator<TryCatch<ReadFeedPage>>
    {
        private readonly CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> crossPartitionEnumerator;
        private CancellationToken cancellationToken;

        private CrossPartitionReadFeedAsyncEnumerator(
            CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> crossPartitionEnumerator,
            CancellationToken cancellationToken)
        {
            this.crossPartitionEnumerator = crossPartitionEnumerator ?? throw new ArgumentNullException(nameof(crossPartitionEnumerator));
            this.cancellationToken = cancellationToken;
        }

        public TryCatch<ReadFeedPage> Current { get; set; }

        public ValueTask<bool> MoveNextAsync()
        {
            return this.MoveNextAsync(NoOpTrace.Singleton);
        }

        public async ValueTask<bool> MoveNextAsync(ITrace trace)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            this.cancellationToken.ThrowIfCancellationRequested();

            using (ITrace moveNextAsyncTrace = trace.StartChild(name: nameof(MoveNextAsync), component: TraceComponent.ReadFeed, level: TraceLevel.Info))
            {
                if (!await this.crossPartitionEnumerator.MoveNextAsync())
                {
                    this.Current = default;
                    return false;
                }

                TryCatch<CrossPartitionPage<ReadFeedPage, ReadFeedState>> monadicCrossPartitionPage = this.crossPartitionEnumerator.Current;
                if (monadicCrossPartitionPage.Failed)
                {
                    this.Current = TryCatch<ReadFeedPage>.FromException(monadicCrossPartitionPage.Exception);
                    return true;
                }

                CrossPartitionPage<ReadFeedPage, ReadFeedState> crossPartitionPage = monadicCrossPartitionPage.Result;
                ReadFeedPage backendPage = crossPartitionPage.Page;
                CrossPartitionState<ReadFeedState> crossPartitionState = crossPartitionPage.State;
                ReadFeedState state;
                if (crossPartitionState != null)
                {
                    IReadOnlyList<(FeedRangeInternal, ReadFeedState)> rangesAndStates = crossPartitionState.Value;
                    List<CosmosElement> changeFeedContinuationTokens = new List<CosmosElement>();
                    foreach ((FeedRangeInternal range, ReadFeedState readFeedState) in rangesAndStates)
                    {
                        this.cancellationToken.ThrowIfCancellationRequested();
                        ReadFeedContinuationToken readFeedContinuationToken = new ReadFeedContinuationToken(
                                range,
                                readFeedState);

                        CosmosElement cosmosElementChangeFeedContinuationToken = ReadFeedContinuationToken.ToCosmosElement(readFeedContinuationToken);

                        changeFeedContinuationTokens.Add(cosmosElementChangeFeedContinuationToken);
                    }

                    CosmosArray cosmosElementTokens = CosmosArray.Create(changeFeedContinuationTokens);
                    state = new ReadFeedState(cosmosElementTokens);
                }
                else
                {
                    state = null;
                }

                ReadFeedPage compositePage = new ReadFeedPage(backendPage.Content, backendPage.RequestCharge, backendPage.ActivityId, state);

                this.Current = TryCatch<ReadFeedPage>.FromResult(compositePage);
                return true;
            }
        }

        public ValueTask DisposeAsync() => this.crossPartitionEnumerator.DisposeAsync();

        public static TryCatch<CrossPartitionReadFeedAsyncEnumerator> MonadicCreate(
            IDocumentContainer documentContainer,
            QueryRequestOptions queryRequestOptions,
            string continuationToken,
            int pageSize,
            CancellationToken cancellationToken)
        {
            if (documentContainer == null)
            {
                throw new ArgumentNullException(nameof(documentContainer));
            }

            TryCatch<CrossPartitionState<ReadFeedState>> monadicCrossPartitionState = MonadicParseCrossPartitionState(continuationToken);
            if (monadicCrossPartitionState.Failed)
            {
                return TryCatch<CrossPartitionReadFeedAsyncEnumerator>.FromException(monadicCrossPartitionState.Exception);
            }

            RntbdConstants.RntdbEnumerationDirection rntdbEnumerationDirection = RntbdConstants.RntdbEnumerationDirection.Forward;
            if ((queryRequestOptions?.Properties != null) && queryRequestOptions.Properties.TryGetValue(HttpConstants.HttpHeaders.EnumerationDirection, out object direction))
            {
                rntdbEnumerationDirection = (byte)direction == (byte)RntbdConstants.RntdbEnumerationDirection.Reverse ? RntbdConstants.RntdbEnumerationDirection.Reverse : RntbdConstants.RntdbEnumerationDirection.Forward;
            }

            IComparer<PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>> comparer;
            if (rntdbEnumerationDirection == RntbdConstants.RntdbEnumerationDirection.Forward)
            {
                comparer = PartitionRangePageAsyncEnumeratorComparerForward.Singleton;
            }
            else
            {
                comparer = PartitionRangePageAsyncEnumeratorComparerReverse.Singleton;
            }

            CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> crossPartitionEnumerator = new CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>(
                documentContainer,
                CrossPartitionReadFeedAsyncEnumerator.MakeCreateFunction(
                    documentContainer,
                    queryRequestOptions,
                    pageSize,
                    cancellationToken),
                comparer: comparer,
                maxConcurrency: default,
                cancellationToken,
                monadicCrossPartitionState.Result);

            CrossPartitionReadFeedAsyncEnumerator enumerator = new CrossPartitionReadFeedAsyncEnumerator(
                crossPartitionEnumerator,
                cancellationToken);

            return TryCatch<CrossPartitionReadFeedAsyncEnumerator>.FromResult(enumerator);
        }

        private static TryCatch<CrossPartitionState<ReadFeedState>> MonadicParseCrossPartitionState(string continuation)
        {
            if (continuation == default)
            {
                // Just start with null continuation for the full range
                return TryCatch<CrossPartitionState<ReadFeedState>>.FromResult(
                    new CrossPartitionState<ReadFeedState>(
                        new List<(FeedRangeInternal, ReadFeedState)>()
                        {
                            (FeedRangeEpk.FullRange, new ReadFeedState(CosmosNull.Create()))
                        }));
            }

            TryCatch<CosmosArray> monadicCosmosArray = CosmosArray.Monadic.Parse(continuation);
            if (monadicCosmosArray.Failed)
            {
                return TryCatch<CrossPartitionState<ReadFeedState>>.FromException(
                    new FormatException($"Expected array for {nameof(CrossPartitionState<ReadFeedState>)}: {continuation}",
                    monadicCosmosArray.Exception));
            }

            List<ReadFeedContinuationToken> readFeedContinuationTokens = new List<ReadFeedContinuationToken>();
            foreach (CosmosElement arrayItem in monadicCosmosArray.Result)
            {
                TryCatch<ReadFeedContinuationToken> monadicReadFeedContinuationToken = ReadFeedContinuationToken.MonadicConvertFromCosmosElement(arrayItem);
                if (monadicReadFeedContinuationToken.Failed)
                {
                    return TryCatch<CrossPartitionState<ReadFeedState>>.FromException(
                        new FormatException($"failed to parse array item for {nameof(CrossPartitionState<ReadFeedState>)}: {continuation}",
                        monadicReadFeedContinuationToken.Exception));
                }

                readFeedContinuationTokens.Add(monadicReadFeedContinuationToken.Result);
            }

            List<(FeedRangeInternal, ReadFeedState)> crossPartitionStateElements = new List<(FeedRangeInternal, ReadFeedState)>();
            foreach (ReadFeedContinuationToken token in readFeedContinuationTokens)
            {
                crossPartitionStateElements.Add((token.Range, token.State));
            }

            CrossPartitionState<ReadFeedState> crossPartitionState = new CrossPartitionState<ReadFeedState>(crossPartitionStateElements);
            return TryCatch<CrossPartitionState<ReadFeedState>>.FromResult(crossPartitionState);
        }

        private static CreatePartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> MakeCreateFunction(
            IReadFeedDataSource readFeedDataSource,
            QueryRequestOptions queryRequestOptions,
            int pageSize,
            CancellationToken cancellationToken) => (FeedRangeInternal range, ReadFeedState state) => new ReadFeedPartitionRangeEnumerator(
                readFeedDataSource,
                range,
                queryRequestOptions,
                pageSize,
                cancellationToken,
                state);

        private sealed class PartitionRangePageAsyncEnumeratorComparerForward : IComparer<PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>>
        {
            public static readonly PartitionRangePageAsyncEnumeratorComparerForward Singleton = new PartitionRangePageAsyncEnumeratorComparerForward();

            public int Compare(
                PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> partitionRangePageEnumerator1,
                PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> partitionRangePageEnumerator2)
            {
                if (object.ReferenceEquals(partitionRangePageEnumerator1, partitionRangePageEnumerator2))
                {
                    return 0;
                }

                // Either both don't have results or both do.
                return string.CompareOrdinal(
                    ((FeedRangeEpk)partitionRangePageEnumerator1.Range).Range.Min,
                    ((FeedRangeEpk)partitionRangePageEnumerator2.Range).Range.Min);
            }
        }

        private sealed class PartitionRangePageAsyncEnumeratorComparerReverse : IComparer<PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>>
        {
            public static readonly PartitionRangePageAsyncEnumeratorComparerReverse Singleton = new PartitionRangePageAsyncEnumeratorComparerReverse();

            public int Compare(
                PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> partitionRangePageEnumerator1,
                PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> partitionRangePageEnumerator2)
            {
                return -1 * PartitionRangePageAsyncEnumeratorComparerForward.Singleton.Compare(
                    partitionRangePageEnumerator1,
                    partitionRangePageEnumerator2);
            }
        }
    }
}
