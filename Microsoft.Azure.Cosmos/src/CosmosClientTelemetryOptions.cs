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
        /// Disable sending telemetry to service, <see cref="CosmosThresholdOptions"/> is not applicable to this as of now. 
        /// This option will disable sending telemetry to service.even it is opt-in from portal.
        /// </summary>
        /// <remarks>By default, it is false</remarks>
#if PREVIEW
        public 
#else
        internal
#endif
        bool DisableSendingMetricsToService { get; set; } =
#if PREVIEW
        false;
#else
        true;
#endif

        /// <summary>
        /// This method enable/disable generation of operation level <see cref="System.Diagnostics.Activity"/> if listener is subscribed to the Source Name "Azure.Cosmos.Operation".
        /// </summary>
        /// <value>
        /// The default value is true
        /// </value>
        /// <remarks> Please Refer https://opentelemetry.io/docs/instrumentation/net/exporters/ to know more about open telemetry exporters</remarks>
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
        /// Threshold values for Distributed Tracing. 
        /// These values decides whether to generate operation level <see cref="System.Diagnostics.Tracing.EventSource"/> with request diagnostics or not.
        /// </summary>
#if PREVIEW
        public 
#else
        internal
#endif
        CosmosThresholdOptions CosmosThresholdOptions { get; set; } = new CosmosThresholdOptions();

    }
}