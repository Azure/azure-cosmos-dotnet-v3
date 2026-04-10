//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary> 
    /// Represents a computed property definition in a Cosmos DB collection.
    /// </summary>
    public sealed class ComputedProperty
    {
        /// <summary>
        /// Gets or sets the name of the computed property.
        /// </summary>
        /// <value>
        /// The name of the computed property.
        /// </value>
        /// <remarks>
        /// Name of the computed property should be chosen such that it does not collide with any existing or future document properties.
        /// </remarks>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the query for the computed property.
        /// </summary>
        /// <value>
        /// The query used to evaluate the value for the computed property.
        /// </value>
        /// <remarks>
        /// For example:
        /// SELECT VALUE LOWER(c.firstName) FROM c
        /// </remarks>
        [JsonProperty(PropertyName = "query")]
        public string Query { get; set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}
