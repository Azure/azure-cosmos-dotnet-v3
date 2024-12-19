//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Antlr4.Runtime;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents the configuration options for collecting metrics related to Cosmos DB network operations.
    /// </summary>
    public class NetworkMetricsOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the routing ID (e.g., PK Range ID for Gateway Mode or Partition/Replica information for Direct Mode, if available) 
        /// should be included in the network metrics. The default value is <c>false</c>.
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
        public bool IncludeRoutingId { get; set; } = false;

        /// <summary>
        /// Gets or sets a collection of custom dimensions to include in the network metrics.
        /// Each dimension is defined as a key-value pair, where the key is the dimension name and the value is a function that returns the dimension value.
        /// </summary>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// var telemetryOptions = new NetworkMetricsOptions
        /// {
        ///     CustomDimensions = new Dictionary<string, Func<string>>
        ///     {
        ///         // Static value
        ///         { "Region", () => "EastUS" },
        ///         
        ///         // Dynamic value based on runtime logic
        ///         { "RequestId", () => Guid.NewGuid().ToString() },
        ///         { "Timestamp", () => DateTime.UtcNow.ToString("o") }
        ///         { "Environment", () => AppSettings.Environment }, // e.g., "Production", "Staging"
        ///         { "ServiceVersion", () => Assembly.GetExecutingAssembly().GetName().Version.ToString() }
        ///         
        ///         // Dynamic value based on user interaction
        ///         { "CustomerType", () => GetCurrentCustomerType() }, // E.g., "Premium", "Standard"
        ///         
        ///         // Dynamic value reflecting the business context of the operation
        ///         { "OrderStatus", () => GetOrderStatusForCurrentTransaction() } // E.g., "Pending", "Shipped"
        /// 
        ///     }
        /// };
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Enabling this option provides enhanced diagnostic granularity based on the added dimensions. 
        /// However, it may increase the cardinality of metrics, potentially leading to higher storage costs and generating a large volume of low-sample-count metrics, 
        /// which can complicate analysis.
        /// </para>
        /// <para>
        /// So, Carefully evaluate whether the additional granularity is necessary 
        /// for your use case, as it may lead to increased resource consumption and complexity.
        /// </para>
        /// </remarks>
        [JsonIgnore]
        public IDictionary<string, Func<string>> CustomDimensions { get; set; }
    }

}
