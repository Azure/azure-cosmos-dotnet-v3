//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents the definition and service-managed state of a materialized view container.
    /// </summary>
    internal sealed class MaterializedViewDefinition
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MaterializedViewDefinition"/> class.
        /// </summary>
        public MaterializedViewDefinition()
        {
        }

        /// <summary>
        /// Gets or sets the resource identifier of the source container.
        /// </summary>
        [JsonProperty(PropertyName = "sourceCollectionRid", NullValueHandling = NullValueHandling.Ignore)]
        public string SourceContainerResourceId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the source container.
        /// </summary>
        [JsonProperty(PropertyName = "sourceCollectionId", NullValueHandling = NullValueHandling.Ignore)]
        public string SourceContainerId { get; set; }

        /// <summary>
        /// Gets or sets the query that defines the materialized view.
        /// </summary>
        [JsonProperty(PropertyName = "definition", NullValueHandling = NullValueHandling.Ignore)]
        public string Definition { get; set; }

        /// <summary>
        /// Gets or sets the optional API-specific materialized view definition.
        /// </summary>
        [JsonProperty(PropertyName = "apiSpecificDefinition", NullValueHandling = NullValueHandling.Ignore)]
        public string ApiSpecificDefinition { get; set; }

        /// <summary>
        /// Gets or sets the optional service-defined container type.
        /// </summary>
        [JsonProperty(PropertyName = "containerType", NullValueHandling = NullValueHandling.Ignore)]
        public string ContainerType { get; set; }

        /// <summary>
        /// Gets or sets the optional service-managed materialized view status.
        /// </summary>
        [JsonProperty(PropertyName = "status", NullValueHandling = NullValueHandling.Ignore)]
        public string Status { get; set; }

        /// <summary>
        /// Gets additional values returned by the service that are not modeled by this SDK.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}
