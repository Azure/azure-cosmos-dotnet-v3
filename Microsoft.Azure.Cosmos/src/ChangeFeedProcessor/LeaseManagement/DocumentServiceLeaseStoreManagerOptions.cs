//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
{
    internal class DocumentServiceLeaseStoreManagerOptions
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
