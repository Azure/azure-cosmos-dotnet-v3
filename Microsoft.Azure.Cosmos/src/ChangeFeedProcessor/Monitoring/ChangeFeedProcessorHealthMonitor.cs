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
    /// <remarks>
    /// Lifecyle is:
    /// 1. AcquireLease. Lease is acquire and starts processing.
    /// 2. ReadingChangeFeedLease. New changes were read from Change Feed for the lease.
    /// 3. ProcessingLease. New changes were delivered to the delegate for the lease.
    /// 4. CheckpointLease. Lease was updated after a successful processing.
    /// 4. ReadingChangeFeedLease, ProcessingLease, and CheckpointLease keep happening until:
    /// 5. ReleaseLease. Lease is being released by the host.
    /// </remarks>
    public abstract class ChangeFeedProcessorHealthMonitor
    {
        /// <summary>
        /// For normal informational events happening on the context of a lease
        /// </summary>
        /// <param name="changeFeedProcessorEvent">The type of event.</param>
        /// <param name="leaseToken">A unique identifier for the lease.</param>
        /// <returns>An asynchronous operation representing the logging operation.</returns>
        public virtual Task NotifyInformationAsync(
            ChangeFeedProcessorEvent changeFeedProcessorEvent,
            string leaseToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// For transient errors that the Change Feed Processor attemps retrying on.
        /// </summary>
        /// <param name="changeFeedProcessorEvent">The type of event.</param>
        /// <param name="leaseToken">A unique identifier for the lease.</param>
        /// <param name="exception">The exception that happened.</param>
        /// <returns>An asynchronous operation representing the logging operation.</returns>
        public virtual Task NotifyErrorAsync(
            ChangeFeedProcessorEvent changeFeedProcessorEvent, 
            string leaseToken, 
            Exception exception)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// For critical errors that the Change Feed Processor that might not be recoverable.
        /// </summary>
        /// <param name="changeFeedProcessorEvent">The type of event.</param>
        /// <param name="leaseToken">A unique identifier for the lease.</param>
        /// <param name="exception">The exception that happened.</param>
        /// <returns>An asynchronous operation representing the logging operation.</returns>
        public virtual Task NotifyCriticalAsync(
            ChangeFeedProcessorEvent changeFeedProcessorEvent, 
            string leaseToken, 
            Exception exception)
        {
            return Task.CompletedTask;
        }
    }
}