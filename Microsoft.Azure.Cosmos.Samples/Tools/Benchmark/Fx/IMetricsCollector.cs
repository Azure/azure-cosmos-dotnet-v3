//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;

    /// <summary>
    /// Represents the metrics collector.
    /// </summary>
    public interface IMetricsCollector
    {
        /// <summary>
        /// Collects the number of successful operations.
        /// </summary>
        void CollectMetricsOnSuccess();

        /// <summary>
        /// Collects the number of failed operations.
        /// </summary>
        void CollectMetricsOnFailure();

        /// <summary>
        /// Records latency for success operations in milliseconda.
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds to record.</param>
        void RecordSuccessOpLatencyAndRps(TimeSpan timeSpan);

        /// <summary>
        /// Records latency for failed operations in milliseconda.
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds to record.</param>
        void RecordFailedOpLatencyAndRps(TimeSpan timeSpan);
    }
}