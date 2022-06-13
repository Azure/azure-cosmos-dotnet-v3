// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.SinglePartition;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using static Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.PartitionMapper;

    internal sealed class TryExecuteQueryPipelineStage : IQueryPipelineStage
    {
        private readonly QueryPartitionRangePageAsyncEnumerator queryPartitionRangePageAsyncEnumerator;
        private CancellationToken cancellationToken;

        private TryExecuteQueryPipelineStage(
            QueryPartitionRangePageAsyncEnumerator queryPartitionRangePageAsyncEnumerator,
            CancellationToken cancellationToken)
        {
            this.queryPartitionRangePageAsyncEnumerator = queryPartitionRangePageAsyncEnumerator ?? throw new ArgumentNullException(nameof(queryPartitionRangePageAsyncEnumerator));
            this.cancellationToken = cancellationToken;
        }
        public TryCatch<QueryPage> Current { get; private set; } 

        public ValueTask DisposeAsync()
        {
            return this.queryPartitionRangePageAsyncEnumerator.DisposeAsync();
        }
        private static CreatePartitionRangePageAsyncEnumerator<QueryPage, QueryState> MakeCreateFunction(
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            QueryPaginationOptions queryPaginationOptions,
            Cosmos.PartitionKey? partitionKey,
            CancellationToken cancellationToken) => (FeedRangeState<QueryState> feedRangeState) => new QueryPartitionRangePageAsyncEnumerator(
                queryDataSource,
                sqlQuerySpec,
                feedRangeState,
                partitionKey,
                queryPaginationOptions,
                cancellationToken);
        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            this.queryPartitionRangePageAsyncEnumerator.SetCancellationToken(cancellationToken);
        }
        public async ValueTask<bool> MoveNextAsync(ITrace trace)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if (!await this.queryPartitionRangePageAsyncEnumerator.MoveNextAsync(trace))
            {
                this.Current = default;
                return false;
            }

            TryCatch<QueryPage> partitionPage = this.queryPartitionRangePageAsyncEnumerator.Current;
            if (partitionPage.Failed)
            {
                this.Current = TryCatch<QueryPage>.FromException(partitionPage.Exception);
                return true;
            }

            Page<QueryState> partitionPageResult = partitionPage.Result;
            QueryPage backendQueryPage = (QueryPage)partitionPageResult;
            QueryState crossPartitionState = partitionPageResult.State;

            QueryState queryState;
            if (crossPartitionState == null)
            {
                queryState = null;
            }
            else
            {
                List<ParallelContinuationToken> activeParallelContinuationTokens = new List<ParallelContinuationToken>();
                QueryState firstState = (QueryState)crossPartitionState;
                {
                    ParallelContinuationToken firstParallelContinuationToken = new ParallelContinuationToken(
                        token: firstState != null ? ((CosmosString)firstState.Value).Value : null,
                        range: ((FeedRangeEpk)this.queryPartitionRangePageAsyncEnumerator.FeedRangeState.FeedRange).Range);

                    activeParallelContinuationTokens.Add(firstParallelContinuationToken);
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
                backendQueryPage.AdditionalHeaders,
                queryState);

            this.Current = TryCatch<QueryPage>.FromResult(crossPartitionQueryPage);
            return true;
        }
        public static TryCatch<IQueryPipelineStage> MonadicCreate(
              IDocumentContainer documentContainer,
              SqlQuerySpec sqlQuerySpec,
              FeedRangeEpk targetRange,
              Cosmos.PartitionKey? partitionKey,
              QueryPaginationOptions queryPaginationOptions,
              CosmosElement continuationToken,
              CancellationToken cancellationToken)
        {
            if (targetRange == null)
            {
                throw new ArgumentNullException(nameof(targetRange));
            }

            TryCatch<FeedRangeState<QueryState>> monadicExtractState = MonadicExtractState(continuationToken, targetRange);
            if (monadicExtractState.Failed)
            {
                return TryCatch<IQueryPipelineStage>.FromException(monadicExtractState.Exception);
            }

            FeedRangeState<QueryState> state = monadicExtractState.Result;

            QueryPartitionRangePageAsyncEnumerator partitionPageEnumerator = new QueryPartitionRangePageAsyncEnumerator(
                queryDataSource: documentContainer,
                sqlQuerySpec: sqlQuerySpec,
                feedRangeState: state,
                partitionKey: partitionKey,
                queryPaginationOptions: queryPaginationOptions,
                cancellationToken: cancellationToken);

            TryExecuteQueryPipelineStage stage = new TryExecuteQueryPipelineStage(partitionPageEnumerator, cancellationToken);
            return TryCatch<IQueryPipelineStage>.FromResult(stage);
        }
        private static TryCatch<FeedRangeState<QueryState>> MonadicExtractState(
            CosmosElement continuationToken,
            FeedRangeEpk range)
        {
            if (continuationToken == null)
            {
                // Full fan out to the ranges with null continuations
                // FeedRangeState<QueryState> fullFanOutState = new (range.Select(range => new FeedRangeState<QueryState>(range, (QueryState)null)).ToArray());
                FeedRangeState<QueryState> fullFanOutState = new (range, (QueryState)null);
                return TryCatch<FeedRangeState<QueryState>>.FromResult(fullFanOutState);
            }

            if (!(continuationToken is CosmosArray tryExecuteContinuationTokenListRaw))
            {
                return TryCatch<FeedRangeState<QueryState>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Invalid format for continuation token {continuationToken} for {nameof(TryExecuteQueryPipelineStage)}"));
            }

            if (tryExecuteContinuationTokenListRaw.Count == 0)
            {
                return TryCatch<FeedRangeState<QueryState>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Invalid format for continuation token {continuationToken} for {nameof(TryExecuteQueryPipelineStage)}"));
            }

            List<TryExecuteContinuationToken> tryExecuteContinuationTokens = new List<TryExecuteContinuationToken>();
            TryCatch<TryExecuteContinuationToken> tryCreateContinuationToken = TryExecuteContinuationToken.TryCreateFromCosmosElement(tryExecuteContinuationTokenListRaw[0]);
            if (tryCreateContinuationToken.Failed)
            {
                return TryCatch<FeedRangeState<QueryState>>.FromException(tryCreateContinuationToken.Exception);
            }

            tryExecuteContinuationTokens.Add(tryCreateContinuationToken.Result);

            TryCatch<PartitionMapping<TryExecuteContinuationToken>> partitionMappingMonad = PartitionMapper.MonadicGetPartitionMappingSingleRange(
                range,
                tryExecuteContinuationTokens);

            if (partitionMappingMonad.Failed)
            {
                return TryCatch<FeedRangeState<QueryState>>.FromException(
                    partitionMappingMonad.Exception);
            }

            PartitionMapping<TryExecuteContinuationToken> partitionMapping = partitionMappingMonad.Result;
            FeedRangeState<QueryState> feedRangeState = new FeedRangeState<QueryState>();

            List<IReadOnlyDictionary<FeedRangeEpk, TryExecuteContinuationToken>> rangesToInitialize = new List<IReadOnlyDictionary<FeedRangeEpk, TryExecuteContinuationToken>>()
            {
                partitionMapping.TargetMapping,
            };

            foreach (IReadOnlyDictionary<FeedRangeEpk, TryExecuteContinuationToken> rangeToInitalize in rangesToInitialize)
            {
                foreach (KeyValuePair<FeedRangeEpk, TryExecuteContinuationToken> kvp in rangeToInitalize)
                {
                    feedRangeState = new FeedRangeState<QueryState>(kvp.Key, kvp.Value?.Token != null ? new QueryState(CosmosString.Create(kvp.Value.Token)) : null);
                }
            }

            return TryCatch<FeedRangeState<QueryState>>.FromResult(feedRangeState);
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
                    ((FeedRangeEpk)partitionRangePageEnumerator1.FeedRangeState.FeedRange).Range.Min,
                    ((FeedRangeEpk)partitionRangePageEnumerator2.FeedRangeState.FeedRange).Range.Min);
            }
        }
    }
}