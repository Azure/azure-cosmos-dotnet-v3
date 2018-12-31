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
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.Internal.RntbdConstants;
    using static Microsoft.Azure.Cosmos.Routing.PartitionRoutingHelper;

    /// <summary>
    /// Handler which manages the continution token and partion-key-range-id selection depending on a provided start and end epk and direction. 
    /// By default start is 00, end is FF and direction is forward. 
    /// It doesn't participates in split logic direclty but on split retry, will select the new 
    /// appropriate partiton-key-range id after a forced refresh of the CollectionRoutingMap.
    /// </summary>
    internal class PartitionKeyRangeHandler : CosmosRequestHandler
    {
        private readonly CosmosClient client;
        private PartitionRoutingHelper partitionRoutingHelper;
        public PartitionKeyRangeHandler(CosmosClient client, PartitionRoutingHelper partitionRoutingHelper = null)
        {
            if(client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            this.client = client;
            this.partitionRoutingHelper = partitionRoutingHelper ?? new PartitionRoutingHelper();
        }

        public override async Task<CosmosResponseMessage> SendAsync(
            CosmosRequestMessage request,
            CancellationToken cancellationToken)
        {
            CosmosResponseMessage response = null;
            string originalContinuation = request.Headers.Continuation;
            try
            {
                RntdbEnumerationDirection rntdbEnumerationDirection = RntdbEnumerationDirection.Forward;
                object direction;
                if (request.Properties.TryGetValue(HttpConstants.HttpHeaders.EnumerationDirection, out direction))
                {
                    rntdbEnumerationDirection = (byte)RntdbEnumerationDirection.Reverse == (byte)direction ? RntdbEnumerationDirection.Reverse : RntdbEnumerationDirection.Forward;
                }

                request.Headers.Remove(HttpConstants.HttpHeaders.IsContinuationExpected);
                request.Headers.Add(HttpConstants.HttpHeaders.IsContinuationExpected, bool.TrueString);

                object startEpk;
                object endEpk;
                if (!request.Properties.TryGetValue(HandlerConstants.StartEpkString, out startEpk))
                {
                    startEpk = PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey;
                }
                if (!request.Properties.TryGetValue(HandlerConstants.EndEpkString, out endEpk))
                {
                    endEpk = PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey;
                }
                startEpk = startEpk ?? PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey;
                endEpk = endEpk ?? PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey;

                List<Range<string>> providedRanges = new List<Range<string>> {
                    new Range<string>(
                        (string)startEpk,
                        (string)endEpk,
                        isMinInclusive: true,
                        isMaxInclusive: false)
                };

                DocumentServiceRequest serviceRequest = request.ToDocumentServiceRequest();

                PartitionKeyRangeCache routingMapProvider = await this.client.DocumentClient.GetPartitionKeyRangeCacheAsync();
                CollectionCache collectionCache = await this.client.DocumentClient.GetCollectionCacheAsync();
                CosmosContainerSettings collectionFromCache =
                    await collectionCache.ResolveCollectionAsync(serviceRequest, CancellationToken.None);

                List<CompositeContinuationToken> suppliedTokens;
                //direction is not expected to change  between continuations.
                Range<string> rangeFromContinuationToken =
                    this.partitionRoutingHelper.ExtractPartitionKeyRangeFromContinuationToken(serviceRequest.Headers, out suppliedTokens);

                ResolvedRangeInfo resolvedRangeInfo =
                    await this.partitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRange(
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
                    resolvedRangeInfo = await this.partitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRange(
                        providedPartitionKeyRanges: providedRanges,
                        routingMapProvider: routingMapProvider,
                        collectionRid: collectionFromCache.ResourceId,
                        rangeFromContinuationToken: rangeFromContinuationToken,
                        suppliedTokens: suppliedTokens,
                        direction: rntdbEnumerationDirection);
                }

                if (resolvedRangeInfo.ResolvedRange == null && resolvedRangeInfo.ContinuationTokens == null)
                {
                    return ((DocumentClientException)
                        new NotFoundException(
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
                        return ((DocumentClientException)
                            new NotFoundException(
                                $"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}: Call to TryAddPartitionKeyRangeToContinuationTokenAsync failed to the following collectionRid: {collectionFromCache.ResourceId} with the supplied tokens: {JsonConvert.SerializeObject(suppliedTokens)}")
                            ).ToCosmosResponseMessage(request);
                    }
                }

                return response;
            }            
            catch (DocumentClientException ex)
            {
                CosmosResponseMessage errorResponse = ex.ToCosmosResponseMessage(request);
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

        private void SetOriginalContinuationToken(CosmosRequestMessage request, CosmosResponseMessage response, string originalContinuation)
        {
            request.Headers.Continuation = originalContinuation;
            if (response != null)
            {
                response.Headers.Continuation = originalContinuation;
            }
        }
    }
}
