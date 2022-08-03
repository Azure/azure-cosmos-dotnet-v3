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
        public static readonly TimeSpan DefaultQueryTimeoutThreshold = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Load Open Telemetry Configurations
        /// </summary>
        /// <param name="clientOptions"></param>
        public OpenTelemetryOptions(CosmosClientOptions clientOptions)
        {
            this.CrudLatencyThreshold = clientOptions.CrudLatencyThresholdForDiagnostics;
            this.QueryLatencyThreshold = clientOptions.QueryLatencyThresholdForDiagnostics;
        }

        /// <summary>
        /// Customer defined Crud Latency Threshold
        /// </summary>
        public TimeSpan? CrudLatencyThreshold { get; }

        /// <summary>
        /// Customer defined Query Latency Threshold
        /// </summary>
        public TimeSpan? QueryLatencyThreshold { get; }

    }
}
