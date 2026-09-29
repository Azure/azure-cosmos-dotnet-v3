//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Client;

    internal sealed class AddressSelector
    {
        private readonly IAddressResolver addressResolver;
        private readonly Protocol protocol;

        public AddressSelector(IAddressResolver addressResolver,
            Protocol protocol)
        {
            this.addressResolver = addressResolver;
            this.protocol = protocol;
        }

        /// <summary>
        /// Resolves the transport address uris from the given request and returns them along with their health statuses.
        /// Note that the returned transport address uris are not ordered by their health status and the two lists could
        /// have a completely random ordering while returning the addresses.
        /// </summary>
        /// <param name="request">An instance of <see cref="DocumentServiceRequest"/> containing the request payload.</param>
        /// <param name="includePrimary">A boolean flag indicating if the primary replica needed to be included while resolving the addresses.</param>
        /// <param name="forceRefresh">A boolean flag indicating if force refresh was requested.</param>
        /// <returns></returns>
        public async Task<(IReadOnlyList<TransportAddressUri>, IReadOnlyList<string>)> ResolveAllTransportAddressUriAsync(
            DocumentServiceRequest request,
            bool includePrimary,
            bool forceRefresh)
        {
            PerProtocolPartitionAddressInformation partitionPerProtocolAddress = await this.ResolveAddressesAsync(request, forceRefresh);

            return includePrimary
                ? (partitionPerProtocolAddress.ReplicaTransportAddressUris, partitionPerProtocolAddress.ReplicaTransportAddressUrisHealthState)
                : (partitionPerProtocolAddress.NonPrimaryReplicaTransportAddressUris, partitionPerProtocolAddress.ReplicaTransportAddressUrisHealthState);
        }

        public async Task<TransportAddressUri> ResolvePrimaryTransportAddressUriAsync(
            DocumentServiceRequest request,
            bool forceAddressRefresh)
        {
            PerProtocolPartitionAddressInformation partitionPerProtocolAddress = await this.ResolveAddressesAsync(request, forceAddressRefresh);
            return partitionPerProtocolAddress.GetPrimaryAddressUri(request);
        }

        public async Task<PerProtocolPartitionAddressInformation> ResolveAddressesAsync(
            DocumentServiceRequest request,
            bool forceAddressRefresh)
        {
            PartitionAddressInformation partitionAddressInformation =
                await this.addressResolver.ResolveAsync(request, forceAddressRefresh, CancellationToken.None);

            request.RequestContext.ResolvedPartitionTargetReplicaSetSize = partitionAddressInformation.PartitionTargetReplicaSetSize;

            PerProtocolPartitionAddressInformation perProtocolAddresses = partitionAddressInformation.Get(this.protocol);

            // Use the per-protocol replica count rather than AllAddresses.Count.
            // AllAddresses includes addresses for ALL protocols (e.g. TCP + HTTPS),
            // which inflates the count on the gateway and prevents the CRSS gate
            // from detecting scale-up events.
            request.RequestContext.ResolvedReplicaAddressCountPerProtocol = perProtocolAddresses.ReplicaTransportAddressUris.Count;

            return perProtocolAddresses;
        }

        /// <summary>
        /// Triggers a background address refresh if the backend-reported
        /// CurrentReplicaSetSize exceeds both the resolved address count
        /// and the per-partition target, indicating a scale-up from a
        /// previously reduced replica set size.
        /// </summary>
        internal void RefreshAddressesIfReplicaSetSizeChanged(
            DocumentServiceRequest request,
            int currentReplicaSetSizeFromResponse)
        {
            // Already refreshed by a prior replica response or GoneException
            // handler within this request — skip to avoid duplicate refreshes.
            if (request.RequestContext.PerformedBackgroundAddressRefresh)
            {
                return;
            }

            int? resolvedPartitionTargetReplicaSetSize = request.RequestContext.ResolvedPartitionTargetReplicaSetSize;

            if (resolvedPartitionTargetReplicaSetSize.HasValue
                && currentReplicaSetSizeFromResponse > request.RequestContext.ResolvedReplicaAddressCountPerProtocol
                && currentReplicaSetSizeFromResponse > resolvedPartitionTargetReplicaSetSize.Value)
            {
                DefaultTrace.TraceInformation(
                    "CRSS scale-up detected: currentReplicaSetSize={0}, resolvedAddressCount={1}, partitionTargetReplicaSetSize={2}",
                    currentReplicaSetSizeFromResponse,
                    request.RequestContext.ResolvedReplicaAddressCountPerProtocol,
                    resolvedPartitionTargetReplicaSetSize);

                this.StartBackgroundAddressRefresh(request);
                request.RequestContext.PerformedBackgroundAddressRefresh = true;
            }
        }

        public void StartBackgroundAddressRefresh(DocumentServiceRequest request)
        {
            try
            {
                // DocumentServiceRequest is not thread safe and must be cloned to avoid
                // concurrency issues since this a background task.
                DocumentServiceRequest requestClone = request.Clone();
                this.ResolveAllTransportAddressUriAsync(requestClone, true, true).ContinueWith((task) =>
                {
                    if (task.IsFaulted)
                    {
                        DefaultTrace.TraceWarning(
                            "Background refresh of the addresses failed with {0}", task.Exception?.Message);
                    }
                });
            }
            catch (Exception exception)
            {
                DefaultTrace.TraceWarning("Background refresh of the addresses failed with {0}", exception.Message);
            }
        }
    }
}