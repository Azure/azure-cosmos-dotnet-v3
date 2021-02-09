//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.Routing.PartitionRoutingHelper;
    using static Microsoft.Azure.Documents.RntbdConstants;

    /// <summary>
    /// Handler which manages the continuation token and partition-key-range-id selection depending on a provided start and end epk and direction. 
    /// By default start is 00, end is FF and direction is forward. 
    /// It doesn't participates in split logic directly but on split retry, will select the new 
    /// appropriate partition-key-range id after a forced refresh of the CollectionRoutingMap.
    /// </summary>
    internal class PartitionKeyRangeHandler : RequestHandler
    {
        private readonly CosmosClient client;
        private readonly PartitionRoutingHelper partitionRoutingHelper;

        public PartitionKeyRangeHandler(CosmosClient client, PartitionRoutingHelper partitionRoutingHelper = null)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.partitionRoutingHelper = partitionRoutingHelper ?? new PartitionRoutingHelper();
        }

        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            using (ITrace childTrace = request.Trace.StartChild(this.FullHandlerName, TraceComponent.RequestHandler, Tracing.TraceLevel.Info))
            {
                request.Trace = childTrace;

                ResponseMessage response = null;
                string originalContinuation = request.Headers.ContinuationToken;
                try
                {
                    RntdbEnumerationDirection rntdbEnumerationDirection = RntdbEnumerationDirection.Forward;
                    if (request.Properties.TryGetValue(HttpConstants.HttpHeaders.EnumerationDirection, out object direction))
                    {
                        rntdbEnumerationDirection = (byte)direction == (byte)RntdbEnumerationDirection.Reverse ? RntdbEnumerationDirection.Reverse : RntdbEnumerationDirection.Forward;
                    }

                    request.Headers.Remove(HttpConstants.HttpHeaders.IsContinuationExpected);
                    request.Headers.Add(HttpConstants.HttpHeaders.IsContinuationExpected, bool.TrueString);

                    if (!request.Properties.TryGetValue(HandlerConstants.StartEpkString, out object startEpk))
                    {
                        startEpk = PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey;
                    }

                    if (!request.Properties.TryGetValue(HandlerConstants.EndEpkString, out object endEpk))
                    {
                        endEpk = PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey;
                    }

                    startEpk ??= PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey;
                    endEpk ??= PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey;

                    List<Range<string>> providedRanges = new List<Range<string>>
                {
                    new Range<string>(
                        (string)startEpk,
                        (string)endEpk,
                        isMinInclusive: true,
                        isMaxInclusive: false)
                };

                    DocumentServiceRequest serviceRequest = request.ToDocumentServiceRequest();

                    PartitionKeyRangeCache routingMapProvider = await this.client.DocumentClient.GetPartitionKeyRangeCacheAsync();
                    CollectionCache collectionCache = await this.client.DocumentClient.GetCollectionCacheAsync(NoOpTrace.Singleton);
                    ContainerProperties collectionFromCache =
                        await collectionCache.ResolveCollectionAsync(serviceRequest, CancellationToken.None);

                    //direction is not expected to change  between continuations.
                    Range<string> rangeFromContinuationToken =
                        this.partitionRoutingHelper.ExtractPartitionKeyRangeFromContinuationToken(serviceRequest.Headers, out List<CompositeContinuationToken> suppliedTokens);

                    ResolvedRangeInfo resolvedRangeInfo =
                        await this.partitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRangeAsync(
                            providedPartitionKeyRanges: providedRanges,
                            routingMapProvider: routingMapProvider,
                            collectionRid: collectionFromCache.ResourceId,
                            rangeFromContinuationToken: rangeFromContinuationToken,
                            suppliedTokens: suppliedTokens,
                            direction: rntdbEnumerationDirection);

                    if (serviceRequest.IsNameBased && resolvedRangeInfo.ResolvedRange == null && resolvedRangeInfo.ContinuationTokens == null)
                    {
                        serviceRequest.ForceNameCacheRefresh = true;
                        collectionFromCache = await collectionCache.ResolveCollectionAsync(serviceRequest, CancellationToken.None);
                        resolvedRangeInfo = await this.partitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRangeAsync(
                            providedPartitionKeyRanges: providedRanges,
                            routingMapProvider: routingMapProvider,
                            collectionRid: collectionFromCache.ResourceId,
                            rangeFromContinuationToken: rangeFromContinuationToken,
                            suppliedTokens: suppliedTokens,
                            direction: rntdbEnumerationDirection);
                    }

                    if (resolvedRangeInfo.ResolvedRange == null && resolvedRangeInfo.ContinuationTokens == null)
                    {
                        return ((DocumentClientException)new NotFoundException(
                                $"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}: Was not able to get queryRoutingInfo even after resolve collection async with force name cache refresh to the following collectionRid: {collectionFromCache.ResourceId} with the supplied tokens: {JsonConvert.SerializeObject(suppliedTokens)}")
                                ).ToCosmosResponseMessage(request);
                    }

                    serviceRequest.RouteTo(new PartitionKeyRangeIdentity(collectionFromCache.ResourceId, resolvedRangeInfo.ResolvedRange.Id));

                    response = await base.SendAsync(request, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        this.SetOriginalContinuationToken(request, response, originalContinuation);
                    }
                    else
                    {
                        if (!await this.partitionRoutingHelper.TryAddPartitionKeyRangeToContinuationTokenAsync(
                            response.Headers.CosmosMessageHeaders,
                            providedPartitionKeyRanges: providedRanges,
                            routingMapProvider: routingMapProvider,
                            collectionRid: collectionFromCache.ResourceId,
                            resolvedRangeInfo: resolvedRangeInfo,
                            direction: rntdbEnumerationDirection))
                        {
                            return ((DocumentClientException)new NotFoundException(
                                    $"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}: Call to TryAddPartitionKeyRangeToContinuationTokenAsync failed to the following collectionRid: {collectionFromCache.ResourceId} with the supplied tokens: {JsonConvert.SerializeObject(suppliedTokens)}")
                                ).ToCosmosResponseMessage(request);
                        }
                    }

                    return response;
                }
                catch (DocumentClientException ex)
                {
                    ResponseMessage errorResponse = ex.ToCosmosResponseMessage(request);
                    this.SetOriginalContinuationToken(request, errorResponse, originalContinuation);
                    return errorResponse;
                }
                catch (CosmosException ex)
                {
                    ResponseMessage errorResponse = ex.ToCosmosResponseMessage(request);
                    this.SetOriginalContinuationToken(request, errorResponse, originalContinuation);
                    return errorResponse;
                }
                catch (AggregateException ex)
                {
                    this.SetOriginalContinuationToken(request, response, originalContinuation);

                    // TODO: because the SDK underneath this path uses ContinueWith or task.Result we need to catch AggregateExceptions here
                    // in order to ensure that underlying DocumentClientExceptions get propagated up correctly. Once all ContinueWith and .Result 
                    // is removed this catch can be safely removed.
                    AggregateException innerExceptions = ex.Flatten();
                    Exception docClientException = innerExceptions.InnerExceptions.FirstOrDefault(innerEx => innerEx is DocumentClientException);
                    if (docClientException != null)
                    {
                        return ((DocumentClientException)docClientException).ToCosmosResponseMessage(request);
                    }

                    throw;
                }
            }
        }

        private void SetOriginalContinuationToken(RequestMessage request, ResponseMessage response, string originalContinuation)
        {
            request.Headers.ContinuationToken = originalContinuation;
            if (response != null)
            {
                response.Headers.ContinuationToken = originalContinuation;
            }
        }
    }
}
