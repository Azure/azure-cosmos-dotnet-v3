//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the configuration options for collecting metrics related to Cosmos DB operations and network.
    /// </summary>
    public class CosmosClientMetricsOptions
    {
        /// <summary>
        /// Gets or sets the configuration for operation-level metrics.
        /// </summary>
        public OperationMetricsOptions OperationMetricsOptions { get; set; }

        /// <summary>
        /// Gets or sets the configuration for network-level metrics.
        /// </summary>
        public NetworkMetricsOptions NetworkMetricsOptions { get; set; }
    }

    /// <summary>
    /// Represents the configuration options for collecting metrics related to Cosmos DB operations.
    /// </summary>
    public class OperationMetricsOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the region information should be included in the operation metrics.
        /// Default value is <c>false</c>.
        /// </summary>
        public bool IsRegionIncluded { get; set; } = false;

        /// <summary>
        /// Gets or sets a collection of custom dimensions to include in the operation metrics.
        /// Each dimension is defined as a key-value pair, where the key is the dimension name and the value is a function that returns the dimension value.
        /// </summary>
        public Dictionary<string, Func<string>> CustomDimensions { get; set; }
    }

    /// <summary>
    /// Represents the configuration options for collecting metrics related to Cosmos DB network operations.
    /// </summary>
    public class NetworkMetricsOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the routing ID should be included in the network metrics.
        /// Default value is <c>false</c>.
        /// </summary>
        public bool IsRoutingIdIncluded { get; set; } = false;

        /// <summary>
        /// Gets or sets a collection of custom dimensions to include in the network metrics.
        /// Each dimension is defined as a key-value pair, where the key is the dimension name and the value is a function that returns the dimension value.
        /// </summary>
        public Dictionary<string, Func<string>> CustomDimensions { get; set; }
    }
}
