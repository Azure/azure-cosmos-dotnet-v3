//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents the embedding settings for the vector index.
    /// </summary>
    internal sealed class Embedding : JsonSerializable
    {
        private EmbeddingSource embeddingSource;
        
        public Embedding() 
        { 
        }

        /// <summary>
        /// Gets or sets a string containing the path of the vector index.
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
        /// Gets or sets the <see cref="Cosmos.VectorDataType"/> representing the corresponding vector data type.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.VectorDataType)]
        public VectorDataType DataType
        {
            get
            {
                VectorDataType result = default(VectorDataType);
                string strValue = base.GetValue<string>(Constants.Properties.VectorDataType);
                if (!string.IsNullOrEmpty(strValue))
                {
                    result = (VectorDataType)Enum.Parse(typeof(VectorDataType), strValue, true);
                }
                return result;
            }
            set
            {
                base.SetValue(Constants.Properties.VectorDataType, value);
            }
        }

        /// <summary>
        /// Gets or sets a long integer representing the dimensions of a vector. 
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.VectorDimensions)]
        public ulong Dimensions {
            get
            {
                return base.GetValue<ulong>(Constants.Properties.VectorDimensions);
            }
            set
            {
                base.SetValue(Constants.Properties.VectorDimensions, value);
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="Cosmos.DistanceFunction"/> which is used to calculate the respective distance between the vectors. 
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.DistanceFunction)]
        [JsonConverter(typeof(StringEnumConverter))]
        public DistanceFunction DistanceFunction
        {
            get
            {
                DistanceFunction result = default(DistanceFunction);
                string strValue = base.GetValue<string>(Constants.Properties.DistanceFunction);
                if (!string.IsNullOrEmpty(strValue))
                {
                    result = (DistanceFunction)Enum.Parse(typeof(DistanceFunction), strValue, true);
                }
                return result;
            }
            set
            {
                base.SetValue(Constants.Properties.DistanceFunction, value);
            }
        }

        /// <summary>
        /// Gets the EmbeddingSource associated with the Embedding.
        /// </summary>
        /// <value>
        /// The EmbeddingSource associated with the VectorEmbeddingPolicy.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.EmbeddingSource, DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        public EmbeddingSource EmbeddingSource
        {
            get
            {
                if (this.embeddingSource == null)
                {
                    this.embeddingSource = base.GetObject<EmbeddingSource>(Constants.Properties.EmbeddingSource);
                }

                return this.embeddingSource;
            }
            set
            {
                this.embeddingSource = value;
                base.SetObject<EmbeddingSource>(Constants.Properties.EmbeddingSource, value);
            }
        }
    }
}
