//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
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
        public bool ShouldIncludeRoutingId { get; set; } = false;

        /// <summary>
        /// Gets or sets a collection of custom dimensions to include in the network metrics.
        /// Each dimension is defined as a key-value pair, where the key is the dimension name and the value is a function that returns the dimension value.
        /// </summary>
        [JsonIgnore]
        public IDictionary<string, Func<string>> CustomDimensions { get; set; }
    }

}
