//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using static Microsoft.Azure.Cosmos.Diagnostics.QueryPipelineDiagnostics;

    internal static class CosmosQueryExecutionContextFactory
    {
        private const string InternalPartitionKeyDefinitionProperty = "x-ms-query-partitionkey-definition";
        private const int PageSizeFactorForTop = 5;

        public static CosmosQueryExecutionContext Create(
            CosmosQueryContext cosmosQueryContext,
            InputParameters inputParameters)
        {
            if (cosmosQueryContext == null)
            {
                throw new ArgumentNullException(nameof(cosmosQueryContext));
            }

            if (inputParameters == null)
            {
                throw new ArgumentNullException(nameof(inputParameters));
            }

            CosmosQueryExecutionContextWithNameCacheStaleRetry cosmosQueryExecutionContextWithNameCacheStaleRetry = new CosmosQueryExecutionContextWithNameCacheStaleRetry(
                cosmosQueryContext: cosmosQueryContext,
                cosmosQueryExecutionContextFactory: () =>
                {
                    // Query Iterator requires that the creation of the query context is deferred until the user calls ReadNextAsync
                    AsyncLazy<(TryCatch<CosmosQueryExecutionContext>, QueryPipelineDiagnostics)> lazyTryCreateCosmosQueryExecutionContext = new AsyncLazy<(TryCatch<CosmosQueryExecutionContext>, QueryPipelineDiagnostics)>(valueFactory: (innerCancellationToken) =>
                    {
                        innerCancellationToken.ThrowIfCancellationRequested();
                        return CosmosQueryExecutionContextFactory.TryCreateCoreContextAsync(
                            cosmosQueryContext,
                            inputParameters,
                            innerCancellationToken);
                    });
                    LazyCosmosQueryExecutionContext lazyCosmosQueryExecutionContext = new LazyCosmosQueryExecutionContext(lazyTryCreateCosmosQueryExecutionContext);
                    return lazyCosmosQueryExecutionContext;
                });

            CatchAllCosmosQueryExecutionContext catchAllCosmosQueryExecutionContext = new CatchAllCosmosQueryExecutionContext(cosmosQueryExecutionContextWithNameCacheStaleRetry);

            return catchAllCosmosQueryExecutionContext;
        }

        private static async Task<(TryCatch<CosmosQueryExecutionContext>, QueryPipelineDiagnostics)> TryCreateCoreContextAsync(
            CosmosQueryContext cosmosQueryContext,
            InputParameters inputParameters,
            CancellationToken cancellationToken)
        {
            QueryPipelineDiagnosticBuilder diagnosticBuilder = new QueryPipelineDiagnosticBuilder(5);
            using (diagnosticBuilder.CreateScope("QueryPipelineCreation"))
            {
                // Try to parse the continuation token.
                string continuationToken = inputParameters.InitialUserContinuationToken;
                PartitionedQueryExecutionInfo queryPlanFromContinuationToken = inputParameters.PartitionedQueryExecutionInfo;
                if (continuationToken != null)
                {
                    using (diagnosticBuilder.CreateScope("ParseContinuationToken"))
                    {
                        if (!PipelineContinuationToken.TryParse(
                        continuationToken,
                        out PipelineContinuationToken pipelineContinuationToken))
                        {
                            return (TryCatch<CosmosQueryExecutionContext>.FromException(
                                new MalformedContinuationTokenException(
                                    $"Malformed {nameof(PipelineContinuationToken)}: {continuationToken}.")), null);
                        }

                        if (PipelineContinuationToken.IsTokenFromTheFuture(pipelineContinuationToken))
                        {
                            return (TryCatch<CosmosQueryExecutionContext>.FromException(
                                new MalformedContinuationTokenException(
                                    $"{nameof(PipelineContinuationToken)} Continuation token is from a newer version of the SDK. " +
                                    $"Upgrade the SDK to avoid this issue." +
                                    $"{continuationToken}.")), null);
                        }

                        if (!PipelineContinuationToken.TryConvertToLatest(
                            pipelineContinuationToken,
                            out PipelineContinuationTokenV1_1 latestVersionPipelineContinuationToken))
                        {
                            return (TryCatch<CosmosQueryExecutionContext>.FromException(
                                new MalformedContinuationTokenException(
                                    $"{nameof(PipelineContinuationToken)}: '{continuationToken}' is no longer supported.")), null);
                        }

                        continuationToken = latestVersionPipelineContinuationToken.SourceContinuationToken;
                        if (latestVersionPipelineContinuationToken.QueryPlan != null)
                        {
                            queryPlanFromContinuationToken = latestVersionPipelineContinuationToken.QueryPlan;
                        }
                    }
                }

                ContainerQueryProperties containerQueryProperties;
                CosmosQueryClient cosmosQueryClient = cosmosQueryContext.QueryClient;
                using (diagnosticBuilder.CreateScope("GetCachedContainerPropertiesAsync"))
                {
                    containerQueryProperties = await cosmosQueryClient.GetCachedContainerQueryPropertiesAsync(
                        cosmosQueryContext.ResourceLink,
                        inputParameters.PartitionKey,
                        cancellationToken);
                    cosmosQueryContext.ContainerResourceId = containerQueryProperties.ResourceId;
                }

                PartitionedQueryExecutionInfo partitionedQueryExecutionInfo;

                if (queryPlanFromContinuationToken != null)
                {
                    partitionedQueryExecutionInfo = queryPlanFromContinuationToken;
                }
                else
                {
                    if (cosmosQueryContext.QueryClient.ByPassQueryParsing())
                    {
                        using (diagnosticBuilder.CreateScope("GetQueryPlanThroughGateway"))
                        {
                            // For non-Windows platforms(like Linux and OSX) in .NET Core SDK, we cannot use ServiceInterop, so need to bypass in that case.
                            // We are also now bypassing this for 32 bit host process running even on Windows as there are many 32 bit apps that will not work without this
                            (PartitionedQueryExecutionInfo executionInfo, CosmosDiagnosticsContext diagnosticsContext) result = await QueryPlanRetriever.GetQueryPlanThroughGatewayAsync(
                                cosmosQueryContext.QueryClient,
                                inputParameters.SqlQuerySpec,
                                cosmosQueryContext.ResourceLink,
                                inputParameters.PartitionKey,
                                cancellationToken);

                            partitionedQueryExecutionInfo = result.executionInfo;
                            diagnosticBuilder.AddGatewayDiagnostics(result.diagnosticsContext);
                        }
                    }
                    else
                    {
                        using (diagnosticBuilder.CreateScope("GetQueryPlanWithServiceInterop"))
                        {
                            //todo:elasticcollections this may rely on information from collection cache which is outdated
                            //if collection is deleted/created with same name.
                            //need to make it not rely on information from collection cache.
                            Documents.PartitionKeyDefinition partitionKeyDefinition;
                            if ((inputParameters.Properties != null)
                                && inputParameters.Properties.TryGetValue(InternalPartitionKeyDefinitionProperty, out object partitionKeyDefinitionObject))
                            {
                                if (partitionKeyDefinitionObject is Documents.PartitionKeyDefinition definition)
                                {
                                    partitionKeyDefinition = definition;
                                }
                                else
                                {
                                    throw new ArgumentException(
                                        "partitionkeydefinition has invalid type",
                                        nameof(partitionKeyDefinitionObject));
                                }
                            }
                            else
                            {
                                partitionKeyDefinition = containerQueryProperties.PartitionKeyDefinition;
                            }

                            partitionedQueryExecutionInfo = await QueryPlanRetriever.GetQueryPlanWithServiceInteropAsync(
                                cosmosQueryContext.QueryClient,
                                inputParameters.SqlQuerySpec,
                                partitionKeyDefinition,
                                inputParameters.PartitionKey != null,
                                cancellationToken);
                        }
                    }
                }

                TryCatch<CosmosQueryExecutionContext> tryCreateExcutionInfo = await TryCreateFromPartitionedQueryExecutionInfoAsync(
                    partitionedQueryExecutionInfo,
                    containerQueryProperties,
                    cosmosQueryContext,
                    inputParameters,
                    cancellationToken);

                return (tryCreateExcutionInfo, diagnosticBuilder.Build());
            }
        }

        public static async Task<TryCatch<CosmosQueryExecutionContext>> TryCreateFromPartitionedQueryExecutionInfoAsync(
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            ContainerQueryProperties containerQueryProperties,
            CosmosQueryContext cosmosQueryContext,
            InputParameters inputParameters,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<Documents.PartitionKeyRange> targetRanges = await CosmosQueryExecutionContextFactory.GetTargetPartitionKeyRangesAsync(
                   cosmosQueryContext.QueryClient,
                   cosmosQueryContext.ResourceLink.OriginalString,
                   partitionedQueryExecutionInfo,
                   containerQueryProperties,
                   inputParameters.Properties);

            if (!string.IsNullOrEmpty(partitionedQueryExecutionInfo.QueryInfo.RewrittenQuery))
            {
                // We need pass down the rewritten query.
                SqlQuerySpec rewrittenQuerySpec = new SqlQuerySpec()
                {
                    QueryText = partitionedQueryExecutionInfo.QueryInfo.RewrittenQuery,
                    Parameters = inputParameters.SqlQuerySpec.Parameters
                };

                inputParameters = new InputParameters(
                    rewrittenQuerySpec,
                    inputParameters.InitialUserContinuationToken,
                    inputParameters.MaxConcurrency,
                    inputParameters.MaxItemCount,
                    inputParameters.MaxBufferedItemCount,
                    inputParameters.PartitionKey,
                    inputParameters.Properties,
                    inputParameters.PartitionedQueryExecutionInfo,
                    inputParameters.ExecutionEnvironment,
                    inputParameters.ReturnResultsInDeterministicOrder,
                    inputParameters.TestInjections);
            }

            return await CosmosQueryExecutionContextFactory.TryCreateSpecializedDocumentQueryExecutionContextAsync(
                cosmosQueryContext,
                inputParameters,
                partitionedQueryExecutionInfo,
                targetRanges,
                containerQueryProperties.ResourceId,
                cancellationToken);
        }

        private static async Task<TryCatch<CosmosQueryExecutionContext>> TryCreateSpecializedDocumentQueryExecutionContextAsync(
            CosmosQueryContext cosmosQueryContext,
            InputParameters inputParameters,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            List<Documents.PartitionKeyRange> targetRanges,
            string collectionRid,
            CancellationToken cancellationToken)
        {
            QueryInfo queryInfo = partitionedQueryExecutionInfo.QueryInfo;

            bool getLazyFeedResponse = queryInfo.HasTop;

            // We need to compute the optimal initial page size for order-by queries
            long optimalPageSize = inputParameters.MaxItemCount;
            if (queryInfo.HasOrderBy)
            {
                int top;
                if (queryInfo.HasTop && (top = partitionedQueryExecutionInfo.QueryInfo.Top.Value) > 0)
                {
                    // All partitions should initially fetch about 1/nth of the top value.
                    long pageSizeWithTop = (long)Math.Min(
                        Math.Ceiling(top / (double)targetRanges.Count) * CosmosQueryExecutionContextFactory.PageSizeFactorForTop,
                        top);

                    optimalPageSize = Math.Min(pageSizeWithTop, optimalPageSize);
                }
                else if (cosmosQueryContext.IsContinuationExpected)
                {
                    optimalPageSize = (long)Math.Min(
                        Math.Ceiling(optimalPageSize / (double)targetRanges.Count) * CosmosQueryExecutionContextFactory.PageSizeFactorForTop,
                        optimalPageSize);
                }
            }

            Debug.Assert(
                (optimalPageSize > 0) && (optimalPageSize <= int.MaxValue),
                $"Invalid MaxItemCount {optimalPageSize}");

            CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams = new CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams(
                sqlQuerySpec: inputParameters.SqlQuerySpec,
                collectionRid: collectionRid,
                partitionedQueryExecutionInfo: partitionedQueryExecutionInfo,
                partitionKeyRanges: targetRanges,
                initialPageSize: (int)optimalPageSize,
                maxConcurrency: inputParameters.MaxConcurrency,
                maxItemCount: inputParameters.MaxItemCount,
                maxBufferedItemCount: inputParameters.MaxBufferedItemCount,
                returnResultsInDeterministicOrder: inputParameters.ReturnResultsInDeterministicOrder,
                testSettings: inputParameters.TestInjections);

            return await PipelinedDocumentQueryExecutionContext.TryCreateAsync(
                inputParameters.ExecutionEnvironment,
                cosmosQueryContext,
                initParams,
                inputParameters.InitialUserContinuationToken,
                cancellationToken);
        }

        /// <summary>
        /// Gets the list of partition key ranges. 
        /// 1. Check partition key range id
        /// 2. Check Partition key
        /// 3. Check the effective partition key
        /// 4. Get the range from the PartitionedQueryExecutionInfo
        /// </summary>
        internal static async Task<List<Documents.PartitionKeyRange>> GetTargetPartitionKeyRangesAsync(
            CosmosQueryClient queryClient,
            string resourceLink,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            ContainerQueryProperties containerQueryProperties,
            IReadOnlyDictionary<string, object> properties)
        {
            List<Documents.PartitionKeyRange> targetRanges;
            if (containerQueryProperties.EffectivePartitionKeyString != null)
            {
                targetRanges = await queryClient.GetTargetPartitionKeyRangesByEpkStringAsync(
                    resourceLink,
                    containerQueryProperties.ResourceId,
                    containerQueryProperties.EffectivePartitionKeyString);
            }
            else if (TryGetEpkProperty(properties, out string effectivePartitionKeyString))
            {
                targetRanges = await queryClient.GetTargetPartitionKeyRangesByEpkStringAsync(
                    resourceLink,
                    containerQueryProperties.ResourceId,
                    effectivePartitionKeyString);
            }
            else
            {
                targetRanges = await queryClient.GetTargetPartitionKeyRangesAsync(
                    resourceLink,
                    containerQueryProperties.ResourceId,
                    partitionedQueryExecutionInfo.QueryRanges);
            }

            return targetRanges;
        }

        private static bool TryGetEpkProperty(
            IReadOnlyDictionary<string, object> properties,
            out string effectivePartitionKeyString)
        {
            if (properties != null
                && properties.TryGetValue(
                   Documents.WFConstants.BackendHeaders.EffectivePartitionKeyString,
                   out object effectivePartitionKeyStringObject))
            {
                effectivePartitionKeyString = effectivePartitionKeyStringObject as string;
                if (string.IsNullOrEmpty(effectivePartitionKeyString))
                {
                    throw new ArgumentOutOfRangeException(nameof(effectivePartitionKeyString));
                }

                return true;
            }

            effectivePartitionKeyString = null;
            return false;
        }

        public sealed class InputParameters
        {
            private const int DefaultMaxConcurrency = 0;
            private const int DefaultMaxItemCount = 1000;
            private const int DefaultMaxBufferedItemCount = 1000;
            private const bool DefaultReturnResultsInDeterministicOrder = true;
            private const ExecutionEnvironment DefaultExecutionEnvironment = ExecutionEnvironment.Client;

            public InputParameters(
                SqlQuerySpec sqlQuerySpec,
                string initialUserContinuationToken,
                int? maxConcurrency,
                int? maxItemCount,
                int? maxBufferedItemCount,
                PartitionKey? partitionKey,
                IReadOnlyDictionary<string, object> properties,
                PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
                ExecutionEnvironment? executionEnvironment,
                bool? returnResultsInDeterministicOrder,
                TestInjections testInjections)
            {
                this.SqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
                this.InitialUserContinuationToken = initialUserContinuationToken;

                int resolvedMaxConcurrency = maxConcurrency.GetValueOrDefault(InputParameters.DefaultMaxConcurrency);
                if (resolvedMaxConcurrency < 0)
                {
                    resolvedMaxConcurrency = int.MaxValue;
                }
                this.MaxConcurrency = resolvedMaxConcurrency;

                int resolvedMaxItemCount = maxItemCount.GetValueOrDefault(InputParameters.DefaultMaxItemCount);
                if (resolvedMaxItemCount < 0)
                {
                    resolvedMaxItemCount = int.MaxValue;
                }
                this.MaxItemCount = resolvedMaxItemCount;

                int resolvedMaxBufferedItemCount = maxBufferedItemCount.GetValueOrDefault(InputParameters.DefaultMaxBufferedItemCount);
                if (resolvedMaxBufferedItemCount < 0)
                {
                    resolvedMaxBufferedItemCount = int.MaxValue;
                }
                this.MaxBufferedItemCount = resolvedMaxBufferedItemCount;

                this.PartitionKey = partitionKey;
                this.Properties = properties;
                this.PartitionedQueryExecutionInfo = partitionedQueryExecutionInfo;
                this.ExecutionEnvironment = executionEnvironment.GetValueOrDefault(InputParameters.DefaultExecutionEnvironment);
                this.ReturnResultsInDeterministicOrder = returnResultsInDeterministicOrder.GetValueOrDefault(InputParameters.DefaultReturnResultsInDeterministicOrder);
                this.TestInjections = testInjections;
            }

            public SqlQuerySpec SqlQuerySpec { get; }
            public string InitialUserContinuationToken { get; }
            public int MaxConcurrency { get; }
            public int MaxItemCount { get; }
            public int MaxBufferedItemCount { get; }
            public PartitionKey? PartitionKey { get; }
            public IReadOnlyDictionary<string, object> Properties { get; }
            public PartitionedQueryExecutionInfo PartitionedQueryExecutionInfo { get; }
            public ExecutionEnvironment ExecutionEnvironment { get; }
            public bool ReturnResultsInDeterministicOrder { get; }
            public TestInjections TestInjections { get; }
        }
    }
}
