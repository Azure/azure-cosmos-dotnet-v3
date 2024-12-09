//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// CosmosClientMetricsOptions
    /// </summary>
    public class CosmosClientMetricsOptions
    {
        /// <summary>
        /// OperationMetricsOption
        /// </summary>
        public OperationMetricsOptions OperationMetricsOptions { get; set; }

        /// <summary>
        /// NetworkMetricsOption
        /// </summary>
        public NetworkMetricsOptions NetworkMetricsOptions { get; set; }
    }

    /// <summary>
    /// OperationMetricsOptions
    /// </summary>
    public class OperationMetricsOptions
    {
        /// <summary>
        /// IsRegionIncluded
        /// </summary>
        public bool IsRegionIncluded { get; set; } = false;

        /// <summary>
        /// CustomDimension
        /// </summary>
        public Dictionary<string, Func<string>> CustomDimensions { get; set; }

    }

    /// <summary>
    /// NetworkMetricsOptions
    /// </summary>
    public class NetworkMetricsOptions
    {
        /// <summary>
        /// IsRoutingIdIncluded
        /// </summary>
        public bool IsRoutingIdIncluded { get; set; } = false;

        /// <summary>
        /// CustomDimension
        /// </summary>
        public Dictionary<string, Func<string>> CustomDimensions { get; set; }
    }
}
