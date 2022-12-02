// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.OptimisticDirectExecutionQuery
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using static Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.CosmosQueryExecutionContextFactory;
    using static Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.PartitionMapper;

    internal sealed class OptimisticDirectExecutionQueryPipelineStage : IQueryPipelineStage
    {
        private const string optimisticDirectExecutionToken = "OptimisticDirectExecutionToken";
        private const string optimisticDirectExecution = "OptimisticDirectExecution";
        private const string specializedDocumentQueryExecution = "SpecializedDocumentQueryExecution";
        public delegate Task<TryCatch<IQueryPipelineStage>> FallbackQueryPipelineStageFactory(CosmosElement continuationToken);
        private readonly FallbackQueryPipelineStageFactory queryPipelineStageFactory;
        private TryCatch<IQueryPipelineStage> inner;
        private CosmosElement continuationToken;
        private string queryState;
        
        private OptimisticDirectExecutionQueryPipelineStage(TryCatch<IQueryPipelineStage> queryPipelineStage, FallbackQueryPipelineStageFactory queryPipelineStageFactory, CosmosElement continuationToken)
        {
            this.inner = queryPipelineStage;
            this.queryPipelineStageFactory = queryPipelineStageFactory;
            this.continuationToken = continuationToken;
        }

        public TryCatch<QueryPage> Current => this.inner.Result.Current;

        public ValueTask DisposeAsync()
        {
            return this.inner.Result.DisposeAsync();
        }
       
        public async ValueTask<bool> MoveNextAsync(ITrace trace)
        {
            if (this.inner.Result == null)
            {
                return false;
            }

            bool success = await this.inner.Result.MoveNextAsync(trace);
            bool isGoneException = this.Current.Failed
                && this.Current.InnerMostException is CosmosException exception
                && exception.StatusCode == System.Net.HttpStatusCode.Gone
                && exception.SubStatusCode == (int)SubStatusCodes.PartitionKeyRangeGone;

            if (success)
            {
                this.queryState = optimisticDirectExecution;
                this.SaveContinuation(this.Current.Result.State?.Value);
            }
            else if (isGoneException)
            {
                // Only Ode pipeline GoneException should be handled
                Debug.Assert(this.queryState == optimisticDirectExecution);

                this.inner = await this.queryPipelineStageFactory(this.UnwrapContinuationToken(this.continuationToken));
                this.queryState = specializedDocumentQueryExecution;
                
                // TODO: Failure check for this.inner
                bool fallbackPipelineSuccess = await this.inner.Result.MoveNextAsync(trace);

                return fallbackPipelineSuccess;
            }

            return success; 
        }

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            this.inner.Result.SetCancellationToken(cancellationToken);
        }

        public CosmosElement UnwrapContinuationToken(CosmosElement continuationToken) 
        {
            ((CosmosObject)continuationToken).TryGetValue(optimisticDirectExecutionToken, out CosmosElement specializedContinuationToken);
            CosmosArray cosmosElementFallbackToken = CosmosArray.Create(specializedContinuationToken);
            return cosmosElementFallbackToken;
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            DocumentContainer documentContainer,
            InputParameters inputParameters,
            FeedRangeEpk targetRange,
            QueryPaginationOptions queryPaginationOptions,
            FallbackQueryPipelineStageFactory queryPipelineStage,
            CancellationToken cancellationToken)
        {
            TryCatch<IQueryPipelineStage> pipelineStage = OptimisticDirectExecutionQueryPipelineImpl.MonadicCreate(
                documentContainer: documentContainer,
                sqlQuerySpec: inputParameters.SqlQuerySpec,
                targetRange: targetRange,
                queryPaginationOptions: queryPaginationOptions,
                partitionKey: inputParameters.PartitionKey,
                continuationToken: inputParameters.InitialUserContinuationToken,
                cancellationToken: cancellationToken);

            if (pipelineStage.Failed)
            {
                return TryCatch<IQueryPipelineStage>.FromException(pipelineStage.Exception);
            }

            OptimisticDirectExecutionQueryPipelineStage odePipelineStageMonadicCreate = new OptimisticDirectExecutionQueryPipelineStage(pipelineStage, queryPipelineStage, inputParameters.InitialUserContinuationToken);

            return TryCatch<IQueryPipelineStage>.FromResult(odePipelineStageMonadicCreate);
        }

        private void SaveContinuation(CosmosElement continuationToken)
        {
            this.continuationToken = continuationToken;
        }

        private class OptimisticDirectExecutionQueryPipelineImpl : IQueryPipelineStage
        {
            private readonly QueryPartitionRangePageAsyncEnumerator queryPartitionRangePageAsyncEnumerator;

            internal OptimisticDirectExecutionQueryPipelineImpl(
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
                    return false;
                }

                QueryPage backendQueryPage = partitionPage.Result;

                QueryState queryState;
                if (backendQueryPage.State == null)
                {
                    queryState = null;
                }
                else
                {
                    QueryState backendQueryState = backendQueryPage.State;
                    ParallelContinuationToken parallelContinuationToken = new ParallelContinuationToken(
                        token: (backendQueryState?.Value as CosmosString)?.Value,
                        range: ((FeedRangeEpk)this.queryPartitionRangePageAsyncEnumerator.FeedRangeState.FeedRange).Range);

                    OptimisticDirectExecutionContinuationToken optimisticDirectExecutionContinuationToken = new OptimisticDirectExecutionContinuationToken(parallelContinuationToken);
                    CosmosElement cosmosElementContinuationToken = OptimisticDirectExecutionContinuationToken.ToCosmosElement(optimisticDirectExecutionContinuationToken);
                    queryState = new QueryState(cosmosElementContinuationToken);
                }

                QueryPage queryPage = new QueryPage(
                    backendQueryPage.Documents,
                    backendQueryPage.RequestCharge,
                    backendQueryPage.ActivityId,
                    backendQueryPage.ResponseLengthInBytes,
                    backendQueryPage.CosmosQueryExecutionInfo,
                    disallowContinuationTokenMessage: null,
                    backendQueryPage.AdditionalHeaders,
                    queryState);

                this.Current = TryCatch<QueryPage>.FromResult(queryPage);
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

                TryCatch<FeedRangeState<QueryState>> monadicExtractState;
                if (continuationToken == null)
                {
                    FeedRangeState<QueryState> getState = new (targetRange, (QueryState)null);
                    monadicExtractState = TryCatch<FeedRangeState<QueryState>>.FromResult(getState);
                }
                else
                {
                    monadicExtractState = MonadicExtractState(continuationToken, targetRange);
                }

                if (monadicExtractState.Failed)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(monadicExtractState.Exception);
                }

                FeedRangeState<QueryState> feedRangeState = monadicExtractState.Result;

                QueryPartitionRangePageAsyncEnumerator partitionPageEnumerator = new QueryPartitionRangePageAsyncEnumerator(
                    documentContainer,
                    sqlQuerySpec,
                    feedRangeState,
                    partitionKey,
                    queryPaginationOptions,
                    cancellationToken);

                OptimisticDirectExecutionQueryPipelineImpl stage = new OptimisticDirectExecutionQueryPipelineImpl(partitionPageEnumerator);
                return TryCatch<IQueryPipelineStage>.FromResult(stage);
            }

            private static TryCatch<FeedRangeState<QueryState>> MonadicExtractState(
                CosmosElement continuationToken,
                FeedRangeEpk range)
            {
                if (continuationToken == null)
                {
                    throw new ArgumentNullException(nameof(continuationToken));
                }

                TryCatch<OptimisticDirectExecutionContinuationToken> tryCreateContinuationToken = OptimisticDirectExecutionContinuationToken.TryCreateFromCosmosElement(continuationToken);
                if (tryCreateContinuationToken.Failed)
                {
                    return TryCatch<FeedRangeState<QueryState>>.FromException(tryCreateContinuationToken.Exception);
                }

                TryCatch<PartitionMapping<OptimisticDirectExecutionContinuationToken>> partitionMappingMonad = PartitionMapper.MonadicGetPartitionMapping(
                    range,
                    tryCreateContinuationToken.Result);

                if (partitionMappingMonad.Failed)
                {
                    return TryCatch<FeedRangeState<QueryState>>.FromException(
                        partitionMappingMonad.Exception);
                }

                PartitionMapping<OptimisticDirectExecutionContinuationToken> partitionMapping = partitionMappingMonad.Result;

                KeyValuePair<FeedRangeEpk, OptimisticDirectExecutionContinuationToken> kvpRange = new KeyValuePair<FeedRangeEpk, OptimisticDirectExecutionContinuationToken>(
                    partitionMapping.TargetMapping.Keys.First(),
                    partitionMapping.TargetMapping.Values.First());

                FeedRangeState<QueryState> feedRangeState = new FeedRangeState<QueryState>(kvpRange.Key, kvpRange.Value?.Token != null ? new QueryState(CosmosString.Create(kvpRange.Value.Token.Token)) : null);

                return TryCatch<FeedRangeState<QueryState>>.FromResult(feedRangeState);
            }
        }
    }
}