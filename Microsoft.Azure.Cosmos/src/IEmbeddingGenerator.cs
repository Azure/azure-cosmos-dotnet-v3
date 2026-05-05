//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a contract for generating vector embeddings from one or more
    /// input text strings. The Azure Cosmos DB SDK invokes this generator when a
    /// query plan returned by the gateway includes an embedding parameter map,
    /// for example for hybrid or vector-search queries that contain literal text
    /// to be embedded such as
    /// <c>ORDER BY RANK VectorDistance(c.text, 'big brown cat')</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The SDK collects every input string referenced by the gateway-returned
    /// embedding parameter map and passes them to
    /// <see cref="GenerateEmbeddingsAsync(IEnumerable{string}, CancellationToken)"/>
    /// in a single batched call. The implementation MUST return the resulting
    /// embeddings in the same order as the input strings, with the same count.
    /// The SDK then injects each returned vector as a parameter on the
    /// rewritten query before per-partition execution.
    /// </para>
    /// <para>
    /// Set an instance on
    /// <see cref="QueryRequestOptions.EmbeddingGenerator"/> to opt in. When the
    /// generator is configured, the SDK advertises support for embedding
    /// generation to the gateway so the returned plan contains an embedding
    /// parameter map; when it is <c>null</c>, behavior is unchanged.
    /// </para>
    /// <para>
    /// Implementations are responsible for any caching, retries, and
    /// authentication required to call the underlying embedding service.
    /// Implementations may be invoked concurrently with other SDK operations
    /// and should be thread-safe.
    /// </para>
    /// </remarks>
#if PREVIEW
    public
#else
    internal
#endif
    interface IEmbeddingGenerator
    {
        /// <summary>
        /// Generates an embedding vector for each of the supplied input strings.
        /// </summary>
        /// <param name="text">
        /// The collection of input strings to embed. The implementation MUST
        /// return one vector per input, in the same order.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> propagated from the originating
        /// SDK call (for example <c>FeedIterator.ReadNextAsync</c>).
        /// Implementations should honor cancellation.
        /// </param>
        /// <returns>
        /// A task that resolves to a sequence of embedding vectors with the
        /// same cardinality and ordering as <paramref name="text"/>.
        /// </returns>
        Task<IEnumerable<ReadOnlyMemory<double>>> GenerateEmbeddingsAsync(
            IEnumerable<string> text,
            CancellationToken cancellationToken = default);
    }
}
