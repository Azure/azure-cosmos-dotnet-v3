//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;

    /// <summary> 
    /// Represents a computed property definition in a Cosmos DB collection.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    sealed class ComputedProperty
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
    }
}
