//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents metadata for a materialized view associated with a source container.
    /// </summary>
    internal sealed class MaterializedViewProperties
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MaterializedViewProperties"/> class.
        /// </summary>
        public MaterializedViewProperties()
        {
        }

        /// <summary>
        /// Gets or sets the identifier of the materialized view container.
        /// </summary>
        [JsonProperty(PropertyName = "id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the resource identifier of the materialized view container.
        /// </summary>
        [JsonProperty(PropertyName = "_rid", NullValueHandling = NullValueHandling.Ignore)]
        public string ResourceId { get; set; }

        /// <summary>
        /// Gets or sets the optional service-defined container type.
        /// </summary>
        [JsonProperty(PropertyName = "containerType", NullValueHandling = NullValueHandling.Ignore)]
        public string ContainerType { get; set; }

        /// <summary>
        /// Gets or sets the optional item paths required in the previous image.
        /// </summary>
        [JsonProperty(PropertyName = "requiredPathsInPreviousImage", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyList<string> RequiredPathsInPreviousImage { get; set; }

        /// <summary>
        /// Gets additional values returned by the service that are not modeled by this SDK.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}
