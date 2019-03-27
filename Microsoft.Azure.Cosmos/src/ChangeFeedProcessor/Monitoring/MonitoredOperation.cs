//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.Monitoring
{
    /// <summary>
    /// The health monitoring phase
    /// </summary>
    public enum MonitoredOperation
    {
        /// <summary>
        /// A phase when the instance tries to acquire the lease
        /// </summary>
        AcquireLease,
    }
}