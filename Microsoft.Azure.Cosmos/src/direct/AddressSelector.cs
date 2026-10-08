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
        /// Exceptionless variant of <see cref="ResolveAllTransportAddressUriAsync"/>.
        /// </summary>
        internal async Task<Res<(IReadOnlyList<TransportAddressUri>, IReadOnlyList<string>)>> NonThrowingResolveAllTransportAddressUriAsync(
            DocumentServiceRequest request,
            bool includePrimary,
            bool forceRefresh)
        {
            Res<PerProtocolPartitionAddressInformation> addressResult = await this.NonThrowingResolveAddressesAsync(request, forceRefresh);
            if (!addressResult.IsSuccess)
            {
                return Res.FromException<(IReadOnlyList<TransportAddressUri>, IReadOnlyList<string>)>(addressResult.Exception);
            }

            PerProtocolPartitionAddressInformation partitionPerProtocolAddress = addressResult.Value;
            return includePrimary
                ? Res.Success<(IReadOnlyList<TransportAddressUri>, IReadOnlyList<string>)>((partitionPerProtocolAddress.ReplicaTransportAddressUris, partitionPerProtocolAddress.ReplicaTransportAddressUrisHealthState))
                : Res.Success<(IReadOnlyList<TransportAddressUri>, IReadOnlyList<string>)>((partitionPerProtocolAddress.NonPrimaryReplicaTransportAddressUris, partitionPerProtocolAddress.ReplicaTransportAddressUrisHealthState));
        }

        /// <summary>
        /// Exceptionless variant of <see cref="ResolvePrimaryTransportAddressUriAsync"/>.
        /// </summary>
        internal async Task<Res<TransportAddressUri>> NonThrowingResolvePrimaryTransportAddressUriAsync(
            DocumentServiceRequest request,
            bool forceAddressRefresh)
        {
            Res<PerProtocolPartitionAddressInformation> addressResult = await this.NonThrowingResolveAddressesAsync(request, forceAddressRefresh);
            if (!addressResult.IsSuccess)
            {
                return Res.FromException<TransportAddressUri>(addressResult.Exception);
            }

            return addressResult.Value.TryGetPrimaryAddressUri(request);
        }

        /// <summary>
        /// Exceptionless variant of <see cref="ResolveAddressesAsync"/>.
        /// Routes through the opt-in <see cref="INonThrowingAddressResolver"/> when the
        /// configured resolver implements it and
        /// <see cref="DocumentServiceRequest.UseExceptionlessAddressResolution"/> is set,
        /// otherwise wraps the throwing <see cref="IAddressResolver.ResolveAsync"/>.
        /// </summary>
        /// <remarks>
        /// This is the single entry point into the exceptionless address-resolution
        /// subtree: the address caches, collection cache and partition-key-range cache
        /// are reached only through <see cref="INonThrowingAddressResolver.NonThrowingResolveAsync"/>.
        /// Gating here therefore disables that whole subtree, which lets the
        /// address-resolution work roll out independently of the exceptionless
        /// transport path. When the flag is off the original throwing resolver runs and
        /// its failure is captured here, so behaviour matches the pre-existing
        /// exceptionless transport path exactly.
        /// </remarks>
        internal async Task<Res<PerProtocolPartitionAddressInformation>> NonThrowingResolveAddressesAsync(
            DocumentServiceRequest request,
            bool forceAddressRefresh)
        {
            Res<PartitionAddressInformation> partitionAddressResult =
                request.UseExceptionlessAddressResolution
                    && this.addressResolver is INonThrowingAddressResolver nonThrowingResolver
                    ? await nonThrowingResolver.NonThrowingResolveAsync(request, forceAddressRefresh, CancellationToken.None)
                    : await Res.Wrap(this.addressResolver.ResolveAsync(request, forceAddressRefresh, CancellationToken.None));

            if (!partitionAddressResult.IsSuccess)
            {
                return Res.FromException<PerProtocolPartitionAddressInformation>(partitionAddressResult.Exception);
            }

            PartitionAddressInformation partitionAddressInformation = partitionAddressResult.Value;
            request.RequestContext.ResolvedPartitionTargetReplicaSetSize = partitionAddressInformation.PartitionTargetReplicaSetSize;

            PerProtocolPartitionAddressInformation perProtocolAddresses = partitionAddressInformation.Get(this.protocol);

            // Use the per-protocol replica count rather than AllAddresses.Count.
            // AllAddresses includes addresses for ALL protocols (e.g. TCP + HTTPS),
            // which inflates the count on the gateway and prevents the CRSS gate
            // from detecting scale-up events.
            request.RequestContext.ResolvedReplicaAddressCountPerProtocol = perProtocolAddresses.ReplicaTransportAddressUris.Count;

            return Res.Success(perProtocolAddresses);
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