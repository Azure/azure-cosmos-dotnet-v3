//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    internal class CosmosQueryClientCore : CosmosQueryClient
    {
        private readonly CosmosClientContext clientContext;
        private readonly ContainerCore cosmosContainerCore;
        private readonly DocumentClient documentClient;
        private readonly SemaphoreSlim semaphore;
        private QueryPartitionProvider queryPartitionProvider;

        internal CosmosQueryClientCore(
            CosmosClientContext clientContext,
            ContainerCore cosmosContainerCore)
        {
            this.clientContext = clientContext ?? throw new ArgumentException(nameof(clientContext));
            this.cosmosContainerCore = cosmosContainerCore;
            this.documentClient = this.clientContext.DocumentClient;
            this.semaphore = new SemaphoreSlim(1, 1);
        }

        internal override Action<IQueryable> OnExecuteScalarQueryCallback => this.documentClient.OnExecuteScalarQueryCallback;

        internal override async Task<ContainerQueryProperties> GetCachedContainerQueryPropertiesAsync(
            Uri containerLink,
            PartitionKey? partitionKey,
            CancellationToken cancellationToken)
        {
            ContainerProperties containerProperties = await this.clientContext.GetCachedContainerPropertiesAsync(
                containerLink.OriginalString,
                cancellationToken);

            string effectivePartitionKeyString = null;
            if (partitionKey != null)
            {
                // Dis-ambiguate the NonePK if used 
                Documents.Routing.PartitionKeyInternal partitionKeyInternal = null;
                if (partitionKey.Value.IsNone)
                {
                    partitionKeyInternal = containerProperties.GetNoneValue();
                }
                else
                {
                    partitionKeyInternal = partitionKey.Value.InternalKey;
                }
                effectivePartitionKeyString = partitionKeyInternal.GetEffectivePartitionKeyString(containerProperties.PartitionKey);
            }

            return new ContainerQueryProperties(
                containerProperties.ResourceId,
                effectivePartitionKeyString,
                containerProperties.PartitionKey);
        }

        internal override async Task<TryCatch<PartitionedQueryExecutionInfo>> TryGetPartitionedQueryExecutionInfoAsync(
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool hasLogicalPartitionKey,
            CancellationToken cancellationToken)
        {
            if (this.queryPartitionProvider == null)
            {
                try
                {
                    await this.semaphore.WaitAsync(cancellationToken);

                    if (this.queryPartitionProvider == null)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        IDictionary<string, object> queryConfiguration = await this.documentClient.GetQueryEngineConfigurationAsync();
                        this.queryPartitionProvider = new QueryPartitionProvider(queryConfiguration);
                    }
                }
                finally
                {
                    this.semaphore.Release();
                }
            }

            return this.queryPartitionProvider.TryGetPartitionedQueryExecutionInfo(
                sqlQuerySpec,
                partitionKeyDefinition,
                requireFormattableOrderByQuery,
                isContinuationExpected,
                allowNonValueAggregateQuery,
                hasLogicalPartitionKey);
        }

        internal override async Task<QueryResponseCore> ExecuteItemQueryAsync<RequestOptionType>(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptionType requestOptions,
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            PartitionKeyRangeIdentity partitionKeyRange,
            bool isContinuationExpected,
            int pageSize,
            SchedulingStopwatch schedulingStopwatch,
            CancellationToken cancellationToken)
        {
            if (!(requestOptions is QueryRequestOptions queryRequestOptions))
            {
                throw new InvalidOperationException($"CosmosQueryClientCore.ExecuteItemQueryAsync only supports RequestOptionType of QueryRequestOptions");
            }

            queryRequestOptions.MaxItemCount = pageSize;

            schedulingStopwatch.Start();
            ResponseMessage message = await this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: queryRequestOptions,
                partitionKey: queryRequestOptions.PartitionKey,
                cosmosContainerCore: this.cosmosContainerCore,
                streamPayload: this.clientContext.SerializerCore.ToStreamSqlQuerySpec(sqlQuerySpec, resourceType),
                requestEnricher: (cosmosRequestMessage) =>
                {
                    this.PopulatePartitionKeyRangeInfo(cosmosRequestMessage, partitionKeyRange);
                    cosmosRequestMessage.Headers.Add(
                        HttpConstants.HttpHeaders.IsContinuationExpected,
                        isContinuationExpected.ToString());
                    QueryRequestOptions.FillContinuationToken(
                        cosmosRequestMessage,
                        continuationToken);
                    cosmosRequestMessage.Headers.Add(HttpConstants.HttpHeaders.ContentType, MediaTypes.QueryJson);
                    cosmosRequestMessage.Headers.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                },
                diagnosticsScope: queryRequestOptions?.DiagnosticContext,
                cancellationToken: cancellationToken);

            schedulingStopwatch.Stop();

            return this.GetCosmosElementResponse(
                queryRequestOptions,
                resourceType,
                message,
                partitionKeyRange,
                schedulingStopwatch);
        }

        internal override async Task<PartitionedQueryExecutionInfo> ExecuteQueryPlanRequestAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            SqlQuerySpec sqlQuerySpec,
            PartitionKey? partitionKey,
            string supportedQueryFeatures,
            CancellationToken cancellationToken)
        {
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo;
            using (ResponseMessage message = await this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: null,
                partitionKey: partitionKey,
                cosmosContainerCore: this.cosmosContainerCore,
                streamPayload: this.clientContext.SerializerCore.ToStreamSqlQuerySpec(sqlQuerySpec, resourceType),
                requestEnricher: (requestMessage) =>
                {
                    requestMessage.Headers.Add(HttpConstants.HttpHeaders.ContentType, RuntimeConstants.MediaTypes.QueryJson);
                    requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsQueryPlanRequest, bool.TrueString);
                    requestMessage.Headers.Add(HttpConstants.HttpHeaders.SupportedQueryFeatures, supportedQueryFeatures);
                    requestMessage.Headers.Add(HttpConstants.HttpHeaders.QueryVersion, new Version(major: 1, minor: 0).ToString());
                    requestMessage.UseGatewayMode = true;
                },
                diagnosticsScope: null,
                cancellationToken: cancellationToken))
            {
                // Syntax exception are argument exceptions and thrown to the user.
                message.EnsureSuccessStatusCode();
                partitionedQueryExecutionInfo = this.clientContext.SerializerCore.FromStream<PartitionedQueryExecutionInfo>(message.Content);
            }

            return partitionedQueryExecutionInfo;
        }

        internal override Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesByEpkStringAsync(
            string resourceLink,
            string collectionResourceId,
            string effectivePartitionKeyString)
        {
            return this.GetTargetPartitionKeyRangesAsync(
                resourceLink,
                collectionResourceId,
                new List<Range<string>>
                {
                    Range<string>.GetPointRange(effectivePartitionKeyString)
                });
        }

        internal override async Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangeByFeedTokenAsync(
            string resourceLink,
            string collectionResourceId,
            PartitionKeyDefinition partitionKeyDefinition,
            IQueryFeedToken queryFeedToken)
        {
            IRoutingMapProvider routingMapProvider = await this.GetRoutingMapProviderAsync();
            List<Range<string>> ranges = await queryFeedToken.GetAffectedRangesAsync(routingMapProvider, collectionResourceId, partitionKeyDefinition);

            return await this.GetTargetPartitionKeyRangesAsync(
                resourceLink,
                collectionResourceId,
                ranges);
        }

        internal override async Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesAsync(
            string resourceLink,
            string collectionResourceId,
            List<Range<string>> providedRanges)
        {
            if (string.IsNullOrEmpty(collectionResourceId))
            {
                throw new ArgumentNullException(nameof(collectionResourceId));
            }

            if (providedRanges == null ||
                !providedRanges.Any() ||
                providedRanges.Any(x => x == null))
            {
                throw new ArgumentNullException(nameof(providedRanges));
            }

            IRoutingMapProvider routingMapProvider = await this.GetRoutingMapProviderAsync();

            List<PartitionKeyRange> ranges = await routingMapProvider.TryGetOverlappingRangesAsync(collectionResourceId, providedRanges);
            if (ranges == null && PathsHelper.IsNameBased(resourceLink))
            {
                // Refresh the cache and don't try to re-resolve collection as it is not clear what already
                // happened based on previously resolved collection rid.
                // Return NotFoundException this time. Next query will succeed.
                // This can only happen if collection is deleted/created with same name and client was not restarted
                // in between.
                CollectionCache collectionCache = await this.documentClient.GetCollectionCacheAsync();
                collectionCache.Refresh(resourceLink);
            }

            if (ranges == null)
            {
                throw new NotFoundException($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}: GetTargetPartitionKeyRanges(collectionResourceId:{collectionResourceId}, providedRanges: {string.Join(",", providedRanges)} failed due to stale cache");
            }

            return ranges;
        }

        internal override bool ByPassQueryParsing()
        {
            return CustomTypeExtensions.ByPassQueryParsing();
        }

        internal override void ClearSessionTokenCache(string collectionFullName)
        {
            ISessionContainer sessionContainer = this.clientContext.DocumentClient.sessionContainer;
            sessionContainer.ClearTokenByCollectionFullname(collectionFullName);
        }

        private QueryResponseCore GetCosmosElementResponse(
            QueryRequestOptions requestOptions,
            ResourceType resourceType,
            ResponseMessage cosmosResponseMessage,
            PartitionKeyRangeIdentity partitionKeyRangeIdentity,
            SchedulingStopwatch schedulingStopwatch)
        {
            using (cosmosResponseMessage)
            {
                QueryPageDiagnostics diagnostics = new QueryPageDiagnostics(
                    partitionKeyRangeId: partitionKeyRangeIdentity.PartitionKeyRangeId,
                    queryMetricText: cosmosResponseMessage.Headers.QueryMetricsText,
                    indexUtilizationText: cosmosResponseMessage.Headers[HttpConstants.HttpHeaders.IndexUtilization],
                    diagnosticsContext: cosmosResponseMessage.DiagnosticsContext,
                    schedulingStopwatch: schedulingStopwatch);

                IReadOnlyCollection<QueryPageDiagnostics> pageDiagnostics = new List<QueryPageDiagnostics>() { diagnostics };
                if (!cosmosResponseMessage.IsSuccessStatusCode)
                {
                    return QueryResponseCore.CreateFailure(
                        statusCode: cosmosResponseMessage.StatusCode,
                        subStatusCodes: cosmosResponseMessage.Headers.SubStatusCode,
                        cosmosException: cosmosResponseMessage.CosmosException,
                        requestCharge: cosmosResponseMessage.Headers.RequestCharge,
                        activityId: cosmosResponseMessage.Headers.ActivityId,
                        diagnostics: pageDiagnostics);
                }

                if (!(cosmosResponseMessage.Content is MemoryStream memoryStream))
                {
                    memoryStream = new MemoryStream();
                    cosmosResponseMessage.Content.CopyTo(memoryStream);
                }

                long responseLengthBytes = memoryStream.Length;
                CosmosArray cosmosArray = CosmosElementSerializer.ToCosmosElements(
                    memoryStream,
                    resourceType,
                    requestOptions.CosmosSerializationFormatOptions);

                int itemCount = cosmosArray.Count;
                return QueryResponseCore.CreateSuccess(
                    result: cosmosArray,
                    requestCharge: cosmosResponseMessage.Headers.RequestCharge,
                    activityId: cosmosResponseMessage.Headers.ActivityId,
                    diagnostics: pageDiagnostics,
                    responseLengthBytes: responseLengthBytes,
                    disallowContinuationTokenMessage: null,
                    continuationToken: cosmosResponseMessage.Headers.ContinuationToken);
            }
        }

        private void PopulatePartitionKeyRangeInfo(
            RequestMessage request,
            PartitionKeyRangeIdentity partitionKeyRangeIdentity)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.ResourceType.IsPartitioned())
            {
                // If the request already has the logical partition key,
                // then we shouldn't add the physical partition key range id.

                bool hasPartitionKey = request.Headers.PartitionKey != null;
                if (!hasPartitionKey)
                {
                    request
                        .ToDocumentServiceRequest()
                        .RouteTo(partitionKeyRangeIdentity);
                }
            }
        }

        internal override async Task ForceRefreshCollectionCacheAsync(string collectionLink, CancellationToken cancellationToken)
        {
            this.ClearSessionTokenCache(collectionLink);

            CollectionCache collectionCache = await this.documentClient.GetCollectionCacheAsync();
            using (Documents.DocumentServiceRequest request = Documents.DocumentServiceRequest.Create(
               Documents.OperationType.Query,
               Documents.ResourceType.Collection,
               collectionLink,
               Documents.AuthorizationTokenType.Invalid)) //this request doesn't actually go to server
            {
                request.ForceNameCacheRefresh = true;
                await collectionCache.ResolveCollectionAsync(request, cancellationToken);
            }
        }

        internal override async Task<IReadOnlyList<PartitionKeyRange>> TryGetOverlappingRangesAsync(
            string collectionResourceId,
            Range<string> range,
            bool forceRefresh = false)
        {
            PartitionKeyRangeCache partitionKeyRangeCache = await this.GetRoutingMapProviderAsync();
            return await partitionKeyRangeCache.TryGetOverlappingRangesAsync(collectionResourceId, range, forceRefresh);
        }

        private Task<PartitionKeyRangeCache> GetRoutingMapProviderAsync()
        {
            return this.documentClient.GetPartitionKeyRangeCacheAsync();
        }
    }
}