//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Monitoring
{
    /// <summary>
    /// The health monitoring phase
    /// </summary>
    internal enum MonitoredOperation
    {
        /// <summary>
        /// A phase when the instance tries to acquire the lease
        /// </summary>
        AcquireLease,
    }
}