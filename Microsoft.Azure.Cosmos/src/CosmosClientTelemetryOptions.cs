//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Telemetry Options for Cosmos Client to enable/disable telemetry and distributed tracing along with corresponding threshold values.
    /// </summary>
#if PREVIEW
        public 
#else
        internal
#endif 
        class CosmosClientTelemetryOptions
    {
        /// <summary>
        /// Disable sending telemetry to service, <see cref="Microsoft.Azure.Cosmos.CosmosThresholdOptions"/> is not applicable to this as of now. 
        /// This option will disable sending telemetry to service.even it is opt-in from portal.
        /// </summary>
        /// <remarks>By default, it is true</remarks>
#if PREVIEW
        public 
#else
        internal
#endif
        bool DisableSendingMetricsToService { get; set; } = true;

        /// <summary>
        /// This method enable/disable generation of operation level <see cref="System.Diagnostics.Activity"/> if listener is subscribed to the Source Name "Azure.Cosmos.Operation".
        /// </summary>
        /// <value>
        /// The default value is false
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