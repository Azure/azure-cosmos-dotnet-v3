//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a contract for generating float32 vector embeddings from input text strings
    /// supplied by the Azure Cosmos DB query pipeline.
    /// The SDK invokes this when a query plan contains <c>GenerateEmbeddings(...)</c> literals
    /// (for example <c>VectorDistance(GenerateEmbeddings("big brown cat"), c.embedding)</c>).
    /// Set a client-wide default via <c>CosmosClientOptions.EmbeddingGenerator</c> or
    /// <c>CosmosClientBuilder.WithEmbeddingGenerator</c>. Implementations MUST be thread-safe and are
    /// responsible for any caching, retries, and authentication required to call the underlying
    /// embedding service.
    /// </summary>
    /// <remarks>
    /// <para><b>Preview surface.</b> The SDK call site that invokes this method is delivered
    /// in a follow-up release. Setting an instance via
    /// <see cref="CosmosClientOptions.EmbeddingGenerator"/> or
    /// <see cref="Fluent.CosmosClientBuilder.WithEmbeddingGenerator"/> has no runtime effect
    /// today; the surface is shipped in this preview so customers can author and test
    /// implementations against the contract ahead of the resolver landing.</para>
    /// <para><b>Lifecycle and disposal.</b> The customer owns the generator instance. The SDK
    /// keeps a reference for the lifetime of the configured <see cref="CosmosClient"/> (or the
    /// <see cref="Container"/> reference it was bound to) but never disposes it. If the
    /// implementation holds disposable resources (for example an <c>HttpClient</c> or an
    /// <c>EmbeddingClient</c>), the customer is responsible for disposing them when their
    /// application tears down.</para>
    ///
    /// <para><b>Error semantics.</b> Implementations are responsible for handling transient
    /// failures from the underlying embedding service (network errors, rate limiting, etc.)
    /// via their own retry policy. The SDK does not retry calls to this method. Any exception
    /// thrown by the implementation is wrapped into a <see cref="CosmosException"/> and
    /// surfaced to the originating SDK caller.</para>
    ///
    /// <para><b>Cancellation.</b> Implementations should honor the supplied
    /// <see cref="CancellationToken"/> cooperatively wherever feasible (typically by forwarding
    /// it to the underlying HTTP call). Best-effort cancellation is acceptable; ignoring the
    /// token entirely is discouraged because it defeats caller-side timeouts.</para>
    ///
    /// <para><b>Idempotency and concurrency.</b> The SDK may invoke this method multiple times
    /// for the same inputs (for example during internal query retry) and may invoke it
    /// concurrently from multiple threads. Implementations must be safe to call repeatedly
    /// and from parallel callers, and must not assume per-call state. Note that each call
    /// typically incurs cost at the underlying embedding service; implementations may cache
    /// responses internally if they want to avoid duplicate billing for identical inputs.</para>
    /// </remarks>
#if PREVIEW
    public
#else
    internal
#endif
    interface ICosmosEmbeddingGenerator
    {
        /// <summary>
        /// Generates an embedding vector for each of the supplied input strings.
        /// </summary>
        /// <param name="texts">
        /// The input strings to embed, in the order the implementation MUST preserve in the
        /// returned <see cref="CosmosEmbeddingResult.Vectors"/> (one vector per input, same
        /// index). Typed as <see cref="IReadOnlyList{T}"/> so implementations can size their
        /// outbound batch without re-enumeration and so the 1:1 ordered contract is encoded
        /// in the signature.
        /// </param>
        /// <param name="endpoint">
        /// The embedding service endpoint to call (for example the Azure OpenAI account endpoint).
        /// Sourced from the container's <c>EmbeddingSource.Endpoint</c> when configured.
        /// </param>
        /// <param name="deploymentName">
        /// The model deployment name to invoke at <paramref name="endpoint"/>. Sourced from the
        /// container's <c>EmbeddingSource.DeploymentName</c> when configured.
        /// </param>
        /// <param name="dimensions">
        /// The vector dimensionality the produced embeddings must match. For models that support
        /// dimensionality reduction (for example <c>text-embedding-3-small</c> /
        /// <c>text-embedding-3-large</c>), implementations MUST forward this value to the
        /// underlying service so the returned vectors have the expected length; otherwise the
        /// service returns its default size, which may not match the container's
        /// <see cref="VectorEmbeddingPolicy"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> propagated from the originating SDK call
        /// (for example <c>FeedIterator.ReadNextAsync</c>). Implementations should honor cancellation.
        /// </param>
        /// <returns>
        /// A task that resolves to a <see cref="CosmosEmbeddingResult"/> whose
        /// <see cref="CosmosEmbeddingResult.Vectors"/> contains one float32 vector per input,
        /// each of length <paramref name="dimensions"/>, in the same order as
        /// <paramref name="texts"/>.
        /// <para>
        /// Query-time vectors are sent to the Azure Cosmos DB gateway as float32 regardless of
        /// the container's stored <see cref="VectorDataType"/>. Implementations targeting
        /// containers configured for <see cref="VectorDataType.Uint8"/>,
        /// <see cref="VectorDataType.Int8"/>, or <see cref="VectorDataType.Float16"/> storage
        /// should still produce float32 vectors here; the Azure Cosmos DB service applies the
        /// configured quantization at write time. This contract
        /// covers all four <see cref="VectorDataType"/> storage configurations supported by
        /// the container's <see cref="VectorEmbeddingPolicy"/>.
        /// </para>
        /// </returns>
        Task<CosmosEmbeddingResult> GenerateEmbeddingsAsync(
            IReadOnlyList<string> texts,
            string endpoint,
            string deploymentName,
            int dimensions,
            CancellationToken cancellationToken = default);
    }
}
