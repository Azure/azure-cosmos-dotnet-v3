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

    internal sealed class PartitionAddressInformation
    {
        private static readonly int AllProtocolsCount = Enum.GetNames(typeof(Protocol)).Length;
        private readonly PerProtocolPartitionAddressInformation[] perProtocolAddressInformation;

        public IReadOnlyList<AddressInformation> AllAddresses { get; }

        public bool IsLocalRegion { get; set; }

        public PartitionAddressInformation(AddressInformation[] replicaAddresses)
            : this(replicaAddresses, null, null)
        {

        }

        public PartitionAddressInformation(AddressInformation[] replicaAddresses, bool inNetworkRequest)
            : this(replicaAddresses, null, null, inNetworkRequest)
        {

        }

        public PartitionAddressInformation(
            AddressInformation[] replicaAddresses,
            PartitionKeyRangeIdentity partitionKeyRangeIdentity,
            Uri serviceEndpoint)
            : this(replicaAddresses, partitionKeyRangeIdentity, serviceEndpoint, false)
        {
           
        }

        public PartitionAddressInformation(
            AddressInformation[] replicaAddresses,
            PartitionKeyRangeIdentity partitionKeyRangeIdentity,
            Uri serviceEndpoint,
            bool inNetworkRequest)
        {
            if (replicaAddresses == null)
            {
                throw new ArgumentNullException(nameof(replicaAddresses));
            }

            this.AllAddresses = (AddressInformation[])replicaAddresses.Clone();
            this.perProtocolAddressInformation = new PerProtocolPartitionAddressInformation[PartitionAddressInformation.AllProtocolsCount];
            foreach (Protocol protocol in (Protocol[])Enum.GetValues(typeof(Protocol)))
            {
                this.perProtocolAddressInformation[(int)protocol] =
                    new PerProtocolPartitionAddressInformation(protocol, this.AllAddresses);
            }

            this.IsLocalRegion = inNetworkRequest;
        }

        public Uri GetPrimaryUri(DocumentServiceRequest request, Protocol protocol)
        {
            return this.perProtocolAddressInformation[(int)protocol].GetPrimaryUri(request);
        }

        public PerProtocolPartitionAddressInformation Get(Protocol protocol)
        {
            return this.perProtocolAddressInformation[(int)protocol];
        }
    }
}
