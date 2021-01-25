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
    using Microsoft.Azure.Cosmos.Common;
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
        private static (bool, ResponseMessage) clientIsValid = (false, null);

        private readonly CosmosClient client;
        private readonly Cosmos.ConsistencyLevel? RequestedClientConsistencyLevel;

        private Cosmos.ConsistencyLevel? AccountConsistencyLevel = null;

        public RequestInvokerHandler(
            CosmosClient client,
            Cosmos.ConsistencyLevel? requestedClientConsistencyLevel)
        {
            this.client = client;
            this.RequestedClientConsistencyLevel = requestedClientConsistencyLevel;
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
            if (promotedRequestOptions != null)
            {
                // Fill request options
                promotedRequestOptions.PopulateRequestOptions(request);
            }

            // Adds the NoContent header if not already added based on 
            this.SetClientLevelNoContentResponseHeaders(request);

            await this.ValidateAndSetConsistencyLevelAsync(request);
            (bool isError, ResponseMessage errorResponse) = await this.EnsureValidClientAsync(request);
            if (isError)
            {
                return errorResponse;
            }

            await request.AssertPartitioningDetailsAsync(this.client, cancellationToken);
            this.FillMultiMasterContext(request);
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
            CosmosDiagnosticsContext diagnosticsScope,
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
                diagnosticsContext: diagnosticsScope,
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
            CosmosDiagnosticsContext diagnosticsContext,
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

            // DEVNOTE: Non-Item operations need to be refactored to always pass
            // the diagnostic context in. https://github.com/Azure/azure-cosmos-dotnet-v3/issues/1276
            bool disposeDiagnosticContext = false;
            if (diagnosticsContext == null)
            {
                diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
                disposeDiagnosticContext = true;
            }

            // This is needed for query where a single
            // user request might span multiple backend requests.
            // This will still have a single request id for retry scenarios
            ActivityScope activityScope = ActivityScope.CreateIfDefaultActivityId();
            Debug.Assert(activityScope == null || (activityScope != null &&
                         (operationType != OperationType.SqlQuery || operationType != OperationType.Query || operationType != OperationType.QueryPlan)),
                "There should be an activity id already set");

            try
            {
                HttpMethod method = RequestInvokerHandler.GetHttpMethod(resourceType, operationType);
                RequestMessage request = new RequestMessage(
                    method,
                    resourceUriString,
                    diagnosticsContext,
                    trace)
                {
                    OperationType = operationType,
                    ResourceType = resourceType,
                    RequestOptions = requestOptions,
                    Content = streamPayload,
                };

                if (feedRange != null)
                {
                    if (feedRange is FeedRangePartitionKey feedRangePartitionKey)
                    {
                        if (cosmosContainerCore == null && object.ReferenceEquals(feedRangePartitionKey.PartitionKey, Cosmos.PartitionKey.None))
                        {
                            throw new ArgumentException($"{nameof(cosmosContainerCore)} can not be null with partition key as PartitionKey.None");
                        }
                        else if (feedRangePartitionKey.PartitionKey.IsNone)
                        {
                            using (diagnosticsContext.CreateScope("GetNonePkValue"))
                            {
                                try
                                {
                                    PartitionKeyInternal partitionKeyInternal = await cosmosContainerCore.GetNonePartitionKeyValueAsync(cancellationToken);
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
                        }
                        else
                        {
                            request.Headers.PartitionKey = feedRangePartitionKey.PartitionKey.ToJsonString();
                        }
                    }
                    else if (feedRange is FeedRangeEpk feedRangeEpk)
                    {
                        DocumentServiceRequest serviceRequest = request.ToDocumentServiceRequest();

                        PartitionKeyRangeCache routingMapProvider = await this.client.DocumentClient.GetPartitionKeyRangeCacheAsync();
                        CollectionCache collectionCache = await this.client.DocumentClient.GetCollectionCacheAsync();
                        ContainerProperties collectionFromCache =
                            await collectionCache.ResolveCollectionAsync(serviceRequest, cancellationToken);

                        IReadOnlyList<PartitionKeyRange> overlappingRanges = await routingMapProvider.TryGetOverlappingRangesAsync(
                            collectionFromCache.ResourceId,
                            feedRangeEpk.Range,
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
                                request.Headers[HttpConstants.HttpHeaders.ReadFeedKeyType] = RntbdConstants.RntdbReadFeedKeyType.EffectivePartitionKeyRange.ToString();
                                request.Headers[HttpConstants.HttpHeaders.StartEpk] = feedRangeEpk.Range.Min;
                                request.Headers[HttpConstants.HttpHeaders.EndEpk] = feedRangeEpk.Range.Max;
                            }
                        }
                    }
                    else if (feedRange is FeedRangePartitionKeyRange feedRangePartitionKeyRange)
                    {
                        request.PartitionKeyRangeId = new Documents.PartitionKeyRangeIdentity(feedRangePartitionKeyRange.PartitionKeyRangeId);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unknown feed range type: '{feedRange.GetType()}'.");
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

                requestEnricher?.Invoke(request);
                return await this.SendAsync(request, cancellationToken);
            }
            finally
            {
                if (disposeDiagnosticContext)
                {
                    diagnosticsContext.GetOverallScope().Dispose();
                }

                activityScope?.Dispose();
            }
        }

        internal static HttpMethod GetHttpMethod(
            ResourceType resourceType,
            OperationType operationType)
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
            else if (operationType == OperationType.Read ||
                operationType == OperationType.ReadFeed)
            {
                return HttpMethod.Get;
            }
            else if (operationType == OperationType.Replace)
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

        private async Task<(bool, ResponseMessage)> EnsureValidClientAsync(RequestMessage request)
        {
            try
            {
                await this.client.DocumentClient.EnsureValidClientAsync();
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

        private async Task ValidateAndSetConsistencyLevelAsync(RequestMessage requestMessage)
        {
            // Validate the request consistency compatibility with account consistency
            // Type based access context for requested consistency preferred for performance
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

                if (ValidationHelpers.IsValidConsistencyLevelOverwrite(this.AccountConsistencyLevel.Value, consistencyLevel.Value))
                {
                    // ConsistencyLevel compatibility with back-end configuration will be done by RequestInvokeHandler
                    requestMessage.Headers.Add(HttpConstants.HttpHeaders.ConsistencyLevel, consistencyLevel.Value.ToString());
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

        private void SetClientLevelNoContentResponseHeaders(RequestMessage request)
        {
            // Sets the headers to only return the headers and status code in
            // the Cosmos DB response for write item operation like Create, Upsert, Patch and Replace.
            if (request.RequestOptions == null)
            {
                this.SetMinimalContentHeader(request);
            }

            if (!(request.RequestOptions is ItemRequestOptions))
            {
                return;
            }

            ItemRequestOptions itemRequestOptions = (ItemRequestOptions)request.RequestOptions;
            if (request.ResourceType == ResourceType.Document
                && !itemRequestOptions.EnableContentResponseOnWrite.HasValue)
            {
                this.SetMinimalContentHeader(request);
            }
        }

        private void SetMinimalContentHeader(RequestMessage request)
        {
            if (this.client.ClientOptions != null
                && ItemRequestOptions.ShouldSetNoContentHeader(enableContentResponseOnWrite: this.client.ClientOptions.EnableContentResponseOnWrite, 
                                                               enableContentResponseOnRead: null, 
                                                               operationType: request.OperationType))
            {
                request.Headers.Add(HttpConstants.HttpHeaders.Prefer, HttpConstants.HttpHeaderValues.PreferReturnMinimal);
            }
        }
    }
}
