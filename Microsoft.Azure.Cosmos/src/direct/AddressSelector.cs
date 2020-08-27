//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
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

        public async Task<IReadOnlyList<Uri>> ResolveAllUriAsync(
           DocumentServiceRequest request,
           bool includePrimary,
           bool forceRefresh)
        {
            PerProtocolPartitionAddressInformation partitionPerProtocolAddress = await this.ResolveAddressesAsync(request, forceRefresh);

            return includePrimary
                ? partitionPerProtocolAddress.ReplicaUris
                : partitionPerProtocolAddress.NonPrimaryReplicaUris;
        }

        public async Task<Uri> ResolvePrimaryUriAsync(
            DocumentServiceRequest request,
            bool forceAddressRefresh)
        {
            PerProtocolPartitionAddressInformation partitionPerProtocolAddress = await this.ResolveAddressesAsync(request, forceAddressRefresh);
            return partitionPerProtocolAddress.GetPrimaryUri(request);
        }

        public async Task<PerProtocolPartitionAddressInformation> ResolveAddressesAsync(
            DocumentServiceRequest request,
            bool forceAddressRefresh)
        {
            PartitionAddressInformation partitionAddressInformation =
                await this.addressResolver.ResolveAsync(request, forceAddressRefresh, CancellationToken.None);

            return partitionAddressInformation.Get(this.protocol);
        }
    }
}