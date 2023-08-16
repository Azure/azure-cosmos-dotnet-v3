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
        /// Successful operation with latency
        /// </summary>
        void OnOperationSuccess(TimeSpan operationLatency);

        /// <summary>
        /// Failed operation with latency
        /// </summary>
        void OnOperationFailure(TimeSpan operationLatency);
    }
}