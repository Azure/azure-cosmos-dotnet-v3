//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Telemetry Options for Cosmos Client
    /// </summary>
    public class CosmosClientTelemetryOptions
    {
        /// <summary>
        /// Enable sending telemetry to service, <see cref="CosmosThresholdOptions"/> is not applicable to this as of now
        /// </summary>
        public bool EnableSendingMetricsToService { get; set; } = false;

        /// <summary>
        /// Gets or sets the flag to generate operation level <see cref="System.Diagnostics.Activity"/> for methods calls using the Source Name "Azure.Cosmos.Operation".
        /// </summary>
        /// <value>
        /// The default value is true (for preview package).
        /// </value>
        /// <remarks>This flag is there to disable it from source. Please Refer https://opentelemetry.io/docs/instrumentation/net/exporters/ to know more about open telemetry exporters</remarks>
#if PREVIEW
        public 
#else
        internal
#endif
        bool DisableDistributedTracing { get; set; } =
#if PREVIEW
        false;
#else
        true;
#endif

        /// <summary>
        /// Threshold values for telemetry
        /// </summary>
#if PREVIEW
        public 
#else
        internal
#endif
        CosmosThresholdOptions CosmosThresholdOptions { get; set; }
    }
}