//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    internal sealed class DocumentServiceLeaseStoreManagerOptions
    {
        private const string PartitionLeasePrefixSeparator = "..";

        internal string ContainerNamePrefix { get; set; }

        internal string HostName { get; set; }

        internal string GetPartitionLeasePrefix()
        {
            return this.ContainerNamePrefix + PartitionLeasePrefixSeparator;
        }
    }
}
