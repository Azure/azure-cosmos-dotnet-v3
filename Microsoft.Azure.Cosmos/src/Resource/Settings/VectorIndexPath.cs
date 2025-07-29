//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
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
    ///             "type": "quantizedFlat",
    ///             "quantizationByteSize": 3,
    ///             "vectorIndexShardKey": ["/ZipCode"]
    ///         },
    ///         {
    ///             "path": "/embeddings/vector",
    ///             "type": "DiskANN",
    ///             "quantizationByteSize": 2,
    ///             "indexingSearchListSize": 100,
    ///             "vectorIndexShardKey": ["/Country"]
    ///         }
    ///     ]
    /// }
    /// ]]>
    /// </example>
    public sealed class VectorIndexPath
    {
        [System.Text.Json.Serialization.JsonPropertyName("indexingSearchListSize")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? indexingSearchListSizeInternal { get; private set; }

        [System.Text.Json.Serialization.JsonPropertyName("quantizationByteSize")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? quantizationByteSizeInternal { get; private set; }

        /// <summary>
        /// Gets or sets the full path in a document used for vector indexing.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName(Constants.Properties.Path)]
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="VectorIndexType"/> for the vector index path.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("type")]
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<VectorIndexType>))]
        public VectorIndexType Type { get; set; }

        /// <summary>
        /// Gets or sets the quantization byte size for the vector index path. This is only applicable for the quantizedFlat and diskann vector index types.
        /// The allowed range for this parameter is between 1 and the minimum of vector dimensions and 512.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
#if PREVIEW
        public
#else
        public
#endif
        int QuantizationByteSize
        {
            get => this.quantizationByteSizeInternal == null ? 0 : this.quantizationByteSizeInternal.Value;
            set => this.quantizationByteSizeInternal = value;
        }

        /// <summary>
        /// Gets or sets the indexing search list size for the vector index path. This is only applicable for the diskann vector index type.
        /// The allowed range for this parameter is between 25 and 500.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
#if PREVIEW
        public
#else
        internal
#endif
        int IndexingSearchListSize
        {
            get => this.indexingSearchListSizeInternal == null ? 0 : this.indexingSearchListSizeInternal.Value;
            set => this.indexingSearchListSizeInternal = value;
        }

        /// <summary>
        /// Gets or sets the vector index shard key for the vector index path. This is only applicable for the quantizedFlat and diskann vector index types.
        /// The maximum length of the vector index shard key is 1.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("vectorIndexShardKey")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[] VectorIndexShardKey { get; set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [System.Text.Json.Serialization.JsonExtensionData]
        public IDictionary<string, JsonElement> AdditionalProperties { get; private set; }
    }
}