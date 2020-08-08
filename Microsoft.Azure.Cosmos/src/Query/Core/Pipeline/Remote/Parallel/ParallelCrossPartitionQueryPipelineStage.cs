// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote.Parallel
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
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

        public TryCatch<QueryPage> Current
        {
            get
            {
                TryCatch<CrossPartitionPage<QueryPage, QueryState>> currentCrossPartitionPage = this.crossPartitionRangePageAsyncEnumerator.Current;
                if (currentCrossPartitionPage.Failed)
                {
                    return TryCatch<QueryPage>.FromException(currentCrossPartitionPage.Exception);
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
                    List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>(crossPartitionState.Value.Count);
                    foreach ((PartitionKeyRange range, QueryState state) in crossPartitionState.Value)
                    {
                        CompositeContinuationToken compositeContinuationToken = new CompositeContinuationToken()
                        {
                            Range = range.ToRange(),
                            Token = state != null ? ((CosmosString)state.Value).Value : null,
                        };

                        compositeContinuationTokens.Add(compositeContinuationToken);
                    }

                    List<CosmosElement> cosmosElementContinuationTokens = compositeContinuationTokens
                        .Select(token => CompositeContinuationToken.ToCosmosElement(token))
                        .ToList();
                    CosmosArray cosmosElementCompositeContinuationTokens = CosmosArray.Create(cosmosElementContinuationTokens);

                    queryState = new QueryState(cosmosElementCompositeContinuationTokens);
                }

                QueryPage crossPartitionQueryPage = new QueryPage(
                    backendQueryPage.Documents,
                    backendQueryPage.RequestCharge,
                    backendQueryPage.ActivityId,
                    backendQueryPage.ResponseLengthInBytes,
                    backendQueryPage.CosmosQueryExecutionInfo,
                    backendQueryPage.DisallowContinuationTokenMessage,
                    queryState);

                return TryCatch<QueryPage>.FromResult(crossPartitionQueryPage);
            }
        }

        public ValueTask DisposeAsync() => this.crossPartitionRangePageAsyncEnumerator.DisposeAsync();

        public ValueTask<bool> MoveNextAsync() => this.crossPartitionRangePageAsyncEnumerator.MoveNextAsync();

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            IDocumentContainer documentContainer,
            SqlQuerySpec sqlQuerySpec,
            int pageSize,
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

            if (!(continuationToken is CosmosArray compositeContinuationTokenListRaw))
            {
                return TryCatch<CrossPartitionState<QueryState>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Invalid format for continuation token {continuationToken} for {nameof(ParallelCrossPartitionQueryPipelineStage)}"));
            }

            if (compositeContinuationTokenListRaw.Count == 0)
            {
                return TryCatch<CrossPartitionState<QueryState>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Invalid format for continuation token {continuationToken} for {nameof(ParallelCrossPartitionQueryPipelineStage)}"));
            }

            List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>();
            foreach (CosmosElement compositeContinuationTokenRaw in compositeContinuationTokenListRaw)
            {
                TryCatch<CompositeContinuationToken> tryCreateCompositeContinuationToken = CompositeContinuationToken.TryCreateFromCosmosElement(compositeContinuationTokenRaw);
                if (tryCreateCompositeContinuationToken.Failed)
                {
                    return TryCatch<CrossPartitionState<QueryState>>.FromException(
                        tryCreateCompositeContinuationToken.Exception);
                }

                compositeContinuationTokens.Add(tryCreateCompositeContinuationToken.Result);
            }

            List<(PartitionKeyRange, QueryState)> rangesAndStates = compositeContinuationTokens
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