// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.TryExecuteQuery
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
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using static Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.PartitionMapper;

    internal sealed class TryExecuteQueryPipelineStage : IQueryPipelineStage
    {
        private readonly QueryPartitionRangePageAsyncEnumerator queryPartitionRangePageAsyncEnumerator;

        private TryExecuteQueryPipelineStage(
            QueryPartitionRangePageAsyncEnumerator queryPartitionRangePageAsyncEnumerator)
        {
            this.queryPartitionRangePageAsyncEnumerator = queryPartitionRangePageAsyncEnumerator ?? throw new ArgumentNullException(nameof(queryPartitionRangePageAsyncEnumerator));
        }

        public TryCatch<QueryPage> Current { get; private set; } 

        public ValueTask DisposeAsync()
        {
            return this.queryPartitionRangePageAsyncEnumerator.DisposeAsync();
        }

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
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
                this.Current = partitionPage;
                return true;
            }

            QueryPage backendQueryPage = partitionPage.Result;

            QueryState queryState;
            if (backendQueryPage.State == null)
            {
                queryState = null;
            }
            else
            {
                TryExecuteContinuationToken tryExecuteContinuationToken;
                QueryState backendQueryState = backendQueryPage.State;
                {
                    tryExecuteContinuationToken = new TryExecuteContinuationToken(
                        token: backendQueryState.Value,
                        range: ((FeedRangeEpk)this.queryPartitionRangePageAsyncEnumerator.FeedRangeState.FeedRange).Range);
                }

                CosmosElement cosmosElementContinuationToken = TryExecuteContinuationToken.ToCosmosElement(tryExecuteContinuationToken);
                CosmosObject cosmosObjectContinuationToken = (CosmosObject)cosmosElementContinuationToken;
                queryState = new QueryState(cosmosObjectContinuationToken["tryExecute"]);
            }

            QueryPage crossPartitionQueryPage = new QueryPage(
                backendQueryPage.Documents,
                backendQueryPage.RequestCharge,
                backendQueryPage.ActivityId,
                backendQueryPage.ResponseLengthInBytes,
                backendQueryPage.CosmosQueryExecutionInfo,
                disallowContinuationTokenMessage: null,
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

            TryExecuteQueryPipelineStage stage = new TryExecuteQueryPipelineStage(partitionPageEnumerator);
            return TryCatch<IQueryPipelineStage>.FromResult(stage);
        }

        private static TryCatch<FeedRangeState<QueryState>> MonadicExtractState(
            CosmosElement continuationToken,
            FeedRangeEpk range)
        {
            if (continuationToken == null)
            {
                // Full fan out to the ranges with null continuations
                FeedRangeState<QueryState> fullFanOutState = new (range, (QueryState)null);
                return TryCatch<FeedRangeState<QueryState>>.FromResult(fullFanOutState);
            }

            List<TryExecuteContinuationToken> tryExecuteContinuationTokens = new List<TryExecuteContinuationToken>();
            TryCatch<TryExecuteContinuationToken> tryCreateContinuationToken = TryExecuteContinuationToken.TryCreateFromCosmosElement(continuationToken);
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

            List<IReadOnlyDictionary<FeedRangeEpk, TryExecuteContinuationToken>> rangeToInitialize = new List<IReadOnlyDictionary<FeedRangeEpk, TryExecuteContinuationToken>>()
            {
                partitionMapping.TargetMapping,
            };

            KeyValuePair<FeedRangeEpk, TryExecuteContinuationToken> kvpRange = rangeToInitialize[0].First();
            FeedRangeState<QueryState> feedRangeState = new FeedRangeState<QueryState>(kvpRange.Key, kvpRange.Value?.Token != null ? new QueryState(CosmosString.Create(kvpRange.Value.Token.Token)) : null);
            
            return TryCatch<FeedRangeState<QueryState>>.FromResult(feedRangeState);
        }
    }
}