//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents the vector embedding policy configuration for specifying the vector embeddings on documents in the collection in the Azure Cosmos DB service.
    /// </summary>
    /// <seealso cref="ContainerProperties"/>
    public sealed class VectorEmbeddingPolicy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VectorEmbeddingPolicy"/> class.
        /// </summary>
        /// <param name="embeddings">List of embeddings to include in the policy definition.</param>        
        public VectorEmbeddingPolicy(Collection<Embedding> embeddings)
        {
            VectorEmbeddingPolicy.ValidateEmbeddings(embeddings);
            this.Embeddings = embeddings;
        }

        /// <summary>
        /// Gets a collection of <see cref="Embedding"/> that contains the vector embeddings of documents in collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = "vectorEmbeddings")]
        public readonly Collection<Embedding> Embeddings;

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
                item.ValidateEmbeddingPath();
            }
        }
    }
}
