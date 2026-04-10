//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Telemetry Options for Cosmos Client to enable/disable telemetry and distributed tracing along with corresponding threshold values.
    /// </summary>
    public class CosmosClientTelemetryOptions
    {
        /// <summary>
        /// Disable sending telemetry data to Microsoft, <see cref="Microsoft.Azure.Cosmos.CosmosThresholdOptions"/> is not applicable for this. 
        /// </summary>
        /// <remarks>This feature has to be enabled at 2 places:
        /// <list type="bullet">
        /// <item>Opt-in from portal to subscribe for this feature.</item>
        /// <item>Setting this property to false, to enable it for a particular client instance.</item>
        /// </list>
        /// </remarks>
        /// <value>true</value>
        public bool DisableSendingMetricsToService { get; set; } = true;

        /// <summary>
        /// This method enable/disable generation of operation level <see cref="System.Diagnostics.Activity"/> if listener is subscribed to the Source Name <i>"Azure.Cosmos.Operation"</i>(to capture operation level traces) 
        /// and <i>"Azure-Cosmos-Operation-Request-Diagnostics"</i>(to capture events with request diagnostics JSON)
        /// </summary>
        /// <value>false</value>
        /// <remarks>
        /// You can set different thresholds values by setting <see cref="Microsoft.Azure.Cosmos.CosmosThresholdOptions"/>. 
        /// It would generate events with Request Diagnostics JSON, if any of the configured threshold is crossed, otherwise it would always generate events with Request Diagnostics JSON for failed requests.
        /// There is some overhead of emitting the more detailed diagnostics - so recommendation is to choose these thresholds that reduce the noise level
        /// and only emit detailed diagnostics when there is really business impact seen.<br></br>
        /// Refer <a href="https://opentelemetry.io/docs/instrumentation/net/exporters/"></a> to know more about open telemetry exporters available. <br></br>
        /// Refer <a href="https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/sdk-observability?tabs=dotnet"></a> to know more about this feature.
        /// </remarks>
        public bool DisableDistributedTracing { get; set; } =
#if PREVIEW
        false;
#else
        true;
#endif

        /// <summary>
        /// Threshold values for Distributed Tracing. 
        /// These values decides whether to generate an <see cref="System.Diagnostics.Tracing.EventSource"/> with request diagnostics or not.
        /// </summary>
        public CosmosThresholdOptions CosmosThresholdOptions { get; set; } = new CosmosThresholdOptions();

        /// <summary>
        /// Enables printing query in Traces db.query.text attribute. By default, query is not printed.
        /// Users have the option to enable printing parameterized or all queries, 
        /// but has to beware that customer data may be shown when the later option is chosen. It's the user's responsibility to sanitize the queries if necessary.
        /// </summary>
        public QueryTextMode QueryTextMode { get; set; } = QueryTextMode.None;

        /// <summary>
        /// Indicates whether client-side metrics collection is enabled or disabled. 
        /// When set to true, the application will capture and report client metrics such as request counts, latencies, errors, and other key performance indicators. 
        /// If false, no metrics related to the client will be gathered or reported.
        /// <remarks>Metrics data can be published to a monitoring system like Prometheus or Azure Monitor, depending on the configured metrics provider.</remarks>
        /// </summary>
#if PREVIEW
        public
#else
        internal
#endif
        bool IsClientMetricsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the configuration for operation-level metrics.
        /// </summary>
#if PREVIEW
        public
#else
        internal
#endif
        OperationMetricsOptions OperationMetricsOptions { get; set; }

        /// <summary>
        /// Gets or sets the configuration for network-level metrics.
        /// </summary>
#if PREVIEW
        public
#else
        internal
#endif
        NetworkMetricsOptions NetworkMetricsOptions { get; set; }
    }
}