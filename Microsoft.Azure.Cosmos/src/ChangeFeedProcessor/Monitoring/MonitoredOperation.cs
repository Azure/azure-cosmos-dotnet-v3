//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
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