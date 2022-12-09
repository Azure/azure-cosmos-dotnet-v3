// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Options for configuring the distributed tracing and event tracing
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

        /// <summary>
        /// SDK generates <see cref="System.Diagnostics.Tracing.EventSource"/> (Event Source Name is "Azure-Cosmos-Operation-Request-Diagnostics") with Request Diagnostics String, If Operation level distributed tracing is not disabled i.e. <see cref="Microsoft.Azure.Cosmos.CosmosClientOptions.IsDistributedTracingEnabled"/>
        /// </summary>
        /// <remarks>If it is not set then, by default, it will generate <see cref="System.Diagnostics.Tracing.EventSource"/> for query operation which are taking more than 500 ms and non-query operations taking more than 100 ms.</remarks>
        public TimeSpan? LatencyThresholdForDiagnosticEvent { get; set; }
    }
}
