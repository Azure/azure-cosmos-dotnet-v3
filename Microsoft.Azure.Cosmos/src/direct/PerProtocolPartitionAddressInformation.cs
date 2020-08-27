//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Microsoft.Azure.Documents.Client;

    internal sealed class PerProtocolPartitionAddressInformation
    {
        public PerProtocolPartitionAddressInformation(
            Protocol protocol,
            IReadOnlyList<AddressInformation> replicaAddresses)
        {
            if (replicaAddresses == null)
            {
                throw new ArgumentNullException(nameof(replicaAddresses));
            }

            IEnumerable<AddressInformation> nonEmptyReplicaAddresses = replicaAddresses
                            .Where(address => !string.IsNullOrEmpty(address.PhysicalUri) && address.Protocol == protocol);

            IEnumerable<AddressInformation> internalAddresses = nonEmptyReplicaAddresses.Where(address => !address.IsPublic);
            this.ReplicaAddresses = internalAddresses.Any() ? internalAddresses.ToArray() 
                                : nonEmptyReplicaAddresses.Where(address => address.IsPublic).ToArray();

            this.ReplicaUris = this.ReplicaAddresses
                                .Select(e => new Uri(e.PhysicalUri))
                                .ToArray();
            this.NonPrimaryReplicaUris = this.ReplicaAddresses
                                .Where(e => !e.IsPrimary)
                                .Select(e => new Uri(e.PhysicalUri))
                                .ToArray();

            AddressInformation primaryReplicaAddress = this.ReplicaAddresses.SingleOrDefault(
                address => address.IsPrimary && !address.PhysicalUri.Contains('['));
            if (primaryReplicaAddress != null)
            {
                this.PrimaryReplicaUri = new Uri(primaryReplicaAddress.PhysicalUri);
            }

            this.Protocol = protocol;
        }

        public Uri GetPrimaryUri(DocumentServiceRequest request)
        {
            Uri primaryReplicaAddress = null;
            // if replicaIndex is not set, or if replicaIndex is 0, we return primary address.
            if (!request.DefaultReplicaIndex.HasValue || request.DefaultReplicaIndex.Value == 0)
            {
                primaryReplicaAddress = this.PrimaryReplicaUri;
            }
            else
            {
                if (request.DefaultReplicaIndex.Value > 0 && request.DefaultReplicaIndex.Value < this.ReplicaUris.Count)
                {
                    primaryReplicaAddress = this.ReplicaUris[(int)request.DefaultReplicaIndex.Value];
                }
            }

            if (primaryReplicaAddress == null)
            {
                // Primary endpoint (of the desired protocol) was not found.
                throw new GoneException(string.Format(CultureInfo.CurrentUICulture, "The requested resource is no longer available at the server. Returned addresses are {0}",
                    string.Join(",", this.ReplicaAddresses.Select(address => address.PhysicalUri).ToList())));
            }

            return primaryReplicaAddress;
        }

        public Protocol Protocol { get; }

        public IReadOnlyList<Uri> NonPrimaryReplicaUris { get; }

        public IReadOnlyList<Uri> ReplicaUris { get; }

        public Uri PrimaryReplicaUri { get; }

        public IReadOnlyList<AddressInformation> ReplicaAddresses { get; }
    }
}
