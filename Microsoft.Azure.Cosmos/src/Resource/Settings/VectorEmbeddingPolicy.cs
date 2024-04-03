//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents the vector embedding policy configuration for specifying the vector embeddings on documents in the collection in the Azure Cosmos DB service.
    /// </summary>
    /// <seealso cref="ContainerProperties"/>
    internal sealed class VectorEmbeddingPolicy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VectorEmbeddingPolicy"/> class.
        /// </summary>
        /// <param name="embeddings">List of embeddings to include in the policy definition.</param>        
        public VectorEmbeddingPolicy(IEnumerable<Embedding> embeddings)
        {
            VectorEmbeddingPolicy.ValidateEmbeddings(embeddings);
            this.Embeddings = embeddings;
        }

        /// <summary>
        /// Gets a collection of <see cref="Embedding"/> that contains the vector embeddings of documents in collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = "vectorEmbeddings")]
        public readonly IEnumerable<Embedding> Embeddings;

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }

        /// <summary>
        /// Ensures that the specified vector embeddings in the policy are valid.
        /// </summary>
        private static void ValidateEmbeddings(
            IEnumerable<Embedding> embeddings)
        {
            foreach (Embedding item in embeddings)
            {
                VectorEmbeddingPolicy.ValidateEmbeddingPath(item.Path);
                VectorEmbeddingPolicy.ValidateEmbeddingDimensions(item.Dimensions);
            }
        }

        /// <summary>
        /// Ensures that the paths specified in the vector embedding policy are valid.
        /// </summary>
        /// <param name="path">A string containing the vector embedding path.</param>
        private static void ValidateEmbeddingPath(
            string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Argument {0} can't be null or empty.", nameof(path));
            }

            if (path[0] != '/' || path.LastIndexOf('/') != 0)
            {
                throw new ArgumentException("The argument {0} is not a valid path.", path);
            }
        }

        /// <summary>
        /// Ensures that the dimensions specified in the vector embedding policy are valid.
        /// </summary>
        /// <param name="dimensions">A long integer containing the vector dimensions.</param>
        private static void ValidateEmbeddingDimensions(
            long dimensions)
        {
            if (dimensions < 1)
            {
                throw new ArgumentException("Argument {0} is not a valid value.", nameof(dimensions));
            }
        }
    }
}
