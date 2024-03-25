//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents the unique key policy configuration for specifying uniqueness constraints on documents in the collection in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// Unique key policies add a layer of data integrity to an Azure Cosmos container. They cannot be modified once the container is created.
    /// <para>
    /// Refer to <see>https://docs.microsoft.com/en-us/azure/cosmos-db/unique-keys</see> for additional information on how to specify
    /// unique key policies.
    /// </para>
    /// </remarks>
    /// <seealso cref="ContainerProperties"/>
    public sealed class VectorEmbeddingPolicy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VectorEmbeddingPolicy"/> class.
        /// The <see cref="PolicyFormatVersion"/> will be set to 1.
        /// Note: If you need to include partition key or id field paths as part of <see cref="ClientEncryptionPolicy"/>, please set <see cref="PolicyFormatVersion"/> to 2.
        /// </summary>
        /// <param name="includedPaths">List of paths to include in the policy definition.</param>        
        public VectorEmbeddingPolicy(IEnumerable<Embedding> embeddings)
        {
            VectorEmbeddingPolicy.ValidateEmbeddings(embeddings);
            this.Embeddings = embeddings;
        }

        /// <summary>
        /// Gets collection of <see cref="UniqueKey"/> that guarantee uniqueness of documents in collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.VectorEmbeddings)]
        private readonly IEnumerable<Embedding> Embeddings;

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }

        private static void ValidateEmbeddings(
            IEnumerable<Embedding> embeddings)
        {
            foreach (Embedding item in embeddings)
            {
                VectorEmbeddingPolicy.ValidateEmbeddingPath(item.Path);
                VectorEmbeddingPolicy.ValidateEmbeddingDimensions(item.Dimensions);
            }
        }

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

        private static void ValidateEmbeddingDimensions(
            long dimensions)
        {
            if (dimensions == null || dimensions < 1)
            {
                throw new ArgumentException("Argument {0} is not a valid value.", nameof(dimensions));
            }
        }
    }
}
