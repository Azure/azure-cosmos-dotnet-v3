//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
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
            this.ReplicaAddresses = internalAddresses.Any() ? internalAddresses.ToList()
                                : nonEmptyReplicaAddresses.Where(address => address.IsPublic).ToList();

            this.ReplicaUris = this.ReplicaAddresses
                                .Select(e => new Uri(e.PhysicalUri))
                                .ToList();

            // The TransportAddressUri object for a replica is expected to be shared across all of the different
            // compositions, like the ReplicaTransportAddressUris, NonPrimaryReplicaTransportAddressUris and
            // PrimaryReplicaTransportAddressUri. This means that unlike the prior implementation, there will be
            // a same object shared across all of the primary and non-primary replica lists and any changes in
            // one of the object will be reflected across.
            List<string> transportUriHealthStates = new ();
            List<TransportAddressUri> transportAddressUris = new (), nonPrimaryTransportAddressUris = new ();
            foreach (AddressInformation addressInfo in this.ReplicaAddresses)
            {
                TransportAddressUri transportAddressUri = new (
                        addressUri: new Uri(
                            uriString: addressInfo.PhysicalUri));

                transportAddressUris.Add(transportAddressUri);

                if (addressInfo.IsPrimary && !addressInfo.PhysicalUri.Contains('['))
                {
                    this.PrimaryReplicaTransportAddressUri = transportAddressUri;
                }
                else
                {
                    nonPrimaryTransportAddressUris.Add(transportAddressUri);
                }

                transportUriHealthStates.Add(transportAddressUri
                    .GetCurrentHealthState()
                    .GetHealthStatusDiagnosticString());
            }

            this.Protocol = protocol;
            this.ReplicaTransportAddressUris = transportAddressUris.AsReadOnly();
            this.ReplicaTransportAddressUrisHealthState = transportUriHealthStates.AsReadOnly();
            this.NonPrimaryReplicaTransportAddressUris = nonPrimaryTransportAddressUris.AsReadOnly();
        }

        public void SetTransportAddressUrisHealthState(List<string> replicaHealthStates)
        {
            this.ReplicaTransportAddressUrisHealthState = replicaHealthStates.AsReadOnly();
        }

        public TransportAddressUri GetPrimaryAddressUri(DocumentServiceRequest request)
        {
            TransportAddressUri primaryReplicaAddress = null;
            // if replicaIndex is not set, or if replicaIndex is 0, we return primary address.
            if (!request.DefaultReplicaIndex.HasValue || request.DefaultReplicaIndex.Value == 0)
            {
                primaryReplicaAddress = this.PrimaryReplicaTransportAddressUri;
            }
            else
            {
                if (request.DefaultReplicaIndex.Value > 0 && request.DefaultReplicaIndex.Value < this.ReplicaUris.Count)
                {
                    primaryReplicaAddress = this.ReplicaTransportAddressUris[(int)request.DefaultReplicaIndex.Value];
                }
            }

            if (primaryReplicaAddress == null)
            {
                // Primary endpoint (of the desired protocol) was not found.
                throw new GoneException(string.Format(CultureInfo.CurrentUICulture, "The requested resource is no longer available at the server. Returned addresses are {0}",
                                                      string.Join(",", this.ReplicaAddresses.Select(address => address.PhysicalUri).ToList())),
                                        SubStatusCodes.ServerGenerated410);
            }

            return primaryReplicaAddress;
        }

        public Protocol Protocol { get; }

        public IReadOnlyList<TransportAddressUri> NonPrimaryReplicaTransportAddressUris { get; }

        public IReadOnlyList<Uri> ReplicaUris { get; }

        public IReadOnlyList<TransportAddressUri> ReplicaTransportAddressUris { get; }

        public IReadOnlyList<string> ReplicaTransportAddressUrisHealthState { get; private set; }
        public Uri PrimaryReplicaUri => this.PrimaryReplicaTransportAddressUri?.Uri;

        public TransportAddressUri PrimaryReplicaTransportAddressUri { get; }

        public IReadOnlyList<AddressInformation> ReplicaAddresses { get; }
    }
}
