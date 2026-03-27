//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

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
    ///         },
    ///         {
    ///             "path": "/vector3",
    ///             "type": "quantizedFlat",
    ///             "quantizerType": "product",
    ///             "quantizationByteSize": 20,
    ///             "vectorIndexShardKey": ["/Country"]
    ///         },
    ///         {
    ///             "path": "/vector3",
    ///             "type": "diskann",
    ///             "quantizerType": "product",
    ///             "quantizationByteSize": 32,
    ///             "indexingSearchListSize": 100,
    ///             "vectorIndexShardKey": ["/Country", "ZipCode"]
    ///         }
    ///     ]
    /// }
    /// ]]>
    /// </example>
    internal sealed class VectorIndexPath : JsonSerializable, ICloneable
    {

        /// <summary>
        /// Gets or sets the full path in a document used for vector indexing.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Path)]
        public string Path
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.Path);
            }
            set
            {
                base.SetValue(Constants.Properties.Path, value);
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="VectorIndexType"/> for the vector index path.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Type)]
        public VectorIndexType Type
        {
            get
            {
                VectorIndexType result = default(VectorIndexType);
                string strValue = base.GetValue<string>(Constants.Properties.Type);
                if (!string.IsNullOrEmpty(strValue))
                {
                    result = (VectorIndexType)Enum.Parse(typeof(VectorIndexType), strValue, true);
                }
                return result;
            }
            set
            {
                base.SetValue(Constants.Properties.Type, value);
            }
        }

        /// <summary>
        /// Gets or sets the quantization byte size for the vector index path.
        /// <!-- This is only applicable for the quantizedFlat and diskann vector index types. -->
        /// <!-- This is an optional parameter. -->
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.QuantizationByteSize, NullValueHandling = NullValueHandling.Ignore)]
        public int? QuantizationByteSize
        {
            get
            {
                return base.GetValue<int?>(Constants.Properties.QuantizationByteSize);
            }
            set
            {
                base.SetValue(Constants.Properties.QuantizationByteSize, value);
            }
        }

        /// <summary>
        /// Gets or sets the quantizer type for the vector index path.
        /// <!-- This is only applicable for the quantizedFlat and diskann vector index types. -->
        /// <!-- This is an optional parameter. -->
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.QuantizerType, NullValueHandling = NullValueHandling.Ignore)]
        public QuantizerType? QuantType
        {
            get
            {
                return base.GetValue<QuantizerType?>(Constants.Properties.QuantizerType);
            }
            set
            {
                base.SetValue(Constants.Properties.QuantizerType, value);
            }
        }

        /// <summary>
        /// Gets or sets the indexing search list size for the vector index path.
        /// <!-- This is only applicable for the diskann vector index type. -->
        /// <!-- This is an optional parameter. -->
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.IndexingSearchListSize, NullValueHandling = NullValueHandling.Ignore)]
        public int? IndexingSearchListSize
        {
            get
            {
                return base.GetValue<int?>(Constants.Properties.IndexingSearchListSize);
            }
            set
            {
                base.SetValue(Constants.Properties.IndexingSearchListSize, value);
            }
        }

        /// <summary>
        /// Gets or sets the vector index shard key for the vector index path.
        /// <!-- This is only applicable for the quantizedFlat and diskann vector index types. -->
        /// <!-- This is an optional parameter. -->
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.VectorIndexShardKey, NullValueHandling = NullValueHandling.Ignore)]
        public string[] VectorIndexShardKey
        {
            get
            {
                return base.GetValue<string[]>(Constants.Properties.VectorIndexShardKey);
            }
            set
            {
                base.SetValue(Constants.Properties.VectorIndexShardKey, value);
            }
        }

        public object Clone()
        {
            VectorIndexPath clonedVectorIndexPath = new VectorIndexPath();
            clonedVectorIndexPath.Path = this.Path;
            clonedVectorIndexPath.Type = this.Type;
            clonedVectorIndexPath.QuantizationByteSize = this.QuantizationByteSize;
            clonedVectorIndexPath.QuantType = this.QuantType;
            clonedVectorIndexPath.IndexingSearchListSize = this.IndexingSearchListSize;
            clonedVectorIndexPath.VectorIndexShardKey = this.VectorIndexShardKey;
            return clonedVectorIndexPath;
        }

        internal override void OnSave()
        {
            base.SetValue(Constants.Properties.Path, this.Path);
            base.SetValue(Constants.Properties.Type, this.Type);
            base.SetValue(Constants.Properties.QuantizationByteSize, this.QuantizationByteSize);
            base.SetValue(Constants.Properties.QuantizerType, this.QuantType);
            base.SetValue(Constants.Properties.IndexingSearchListSize, this.IndexingSearchListSize);
            base.SetValue(Constants.Properties.VectorIndexShardKey, this.VectorIndexShardKey);
        }
    }
}