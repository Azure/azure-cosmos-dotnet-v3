//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents the definition and service-managed state of a materialized view container in the Azure Cosmos DB service.
    /// </summary>
    public sealed class MaterializedViewDefinition
    {
        [JsonConstructor]
        internal MaterializedViewDefinition()
        {
        }

        /// <summary>
        /// Gets the resource identifier of the source container.
        /// </summary>
        /// <value>The source container resource identifier.</value>
        [JsonProperty(PropertyName = "sourceCollectionRid", NullValueHandling = NullValueHandling.Ignore)]
        public string SourceContainerResourceId { get; internal set; }

        /// <summary>
        /// Gets the identifier of the source container.
        /// </summary>
        /// <value>The source container identifier, or <see langword="null"/> when it is not returned.</value>
        [JsonProperty(PropertyName = "sourceCollectionId", NullValueHandling = NullValueHandling.Ignore)]
        public string SourceContainerId { get; internal set; }

        /// <summary>
        /// Gets the materialized view definition.
        /// </summary>
        /// <value>The service-independent materialized view definition.</value>
        [JsonProperty(PropertyName = "definition", NullValueHandling = NullValueHandling.Ignore)]
        public string Definition { get; internal set; }

        /// <summary>
        /// Gets the API-specific materialized view definition.
        /// </summary>
        /// <value>The API-specific definition, or <see langword="null"/> when it is not returned.</value>
        [JsonProperty(PropertyName = "apiSpecificDefinition", NullValueHandling = NullValueHandling.Ignore)]
        public string ApiSpecificDefinition { get; internal set; }

        /// <summary>
        /// Gets the service-defined container type of the materialized view.
        /// </summary>
        /// <value>The service-defined container type, or <see langword="null"/> when it is not returned.</value>
        [JsonProperty(PropertyName = "containerType", NullValueHandling = NullValueHandling.Ignore)]
        public string ContainerType { get; internal set; }

        /// <summary>
        /// Gets the service-managed status of the materialized view.
        /// </summary>
        /// <value>The service-managed status, or <see langword="null"/> when it is not returned.</value>
        [JsonProperty(PropertyName = "status", NullValueHandling = NullValueHandling.Ignore)]
        public string Status { get; internal set; }

        /// <summary>
        /// Gets the throughput bucket used by the Azure Cosmos DB service to build this materialized view.
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
