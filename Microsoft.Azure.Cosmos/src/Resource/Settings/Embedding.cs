//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents the embedding settings for the vector index.
    /// </summary>
    public class Embedding : IEquatable<Embedding>
    {
        /// <summary>
        /// Gets or sets a string containing the path of the vector index.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Path)]
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Cosmos.VectorDataType"/> representing the corresponding vector data type.
        /// </summary>
        [JsonProperty(PropertyName = "dataType")]
        [JsonConverter(typeof(StringEnumConverter))]
        public VectorDataType DataType { get; set; }

        /// <summary>
        /// Gets or sets an integer representing the dimensions of a vector.
        /// </summary>
        [JsonProperty(PropertyName = "dimensions")]
        public int Dimensions { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Cosmos.DistanceFunction"/> which is used to calculate the respective distance between the vectors. 
        /// </summary>
        [JsonProperty(PropertyName = "distanceFunction")]
        [JsonConverter(typeof(StringEnumConverter))]
        public DistanceFunction DistanceFunction { get; set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }

        /// <summary>
        /// Ensures that the paths specified in the vector embedding policy are valid.
        /// </summary>
        public void ValidateEmbeddingPath()
        {
            if (string.IsNullOrEmpty(this.Path))
            {
                throw new ArgumentException("Argument {0} can't be null or empty.", nameof(this.Path));
            }

            if (this.Path[0] != '/')
            {
                throw new ArgumentException("The argument {0} is not a valid path.", this.Path);
            }
        }

        /// <inheritdoc/>
        public bool Equals(Embedding that)
        {
            return this.Path.Equals(that.Path)
                && this.DataType.Equals(that.DataType)
                && this.Dimensions == that.Dimensions
                && this.Dimensions.Equals(that.Dimensions);
        }
    }
}
