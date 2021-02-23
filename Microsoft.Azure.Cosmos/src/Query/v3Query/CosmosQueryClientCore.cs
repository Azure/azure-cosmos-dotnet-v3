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
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    using Newtonsoft.Json;

    using static Microsoft.Azure.Documents.RuntimeConstants;

    internal class CosmosQueryClientCore : CosmosQueryClient
    {
        private const string QueryExecutionInfoHeader = "x-ms-cosmos-query-execution-info";

        private readonly CosmosClientContext clientContext;
        private readonly ContainerInternal cosmosContainerCore;
        private readonly DocumentClient documentClient;
        private readonly SemaphoreSlim semaphore;

        public CosmosQueryClientCore(
            CosmosClientContext clientContext,
            ContainerInternal cosmosContainerCore)
        {
            this.clientContext = clientContext ?? throw new ArgumentException(nameof(clientContext));
            this.cosmosContainerCore = cosmosContainerCore;
            this.documentClient = this.clientContext.DocumentClient;
            this.semaphore = new SemaphoreSlim(1, 1);
        }

        public override Action<IQueryable> OnExecuteScalarQueryCallback => this.documentClient.OnExecuteScalarQueryCallback;

        public override async Task<ContainerQueryProperties> GetCachedContainerQueryPropertiesAsync(
            string containerLink,
            PartitionKey? partitionKey,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ContainerProperties containerProperties = await this.clientContext.GetCachedContainerPropertiesAsync(
                containerLink,
                trace,
                cancellationToken);

            string effectivePartitionKeyString = null;
            if (partitionKey != null)
            {
                // Dis-ambiguate the NonePK if used 
                PartitionKeyInternal partitionKeyInternal = partitionKey.Value.IsNone ? containerProperties.GetNoneValue() : partitionKey.Value.InternalKey;
                effectivePartitionKeyString = partitionKeyInternal.GetEffectivePartitionKeyString(containerProperties.PartitionKey);
            }

            return new ContainerQueryProperties(
                containerProperties.ResourceId,
                effectivePartitionKeyString,
                containerProperties.PartitionKey);
        }

        public override async Task<TryCatch<PartitionedQueryExecutionInfo>> TryGetPartitionedQueryExecutionInfoAsync(
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool hasLogicalPartitionKey,
            CancellationToken cancellationToken)
        {
            return (await this.documentClient.QueryPartitionProvider).TryGetPartitionedQueryExecutionInfo(
                sqlQuerySpec,
                partitionKeyDefinition,
                requireFormattableOrderByQuery,
                isContinuationExpected,
                allowNonValueAggregateQuery,
                hasLogicalPartitionKey);
        }

        public override async Task<TryCatch<QueryPage>> ExecuteItemQueryAsync(
            string resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            Guid clientQueryCorrelationId,
            FeedRange feedRange,
            QueryRequestOptions requestOptions,
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            bool isContinuationExpected,
            int pageSize,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            requestOptions.MaxItemCount = pageSize;

            ResponseMessage message = await this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                feedRange: feedRange,
                cosmosContainerCore: this.cosmosContainerCore,
                streamPayload: this.clientContext.SerializerCore.ToStreamSqlQuerySpec(sqlQuerySpec, resourceType),
                requestEnricher: (cosmosRequestMessage) =>
                {
                    cosmosRequestMessage.Headers.Add(
                        HttpConstants.HttpHeaders.IsContinuationExpected,
                        isContinuationExpected.ToString());
                    QueryRequestOptions.FillContinuationToken(
                        cosmosRequestMessage,
                        continuationToken);
                    cosmosRequestMessage.Headers.Add(HttpConstants.HttpHeaders.ContentType, MediaTypes.QueryJson);
                    cosmosRequestMessage.Headers.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                },
                trace: trace,
                cancellationToken: cancellationToken);

            return CosmosQueryClientCore.GetCosmosElementResponse(
                requestOptions,
                resourceType,
                message,
                trace);
        }

        public override async Task<PartitionedQueryExecutionInfo> ExecuteQueryPlanRequestAsync(
            string resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            SqlQuerySpec sqlQuerySpec,
            PartitionKey? partitionKey,
            string supportedQueryFeatures,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo;
            using (ResponseMessage message = await this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: null,
                feedRange: partitionKey.HasValue ? new FeedRangePartitionKey(partitionKey.Value) : null,
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
                trace: trace,
                cancellationToken: cancellationToken))
            {
                // Syntax exception are argument exceptions and thrown to the user.
                message.EnsureSuccessStatusCode();
                partitionedQueryExecutionInfo = this.clientContext.SerializerCore.FromStream<PartitionedQueryExecutionInfo>(message.Content);
            }

            return partitionedQueryExecutionInfo;
        }

        public override Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesByEpkStringAsync(
            string resourceLink,
            string collectionResourceId,
            string effectivePartitionKeyString,
            bool forceRefresh,
            ITrace trace)
        {
            return this.GetTargetPartitionKeyRangesAsync(
                resourceLink,
                collectionResourceId,
                new List<Range<string>>
                {
                    Range<string>.GetPointRange(effectivePartitionKeyString)
                },
                forceRefresh,
                trace);
        }

        public override async Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangeByFeedRangeAsync(
            string resourceLink,
            string collectionResourceId,
            PartitionKeyDefinition partitionKeyDefinition,
            FeedRangeInternal feedRangeInternal,
            bool forceRefresh,
            ITrace trace)
        {
            using (ITrace childTrace = trace.StartChild("Get Overlapping Feed Ranges", TraceComponent.Routing, Tracing.TraceLevel.Info))
            {
                IRoutingMapProvider routingMapProvider = await this.GetRoutingMapProviderAsync();
                List<Range<string>> ranges = await feedRangeInternal.GetEffectiveRangesAsync(routingMapProvider, collectionResourceId, partitionKeyDefinition);

                return await this.GetTargetPartitionKeyRangesAsync(
                    resourceLink,
                    collectionResourceId,
                    ranges,
                    forceRefresh,
                    childTrace);
            }
        }

        public override async Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesAsync(
            string resourceLink,
            string collectionResourceId,
            List<Range<string>> providedRanges,
            bool forceRefresh,
            ITrace trace)
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

            using (ITrace getPKRangesTrace = trace.StartChild("Get Partition Key Ranges", TraceComponent.Routing, Tracing.TraceLevel.Info))
            {
                IRoutingMapProvider routingMapProvider = await this.GetRoutingMapProviderAsync();

                List<PartitionKeyRange> ranges = await routingMapProvider.TryGetOverlappingRangesAsync(collectionResourceId, providedRanges);
                if (ranges == null && PathsHelper.IsNameBased(resourceLink))
                {
                    // Refresh the cache and don't try to re-resolve collection as it is not clear what already
                    // happened based on previously resolved collection rid.
                    // Return NotFoundException this time. Next query will succeed.
                    // This can only happen if collection is deleted/created with same name and client was not restarted
                    // in between.
                    CollectionCache collectionCache = await this.documentClient.GetCollectionCacheAsync(getPKRangesTrace);
                    collectionCache.Refresh(resourceLink);
                }

                if (ranges == null)
                {
                    throw new NotFoundException($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}: GetTargetPartitionKeyRanges(collectionResourceId:{collectionResourceId}, providedRanges: {string.Join(",", providedRanges)} failed due to stale cache");
                }

                return ranges;
            }
        }

        public override bool ByPassQueryParsing()
        {
            return CustomTypeExtensions.ByPassQueryParsing();
        }

        public override void ClearSessionTokenCache(string collectionFullName)
        {
            ISessionContainer sessionContainer = this.clientContext.DocumentClient.sessionContainer;
            sessionContainer.ClearTokenByCollectionFullname(collectionFullName);
        }

        private static TryCatch<QueryPage> GetCosmosElementResponse(
            QueryRequestOptions requestOptions,
            ResourceType resourceType,
            ResponseMessage cosmosResponseMessage,
            ITrace trace)
        {
            using (ITrace getCosmosElementResponse = trace.StartChild("Get Cosmos Element Response", TraceComponent.Json, Tracing.TraceLevel.Info))
            {
                using (cosmosResponseMessage)
                {
                    if (
                        cosmosResponseMessage.Headers.QueryMetricsText != null &&
                        BackendMetricsParser.TryParse(cosmosResponseMessage.Headers.QueryMetricsText, out BackendMetrics backendMetrics))
                    {
                        QueryMetricsTraceDatum datum = new QueryMetricsTraceDatum(
                            new QueryMetrics(backendMetrics, IndexUtilizationInfo.Empty, ClientSideMetrics.Empty));
                        trace.AddDatum("Query Metrics", datum);
                    }

                    if (!cosmosResponseMessage.IsSuccessStatusCode)
                    {
                        CosmosException exception = cosmosResponseMessage.CosmosException ?? new CosmosException(
                            cosmosResponseMessage.ErrorMessage,
                            cosmosResponseMessage.StatusCode,
                            (int)cosmosResponseMessage.Headers.SubStatusCode,
                            cosmosResponseMessage.Headers.ActivityId,
                            cosmosResponseMessage.Headers.RequestCharge);
                        return TryCatch<QueryPage>.FromException(exception);
                    }

                    if (!(cosmosResponseMessage.Content is MemoryStream memoryStream))
                    {
                        memoryStream = new MemoryStream();
                        cosmosResponseMessage.Content.CopyTo(memoryStream);
                    }

                    long responseLengthBytes = memoryStream.Length;
                    CosmosArray documents = CosmosQueryClientCore.ParseElementsFromRestStream(
                        memoryStream,
                        resourceType,
                        requestOptions.CosmosSerializationFormatOptions);

                    CosmosQueryExecutionInfo cosmosQueryExecutionInfo;
                    if (cosmosResponseMessage.Headers.TryGetValue(QueryExecutionInfoHeader, out string queryExecutionInfoString))
                    {
                        cosmosQueryExecutionInfo = JsonConvert.DeserializeObject<CosmosQueryExecutionInfo>(queryExecutionInfoString);
                    }
                    else
                    {
                        cosmosQueryExecutionInfo = default;
                    }

                    QueryState queryState;
                    if (cosmosResponseMessage.Headers.ContinuationToken != null)
                    {
                        queryState = new QueryState(CosmosString.Create(cosmosResponseMessage.Headers.ContinuationToken));
                    }
                    else
                    {
                        queryState = default;
                    }

                    Dictionary<string, string> additionalHeaders = new Dictionary<string, string>();
                    foreach (string key in cosmosResponseMessage.Headers)
                    {
                        if (!QueryPage.BannedHeaders.Contains(key))
                        {
                            additionalHeaders[key] = cosmosResponseMessage.Headers[key];
                        }
                    }

                    QueryPage response = new QueryPage(
                        documents,
                        cosmosResponseMessage.Headers.RequestCharge,
                        cosmosResponseMessage.Headers.ActivityId,
                        responseLengthBytes,
                        cosmosQueryExecutionInfo,
                        disallowContinuationTokenMessage: null,
                        additionalHeaders,
                        queryState);

                    return TryCatch<QueryPage>.FromResult(response);
                }
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

        public override async Task ForceRefreshCollectionCacheAsync(string collectionLink, CancellationToken cancellationToken)
        {
            this.ClearSessionTokenCache(collectionLink);

            CollectionCache collectionCache = await this.documentClient.GetCollectionCacheAsync(NoOpTrace.Singleton);
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

        public override async Task<IReadOnlyList<PartitionKeyRange>> TryGetOverlappingRangesAsync(
            string collectionResourceId,
            Range<string> range,
            bool forceRefresh = false)
        {
            PartitionKeyRangeCache partitionKeyRangeCache = await this.GetRoutingMapProviderAsync();
            return await partitionKeyRangeCache.TryGetOverlappingRangesAsync( 
                collectionResourceId, 
                range,
                NoOpTrace.Singleton,
                forceRefresh);
        }

        private Task<PartitionKeyRangeCache> GetRoutingMapProviderAsync()
        {
            return this.documentClient.GetPartitionKeyRangeCacheAsync();
        }

        /// <summary>
        /// Converts a list of CosmosElements into a memory stream.
        /// </summary>
        /// <param name="stream">The memory stream response for the query REST response Azure Cosmos</param>
        /// <param name="resourceType">The resource type</param>
        /// <param name="cosmosSerializationOptions">The custom serialization options. This allows custom serialization types like BSON, JSON, or other formats</param>
        /// <returns>An array of CosmosElements parsed from the response body.</returns>
        public static CosmosArray ParseElementsFromRestStream(
            Stream stream,
            ResourceType resourceType,
            CosmosSerializationFormatOptions cosmosSerializationOptions)
        {
            if (!(stream is MemoryStream memoryStream))
            {
                memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
            }

            if (!memoryStream.CanRead)
            {
                throw new InvalidDataException("Stream can not be read");
            }

            // Parse out the document from the REST response like this:
            // {
            //    "_rid": "qHVdAImeKAQ=",
            //    "Documents": [{
            //        "id": "03230",
            //        "_rid": "qHVdAImeKAQBAAAAAAAAAA==",
            //        "_self": "dbs\/qHVdAA==\/colls\/qHVdAImeKAQ=\/docs\/qHVdAImeKAQBAAAAAAAAAA==\/",
            //        "_etag": "\"410000b0-0000-0000-0000-597916b00000\"",
            //        "_attachments": "attachments\/",
            //        "_ts": 1501107886
            //    }],
            //    "_count": 1
            // }
            // You want to create a CosmosElement for each document in "Documents".

            ReadOnlyMemory<byte> content = memoryStream.TryGetBuffer(out ArraySegment<byte> buffer) ? buffer : (ReadOnlyMemory<byte>)memoryStream.ToArray();
            IJsonNavigator jsonNavigator;
            if (cosmosSerializationOptions != null)
            {
                // Use the users custom navigator
                jsonNavigator = cosmosSerializationOptions.CreateCustomNavigatorCallback(content);
                if (jsonNavigator == null)
                {
                    throw new InvalidOperationException("The CosmosSerializationOptions did not return a JSON navigator.");
                }
            }
            else
            {
                jsonNavigator = JsonNavigator.Create(content);
            }

            string resourceName = resourceType switch
            {
                ResourceType.Collection => "DocumentCollections",
                _ => resourceType.ToResourceTypeString() + "s",
            };

            if (!jsonNavigator.TryGetObjectProperty(
                jsonNavigator.GetRootNode(),
                resourceName,
                out ObjectProperty objectProperty))
            {
                throw new InvalidOperationException($"Response Body Contract was violated. QueryResponse did not have property: {resourceName}");
            }

            if (!(CosmosElement.Dispatch(
                jsonNavigator,
                objectProperty.ValueNode) is CosmosArray cosmosArray))
            {
                throw new InvalidOperationException($"QueryResponse did not have an array of : {resourceName}");
            }

            return cosmosArray;
        }
    }
}