//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Threshold values for Distributed Tracing
    /// </summary>
#if PREVIEW
    public 
#else
    internal
#endif 
        class CosmosThresholdOptions
    {
        /// <summary>
        /// Latency Threshold for non point operations
        /// </summary>
        public TimeSpan NonPointOperationLatencyThreshold { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Latency Threshold for point operations
        /// </summary>
        public TimeSpan PointOperationLatencyThreshold { get; set; } = TimeSpan.FromMilliseconds(100);
    }
}
