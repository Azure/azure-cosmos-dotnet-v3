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

        public async Task<IReadOnlyList<TransportAddressUri>> ResolveAllTransportAddressUriAsync(
            DocumentServiceRequest request,
            bool includePrimary,
            bool forceRefresh)
        {
            PerProtocolPartitionAddressInformation partitionPerProtocolAddress = await this.ResolveAddressesAsync(request, forceRefresh);

            return includePrimary
                ? partitionPerProtocolAddress.ReplicaTransportAddressUris
                : partitionPerProtocolAddress.NonPrimaryReplicaTransportAddressUris;
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

            return partitionAddressInformation.Get(this.protocol);
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
                            "Background refresh of the addresses failed with {0}", task.Exception.ToString());
                    }
                });
            }
            catch (Exception exception)
            {
                DefaultTrace.TraceWarning("Background refresh of the addresses failed with {0}", exception.ToString());
            }
        }
    }
}