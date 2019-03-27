//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement
{
    internal class DocumentServiceLeaseStoreManagerSettings
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
