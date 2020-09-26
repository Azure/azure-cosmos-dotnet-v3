// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;

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

        private ParallelCrossPartitionQueryPipelineStage(
            CrossPartitionRangePageAsyncEnumerator<QueryPage, QueryState> crossPartitionRangePageAsyncEnumerator)
        {
            this.crossPartitionRangePageAsyncEnumerator = crossPartitionRangePageAsyncEnumerator ?? throw new ArgumentNullException(nameof(crossPartitionRangePageAsyncEnumerator));
        }

        public TryCatch<QueryPage> Current { get; private set; }

        public ValueTask DisposeAsync() => this.crossPartitionRangePageAsyncEnumerator.DisposeAsync();

        public async ValueTask<bool> MoveNextAsync()
        {
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
                List<ParallelContinuationToken> parallelContinuationTokens = new List<ParallelContinuationToken>(crossPartitionState.Value.Count);
                foreach ((PartitionKeyRange range, QueryState state) in crossPartitionState.Value)
                {
                    ParallelContinuationToken parallelContinuationToken = new ParallelContinuationToken(
                        token: state != null ? ((CosmosString)state.Value).Value : null,
                        range: range.ToRange());

                    parallelContinuationTokens.Add(parallelContinuationToken);
                }

                List<CosmosElement> cosmosElementContinuationTokens = parallelContinuationTokens
                    .Select(token => ParallelContinuationToken.ToCosmosElement(token))
                    .ToList();
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
            int pageSize,
            int maxConcurrency,
            CosmosElement continuationToken)
        {
            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            TryCatch<CrossPartitionState<QueryState>> monadicExtractState = MonadicExtractState(continuationToken);
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
                state: state);

            ParallelCrossPartitionQueryPipelineStage stage = new ParallelCrossPartitionQueryPipelineStage(crossPartitionPageEnumerator);
            return TryCatch<IQueryPipelineStage>.FromResult(stage);
        }

        private static TryCatch<CrossPartitionState<QueryState>> MonadicExtractState(
            CosmosElement continuationToken)
        {
            if (continuationToken == null)
            {
                return TryCatch<CrossPartitionState<QueryState>>.FromResult(default);
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

            List<(PartitionKeyRange, QueryState)> rangesAndStates = parallelContinuationTokens
                .Select(token => (
                    new PartitionKeyRange()
                    {
                        MinInclusive = token.Range.Min,
                        MaxExclusive = token.Range.Max,
                    },
                    token.Token != null ? new QueryState(CosmosString.Create(token.Token)) : null))
                .ToList();

            CrossPartitionState<QueryState> state = new CrossPartitionState<QueryState>(rangesAndStates);

            return TryCatch<CrossPartitionState<QueryState>>.FromResult(state);
        }

        private static CreatePartitionRangePageAsyncEnumerator<QueryPage, QueryState> MakeCreateFunction(
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            int pageSize) => (PartitionKeyRange range, QueryState state) => new QueryPartitionRangePageAsyncEnumerator(
                queryDataSource,
                sqlQuerySpec,
                range,
                pageSize,
                state);

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