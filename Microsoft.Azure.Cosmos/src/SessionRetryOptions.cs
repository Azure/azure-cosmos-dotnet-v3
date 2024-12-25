namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    /// <summary>
    /// Telemetry Options for Cosmos Client to enable/disable telemetry and distributed tracing along with corresponding threshold values.
    /// </summary>
    public class SessionRetryOptions
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
        public int MinInRegionRetryTime { get; set; }

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
        public int MaxInRegionRetryCount { get; set; }

    }
}
