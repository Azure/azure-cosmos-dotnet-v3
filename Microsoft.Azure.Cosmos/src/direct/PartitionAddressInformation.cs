//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Documents.Client;

    internal sealed class PartitionAddressInformation : IEquatable<PartitionAddressInformation>
    {
        private static readonly int AllProtocolsCount = Enum.GetNames(typeof(Protocol)).Length;
        private readonly PerProtocolPartitionAddressInformation[] perProtocolAddressInformation;
        private readonly Lazy<int> generateHashCode;

        public IReadOnlyList<AddressInformation> AllAddresses { get; }

        public bool IsLocalRegion { get; set; }

        public PartitionAddressInformation(IReadOnlyList<AddressInformation> replicaAddresses)
            : this(replicaAddresses, false)
        {

        }

        public PartitionAddressInformation(
            IReadOnlyList<AddressInformation> replicaAddresses,
            bool inNetworkRequest)
        {
            if (replicaAddresses == null)
            {
                throw new ArgumentNullException(nameof(replicaAddresses));
            }

            // Verify the list is sorted. If not sort it.
            for(int i = 1; i < replicaAddresses.Count; i++)
            {
                if(replicaAddresses[i-1].CompareTo(replicaAddresses[i]) > 0)
                {
                    AddressInformation[] clone = replicaAddresses.ToArray();
                    Array.Sort(clone);
                    replicaAddresses = clone;
                    break;
                }
            }

            this.AllAddresses = replicaAddresses;
            this.generateHashCode = new Lazy<int>(() =>
            {
                int hashCode = 17;
                foreach (AddressInformation replicaAddress in this.AllAddresses)
                {
                    hashCode = (hashCode * 397) ^ replicaAddress.GetHashCode();
                }
                return hashCode;
            });

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
            return this.perProtocolAddressInformation[(int)protocol].GetPrimaryAddressUri(request).Uri;
        }

        public PerProtocolPartitionAddressInformation Get(Protocol protocol)
        {
            return this.perProtocolAddressInformation[(int)protocol];
        }

        public override int GetHashCode()
        {
            return this.generateHashCode.Value;
        }

        public bool Equals(PartitionAddressInformation other)
        {
            if (other == null)
            {
                return false;
            }

            if (this.AllAddresses.Count != other.AllAddresses.Count)
            {
                return false;
            }

            return this.GetHashCode() == other.GetHashCode();
        }
    }
}
