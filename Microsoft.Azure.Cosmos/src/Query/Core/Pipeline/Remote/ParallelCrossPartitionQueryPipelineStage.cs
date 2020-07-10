// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote
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

    internal sealed class ParallelCrossPartitionQueryPipelineStage : IQueryPipelineStage
    {
        private readonly CrossPartitionRangePageEnumerator<QueryPage, QueryState> crossPartitionRangePageEnumerator;

        private ParallelCrossPartitionQueryPipelineStage(
            CrossPartitionRangePageEnumerator<QueryPage, QueryState> crossPartitionRangePageEnumerator)
        {
            this.crossPartitionRangePageEnumerator = crossPartitionRangePageEnumerator ?? throw new ArgumentNullException(nameof(crossPartitionRangePageEnumerator));
        }

        public TryCatch<QueryPage> Current
        {
            get
            {
                TryCatch<CrossPartitionPage<QueryPage, QueryState>> currentCrossPartitionPage = this.crossPartitionRangePageEnumerator.Current;
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

        public ValueTask DisposeAsync() => this.crossPartitionRangePageEnumerator.DisposeAsync();

        public ValueTask<bool> MoveNextAsync() => this.crossPartitionRangePageEnumerator.MoveNextAsync();

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            IFeedRangeProvider feedRangeProvider,
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            int pageSize,
            CosmosElement continuationToken)
        {
            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            CrossPartitionState<QueryState> state;
            if (continuationToken == null)
            {
                state = default;
            }
            else
            {
                if (!(continuationToken is CosmosArray compositeContinuationTokenListRaw))
                {
                    return TryCatch<IQueryPipelineStage>.FromException(
                        new MalformedContinuationTokenException(
                            $"Invalid format for continuation token {continuationToken} for {nameof(ParallelCrossPartitionQueryPipelineStage)}"));
                }

                if (compositeContinuationTokenListRaw.Count == 0)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(
                        new MalformedContinuationTokenException(
                            $"Invalid format for continuation token {continuationToken} for {nameof(ParallelCrossPartitionQueryPipelineStage)}"));
                }

                List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>();
                foreach (CosmosElement compositeContinuationTokenRaw in compositeContinuationTokenListRaw)
                {
                    TryCatch<CompositeContinuationToken> tryCreateCompositeContinuationToken = CompositeContinuationToken.TryCreateFromCosmosElement(compositeContinuationTokenRaw);
                    if (tryCreateCompositeContinuationToken.Failed)
                    {
                        return TryCatch<IQueryPipelineStage>.FromException(
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

                state = new CrossPartitionState<QueryState>(rangesAndStates);
            }

            CrossPartitionRangePageEnumerator<QueryPage, QueryState> crossPartitionPageEnumerator = new CrossPartitionRangePageEnumerator<QueryPage, QueryState>(
                feedRangeProvider,
                ParallelCrossPartitionQueryPipelineStage.MakeCreateFunction(queryDataSource, sqlQuerySpec, pageSize),
                Comparer.Singleton,
                state: state);

            ParallelCrossPartitionQueryPipelineStage stage = new ParallelCrossPartitionQueryPipelineStage(crossPartitionPageEnumerator);
            return TryCatch<IQueryPipelineStage>.FromResult(stage);
        }

        private static CreatePartitionRangePageEnumerator<QueryPage, QueryState> MakeCreateFunction(
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            int pageSize) => (PartitionKeyRange range, QueryState state) => new QueryPartitionRangePageEnumerator(
                queryDataSource,
                sqlQuerySpec,
                range,
                pageSize,
                state);

        private sealed class Comparer : IComparer<PartitionRangePageEnumerator<QueryPage, QueryState>>
        {
            public static readonly Comparer Singleton = new Comparer();

            public int Compare(
                PartitionRangePageEnumerator<QueryPage, QueryState> partitionRangePageEnumerator1,
                PartitionRangePageEnumerator<QueryPage, QueryState> partitionRangePageEnumerator2)
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