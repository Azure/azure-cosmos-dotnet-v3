//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents metadata for a materialized view associated with a source container in the Azure Cosmos DB service.
    /// </summary>
    public sealed class MaterializedViewProperties
    {
        [JsonConstructor]
        internal MaterializedViewProperties()
        {
        }

        /// <summary>
        /// Gets the identifier of the materialized view container.
        /// </summary>
        /// <value>The identifier of the materialized view container.</value>
        [JsonProperty(PropertyName = "id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; internal set; }

        /// <summary>
        /// Gets the resource identifier of the materialized view container.
        /// </summary>
        /// <value>The resource identifier assigned by the Azure Cosmos DB service.</value>
        [JsonProperty(PropertyName = "_rid", NullValueHandling = NullValueHandling.Ignore)]
        public string ResourceId { get; internal set; }

        /// <summary>
        /// Gets the service-defined container type of the materialized view.
        /// </summary>
        /// <value>The service-defined container type, or <see langword="null"/> when it is not returned.</value>
        [JsonProperty(PropertyName = "containerType", NullValueHandling = NullValueHandling.Ignore)]
        public string ContainerType { get; internal set; }

        /// <summary>
        /// Gets the item paths that the service requires in the previous image when maintaining the materialized view.
        /// </summary>
        /// <value>The required previous-image paths, or <see langword="null"/> when they are not returned.</value>
        [JsonProperty(PropertyName = "requiredPathsInPreviousImage", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyList<string> RequiredPathsInPreviousImage { get; internal set; }

        /// <summary>
        /// Contains additional values returned by the service that are not yet modeled by this SDK.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}
