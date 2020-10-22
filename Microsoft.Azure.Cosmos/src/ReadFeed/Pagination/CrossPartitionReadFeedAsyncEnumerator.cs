// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
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

            TryCatch<CrossPartitionPage<ReadFeedPage, ReadFeedState>> monadicCrossPartitionPage = this.crossPartitionEnumerator.Current;
            if (monadicCrossPartitionPage.Failed)
            {
                this.Current = TryCatch<ReadFeedPage>.FromException(monadicCrossPartitionPage.Exception);
                return true;
            }

            CrossPartitionPage<ReadFeedPage, ReadFeedState> crossPartitionPage = monadicCrossPartitionPage.Result;
            ReadFeedPage backendPage = crossPartitionPage.Page;
            CrossPartitionState<ReadFeedState> crossPartitionState = crossPartitionPage.State;

            // left most and any non null continuations
            List<(FeedRangeInternal, ReadFeedState)> rangesAndStates = crossPartitionState
                .Value
                .OrderBy(tuple => (FeedRangeEpk)tuple.Item1, EpkRangeComparer.Singleton)
                .ToList();
            List<CosmosElement> changeFeedContinuationTokens = new List<CosmosElement>();
            for (int i = 0; i < rangesAndStates.Count; i++)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                (FeedRangeInternal range, ReadFeedState state) = rangesAndStates[i];
                if ((i == 0) || (state != null))
                {
                    ParallelContinuationToken parallelContinuationToken = new ParallelContinuationToken(
                        token: state != null ? ((CosmosString)state.Value).Value : null,
                        range: ((FeedRangeEpk)range).Range);

                    activeParallelContinuationTokens.Add(parallelContinuationToken);
                }
            }
            foreach ((FeedRangeInternal range, ReadFeedState state) rangeAndState in crossPartitionState.Value)
            {
                ReadFeedContinuationToken readFeedContinuationToken = new ReadFeedContinuationToken(
                    rangeAndState.range,
                    rangeAndState.state);
                CosmosElement cosmosElementChangeFeedContinuationToken = ReadFeedContinuationToken.ToCosmosElement(readFeedContinuationToken);
                changeFeedContinuationTokens.Add(cosmosElementChangeFeedContinuationToken);
            }

            CosmosArray cosmosElementTokens = CosmosArray.Create(changeFeedContinuationTokens);
            ReadFeedState state = new ReadFeedState(cosmosElementTokens);
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

            TryCatch<CrossPartitionState<ReadFeedState>> monadicCrossPartitionState = MonadicParseCrossPartitionState(continuationToken);
            if (monadicCrossPartitionState.Failed)
            {
                return TryCatch<CrossPartitionReadFeedAsyncEnumerator>.FromException(monadicCrossPartitionState.Exception);
            }

            CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> crossPartitionEnumerator = new CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>(
                documentContainer,
                CrossPartitionReadFeedAsyncEnumerator.MakeCreateFunction(
                    documentContainer,
                    queryRequestOptions,
                    pageSize,
                    cancellationToken),
                comparer: PartitionRangePageAsyncEnumeratorComparer.Singleton,
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
                return TryCatch<CrossPartitionState<ReadFeedState>>.FromResult(default);
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

        private sealed class PartitionRangePageAsyncEnumeratorComparer : IComparer<PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>>
        {
            public static readonly PartitionRangePageAsyncEnumeratorComparer Singleton = new PartitionRangePageAsyncEnumeratorComparer();

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
    }
}
