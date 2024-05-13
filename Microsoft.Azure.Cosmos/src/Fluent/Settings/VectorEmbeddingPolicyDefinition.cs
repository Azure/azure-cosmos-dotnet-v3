//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    /// <summary>
    /// <see cref="VectorEmbeddingPolicy"/> fluent definition.
    /// </summary>
    public class VectorEmbeddingPolicyDefinition
    {
        private readonly ContainerBuilder parent;
        private readonly Action<VectorEmbeddingPolicy> attachCallback;
        private readonly Collection<Embedding> vectorEmbeddings;

        /// <summary>
        /// Initializes a new instance of the <see cref="VectorEmbeddingPolicyDefinition"/> class.
        /// </summary>
        /// <param name="parent">The original instance of <see cref="ContainerBuilder"/>.</param>
        /// <param name="embeddings">List of embeddings to include in the policy definition.</param>
        /// <param name="attachCallback">A callback delegate to be used at a later point of time.</param>
        public VectorEmbeddingPolicyDefinition(
            ContainerBuilder parent,
            Collection<Embedding> embeddings,
            Action<VectorEmbeddingPolicy> attachCallback)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.attachCallback = attachCallback ?? throw new ArgumentNullException(nameof(attachCallback));
            this.vectorEmbeddings = embeddings;
        }

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        /// <returns>An instance of the parent.</returns>
        public ContainerBuilder Attach()
        {
            VectorEmbeddingPolicy embeddingPolicy = new (this.vectorEmbeddings);

            this.attachCallback(embeddingPolicy);
            return this.parent;
        }
    }
}
