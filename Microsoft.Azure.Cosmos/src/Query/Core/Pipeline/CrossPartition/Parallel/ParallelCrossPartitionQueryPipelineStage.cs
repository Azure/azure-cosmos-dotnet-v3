// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;
    using static Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.PartitionMapper;

    /// <summary>
    /// <see cref="ParallelCrossPartitionQueryPipelineStage"/> is an implementation of <see cref="IQueryPipelineStage"/> that drain results from multiple remote nodes.
    /// This class is responsible for draining cross partition queries that do not have order by conditions.
    /// The way parallel queries work is that it drains from the left most partition first.
    /// This class handles draining in the correct order and can also stop and resume the query 
    /// by generating a continuation token and resuming from said continuation token.
    /// </summary>
    internal sealed class ParallelCrossPartitionQueryPipelineStage : IQueryPipelineStage
    {
        private readonly CrossPartitionRangePageAsyncEnumerator<QueryPage, QueryState> crossPartitionRangePageAsyncEnumerator;
        private CancellationToken cancellationToken;

        private ParallelCrossPartitionQueryPipelineStage(
            CrossPartitionRangePageAsyncEnumerator<QueryPage, QueryState> crossPartitionRangePageAsyncEnumerator,
            CancellationToken cancellationToken)
        {
            this.crossPartitionRangePageAsyncEnumerator = crossPartitionRangePageAsyncEnumerator ?? throw new ArgumentNullException(nameof(crossPartitionRangePageAsyncEnumerator));
            this.cancellationToken = cancellationToken;
        }

        public TryCatch<QueryPage> Current { get; private set; }

        public ValueTask DisposeAsync() => this.crossPartitionRangePageAsyncEnumerator.DisposeAsync();

        // In order to maintain the continuation token for the user we must drain with a few constraints
        // 1) We fully drain from the left most partition before moving on to the next partition
        // 2) We drain only full pages from the document producer so we aren't left with a partial page
        //  otherwise we would need to add to the continuation token how many items to skip over on that page.
        public async ValueTask<bool> MoveNextAsync()
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            if (!await this.crossPartitionRangePageAsyncEnumerator.MoveNextAsync())
            {
                this.Current = default;
                return false;
            }

            TryCatch<CrossFeedRangePage<QueryPage, QueryState>> currentCrossPartitionPage = this.crossPartitionRangePageAsyncEnumerator.Current;
            if (currentCrossPartitionPage.Failed)
            {
                this.Current = TryCatch<QueryPage>.FromException(currentCrossPartitionPage.Exception);
                return true;
            }

            CrossFeedRangePage<QueryPage, QueryState> crossPartitionPageResult = currentCrossPartitionPage.Result;
            QueryPage backendQueryPage = crossPartitionPageResult.Page;
            CrossFeedRangeState<QueryState> crossPartitionState = crossPartitionPageResult.State;

            QueryState queryState;
            if (crossPartitionState == null)
            {
                queryState = null;
            }
            else
            {
                // left most and any non null continuations
                List<FeedRangeState<QueryState>> feedRangeStates = crossPartitionState
                    .Value
                    .OrderBy(tuple => (FeedRangeEpk)tuple.FeedRange, EpkRangeComparer.Singleton)
                    .ToList();
                List<ParallelContinuationToken> activeParallelContinuationTokens = new List<ParallelContinuationToken>();
                for (int i = 0; i < feedRangeStates.Count; i++)
                {
                    this.cancellationToken.ThrowIfCancellationRequested();

                    FeedRangeState<QueryState> feedRangeState = feedRangeStates[i];
                    if ((i == 0) || (feedRangeState.State != null))
                    {
                        ParallelContinuationToken parallelContinuationToken = new ParallelContinuationToken(
                            token: feedRangeState.State != null ? ((CosmosString)feedRangeState.State.Value).Value : null,
                            range: ((FeedRangeEpk)feedRangeState.FeedRange).Range);

                        activeParallelContinuationTokens.Add(parallelContinuationToken);
                    }
                }

                IEnumerable<CosmosElement> cosmosElementContinuationTokens = activeParallelContinuationTokens
                    .Select(token => ParallelContinuationToken.ToCosmosElement(token));
                CosmosArray cosmosElementParallelContinuationTokens = CosmosArray.Create(cosmosElementContinuationTokens);

                queryState = new QueryState(cosmosElementParallelContinuationTokens);
            }

            QueryPage crossPartitionQueryPage = new QueryPage(
                backendQueryPage.Documents,
                backendQueryPage.RequestCharge,
                backendQueryPage.ActivityId,
                backendQueryPage.ResponseLengthInBytes,
                backendQueryPage.CosmosQueryExecutionInfo,
                backendQueryPage.DisallowContinuationTokenMessage,
                queryState);

            this.Current = TryCatch<QueryPage>.FromResult(crossPartitionQueryPage);
            return true;
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            IDocumentContainer documentContainer,
            SqlQuerySpec sqlQuerySpec,
            IReadOnlyList<FeedRangeEpk> targetRanges,
            Cosmos.PartitionKey? partitionKey,
            int pageSize,
            int maxConcurrency,
            CosmosElement continuationToken,
            CancellationToken cancellationToken)
        {
            if (targetRanges == null)
            {
                throw new ArgumentNullException(nameof(targetRanges));
            }

            if (targetRanges.Count == 0)
            {
                throw new ArgumentException($"{nameof(targetRanges)} must have some elements");
            }

            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            TryCatch<CrossFeedRangeState<QueryState>> monadicExtractState = MonadicExtractState(continuationToken, targetRanges);
            if (monadicExtractState.Failed)
            {
                return TryCatch<IQueryPipelineStage>.FromException(monadicExtractState.Exception);
            }

            CrossFeedRangeState<QueryState> state = monadicExtractState.Result;

            CrossPartitionRangePageAsyncEnumerator<QueryPage, QueryState> crossPartitionPageEnumerator = new CrossPartitionRangePageAsyncEnumerator<QueryPage, QueryState>(
                documentContainer,
                ParallelCrossPartitionQueryPipelineStage.MakeCreateFunction(documentContainer, sqlQuerySpec, pageSize, partitionKey, cancellationToken),
                comparer: Comparer.Singleton,
                maxConcurrency,
                state: state,
                cancellationToken: cancellationToken);

            ParallelCrossPartitionQueryPipelineStage stage = new ParallelCrossPartitionQueryPipelineStage(crossPartitionPageEnumerator, cancellationToken);
            return TryCatch<IQueryPipelineStage>.FromResult(stage);
        }

        private static TryCatch<CrossFeedRangeState<QueryState>> MonadicExtractState(
            CosmosElement continuationToken,
            IReadOnlyList<FeedRangeEpk> ranges)
        {
            if (continuationToken == null)
            {
                // Full fan out to the ranges with null continuations
                CrossFeedRangeState<QueryState> fullFanOutState = new CrossFeedRangeState<QueryState>(ranges.Select(range => new FeedRangeState<QueryState>((FeedRangeInternal)range, (QueryState)null)).ToImmutableArray());
                return TryCatch<CrossFeedRangeState<QueryState>>.FromResult(fullFanOutState);
            }

            if (!(continuationToken is CosmosArray parallelContinuationTokenListRaw))
            {
                return TryCatch<CrossFeedRangeState<QueryState>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Invalid format for continuation token {continuationToken} for {nameof(ParallelCrossPartitionQueryPipelineStage)}"));
            }

            if (parallelContinuationTokenListRaw.Count == 0)
            {
                return TryCatch<CrossFeedRangeState<QueryState>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Invalid format for continuation token {continuationToken} for {nameof(ParallelCrossPartitionQueryPipelineStage)}"));
            }

            List<ParallelContinuationToken> parallelContinuationTokens = new List<ParallelContinuationToken>();
            foreach (CosmosElement parallelContinuationTokenRaw in parallelContinuationTokenListRaw)
            {
                TryCatch<ParallelContinuationToken> tryCreateParallelContinuationToken = ParallelContinuationToken.TryCreateFromCosmosElement(parallelContinuationTokenRaw);
                if (tryCreateParallelContinuationToken.Failed)
                {
                    return TryCatch<CrossFeedRangeState<QueryState>>.FromException(
                        tryCreateParallelContinuationToken.Exception);
                }

                parallelContinuationTokens.Add(tryCreateParallelContinuationToken.Result);
            }

            TryCatch<PartitionMapping<ParallelContinuationToken>> partitionMappingMonad = PartitionMapper.MonadicGetPartitionMapping(
                ranges,
                parallelContinuationTokens);
            if (partitionMappingMonad.Failed)
            {
                return TryCatch<CrossFeedRangeState<QueryState>>.FromException(
                    partitionMappingMonad.Exception);
            }

            PartitionMapping<ParallelContinuationToken> partitionMapping = partitionMappingMonad.Result;
            List<FeedRangeState<QueryState>> feedRangeStates = new List<FeedRangeState<QueryState>>();

            List<IReadOnlyDictionary<FeedRangeEpk, ParallelContinuationToken>> rangesToInitialize = new List<IReadOnlyDictionary<FeedRangeEpk, ParallelContinuationToken>>()
            {
                // Skip all the partitions left of the target range, since they have already been drained fully.
                partitionMapping.TargetPartition,
                partitionMapping.PartitionsRightOfTarget,
            };

            foreach (IReadOnlyDictionary<FeedRangeEpk, ParallelContinuationToken> rangeToInitalize in rangesToInitialize)
            {
                foreach (KeyValuePair<FeedRangeEpk, ParallelContinuationToken> kvp in rangeToInitalize)
                {
                    FeedRangeState<QueryState> feedRangeState = new FeedRangeState<QueryState>(kvp.Key, kvp.Value?.Token != null ? new QueryState(CosmosString.Create(kvp.Value.Token)) : null);
                    feedRangeStates.Add(feedRangeState);
                }
            }

            CrossFeedRangeState<QueryState> crossPartitionState = new CrossFeedRangeState<QueryState>(feedRangeStates.ToImmutableArray());

            return TryCatch<CrossFeedRangeState<QueryState>>.FromResult(crossPartitionState);
        }

        private static CreatePartitionRangePageAsyncEnumerator<QueryPage, QueryState> MakeCreateFunction(
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            int pageSize,
            Cosmos.PartitionKey? partitionKey,
            CancellationToken cancellationToken) => (FeedRangeInternal range, QueryState state) => new QueryPartitionRangePageAsyncEnumerator(
                queryDataSource,
                sqlQuerySpec,
                range,
                partitionKey,
                pageSize,
                cancellationToken: cancellationToken,
                state);

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            this.crossPartitionRangePageAsyncEnumerator.SetCancellationToken(cancellationToken);
        }

        private sealed class Comparer : IComparer<PartitionRangePageAsyncEnumerator<QueryPage, QueryState>>
        {
            public static readonly Comparer Singleton = new Comparer();

            public int Compare(
                PartitionRangePageAsyncEnumerator<QueryPage, QueryState> partitionRangePageEnumerator1,
                PartitionRangePageAsyncEnumerator<QueryPage, QueryState> partitionRangePageEnumerator2)
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