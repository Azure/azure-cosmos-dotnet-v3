//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// The event type during the Change Feed Processor lifecycle
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
    public enum ChangeFeedProcessorEvent
    {
        /// <summary>
        /// The host acquires the lease and starts processing
        /// </summary>
        AcquireLease,
        /// <summary>
        /// The host is reading the Change Feed for the lease
        /// </summary>
        ReadingChangeFeedLease,
        /// <summary>
        /// The host sent the new changes to the delegate for processing
        /// </summary>
        ProcessingLease,
        /// <summary>
        /// The host updates the lease after a successful processing
        /// </summary>
        CheckpointLease,
        /// <summary>
        /// The host releases the lease due to shutdown, rebalancing, error during processing
        /// </summary>
        ReleaseLease,
    }
}