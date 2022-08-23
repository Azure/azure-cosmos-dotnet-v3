// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Open Telemetry Configuration
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
        sealed class DistributedTracingOptions
    {
        /// <summary>
        /// Default Latency threshold for other than query Operation
        /// </summary>
        internal static readonly TimeSpan DefaultCrudLatencyThreshold = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Default Latency threshold for QUERY operation
        /// </summary>
        internal static readonly TimeSpan DefaultQueryTimeoutThreshold = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Latency Threshold to generate (<see cref="System.Diagnostics.Tracing.EventSource"/>) with Request diagnostics in distributing Tracing.<br></br>
        /// If it is not set then by default it will generate (<see cref="System.Diagnostics.Tracing.EventSource"/>) for query operation which are taking more than 500 ms and non-query operations taking more than 100 ms.
        /// </summary>
        public TimeSpan? LatencyThresholdToIncludeDiagnostics { get; set; }

    }
}
