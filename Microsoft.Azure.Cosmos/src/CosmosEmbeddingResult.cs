//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The result of a call to <see cref="ICosmosEmbeddingGenerator.GenerateEmbeddingsAsync"/>.
    /// Carries the generated float32 vectors plus optional diagnostic fields (token usage,
    /// latency) the SDK surfaces through <c>CosmosDiagnostics</c>.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    sealed class CosmosEmbeddingResult
    {
        /// <summary>
        /// Initializes a new instance of <see cref="CosmosEmbeddingResult"/>.
        /// </summary>
        /// <param name="vectors">
        /// The generated float32 embedding vectors, one per input string supplied to the
        /// originating <see cref="ICosmosEmbeddingGenerator.GenerateEmbeddingsAsync"/> call,
        /// in the same order as the inputs.
        /// </param>
        /// <param name="totalTokens">
        /// Optional total token count consumed by the embedding service to produce these vectors.
        /// Pass <c>null</c> when the underlying service does not report token usage.
        /// </param>
        /// <param name="latency">
        /// Optional duration the implementation observed for the embedding service call (for
        /// example, the wall-clock time around the underlying HTTP request). Surfaced through
        /// <c>CosmosDiagnostics</c> for query-time observability. Pass <c>null</c> when the
        /// implementation does not measure latency.
        /// </param>
        public CosmosEmbeddingResult(
            IReadOnlyList<ReadOnlyMemory<float>> vectors,
            int? totalTokens = null,
            TimeSpan? latency = null)
        {
            this.Vectors = vectors ?? throw new ArgumentNullException(nameof(vectors));
            this.TotalTokens = totalTokens;
            this.Latency = latency;
        }

        /// <summary>
        /// Gets the generated float32 embedding vectors, one per input string, in the same
        /// order as the inputs supplied to <see cref="ICosmosEmbeddingGenerator.GenerateEmbeddingsAsync"/>.
        /// </summary>
        public IReadOnlyList<ReadOnlyMemory<float>> Vectors { get; }

        /// <summary>
        /// Gets the total number of tokens the embedding service consumed to generate
        /// <see cref="Vectors"/>, or <c>null</c> when the underlying service does not report it.
        /// </summary>
        public int? TotalTokens { get; }

        /// <summary>
        /// Gets the duration the implementation observed for the underlying embedding service
        /// call, or <c>null</c> when the implementation does not measure it. Surfaced through
        /// <c>CosmosDiagnostics</c> for query-time observability.
        /// </summary>
        public TimeSpan? Latency { get; }
    }
}
