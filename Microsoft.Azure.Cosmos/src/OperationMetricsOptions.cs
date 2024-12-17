//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents the configuration options for collecting metrics related to Cosmos DB operations.
    /// </summary>
    public class OperationMetricsOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the region information should be included in the operation metrics.
        /// Default value is <c>false</c>.
        /// </summary>
        public bool ShouldIncludeRegion { get; set; } = false;

        /// <summary>
        /// Gets or sets a collection of custom dimensions to include in the operation metrics.
        /// Each dimension is defined as a key-value pair, where the key is the dimension name and the value is a function that returns the dimension value.
        /// </summary>
        [JsonIgnore]
        public IDictionary<string, Func<string>> CustomDimensions { get; set; }
    }
 }
