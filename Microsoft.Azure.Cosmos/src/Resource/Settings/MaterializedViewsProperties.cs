//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents service-managed build properties for the materialized views associated with a source container.
    /// </summary>
    public sealed class MaterializedViewsProperties
    {
        [JsonConstructor]
        internal MaterializedViewsProperties()
        {
        }

        /// <summary>
        /// Gets the throughput bucket used by the Azure Cosmos DB service to build materialized views.
        /// </summary>
        /// <value>The throughput bucket, or <see langword="null"/> when it is not returned.</value>
        [JsonProperty(PropertyName = "throughputBucketForBuild", NullValueHandling = NullValueHandling.Ignore)]
        public int? ThroughputBucketForBuild { get; internal set; }

        /// <summary>
        /// Contains additional values returned by the service that are not yet modeled by this SDK.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}
