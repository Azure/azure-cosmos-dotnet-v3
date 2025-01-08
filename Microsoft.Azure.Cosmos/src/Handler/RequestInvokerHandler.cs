//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// HttpMessageHandler can only be invoked by derived classed or internal classes inside http assembly
    /// </summary>
    internal class RequestInvokerHandler : RequestHandler
    {
        private static readonly HttpMethod httpPatchMethod = new HttpMethod(HttpConstants.HttpMethods.Patch);
        private static readonly string BinarySerializationFormat = SupportedSerializationFormats.CosmosBinary.ToString();
        private static (bool, ResponseMessage) clientIsValid = (false, null);

        private readonly CosmosClient client;
        private readonly Cosmos.ConsistencyLevel? RequestedClientConsistencyLevel;
        private readonly Cosmos.PriorityLevel? RequestedClientPriorityLevel;
        private readonly int? RequestedClientThroughputBucket;

        private bool? IsLocalQuorumConsistency;
        private Cosmos.ConsistencyLevel? AccountConsistencyLevel = null;

        public RequestInvokerHandler(
            CosmosClient client,
            Cosmos.ConsistencyLevel? requestedClientConsistencyLevel,
            Cosmos.PriorityLevel? requestedClientPriorityLevel,
            int? requestedClientThroughputBucket)
        {
            this.client = client;

            this.RequestedClientConsistencyLevel = requestedClientConsistencyLevel;       
            this.RequestedClientPriorityLevel = requestedClientPriorityLevel;
            this.RequestedClientThroughputBucket = requestedClientThroughputBucket;
        }

        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            RequestOptions promotedRequestOptions = request.RequestOptions;
            // Fill request options
            promotedRequestOptions?.PopulateRequestOptions(request);

            // Adds the NoContent header if not already added based on Client Level flag
            if (RequestInvokerHandler.ShouldSetNoContentResponseHeaders(
                request.RequestOptions,
                this.client.ClientOptions,
                request.OperationType,
                request.ResourceType))
            {
                request.Headers.Add(HttpConstants.HttpHeaders.Prefer, HttpConstants.HttpHeaderValues.PreferReturnMinimal);
            }

            if (ConfigurationManager.IsBinaryEncodingEnabled()
                && RequestInvokerHandler.IsPointOperationSupportedForBinaryEncoding(request))
            {
                request.Headers.Add(HttpConstants.HttpHeaders.SupportedSerializationFormats, RequestInvokerHandler.BinarySerializationFormat);
            }

            await this.ValidateAndSetConsistencyLevelAsync(request);
            this.SetPriorityLevel(request);
            this.ValidateAndSetThroughputBucket(request);

            (bool isError, ResponseMessage errorResponse) = await this.EnsureValidClientAsync(request, request.Trace);
            if (isError)
            {
                return errorResponse;
            }

            await request.AssertPartitioningDetailsAsync(this.client, cancellationToken, request.Trace);
            this.FillMultiMasterContext(request);

            AvailabilityStrategyInternal strategy = this.AvailabilityStrategy(request);

            ResponseMessage response = strategy != null && strategy.Enabled()
                ? await strategy.ExecuteAvailabilityStrategyAsync(
                    this.BaseSendAsync,
                    this.client,
                    request,
                    cancellationToken)
                : await this.BaseSendAsync(request, cancellationToken);

            if (request.RequestOptions?.ExcludeRegions != null)
            {
                ((CosmosTraceDiagnostics)response.Diagnostics).Value.AddOrUpdateDatum("ExcludedRegions", request.RequestOptions.ExcludeRegions);
            }

            if (ConfigurationManager.IsBinaryEncodingEnabled()
                && RequestInvokerHandler.IsPointOperationSupportedForBinaryEncoding(request)
                && response.Content != null
                && response.Content is not CloneableStream)
            {
                response.Content = await StreamExtension.AsClonableStreamAsync(
                    mediaStream: response.Content,
                    allowUnsafeDataAccess: true);
            }

            return response;
        }

        /// <summary>
        /// This method determines if there is an availability strategy that the request can use.
        /// Note that the request level availability strategy options override the client level options.
        /// </summary>
        /// <param name="request"></param>
        /// <returns>whether the request should be a parallel hedging request.</returns>
        public AvailabilityStrategyInternal AvailabilityStrategy(RequestMessage request)
        {
            AvailabilityStrategy strategy = request.RequestOptions?.AvailabilityStrategy
                    ?? this.client.ClientOptions.AvailabilityStrategy;

            if (strategy == null)
            {
                return null;
            }

            return strategy as AvailabilityStrategyInternal;
        }

        public virtual async Task<ResponseMessage> BaseSendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        public virtual async Task<T> SendAsync<T>(
            string resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerInternal cosmosContainerCore,
            FeedRange feedRange,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            Func<ResponseMessage, T> responseCreator,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (responseCreator == null)
            {
                throw new ArgumentNullException(nameof(responseCreator));
            }

            ResponseMessage responseMessage = await this.SendAsync(
                resourceUriString: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: cosmosContainerCore,
                feedRange: feedRange,
                streamPayload: streamPayload,
                requestEnricher: requestEnricher,
                trace: trace,
                cancellationToken: cancellationToken);

            return responseCreator(responseMessage);
        }

        public virtual async Task<ResponseMessage> SendAsync(
            string resourceUriString,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerInternal cosmosContainerCore,
            FeedRange feedRange,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (resourceUriString == null)
            {
                throw new ArgumentNullException(nameof(resourceUriString));
            }

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            // This is needed for query where a single
            // user request might span multiple backend requests.
            // This will still have a single request id for retry scenarios
            ActivityScope activityScope = ActivityScope.CreateIfDefaultActivityId();
            Debug.Assert(activityScope == null || (activityScope != null &&
                         (operationType != OperationType.SqlQuery || operationType != OperationType.Query || operationType != OperationType.QueryPlan)),
                "There should be an activity id already set");

            using (ITrace childTrace = trace.StartChild(this.FullHandlerName, TraceComponent.RequestHandler, Tracing.TraceLevel.Info))
            {
                try
                {
                    HttpMethod method = RequestInvokerHandler.GetHttpMethod(resourceType, operationType, streamPayload != null);

                    RequestMessage request = new RequestMessage(
                        method,
                        resourceUriString,
                        childTrace)
                    {
                        OperationType = operationType,
                        ResourceType = resourceType,
                        RequestOptions = requestOptions,
                        Content = streamPayload,
                    };

                    request.Headers.SDKSupportedCapabilities = Headers.SDKSUPPORTEDCAPABILITIES;

                    if (feedRange != null)
                    {
                        if (!request.OperationType.IsPointOperation()) 
                        {
                            feedRange = await RequestInvokerHandler.ResolveFeedRangeBasedOnPrefixContainerAsync(
                                feedRange: feedRange,
                                cosmosContainerCore: cosmosContainerCore,
                                cancellationToken: cancellationToken);
                        }
                        
                        if (feedRange is FeedRangePartitionKey feedRangePartitionKey)
                        {
                            if (cosmosContainerCore == null && object.ReferenceEquals(feedRangePartitionKey.PartitionKey, Cosmos.PartitionKey.None))
                            {
                                throw new ArgumentException($"{nameof(cosmosContainerCore)} can not be null with partition key as PartitionKey.None");
                            }
                            else if (feedRangePartitionKey.PartitionKey.IsNone)
                            {
                                try
                                {
                                    PartitionKeyInternal partitionKeyInternal = await cosmosContainerCore.GetNonePartitionKeyValueAsync(
                                        childTrace,
                                        cancellationToken);
                                    request.Headers.PartitionKey = partitionKeyInternal.ToJsonString();
                                }
                                catch (DocumentClientException dce)
                                {
                                    return dce.ToCosmosResponseMessage(request);
                                }
                                catch (CosmosException ce)
                                {
                                    return ce.ToCosmosResponseMessage(request);
                                }
                            }
                            else
                            {
                                request.Headers.PartitionKey = feedRangePartitionKey.PartitionKey.ToJsonString();
                            }
                        }
                        else if (feedRange is FeedRangeEpk feedRangeEpk)
                        {
                            ContainerProperties collectionFromCache;
                            try
                            {
                                if (cosmosContainerCore == null)
                                {
                                    throw new ArgumentException($"The container core can not be null for FeedRangeEpk");
                                }

                                collectionFromCache = await cosmosContainerCore.GetCachedContainerPropertiesAsync(
                                    forceRefresh: false,
                                    childTrace,
                                    cancellationToken);
                            }
                            catch (CosmosException ex)
                            {
                                return ex.ToCosmosResponseMessage(request);
                            }

                            PartitionKeyRangeCache routingMapProvider = await this.client.DocumentClient.GetPartitionKeyRangeCacheAsync(childTrace);
                            IReadOnlyList<PartitionKeyRange> overlappingRanges = await routingMapProvider.TryGetOverlappingRangesAsync(
                                collectionFromCache.ResourceId,
                                feedRangeEpk.Range,
                                childTrace,
                                forceRefresh: false);
                            if (overlappingRanges == null)
                            {
                                CosmosException notFound = new CosmosException(
                                    $"Stale cache for rid '{collectionFromCache.ResourceId}'",
                                    statusCode: System.Net.HttpStatusCode.NotFound,
                                    subStatusCode: default,
                                    activityId: Guid.Empty.ToString(),
                                    requestCharge: default);
                                return notFound.ToCosmosResponseMessage(request);
                            }

                            // For epk range filtering we can end up in one of 3 cases:
                            if (overlappingRanges.Count > 1)
                            {
                                // 1) The EpkRange spans more than one physical partition
                                // In this case it means we have encountered a split and 
                                // we need to bubble that up to the higher layers to update their datastructures
                                CosmosException goneException = new CosmosException(
                                    message: $"Epk Range: {feedRangeEpk.Range} is gone.",
                                    statusCode: System.Net.HttpStatusCode.Gone,
                                    subStatusCode: (int)SubStatusCodes.PartitionKeyRangeGone,
                                    activityId: Guid.NewGuid().ToString(),
                                    requestCharge: default);

                                return goneException.ToCosmosResponseMessage(request);
                            }
                            // overlappingRanges.Count == 1
                            else
                            {
                                Range<string> singleRange = overlappingRanges[0].ToRange();
                                if ((singleRange.Min == feedRangeEpk.Range.Min) && (singleRange.Max == feedRangeEpk.Range.Max))
                                {
                                    // 2) The EpkRange spans exactly one physical partition
                                    // In this case we can route to the physical pkrange id
                                    request.PartitionKeyRangeId = new Documents.PartitionKeyRangeIdentity(overlappingRanges[0].Id);
                                }
                                else
                                {
                                    // 3) The EpkRange spans less than single physical partition
                                    // In this case we route to the physical partition and 
                                    // pass the epk range headers to filter within partition
                                    request.PartitionKeyRangeId = new Documents.PartitionKeyRangeIdentity(overlappingRanges[0].Id);
                                    request.Headers.ReadFeedKeyType = RntbdConstants.RntdbReadFeedKeyType.EffectivePartitionKeyRange.ToString();
                                    request.Headers.StartEpk = feedRangeEpk.Range.Min;
                                    request.Headers.EndEpk = feedRangeEpk.Range.Max;
                                }
                            }
                        }
                        else
                        {
                            request.PartitionKeyRangeId = feedRange is FeedRangePartitionKeyRange feedRangePartitionKeyRange
                                ? new Documents.PartitionKeyRangeIdentity(feedRangePartitionKeyRange.PartitionKeyRangeId)
                                : throw new InvalidOperationException($"Unknown feed range type: '{feedRange.GetType()}'.");
                        }
                    }

                    if (operationType == OperationType.Upsert)
                    {
                        request.Headers.IsUpsert = bool.TrueString;
                    }
                    else if (operationType == OperationType.Patch)
                    {
                        request.Headers.ContentType = RuntimeConstants.MediaTypes.JsonPatch;
                    }

                    if (ChangeFeedHelper.IsChangeFeedWithQueryRequest(operationType, streamPayload != null))
                    {
                        request.Headers.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                        request.Headers.Add(HttpConstants.HttpHeaders.ContentType, RuntimeConstants.MediaTypes.QueryJson);
                    }

                    if (cosmosContainerCore != null)
                    {
                        request.ContainerId = cosmosContainerCore?.Id;
                        request.DatabaseId = cosmosContainerCore?.Database.Id;
                    }
                    requestEnricher?.Invoke(request);

                    return await this.SendAsync(request, cancellationToken);
                }
                finally
                {
                    activityScope?.Dispose();
                }
            }
        }

        internal static HttpMethod GetHttpMethod(
            ResourceType resourceType,
            OperationType operationType,
            bool hasPayload = false)
        {
            if (operationType == OperationType.Create ||
                operationType == OperationType.Upsert ||
                operationType == OperationType.Query ||
                operationType == OperationType.SqlQuery ||
                operationType == OperationType.QueryPlan ||
                operationType == OperationType.Batch ||
                operationType == OperationType.ExecuteJavaScript ||
                operationType == OperationType.CompleteUserTransaction ||
                (resourceType == ResourceType.PartitionKey && operationType == OperationType.Delete))
            {
                return HttpMethod.Post;
            }
            else if (ChangeFeedHelper.IsChangeFeedWithQueryRequest(operationType, hasPayload))
            {
                // ChangeFeed with payload is a CF with query support and will
                // be a POST request.
                return HttpMethod.Post;
            }
            else if (operationType == OperationType.Read ||
                operationType == OperationType.ReadFeed)
            {
                return HttpMethod.Get;
            }
            else if ((operationType == OperationType.Replace) || (operationType == OperationType.CollectionTruncate))
            {
                return HttpMethod.Put;
            }
            else if (operationType == OperationType.Delete)
            {
                return HttpMethod.Delete;
            }
            else if (operationType == OperationType.Patch)
            {
                // There isn't support for PATCH method in .NetStandard 2.0
                return httpPatchMethod;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private async Task<(bool, ResponseMessage)> EnsureValidClientAsync(RequestMessage request, ITrace trace)
        {
            try
            {
                await this.client.DocumentClient.EnsureValidClientAsync(trace);
                return RequestInvokerHandler.clientIsValid;
            }
            catch (DocumentClientException dce)
            {
                return (true, dce.ToCosmosResponseMessage(request));
            }
        }

        private void FillMultiMasterContext(RequestMessage request)
        {
            if (this.client.DocumentClient.UseMultipleWriteLocations)
            {
                request.Headers.Set(HttpConstants.HttpHeaders.AllowTentativeWrites, bool.TrueString);
            }
        }

        /// <summary>
        /// Validate the request consistency compatibility with account consistency
        /// Type based access context for requested consistency preferred for performance
        /// </summary>
        /// <param name="requestMessage"></param>
        /// <exception cref="ArgumentException">In case, Invalid consistency is passed</exception>
        private async Task ValidateAndSetConsistencyLevelAsync(RequestMessage requestMessage)
        {
            Cosmos.ConsistencyLevel? consistencyLevel = null;
            RequestOptions promotedRequestOptions = requestMessage.RequestOptions;
            if (promotedRequestOptions != null && promotedRequestOptions.BaseConsistencyLevel.HasValue)
            {
                consistencyLevel = promotedRequestOptions.BaseConsistencyLevel;
            }
            else if (this.RequestedClientConsistencyLevel.HasValue)
            {
                consistencyLevel = this.RequestedClientConsistencyLevel;
            }

            if (consistencyLevel.HasValue)
            {
                if (!this.AccountConsistencyLevel.HasValue)
                {
                    this.AccountConsistencyLevel = await this.client.GetAccountConsistencyLevelAsync();
                }

                if (!this.IsLocalQuorumConsistency.HasValue)
                {
                    this.IsLocalQuorumConsistency = this.client.ClientOptions.EnableUpgradeConsistencyToLocalQuorum;
                }

                if (ValidationHelpers.IsValidConsistencyLevelOverwrite(
                            backendConsistency: this.AccountConsistencyLevel.Value, 
                            desiredConsistency: consistencyLevel.Value,
                            isLocalQuorumConsistency: this.IsLocalQuorumConsistency.Value,
                            operationType: requestMessage.OperationType,
                            resourceType: requestMessage.ResourceType))
                {
                    // ConsistencyLevel compatibility with back-end configuration will be done by RequestInvokeHandler
                    requestMessage.Headers.ConsistencyLevel = consistencyLevel.Value.ToString();
                }
                else
                {
                    throw new ArgumentException(string.Format(
                            CultureInfo.CurrentUICulture,
                            RMResources.InvalidConsistencyLevel,
                            consistencyLevel.Value.ToString(),
                            this.AccountConsistencyLevel));
                }
            }
        }

        /// <summary>
        /// Set the PriorityLevel in the request headers
        /// </summary>
        /// <param name="requestMessage"></param>
        private void SetPriorityLevel(RequestMessage requestMessage)
        {
            Cosmos.PriorityLevel? priorityLevel = this.RequestedClientPriorityLevel;
            RequestOptions promotedRequestOptions = requestMessage.RequestOptions;
            if (promotedRequestOptions?.PriorityLevel.HasValue == true)
            {
                priorityLevel = promotedRequestOptions.PriorityLevel.Value;
            }

            if (priorityLevel.HasValue)
            {
                requestMessage.Headers.Set(HttpConstants.HttpHeaders.PriorityLevel, priorityLevel.ToString());
            }
        }

        /// <summary>
        /// Set the ThroughputBucket in the request headers
        /// </summary>
        /// <param name="requestMessage"></param>
        private void ValidateAndSetThroughputBucket(RequestMessage requestMessage)
        {
            int? throughputBucket = this.RequestedClientThroughputBucket;
            RequestOptions promotedRequestOptions = requestMessage.RequestOptions;

            if (promotedRequestOptions?.ThroughputBucket.HasValue == true)
            {
                if (this.client.ClientOptions.AllowBulkExecution)
                {
                    throw new ArgumentException($"{nameof(requestMessage.RequestOptions.ThroughputBucket)} cannot be set in " +
                        $"{nameof(requestMessage.RequestOptions)} when {nameof(this.client.ClientOptions.AllowBulkExecution)} is set to true. " +
                        $"Instead, set {nameof(this.client.ClientOptions.ThroughputBucket)} only in {nameof(this.client.ClientOptions)}.");
                }
                throughputBucket = promotedRequestOptions.ThroughputBucket.Value;
            }

            if (throughputBucket.HasValue)
            {
                requestMessage.Headers.Set(HttpConstants.HttpHeaders.ThroughputBucket, throughputBucket.ToString());
            }
        }

        internal static bool ShouldSetNoContentResponseHeaders(RequestOptions requestOptions,
            CosmosClientOptions clientOptions,
            OperationType operationType,
            ResourceType resourceType)
        {
            if (resourceType != ResourceType.Document)
            {
                return false;
            }

            if (requestOptions == null)
            {
                return RequestInvokerHandler.IsClientNoResponseSet(clientOptions, operationType);
            }

            if (requestOptions is ItemRequestOptions itemRequestOptions)
            {
                if (itemRequestOptions.EnableContentResponseOnWrite.HasValue)
                {
                    return RequestInvokerHandler.IsItemNoRepsonseSet(itemRequestOptions.EnableContentResponseOnWrite.Value, operationType);
                }
                else
                {
                    return RequestInvokerHandler.IsClientNoResponseSet(clientOptions, operationType);
                }
            }

            if (requestOptions is TransactionalBatchItemRequestOptions batchRequestOptions)
            {
                if (batchRequestOptions.EnableContentResponseOnWrite.HasValue)
                {
                    return RequestInvokerHandler.IsItemNoRepsonseSet(batchRequestOptions.EnableContentResponseOnWrite.Value, operationType);
                }
                else
                {
                    return RequestInvokerHandler.IsClientNoResponseSet(clientOptions, operationType);
                }
            }

            return false;
        }

        private static bool IsItemNoRepsonseSet(bool enableContentResponseOnWrite, OperationType operationType)
        {
            return !enableContentResponseOnWrite &&
              (operationType == OperationType.Create ||
              operationType == OperationType.Replace ||
              operationType == OperationType.Upsert ||
              operationType == OperationType.Patch);
        }

        private static bool IsPointOperationSupportedForBinaryEncoding(RequestMessage request)
        {
            return request.ResourceType == ResourceType.Document 
                && (request.OperationType == OperationType.Create
                    || request.OperationType == OperationType.Replace
                    || request.OperationType == OperationType.Delete
                    || request.OperationType == OperationType.Read
                    || request.OperationType == OperationType.Upsert);
        }

        private static bool IsClientNoResponseSet(CosmosClientOptions clientOptions, OperationType operationType)
        {
            return clientOptions != null
                && clientOptions.EnableContentResponseOnWrite.HasValue
                && RequestInvokerHandler.IsItemNoRepsonseSet(clientOptions.EnableContentResponseOnWrite.Value, operationType);
        }

        internal static async Task<FeedRange> ResolveFeedRangeBasedOnPrefixContainerAsync(
            FeedRange feedRange,
            ContainerInternal cosmosContainerCore,
            CancellationToken cancellationToken)
        {
            if (feedRange is FeedRangePartitionKey feedRangePartitionKey)
            {
                PartitionKeyDefinition partitionKeyDefinition = await cosmosContainerCore
                    .GetPartitionKeyDefinitionAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (partitionKeyDefinition != null && partitionKeyDefinition.Kind == PartitionKind.MultiHash
                    && feedRangePartitionKey.PartitionKey.InternalKey?.Components?.Count < partitionKeyDefinition.Paths?.Count)
                {
                   feedRange = new FeedRangeEpk(feedRangePartitionKey.PartitionKey.InternalKey.GetEPKRangeForPrefixPartitionKey(partitionKeyDefinition));
                }
            }

            return feedRange;
        }
    }
}
