// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
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

        public async ValueTask<bool> MoveNextAsync()
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            if (!await this.crossPartitionEnumerator.MoveNextAsync())
            {
                this.Current = default;
                return false;
            }

            TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>> monadicCrossPartitionPage = this.crossPartitionEnumerator.Current;
            if (monadicCrossPartitionPage.Failed)
            {
                this.Current = TryCatch<ReadFeedPage>.FromException(monadicCrossPartitionPage.Exception);
                return true;
            }

            CrossFeedRangePage<ReadFeedPage, ReadFeedState> crossPartitionPage = monadicCrossPartitionPage.Result;
            ReadFeedPage backendPage = crossPartitionPage.Page;
            CrossFeedRangeState<ReadFeedState> crossPartitionState = crossPartitionPage.State;
            ReadFeedState state;
            if (crossPartitionState != null)
            {
                List<CosmosElement> changeFeedContinuationTokens = new List<CosmosElement>();
                foreach (FeedRangeState<ReadFeedState> feedRangeState in crossPartitionState.Value)
                {
                    this.cancellationToken.ThrowIfCancellationRequested();
                    ReadFeedContinuationToken readFeedContinuationToken = new ReadFeedContinuationToken(
                        feedRangeState.FeedRange,
                        feedRangeState.State);

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

            TryCatch<CrossFeedRangeState<ReadFeedState>> monadicCrossPartitionState = MonadicParseCrossPartitionState(continuationToken);
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

        private static TryCatch<CrossFeedRangeState<ReadFeedState>> MonadicParseCrossPartitionState(string continuation)
        {
            if (continuation == default)
            {
                // Just start with null continuation for the full range
                return TryCatch<CrossFeedRangeState<ReadFeedState>>.FromResult(
                    new CrossFeedRangeState<ReadFeedState>(
                        new List<FeedRangeState<ReadFeedState>>()
                        {
                            new FeedRangeState<ReadFeedState>(FeedRangeEpk.FullRange, new ReadFeedState(CosmosNull.Create()))
                        }.ToImmutableArray()));
            }

            TryCatch<CosmosArray> monadicCosmosArray = CosmosArray.Monadic.Parse(continuation);
            if (monadicCosmosArray.Failed)
            {
                return TryCatch<CrossFeedRangeState<ReadFeedState>>.FromException(
                    new FormatException($"Expected array for {nameof(CrossFeedRangeState<ReadFeedState>)}: {continuation}",
                    monadicCosmosArray.Exception));
            }

            List<ReadFeedContinuationToken> readFeedContinuationTokens = new List<ReadFeedContinuationToken>();
            foreach (CosmosElement arrayItem in monadicCosmosArray.Result)
            {
                TryCatch<ReadFeedContinuationToken> monadicReadFeedContinuationToken = ReadFeedContinuationToken.MonadicConvertFromCosmosElement(arrayItem);
                if (monadicReadFeedContinuationToken.Failed)
                {
                    return TryCatch<CrossFeedRangeState<ReadFeedState>>.FromException(
                        new FormatException($"failed to parse array item for {nameof(CrossFeedRangeState<ReadFeedState>)}: {continuation}",
                        monadicReadFeedContinuationToken.Exception));
                }

                readFeedContinuationTokens.Add(monadicReadFeedContinuationToken.Result);
            }

            List<FeedRangeState<ReadFeedState>> feedRangeStates = new List<FeedRangeState<ReadFeedState>>();
            foreach (ReadFeedContinuationToken token in readFeedContinuationTokens)
            {
                feedRangeStates.Add(new FeedRangeState<ReadFeedState>(token.Range, token.State));
            }

            CrossFeedRangeState<ReadFeedState> crossPartitionState = new CrossFeedRangeState<ReadFeedState>(feedRangeStates.ToImmutableArray());
            return TryCatch<CrossFeedRangeState<ReadFeedState>>.FromResult(crossPartitionState);
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
