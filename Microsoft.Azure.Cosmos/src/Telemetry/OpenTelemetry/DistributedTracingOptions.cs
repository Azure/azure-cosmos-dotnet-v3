// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Open Telemetry Configuration
    /// It needs to be public once AppInsight is ready
    /// </summary>
    internal sealed class DistributedTracingOptions
    {
        /// <summary>
        /// Default Latency threshold for other than query Operation
        /// </summary>
        internal static readonly TimeSpan DefaultCrudLatencyThreshold = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Default Latency threshold for QUERY operation
        /// </summary>
        internal static readonly TimeSpan DefaultQueryTimeoutThreshold = TimeSpan.FromMilliseconds(500);
        private bool enableDiagnosticsTraceForAllRequests;
        private TimeSpan? diagnosticsLatencyThreshold;

        /// <summary>
        /// Latency Threshold to generate (<see cref="System.Diagnostics.Tracing.EventSource"/>) with Request diagnostics in distributing Tracing.<br></br>
        /// If it is not set then by default it will generate (<see cref="System.Diagnostics.Tracing.EventSource"/>) for query operation which are taking more than 500 ms and non-query operations taking more than 100 ms.
        /// </summary>
        public TimeSpan? DiagnosticsLatencyThreshold
        {
            get => this.diagnosticsLatencyThreshold;
            set
            {
                if (this.EnableDiagnosticsTraceForAllRequests)
                {
                    throw new ArgumentException("EnableDiagnosticsTraceForAllRequests can not be true along with DiagnosticsLatencyThreshold.");
                }
                
                this.diagnosticsLatencyThreshold = value;
            }
        }

        /// <summary>
        /// Set this flag as true if you want to generate (<see cref="System.Diagnostics.Tracing.EventSource"/>) containing request diagnostics string for all the operations.
        /// If this flag is true then, it won't honour <see cref="DiagnosticsLatencyThreshold"/> value to generate diagnostic traces.
        /// </summary>
        public bool EnableDiagnosticsTraceForAllRequests
        {
            get => this.enableDiagnosticsTraceForAllRequests;
            set
            {
                if (value && this.DiagnosticsLatencyThreshold != null)
                {
                    throw new ArgumentException("EnableDiagnosticsTraceForAllRequests can not be true along with DiagnosticsLatencyThreshold.");
                }
                
                this.enableDiagnosticsTraceForAllRequests = value;
            }
        }
    }
}
