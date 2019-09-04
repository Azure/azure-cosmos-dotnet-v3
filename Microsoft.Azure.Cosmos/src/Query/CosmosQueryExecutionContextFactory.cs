//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Query.ParallelQuery;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Factory class for creating the appropriate DocumentQueryExecutionContext for the provided type of query.
    /// </summary>
    internal sealed class CosmosQueryExecutionContextFactory : CosmosQueryExecutionContext
    {
        internal const string InternalPartitionKeyDefinitionProperty = "x-ms-query-partitionkey-definition";
        private const int PageSizeFactorForTop = 5;

        internal readonly CosmosQueryContext CosmosQueryContext;
        private readonly string InitialUserContinuationToken;
        private CosmosQueryExecutionContext innerExecutionContext;

        /// <summary>
        /// Store the failed response
        /// </summary>
        private QueryResponseCore? responseMessageException;

        /// <summary>
        /// Store any exception thrown
        /// </summary>
        private Exception exception;

        /// <summary>
        /// Test flag for making the query use the opposite code path for query plan retrieval.
        /// If the SDK would have went to Gateway, then it will use ServiceInterop and visa versa.
        /// </summary>
        public static bool TestFlag = true;

        public CosmosQueryExecutionContextFactory(
            CosmosQueryClient client,
            ResourceType resourceTypeEnum,
            OperationType operationType,
            Type resourceType,
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            QueryRequestOptions queryRequestOptions,
            Uri resourceLink,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            Guid correlatedActivityId)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (sqlQuerySpec == null)
            {
                throw new ArgumentNullException(nameof(sqlQuerySpec));
            }

            if (queryRequestOptions == null)
            {
                throw new ArgumentNullException(nameof(queryRequestOptions));
            }

            if (resourceLink == null)
            {
                throw new ArgumentNullException(nameof(resourceLink));
            }

            // Prevent users from updating the values after creating the execution context.
            QueryRequestOptions cloneQueryRequestOptions = queryRequestOptions.Clone();

            // Swapping out negative values in feedOptions for int.MaxValue
            if (cloneQueryRequestOptions.MaxBufferedItemCount.HasValue && cloneQueryRequestOptions.MaxBufferedItemCount < 0)
            {
                cloneQueryRequestOptions.MaxBufferedItemCount = int.MaxValue;
            }

            if (cloneQueryRequestOptions.MaxConcurrency.HasValue && cloneQueryRequestOptions.MaxConcurrency < 0)
            {
                cloneQueryRequestOptions.MaxConcurrency = int.MaxValue;
            }

            if (cloneQueryRequestOptions.MaxItemCount.HasValue && cloneQueryRequestOptions.MaxItemCount < 0)
            {
                cloneQueryRequestOptions.MaxItemCount = int.MaxValue;
            }

            this.InitialUserContinuationToken = continuationToken;

            this.CosmosQueryContext = new CosmosQueryContext(
                  client: client,
                  resourceTypeEnum: resourceTypeEnum,
                  operationType: operationType,
                  resourceType: resourceType,
                  sqlQuerySpecFromUser: sqlQuerySpec,
                  queryRequestOptions: cloneQueryRequestOptions,
                  resourceLink: resourceLink,
                  isContinuationExpected: isContinuationExpected,
                  allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                  correlatedActivityId: correlatedActivityId);
        }

        public override bool IsDone
        {
            get
            {
                // No more results if an exception is hit
                if (this.responseMessageException != null || this.exception != null)
                {
                    return true;
                }

                return this.innerExecutionContext != null ? this.innerExecutionContext.IsDone : false;
            }
        }

        public override async Task<QueryResponseCore> ExecuteNextAsync(CancellationToken cancellationToken)
        {
            if (this.responseMessageException != null)
            {
                return this.responseMessageException.Value;
            }

            if (this.exception != null)
            {
                throw this.exception;
            }

            try
            {
                bool isFirstExecute = false;
                QueryResponseCore response;
                while (true)
                {
                    // The retry policy handles the scenario when the name cache is stale. If the cache is stale the entire 
                    // execute context has incorrect values and should be recreated. This should only be done for the first 
                    // execution. If results have already been pulled an error should be returned to the user since it's 
                    // not possible to combine query results from multiple containers.
                    if (this.innerExecutionContext == null)
                    {
                        this.innerExecutionContext = await this.CreateItemQueryExecutionContextAsync(cancellationToken);
                        isFirstExecute = true;
                    }

                    response = await this.innerExecutionContext.ExecuteNextAsync(cancellationToken);

                    if (response.IsSuccess)
                    {
                        break;
                    }

                    if (isFirstExecute && response.StatusCode == HttpStatusCode.Gone && response.SubStatusCode == SubStatusCodes.NameCacheIsStale)
                    {
                        await this.ForceRefreshCollectionCacheAsync(cancellationToken);
                        this.innerExecutionContext = await this.CreateItemQueryExecutionContextAsync(cancellationToken);
                        isFirstExecute = false;
                    }
                    else
                    {
                        break;
                    }
                }

                if (!response.IsSuccess)
                {
                    this.responseMessageException = response;
                }

                return response;
            }
            catch (Exception e)
            {
                this.exception = e;
                this.Dispose();
                throw;
            }
        }

        private async Task ForceRefreshCollectionCacheAsync(CancellationToken cancellationToken)
        {
            this.CosmosQueryContext.QueryClient.ClearSessionTokenCache(this.CosmosQueryContext.ResourceLink.OriginalString);

            CollectionCache collectionCache = await this.CosmosQueryContext.QueryClient.GetCollectionCacheAsync();
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
               OperationType.Query,
               this.CosmosQueryContext.ResourceTypeEnum,
               this.CosmosQueryContext.ResourceLink.OriginalString,
               AuthorizationTokenType.Invalid)) //this request doesn't actually go to server
            {
                request.ForceNameCacheRefresh = true;
                await collectionCache.ResolveCollectionAsync(request, cancellationToken);
            }
        }

        private async Task<CosmosQueryExecutionContext> CreateItemQueryExecutionContextAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CosmosQueryClient cosmosQueryClient = this.CosmosQueryContext.QueryClient;
            ContainerProperties containerProperties = await cosmosQueryClient.GetCachedContainerPropertiesAsync(cancellationToken);
            this.CosmosQueryContext.ContainerResourceId = containerProperties.ResourceId;

            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo;
            if (this.CosmosQueryContext.QueryClient.ByPassQueryParsing() && TestFlag)
            {
                // For non-Windows platforms(like Linux and OSX) in .NET Core SDK, we cannot use ServiceInterop, so need to bypass in that case.
                // We are also now bypassing this for 32 bit host process running even on Windows as there are many 32 bit apps that will not work without this
                partitionedQueryExecutionInfo = await QueryPlanRetriever.GetQueryPlanThroughGatewayAsync(
                    this.CosmosQueryContext.QueryClient,
                    this.CosmosQueryContext.SqlQuerySpec,
                    this.CosmosQueryContext.ResourceLink,
                    cancellationToken);
            }
            else
            {
                //todo:elasticcollections this may rely on information from collection cache which is outdated
                //if collection is deleted/created with same name.
                //need to make it not rely on information from collection cache.
                PartitionKeyDefinition partitionKeyDefinition;
                object partitionKeyDefinitionObject;
                if (this.CosmosQueryContext.QueryRequestOptions?.Properties != null
                    && this.CosmosQueryContext.QueryRequestOptions.Properties.TryGetValue(InternalPartitionKeyDefinitionProperty, out partitionKeyDefinitionObject))
                {
                    if (partitionKeyDefinitionObject is PartitionKeyDefinition definition)
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
                    partitionKeyDefinition = containerProperties.PartitionKey;
                }

                partitionedQueryExecutionInfo = await QueryPlanRetriever.GetQueryPlanWithServiceInteropAsync(
                    this.CosmosQueryContext.QueryClient,
                    this.CosmosQueryContext.SqlQuerySpec,
                    partitionKeyDefinition,
                    this.CosmosQueryContext.QueryRequestOptions?.PartitionKey != null,
                    cancellationToken);
            }

            List<PartitionKeyRange> targetRanges = await GetTargetPartitionKeyRangesAsync(
                   this.CosmosQueryContext.QueryClient,
                   this.CosmosQueryContext.ResourceLink.OriginalString,
                   partitionedQueryExecutionInfo,
                   containerProperties,
                   this.CosmosQueryContext.QueryRequestOptions);

            CosmosQueryContext rewrittenComosQueryContext;
            if (!string.IsNullOrEmpty(partitionedQueryExecutionInfo.QueryInfo.RewrittenQuery))
            {
                // We need pass down the rewritten query.
                SqlQuerySpec rewrittenQuerySpec = new SqlQuerySpec()
                {
                    QueryText = partitionedQueryExecutionInfo.QueryInfo.RewrittenQuery,
                    Parameters = this.CosmosQueryContext.SqlQuerySpec.Parameters
                };

                rewrittenComosQueryContext = new CosmosQueryContext(
                    client: this.CosmosQueryContext.QueryClient,
                    resourceTypeEnum: this.CosmosQueryContext.ResourceTypeEnum,
                    operationType: this.CosmosQueryContext.OperationTypeEnum,
                    resourceType: this.CosmosQueryContext.ResourceType,
                    sqlQuerySpecFromUser: rewrittenQuerySpec,
                    queryRequestOptions: this.CosmosQueryContext.QueryRequestOptions,
                    resourceLink: this.CosmosQueryContext.ResourceLink,
                    correlatedActivityId: this.CosmosQueryContext.CorrelatedActivityId,
                    isContinuationExpected: this.CosmosQueryContext.IsContinuationExpected,
                    allowNonValueAggregateQuery: this.CosmosQueryContext.AllowNonValueAggregateQuery,
                    containerResourceId: this.CosmosQueryContext.ContainerResourceId);
            }
            else
            {
                rewrittenComosQueryContext = this.CosmosQueryContext;
            }

            return await this.CreateSpecializedDocumentQueryExecutionContextAsync(
                rewrittenComosQueryContext,
                partitionedQueryExecutionInfo,
                targetRanges,
                containerProperties.ResourceId,
                cancellationToken);
        }

        public async Task<CosmosQueryExecutionContext> CreateSpecializedDocumentQueryExecutionContextAsync(
            CosmosQueryContext cosmosQueryContext,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            List<PartitionKeyRange> targetRanges,
            string collectionRid,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(partitionedQueryExecutionInfo.QueryInfo?.RewrittenQuery))
            {
                cosmosQueryContext.SqlQuerySpec = new SqlQuerySpec(
                    partitionedQueryExecutionInfo.QueryInfo.RewrittenQuery,
                    cosmosQueryContext.SqlQuerySpec.Parameters);
            }

            // Figure out the optimal page size.
            long initialPageSize = cosmosQueryContext.QueryRequestOptions.MaxItemCount.GetValueOrDefault(ParallelQueryConfig.GetConfig().ClientInternalPageSize);

            if (initialPageSize < -1 || initialPageSize == 0)
            {
                throw new BadRequestException(string.Format(CultureInfo.InvariantCulture, "Invalid MaxItemCount {0}", initialPageSize));
            }

            QueryInfo queryInfo = partitionedQueryExecutionInfo.QueryInfo;

            bool getLazyFeedResponse = queryInfo.HasTop;

            // We need to compute the optimal initial page size for order-by queries
            if (queryInfo.HasOrderBy)
            {
                int top;
                if (queryInfo.HasTop && (top = partitionedQueryExecutionInfo.QueryInfo.Top.Value) > 0)
                {
                    // All partitions should initially fetch about 1/nth of the top value.
                    long pageSizeWithTop = (long)Math.Min(
                        Math.Ceiling(top / (double)targetRanges.Count) * PageSizeFactorForTop,
                        top);

                    if (initialPageSize > 0)
                    {
                        initialPageSize = Math.Min(pageSizeWithTop, initialPageSize);
                    }
                    else
                    {
                        initialPageSize = pageSizeWithTop;
                    }
                }
                else if (cosmosQueryContext.IsContinuationExpected)
                {
                    if (initialPageSize < 0)
                    {
                        if (cosmosQueryContext.QueryRequestOptions.MaxBufferedItemCount.HasValue)
                        {
                            // Max of what the user is willing to buffer and the default (note this is broken if MaxBufferedItemCount = -1)
                            initialPageSize = Math.Max(cosmosQueryContext.QueryRequestOptions.MaxBufferedItemCount.Value, ParallelQueryConfig.GetConfig().DefaultMaximumBufferSize);
                        }
                        else
                        {
                            initialPageSize = ParallelQueryConfig.GetConfig().DefaultMaximumBufferSize;
                        }
                    }

                    initialPageSize = (long)Math.Min(
                        Math.Ceiling(initialPageSize / (double)targetRanges.Count) * PageSizeFactorForTop,
                        initialPageSize);
                }
            }

            Debug.Assert(initialPageSize > 0 && initialPageSize <= int.MaxValue,
                string.Format(CultureInfo.InvariantCulture, "Invalid MaxItemCount {0}", initialPageSize));

            return await PipelinedDocumentQueryExecutionContext.CreateAsync(
                cosmosQueryContext,
                collectionRid,
                partitionedQueryExecutionInfo,
                targetRanges,
                (int)initialPageSize,
                this.InitialUserContinuationToken,
                cancellationToken);
        }

        /// <summary>
        /// Gets the list of partition key ranges. 
        /// 1. Check partition key range id
        /// 2. Check Partition key
        /// 3. Check the effective partition key
        /// 4. Get the range from the PartitionedQueryExecutionInfo
        /// </summary>
        internal static async Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesAsync(
            CosmosQueryClient queryClient,
            string resourceLink,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            ContainerProperties collection,
            QueryRequestOptions queryRequestOptions)
        {
            List<PartitionKeyRange> targetRanges;
            if (queryRequestOptions.PartitionKey != null)
            {
                // Dis-ambiguate the NonePK if used 
                PartitionKeyInternal partitionKeyInternal = null;
                if (queryRequestOptions.PartitionKey.Value.IsNone)
                {
                    partitionKeyInternal = collection.GetNoneValue();
                }
                else
                {
                    partitionKeyInternal = queryRequestOptions.PartitionKey.Value.InternalKey;
                }

                targetRanges = await queryClient.GetTargetPartitionKeyRangesByEpkStringAsync(
                    resourceLink,
                    collection.ResourceId,
                    partitionKeyInternal.GetEffectivePartitionKeyString(collection.PartitionKey));
            }
            else if (TryGetEpkProperty(queryRequestOptions, out string effectivePartitionKeyString))
            {
                targetRanges = await queryClient.GetTargetPartitionKeyRangesByEpkStringAsync(
                    resourceLink,
                    collection.ResourceId,
                    effectivePartitionKeyString);
            }
            else
            {
                targetRanges = await queryClient.GetTargetPartitionKeyRangesAsync(
                    resourceLink,
                    collection.ResourceId,
                    partitionedQueryExecutionInfo.QueryRanges);
            }

            return targetRanges;
        }

        private static bool TryGetEpkProperty(
            QueryRequestOptions queryRequestOptions,
            out string effectivePartitionKeyString)
        {
            if (queryRequestOptions?.Properties != null
                && queryRequestOptions.Properties.TryGetValue(
                   WFConstants.BackendHeaders.EffectivePartitionKeyString,
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

        public override void Dispose()
        {
            if (this.innerExecutionContext != null && !this.innerExecutionContext.IsDone)
            {
                this.innerExecutionContext.Dispose();
            }
        }
    }
}
