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

        public async ValueTask<bool> MoveNextAsync()
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            if (!await this.crossPartitionRangePageAsyncEnumerator.MoveNextAsync())
            {
                this.Current = default;
                return false;
            }

            TryCatch<CrossPartitionPage<QueryPage, QueryState>> currentCrossPartitionPage = this.crossPartitionRangePageAsyncEnumerator.Current;
            if (currentCrossPartitionPage.Failed)
            {
                this.Current = TryCatch<QueryPage>.FromException(currentCrossPartitionPage.Exception);
                return true;
            }

            CrossPartitionPage<QueryPage, QueryState> crossPartitionPageResult = currentCrossPartitionPage.Result;
            QueryPage backendQueryPage = crossPartitionPageResult.Page;
            CrossPartitionState<QueryState> crossPartitionState = crossPartitionPageResult.State;

            QueryState queryState;
            if (crossPartitionState == null)
            {
                queryState = null;
            }
            else
            {
                // left most and any non null continuations
                List<(PartitionKeyRange, QueryState)> rangesAndStates = crossPartitionState.Value.OrderBy(tuple => tuple.Item1, PartitionKeyRangeComparer.Singleton).ToList();
                List<ParallelContinuationToken> activeParallelContinuationTokens = new List<ParallelContinuationToken>();
                for (int i = 0; i < rangesAndStates.Count; i++)
                {
                    this.cancellationToken.ThrowIfCancellationRequested();

                    (PartitionKeyRange range, QueryState state) = rangesAndStates[i];
                    if ((i == 0) || (state != null))
                    {
                        ParallelContinuationToken parallelContinuationToken = new ParallelContinuationToken(
                            token: state != null ? ((CosmosString)state.Value).Value : null,
                            range: range.ToRange());

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
            IReadOnlyList<PartitionKeyRange> targetRanges,
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

            TryCatch<CrossPartitionState<QueryState>> monadicExtractState = MonadicExtractState(continuationToken, targetRanges);
            if (monadicExtractState.Failed)
            {
                return TryCatch<IQueryPipelineStage>.FromException(monadicExtractState.Exception);
            }

            CrossPartitionState<QueryState> state = monadicExtractState.Result;

            CrossPartitionRangePageAsyncEnumerator<QueryPage, QueryState> crossPartitionPageEnumerator = new CrossPartitionRangePageAsyncEnumerator<QueryPage, QueryState>(
                documentContainer,
                ParallelCrossPartitionQueryPipelineStage.MakeCreateFunction(documentContainer, sqlQuerySpec, pageSize),
                Comparer.Singleton,
                maxConcurrency,
                state: state,
                cancellationToken: cancellationToken);

            ParallelCrossPartitionQueryPipelineStage stage = new ParallelCrossPartitionQueryPipelineStage(crossPartitionPageEnumerator, cancellationToken);
            return TryCatch<IQueryPipelineStage>.FromResult(stage);
        }

        private static TryCatch<CrossPartitionState<QueryState>> MonadicExtractState(
            CosmosElement continuationToken,
            IReadOnlyList<PartitionKeyRange> ranges)
        {
            if (continuationToken == null)
            {
                // Full fan out to the ranges with null continuations
                CrossPartitionState<QueryState> fullFanOutState = new CrossPartitionState<QueryState>(ranges.Select(range => (range, (QueryState)null)).ToArray());
                return TryCatch<CrossPartitionState<QueryState>>.FromResult(fullFanOutState);
            }

            if (!(continuationToken is CosmosArray parallelContinuationTokenListRaw))
            {
                return TryCatch<CrossPartitionState<QueryState>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Invalid format for continuation token {continuationToken} for {nameof(ParallelCrossPartitionQueryPipelineStage)}"));
            }

            if (parallelContinuationTokenListRaw.Count == 0)
            {
                return TryCatch<CrossPartitionState<QueryState>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Invalid format for continuation token {continuationToken} for {nameof(ParallelCrossPartitionQueryPipelineStage)}"));
            }

            List<ParallelContinuationToken> parallelContinuationTokens = new List<ParallelContinuationToken>();
            foreach (CosmosElement parallelContinuationTokenRaw in parallelContinuationTokenListRaw)
            {
                TryCatch<ParallelContinuationToken> tryCreateParallelContinuationToken = ParallelContinuationToken.TryCreateFromCosmosElement(parallelContinuationTokenRaw);
                if (tryCreateParallelContinuationToken.Failed)
                {
                    return TryCatch<CrossPartitionState<QueryState>>.FromException(
                        tryCreateParallelContinuationToken.Exception);
                }

                parallelContinuationTokens.Add(tryCreateParallelContinuationToken.Result);
            }

            TryCatch<PartitionMapping<ParallelContinuationToken>> partitionMappingMonad = PartitionMapper.MonadicGetPartitionMapping(
                ranges,
                parallelContinuationTokens);
            if (partitionMappingMonad.Failed)
            {
                return TryCatch<CrossPartitionState<QueryState>>.FromException(
                    partitionMappingMonad.Exception);
            }

            PartitionMapping<ParallelContinuationToken> partitionMapping = partitionMappingMonad.Result;
            List<(PartitionKeyRange, QueryState)> rangesAndStates = new List<(PartitionKeyRange, QueryState)>();

            List<IReadOnlyDictionary<PartitionKeyRange, ParallelContinuationToken>> rangesToInitialize = new List<IReadOnlyDictionary<PartitionKeyRange, ParallelContinuationToken>>()
            {
                // Skip all the partitions left of the target range, since they have already been drained fully.
                partitionMapping.TargetPartition,
                partitionMapping.PartitionsRightOfTarget,
            };

            foreach (IReadOnlyDictionary<PartitionKeyRange, ParallelContinuationToken> rangeToInitalize in rangesToInitialize)
            {
                foreach (KeyValuePair<PartitionKeyRange, ParallelContinuationToken> kvp in rangeToInitalize)
                {
                    (PartitionKeyRange, QueryState) rangeAndState = (kvp.Key, kvp.Value?.Token != null ? new QueryState(CosmosString.Create(kvp.Value.Token)) : null);
                    rangesAndStates.Add(rangeAndState);
                }
            }

            CrossPartitionState<QueryState> crossPartitionState = new CrossPartitionState<QueryState>(rangesAndStates);

            return TryCatch<CrossPartitionState<QueryState>>.FromResult(crossPartitionState);
        }

        private static CreatePartitionRangePageAsyncEnumerator<QueryPage, QueryState> MakeCreateFunction(
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            int pageSize) => (PartitionKeyRange range, QueryState state) => new QueryPartitionRangePageAsyncEnumerator(
                queryDataSource,
                sqlQuerySpec,
                range,
                pageSize,
                cancellationToken: default,
                state);

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;

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
                    partitionRangePageEnumerator1.Range.MinInclusive,
                    partitionRangePageEnumerator2.Range.MinInclusive);
            }
        }
    }
}