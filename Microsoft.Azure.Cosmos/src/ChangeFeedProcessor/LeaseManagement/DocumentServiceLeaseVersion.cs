//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    /// <summary>
    /// Versioning of the lease schema.
    /// </summary>
    internal enum DocumentServiceLeaseVersion
    {
        PartitionKeyRangeBasedLease = 0,
        EPKRangeBasedLease = 1
    }
}