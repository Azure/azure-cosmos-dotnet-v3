//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#nullable enable
namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// This handler is designed to support failing over a single partition to a different region.
    /// 1. Only works in single master with strong consistency
    /// 2. Account information will only show 1 write region.
    /// 3. Failover will only be retried on ServiceUnavailable and WriteForbbin exceptions
    /// 
    /// Implementation logic for initial request
    /// 1. Send the original request without changing anything
    /// 2. If the response is not a ServiceUnavailable or WriteForbbin it returns the response.
    /// 3. It will now iterate and try the request against all read regions. Doing one last retry against primary region.
    /// 4. If one of the retires succeeds. It will get the physical URI for the request and add it to the lazyUriToFailedOverLocation
    /// 
    /// Following requests:
    /// 1. If there is a failed over regions all new request will get the physical URI based on the default location.
    /// 2. It will check the lazyUriToFailedOverLocation and if it is overridden then request will be updated to use the new location.
    /// 
    /// The 
    /// </summary>
    internal class PartitionKeyRangeWriteFailoverHandler : RequestHandler
    {
        private readonly Lazy<ConcurrentDictionary<PartitionKeyRange, Uri>> lazyUriToFailedOverLocation;
        private readonly Func<IReadOnlyCollection<Uri>> getReadEndpoints;
        private readonly Func<Task<Cosmos.ConsistencyLevel>> getAccountConsistencyAsync;
        private readonly ReaderWriterLockSlim writeFailedOverLock = new ReaderWriterLockSlim();
        private readonly Lazy<IAddressResolver> addressResolver;

        private Uri? primaryWriteLocationFailedOver = null;

        private PartitionKeyRangeWriteFailoverHandler(
            Func<Task<Cosmos.ConsistencyLevel>> getAccountConsistency,
            Func<IReadOnlyCollection<Uri>> getReadEndpoints,
            Lazy<IAddressResolver> addressResolver)
        {
            this.getAccountConsistencyAsync = getAccountConsistency ?? throw new ArgumentNullException(nameof(getAccountConsistency));
            this.lazyUriToFailedOverLocation = new Lazy<ConcurrentDictionary<PartitionKeyRange, Uri>>();
            this.getReadEndpoints = getReadEndpoints ?? throw new ArgumentNullException(nameof(getReadEndpoints));
            this.addressResolver = addressResolver ?? throw new ArgumentNullException(nameof(addressResolver));
        }

        public static bool TryCreate(
            Func<Task<Cosmos.ConsistencyLevel>> getAccountConsistency,
            Func<IReadOnlyCollection<Uri>> getReadEndpoints,
            Lazy<IAddressResolver> addressResolver,
            Cosmos.ConsistencyLevel? requestedClientConsistencyLevel,
            ConnectionMode connectionMode,
            out RequestHandler? requestHandler)
        {
            requestHandler = null;
            
            if (connectionMode != ConnectionMode.Direct)
            {
                return false;
            }

            if (requestedClientConsistencyLevel.HasValue &&
                requestedClientConsistencyLevel.Value != Cosmos.ConsistencyLevel.Strong)
            {
                return false;
            }

            requestHandler = new PartitionKeyRangeWriteFailoverHandler(
                getAccountConsistency,
                getReadEndpoints,
                addressResolver);

            return true;
        }

        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            // PartitionKeyRange failover is only done for document write operations and the account has more than 1 region.
            // All other operations skip logic
            if (request.ResourceType == ResourceType.Document
                && request.OperationType.IsWriteOperation()
                && this.getReadEndpoints().Count > 1)
            {
                Cosmos.ConsistencyLevel consistencyLevel = await this.getAccountConsistencyAsync();
                if (consistencyLevel == Cosmos.ConsistencyLevel.Strong)
                {
                    return await this.WriteRegionDownHelperAsync(
                        request,
                        cancellationToken);
                }
            }

            return await base.SendAsync(request, cancellationToken);
        }

        private async Task<ResponseMessage> WriteRegionDownHelperAsync(
           RequestMessage request,
           CancellationToken cancellationToken)
        {
            DocumentServiceRequest documentServiceRequest = request.ToDocumentServiceRequest();
            Uri startingLocation = documentServiceRequest.RequestContext.LocationEndpointToRoute;
            Lazy<IEnumerator<(Uri endpoint, bool isPrimaryLocation)>> locationToFailover = new Lazy<IEnumerator<(Uri endpoint, bool isPrimaryLocation)>>(
                () => this.GetLocationsToRetryOn(
                 this.getReadEndpoints(),
                 startingLocation));

            if (this.primaryWriteLocationFailedOver != null)
            {
                this.writeFailedOverLock.EnterReadLock();
                if (this.primaryWriteLocationFailedOver != null)
                {
                    try
                    {
                        documentServiceRequest.RequestContext.RouteToLocation(this.primaryWriteLocationFailedOver);
                    }
                    finally
                    {
                        this.writeFailedOverLock.ExitReadLock();
                    }
                }
            }

            bool hitHttpRequestException = false;
            do
            {
                try
                {
                    (bool isSuccess, ResponseMessage responseMessage) = await this.DocumentWritePartitionKeyRangeFailoverHelperAsync(
                        request,
                        documentServiceRequest,
                        locationToFailover,
                        cancellationToken);

                    if (hitHttpRequestException &&
                        isSuccess)
                    {
                        this.writeFailedOverLock.EnterWriteLock();
                        try
                        {
                            // The primary region is now the default. No need for additional failover logic
                            this.primaryWriteLocationFailedOver = this.primaryWriteLocationFailedOver == startingLocation ? null : locationToFailover.Value.Current.endpoint;
                        }
                        finally
                        {
                            this.writeFailedOverLock.ExitWriteLock();
                        }
                    }

                    // If the iterator was created then dispose of it.
                    if (locationToFailover.IsValueCreated)
                    {
                        locationToFailover.Value.Dispose();
                    }

                    return responseMessage;
                }
                catch (HttpRequestException)
                {
                    hitHttpRequestException = true;
                    if (!this.TryUpdateLocation(
                        documentServiceRequest.RequestContext,
                        locationToFailover.Value))
                    {
                        throw;
                    }
                }
            }
            while (true);
        }

        private async Task<(bool isSuccess, ResponseMessage responseMessage)> DocumentWritePartitionKeyRangeFailoverHelperAsync(
            RequestMessage request,
            DocumentServiceRequest documentServiceRequest,
            Lazy<IEnumerator<(Uri endpoint, bool isPrimaryLocation)>> locationToFailover,
            CancellationToken cancellationToken)
        {
            PartitionKeyRange? primaryReplicaUri = null;
            bool changedInitialLocation = false;

            if (this.lazyUriToFailedOverLocation.IsValueCreated &&
                    this.lazyUriToFailedOverLocation.Value.Any())
            {
                primaryReplicaUri = await this.GetPrimaryReplicaUriAsync(
                    documentServiceRequest,
                    cancellationToken);

                // The partition failed over to a different region. Change the location to the new region.
                if (this.lazyUriToFailedOverLocation.Value.TryGetValue(primaryReplicaUri, out Uri initialOverrideLocation))
                {
                    using (request.Trace.StartChild("PartitionKeyRangeWriteFailoverHandler update initial request location"))
                    {
                        changedInitialLocation = true;
                        documentServiceRequest.RequestContext.RouteToLocation(initialOverrideLocation);
                    }
                }
            }

            bool requestDidPartitionLevelRetry = false;
            while (true)
            {
                ResponseMessage responseMessage;

                using (request.Trace.StartChild("PartitionKeyRangeWriteFailoverHandler SendAsync"))
                {
                    responseMessage = await base.SendAsync(request, cancellationToken);
                }

                bool isWriteForbidden = responseMessage.StatusCode == HttpStatusCode.Forbidden &&
                    responseMessage.Headers.SubStatusCode == SubStatusCodes.WriteForbidden;

                bool isServiceUnavailable = responseMessage.StatusCode == HttpStatusCode.ServiceUnavailable;

                // The request should not be retried in another region
                if (!isWriteForbidden && !isServiceUnavailable)
                {
                    // No retries occurred. Return original response
                    if (!requestDidPartitionLevelRetry)
                    {
                        return (true, responseMessage);
                    }

                    // The starting location is now the correct location. 
                    if (locationToFailover.Value.Current.isPrimaryLocation)
                    {
                        // Remove the override so it uses default location for future requests.
                        if (changedInitialLocation && primaryReplicaUri != null)
                        {
                            this.lazyUriToFailedOverLocation.Value.TryRemove(primaryReplicaUri, out _);
                        }
                    }
                    else
                    {
                        // Update the primary replica Uri to point to the new location
                        if (primaryReplicaUri == null)
                        {
                            primaryReplicaUri = await this.GetPrimaryReplicaUriAsync(
                                documentServiceRequest,
                                cancellationToken);
                        }

                        using (request.Trace.StartChild("PartitionKeyRangeWriteFailoverHandler AddOrUpdate location"))
                        {
                            this.lazyUriToFailedOverLocation.Value.AddOrUpdate(
                                primaryReplicaUri,
                                locationToFailover.Value.Current.endpoint,
                                (key, currentLocation) => locationToFailover.Value.Current.endpoint);
                        }
                    }

                    return (true, responseMessage);
                }

                if (!this.TryUpdateLocation(
                     documentServiceRequest.RequestContext,
                     locationToFailover.Value))
                {
                    return (false, responseMessage);
                }

                requestDidPartitionLevelRetry = true;
            }
        }

        private bool TryUpdateLocation(
            DocumentServiceRequestContext requestContext,
            IEnumerator<(Uri endpoint, bool isPrimaryLocation)> locationToFailover)
        {
            // No more location to retry on
            if (!locationToFailover.MoveNext())
            {
                return false;
            }

            // Update the request to use the new location
            requestContext.ClearRouteToLocation();
            requestContext.RouteToLocation(locationToFailover.Current.endpoint);
            return true;
        }

        private async Task<PartitionKeyRange> GetPrimaryReplicaUriAsync(
            DocumentServiceRequest documentServiceRequest,
            CancellationToken cancellationToken)
        {
            if (documentServiceRequest.PartitionKeyRangeIdentity == null)
            {
                await this.addressResolver.Value.ResolveAsync(
                     documentServiceRequest,
                     false,
                     cancellationToken);
            }

            return documentServiceRequest.RequestContext.ResolvedPartitionKeyRange;
        }

        private IEnumerator<(Uri endpoint, bool isPrimaryLocation)> GetLocationsToRetryOn(IReadOnlyCollection<Uri> endpoints, Uri startingEndpoint)
        {
            bool isStartingUriFound = false;
            // Skip the starting point since it was already tried
            foreach (Uri endpoint in endpoints)
            {
                // Most cases the first endpoint will be the starting point.
                // This avoid URI comparison overhead for all the other APIs.
                if (!isStartingUriFound &&
                    endpoint == startingEndpoint)
                {
                    isStartingUriFound = true;
                    continue;
                }

                yield return (endpoint, false);
            }
        }
    }
}
