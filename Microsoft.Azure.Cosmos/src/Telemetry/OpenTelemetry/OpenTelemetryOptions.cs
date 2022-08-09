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
    internal class OpenTelemetryOptions
    {
        /// <summary>
        /// Default Latency threshold for other than query Operation
        /// </summary>
        public static readonly TimeSpan DefaultCrudLatencyThreshold = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Default Latency threshold for QUERY operation
        /// </summary>
        public static readonly TimeSpan DefaultQueryTimeoutThreshold = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Load Open Telemetry Configurations
        /// </summary>
        /// <param name="clientOptions"></param>
        /// <param name="requestOptions"></param>
        public OpenTelemetryOptions(CosmosClientOptions clientOptions, RequestOptions requestOptions)
        {
            this.LatencyThreshold = requestOptions?.LatencyThresholdForDiagnosticsOnDistributingTracing ?? clientOptions?.LatencyThresholdForDiagnosticsOnDistributingTracing;
            this.EnableOpenTelemetrySupport = clientOptions?.EnableDistributedTracing ?? false;
        }

        /// <summary>
        /// Customer defined Latency Threshold
        /// </summary>
        public TimeSpan? LatencyThreshold { get; }

        /// <summary>
        /// Enable Open Telemetry Support
        /// </summary>
        public bool EnableOpenTelemetrySupport { get; }
    }
}
