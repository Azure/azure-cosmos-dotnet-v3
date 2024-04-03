//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// DOM for a vector index path. A vector index path is used in a vector index.
    /// </summary>
    /// <example>
    /// <![CDATA[
    /// "indexingPolicy": {
    ///     "includedPaths": [
    ///         {
    ///             "path": "/*"
    ///         }
    ///     ],
    ///     "excludedPaths": [
    ///         {
    ///             "path": "/embeddings/vector/*"
    ///         }
    ///     ],
    ///     "vectorIndexes": [
    ///         {
    ///             "path": "/vector1",
    ///             "type": "flat"
    ///         },
    ///         {
    ///             "path": "/vector2",
    ///             "type": "flat"
    ///         },
    ///         {
    ///             "path": "/embeddings/vector",
    ///             "type": "flat"
    ///         }
    ///     ]
    /// }
    /// ]]>
    /// </example>
    internal sealed class VectorIndexPath
    {
        /// <summary>
        /// Gets or sets the full path in a document used for vector indexing.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Path)]
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="VectorIndexType"/> for the vector index path.
        /// </summary>
        [JsonProperty(PropertyName = "type")]
        [JsonConverter(typeof(StringEnumConverter))]
        public VectorIndexType Type { get; set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}