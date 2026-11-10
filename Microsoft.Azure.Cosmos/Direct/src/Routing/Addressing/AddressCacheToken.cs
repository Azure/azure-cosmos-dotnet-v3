//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;

    /// <summary>
    /// Models the address cache token used to clear DocClient's address cache
    /// when TransportClient triggers an event on conection reset by the node
    /// </summary>
    internal sealed class AddressCacheToken
    {
        public readonly PartitionKeyRangeIdentity PartitionKeyRangeIdentity;
        public Uri ServiceEndpoint { get; private set; }

        public AddressCacheToken(
            PartitionKeyRangeIdentity partitionKeyRangeIdentity,
            Uri serviceEndpoint)
        {
            this.PartitionKeyRangeIdentity = partitionKeyRangeIdentity;
            this.ServiceEndpoint = serviceEndpoint;
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as AddressCacheToken);
        }

        public bool Equals(AddressCacheToken token)
        {
            return token != null && this.PartitionKeyRangeIdentity.Equals(token.PartitionKeyRangeIdentity) &&
                this.ServiceEndpoint.Equals(token.ServiceEndpoint);
        }

        public override int GetHashCode()
        {
            return this.PartitionKeyRangeIdentity.GetHashCode() ^ this.ServiceEndpoint.GetHashCode();
        }
    }
}