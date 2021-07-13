//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Health monitor to capture lifecycle events of the Change Feed Processor.
    /// </summary>
    internal abstract class ChangeFeedProcessorHealthMonitor
    {
        /// <summary>
        /// For normal informational events happening on the context of a lease
        /// </summary>
        /// <param name="leaseToken">A unique identifier for the lease.</param>
        /// <returns>An asynchronous operation representing the logging operation.</returns>
        public abstract Task NotifyLeaseAcquireAsync(string leaseToken);

        /// <summary>
        /// For transient errors that the Change Feed Processor attemps retrying on.
        /// </summary>
        /// <param name="leaseToken">A unique identifier for the lease.</param>
        /// <returns>An asynchronous operation representing the logging operation.</returns>
        public abstract Task NotifyLeaseReleaseAsync(string leaseToken);

        /// <summary>
        /// For critical errors that the Change Feed Processor that might not be recoverable.
        /// </summary>
        /// <param name="leaseToken">A unique identifier for the lease.</param>
        /// <param name="exception">The exception that happened.</param>
        /// <returns>An asynchronous operation representing the logging operation.</returns>
        public abstract Task NotifyErrorAsync(string leaseToken, Exception exception);
    }
}