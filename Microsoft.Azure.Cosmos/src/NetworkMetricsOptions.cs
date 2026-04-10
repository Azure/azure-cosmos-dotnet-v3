//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents the configuration options for collecting metrics related to Cosmos DB network operations.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    class NetworkMetricsOptions
    {
        /// <summary>
        /// <para>
        /// Gets or sets a value indicating whether the routing ID (e.g., PK Range ID for Gateway Mode or Partition/Replica information for Direct Mode, if available) 
        /// should be included in the network metrics.
        /// </para>
        /// By default, Routing Id information is not included as a dimension in the network metrics.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Enabling this option provides greater diagnostic granularity, allowing you to identify issues 
        /// with specific Partition Key Range IDs, replicas, or partitions. 
        /// However, including the routing ID as a dimension increases the cardinality of metrics. This can result 
        /// in significantly higher storage costs and generate a large number of metrics with low sample counts, 
        /// making analysis more challenging.
        /// </para>
        /// <para>
        /// Carefully evaluate whether the additional granularity is necessary 
        /// for your use case, as it may lead to increased resource consumption and complexity.
        /// </para>
        /// </remarks>
        public bool? IncludeRoutingId { get; set; }

        /// <summary>
        /// Gets or sets a collection of custom dimensions to include in the network metrics. Each dimension is defined as a key-value pair.
        /// </summary>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// var telemetryOptions = new NetworkMetricsOptions
        /// {
        ///     CustomDimensions = new Dictionary<string, string>
        ///     {
        ///         { "Region", "EastUS" },
        ///     }
        /// };
        /// ]]>
        /// </code>
        /// </example>
        public IDictionary<string, string> CustomDimensions { get; set; }
    }

}
