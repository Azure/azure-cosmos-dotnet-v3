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

    internal sealed class OptimisticDirectExecutionQueryPipelineStage : IQueryPipelineStage
    {
        private enum ExecutionState
        {
            OptimisticDirectExecution,
            SpecializedDocumentQueryExecution,
        }

        private const string OptimisticDirectExecutionToken = "OptimisticDirectExecutionToken";
        private readonly FallbackQueryPipelineStageFactory queryPipelineStageFactory;
        private TryCatch<IQueryPipelineStage> inner;
        private CosmosElement continuationToken;
        private ExecutionState executionState;
        private bool? previousRequiresDistribution;

        private OptimisticDirectExecutionQueryPipelineStage(TryCatch<IQueryPipelineStage> inner, FallbackQueryPipelineStageFactory queryPipelineStageFactory, CosmosElement continuationToken)
        {
            this.inner = inner;
            this.queryPipelineStageFactory = queryPipelineStageFactory;
            this.continuationToken = continuationToken;
            this.executionState = ExecutionState.OptimisticDirectExecution;

            if (this.continuationToken != null)
            {
                this.previousRequiresDistribution = false;
            }
        }

        public delegate Task<TryCatch<IQueryPipelineStage>> FallbackQueryPipelineStageFactory(CosmosElement continuationToken);

        public TryCatch<QueryPage> Current => this.inner.Try<QueryPage>(pipelineStage => pipelineStage.Current);

        public ValueTask DisposeAsync()
        {
            return this.inner.Failed ? default : this.inner.Result.DisposeAsync();
        }

        public async ValueTask<bool> MoveNextAsync(ITrace trace)
        {
            TryCatch<bool> hasNext = await this.inner.TryAsync(pipelineStage => pipelineStage.MoveNextAsync(trace));
            bool success = hasNext.Succeeded && hasNext.Result;
            if (this.executionState == ExecutionState.OptimisticDirectExecution)
            {
                bool isPartitionSplitException = hasNext.Succeeded && this.Current.Failed && this.Current.InnerMostException.IsPartitionSplitException();
                if (success && !isPartitionSplitException)
                {
                    this.continuationToken = this.Current.Succeeded ? this.Current.Result.State?.Value : null;
                    if (this.continuationToken != null)
                    {
                        bool requiresDistribution;
                        if (this.Current.Result.AdditionalHeaders.TryGetValue(HttpConstants.HttpHeaders.RequiresDistribution, out string requiresDistributionHeaderValue))
                        {
                            requiresDistribution = bool.Parse(requiresDistributionHeaderValue);
                        }
                        else
                        {
                            requiresDistribution = true;
                        }

                        if (this.previousRequiresDistribution.HasValue && this.previousRequiresDistribution != requiresDistribution)
                        {
                            // We should never come here as requiresDistribution flag can never switch mid execution.
                            // Hence, this exception should never be thrown.
                            throw new InvalidOperationException($"Unexpected switch in {HttpConstants.HttpHeaders.RequiresDistribution} value. Previous value : {this.previousRequiresDistribution} Current value : {requiresDistribution}.");
                        }

                        if (requiresDistribution)
                        {
                            // This is where we will unwrap tne continuation token and extract the client distribution plan
                            // Pipelines to handle client distribution would be generated here
                            success = await this.SwitchToFallbackPipelineAsync(continuationToken: null, trace);
                        }

                        this.previousRequiresDistribution = requiresDistribution;
                    }
                }
                else if (isPartitionSplitException)
                {
                    success = await this.SwitchToFallbackPipelineAsync(continuationToken: UnwrapContinuationToken(this.continuationToken), trace);
                }
            }

            return success;
        }

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            this.inner.Try(pipelineStage => pipelineStage.SetCancellationToken(cancellationToken));
        }

        private static CosmosElement UnwrapContinuationToken(CosmosElement continuationToken)
        {
            if (continuationToken == null) return null;

            CosmosObject cosmosObject = continuationToken as CosmosObject;
            CosmosElement backendContinuationToken = cosmosObject[OptimisticDirectExecutionToken];
            Debug.Assert(backendContinuationToken != null);

            return CosmosArray.Create(backendContinuationToken);
        }

        private async Task<bool> SwitchToFallbackPipelineAsync(CosmosElement continuationToken, ITrace trace)
        {
            Debug.Assert(this.executionState == ExecutionState.OptimisticDirectExecution, "OptimisticDirectExecuteQueryPipelineStage Assert!", "Only OptimisticDirectExecute pipeline can create this fallback pipeline");
            this.executionState = ExecutionState.SpecializedDocumentQueryExecution;
            this.inner = continuationToken != null
                ? await this.queryPipelineStageFactory(continuationToken)
                : await this.queryPipelineStageFactory(null);

            if (this.inner.Failed)
            {
                return false;
            }

            return await this.inner.Result.MoveNextAsync(trace);
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            DocumentContainer documentContainer,
            CosmosQueryExecutionContextFactory.InputParameters inputParameters,
            FeedRangeEpk targetRange,
            ContainerQueryProperties containerQueryProperties,
            FallbackQueryPipelineStageFactory fallbackQueryPipelineStageFactory,
            CancellationToken cancellationToken)
        {
            QueryPaginationOptions paginationOptions = new QueryPaginationOptions(pageSizeHint: inputParameters.MaxItemCount, optimisticDirectExecute: true);
            TryCatch<IQueryPipelineStage> pipelineStage = OptimisticDirectExecutionQueryPipelineImpl.MonadicCreate(
                documentContainer: documentContainer,
                sqlQuerySpec: inputParameters.SqlQuerySpec,
                targetRange: targetRange,
                queryPaginationOptions: paginationOptions,
                partitionKey: inputParameters.PartitionKey,
                containerQueryProperties: containerQueryProperties,
                continuationToken: inputParameters.InitialUserContinuationToken,
                cancellationToken: cancellationToken);

            if (pipelineStage.Failed)
            {
                return pipelineStage;
            }

            OptimisticDirectExecutionQueryPipelineStage odePipelineStageMonadicCreate = new OptimisticDirectExecutionQueryPipelineStage(pipelineStage, fallbackQueryPipelineStageFactory, inputParameters.InitialUserContinuationToken);
            return TryCatch<IQueryPipelineStage>.FromResult(odePipelineStageMonadicCreate);
        }

        private sealed class OptimisticDirectExecutionQueryPipelineImpl : IQueryPipelineStage
        {
            private const int ClientQLCompatibilityLevel = 1;
            private readonly QueryPartitionRangePageAsyncEnumerator queryPartitionRangePageAsyncEnumerator;

            private OptimisticDirectExecutionQueryPipelineImpl(
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
                    this.Current = TryCatch<QueryPage>.FromException(partitionPage.Exception);
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
                    backendQueryPage.DistributionPlanSpec,
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
                ContainerQueryProperties containerQueryProperties,
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

                SqlQuerySpec updatedSqlQuerySpec = new SqlQuerySpec(sqlQuerySpec.QueryText, sqlQuerySpec.Parameters)
                {
                    ClientQLCompatibilityLevel = ClientQLCompatibilityLevel
                };

                FeedRangeState<QueryState> feedRangeState = monadicExtractState.Result;
                QueryPartitionRangePageAsyncEnumerator partitionPageEnumerator = new QueryPartitionRangePageAsyncEnumerator(
                    documentContainer,
                    updatedSqlQuerySpec,
                    feedRangeState,
                    partitionKey,
                    queryPaginationOptions,
                    containerQueryProperties,
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

                TryCatch<PartitionMapper.PartitionMapping<OptimisticDirectExecutionContinuationToken>> partitionMappingMonad = PartitionMapper.MonadicGetPartitionMapping(
                    range,
                    tryCreateContinuationToken.Result);

                if (partitionMappingMonad.Failed)
                {
                    return TryCatch<FeedRangeState<QueryState>>.FromException(
                        partitionMappingMonad.Exception);
                }

                PartitionMapper.PartitionMapping<OptimisticDirectExecutionContinuationToken> partitionMapping = partitionMappingMonad.Result;

                KeyValuePair<FeedRangeEpk, OptimisticDirectExecutionContinuationToken> kvpRange = new KeyValuePair<FeedRangeEpk, OptimisticDirectExecutionContinuationToken>(
                    partitionMapping.TargetMapping.Keys.First(),
                    partitionMapping.TargetMapping.Values.First());

                FeedRangeState<QueryState> feedRangeState = new FeedRangeState<QueryState>(kvpRange.Key, kvpRange.Value?.Token != null ? new QueryState(CosmosString.Create(kvpRange.Value.Token.Token)) : null);

                return TryCatch<FeedRangeState<QueryState>>.FromResult(feedRangeState);
            }
        }
    }
}