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
        private readonly Lazy<ConcurrentDictionary<Uri, Uri>> lazyUriToFailedOverLocation;
        private readonly Func<IReadOnlyCollection<Uri>> getReadEndpoints;
        private readonly IAddressResolver addressResolver;
        private readonly Func<Task<Cosmos.ConsistencyLevel>> getAccountConsistencyAsync;

        private PartitionKeyRangeWriteFailoverHandler(
            Func<Task<Cosmos.ConsistencyLevel>> getAccountConsistency,
            Func<IReadOnlyCollection<Uri>> getReadEndpoints,
            IAddressResolver addressResolver)
        {
            this.getAccountConsistencyAsync = getAccountConsistency ?? throw new ArgumentNullException(nameof(getAccountConsistency));
            this.lazyUriToFailedOverLocation = new Lazy<ConcurrentDictionary<Uri, Uri>>();
            this.getReadEndpoints = getReadEndpoints ?? throw new ArgumentNullException(nameof(getReadEndpoints));
            this.addressResolver = addressResolver ?? throw new ArgumentNullException(nameof(addressResolver));
        }

        public static bool TryCreate(
            Func<Task<Cosmos.ConsistencyLevel>> getAccountConsistency,
            Func<IReadOnlyCollection<Uri>> getReadEndpoints,
            IAddressResolver addressResolver,
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
                    return await this.DocumentWritePartitionKeyRangeFailoverHelperAsync(request, cancellationToken);
                }
            }

            return await base.SendAsync(request, cancellationToken);
        }

        private async Task<ResponseMessage> DocumentWritePartitionKeyRangeFailoverHelperAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            Uri? startingLocation = null;
            DocumentServiceRequest? documentServiceRequest = null;
            Uri? primaryReplicaUri = null;
            bool changedInitialLocation = false;

            if (this.lazyUriToFailedOverLocation.IsValueCreated &&
                    this.lazyUriToFailedOverLocation.Value.Any())
            {
                documentServiceRequest = request.ToDocumentServiceRequest();
                startingLocation = documentServiceRequest.RequestContext.LocationEndpointToRoute;

                primaryReplicaUri = await this.GetPrimaryReplicaUriAsync(
                    documentServiceRequest,
                    cancellationToken);

                // The partition failed over to a different region. Change the location to the new region.
                if (this.lazyUriToFailedOverLocation.Value.TryGetValue(primaryReplicaUri, out Uri initialOverrideLocation))
                {
                    changedInitialLocation = true;
                    documentServiceRequest.RequestContext.RouteToLocation(initialOverrideLocation);
                }
            }

            IEnumerator<(Uri endpoint, bool isPrimaryLocation)>? locationToFailover = null;
            while (true)
            {
                ResponseMessage responseMessage = await base.SendAsync(request, cancellationToken);

                bool isWriteForbidden = responseMessage.StatusCode == HttpStatusCode.Forbidden &&
                    responseMessage.Headers.SubStatusCode == SubStatusCodes.WriteForbidden;

                bool isServiceUnavailable = responseMessage.StatusCode != HttpStatusCode.ServiceUnavailable;

                // The request should not be retried in another region
                if (!isWriteForbidden || !isServiceUnavailable)
                {
                    // The initial location failed and was retried on other regions
                    if (locationToFailover != null)
                    {
                        documentServiceRequest ??= request.ToDocumentServiceRequest();

                        // The starting location is now the correct location. 
                        if (locationToFailover.Current.isPrimaryLocation)
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

                            this.lazyUriToFailedOverLocation.Value.AddOrUpdate(
                                primaryReplicaUri,
                                locationToFailover.Current.endpoint,
                                (key, currentLocation) => locationToFailover.Current.endpoint);
                        }
                    }

                    return responseMessage;
                }

                documentServiceRequest ??= request.ToDocumentServiceRequest();
                DocumentServiceRequestContext requestContext = documentServiceRequest.RequestContext;

                if (locationToFailover == null)
                {
                    startingLocation ??= requestContext.LocationEndpointToRoute;
                    locationToFailover = this.GetLocationsToRetryOn(
                        this.getReadEndpoints(),
                        startingLocation);
                }

                // No more location to retry on
                if (!locationToFailover.MoveNext())
                {
                    return responseMessage;
                }

                // Update the request to use the new location
                requestContext.ClearRouteToLocation();
                requestContext.RouteToLocation(locationToFailover.Current.endpoint);
            }
        }

        private async Task<Uri> GetPrimaryReplicaUriAsync(
            DocumentServiceRequest documentServiceRequest,
            CancellationToken cancellationToken)
        {
            PartitionAddressInformation partitionInfo = await this.addressResolver.ResolveAsync(
                 documentServiceRequest,
                 false,
                 cancellationToken);

            Uri primaryReplicaUri = partitionInfo.GetPrimaryUri(documentServiceRequest, Documents.Client.Protocol.Tcp);
            return primaryReplicaUri;
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

            yield return (startingEndpoint, true);
        }
    }
}
