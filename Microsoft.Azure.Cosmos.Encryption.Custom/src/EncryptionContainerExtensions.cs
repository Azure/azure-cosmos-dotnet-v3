//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Linq;

    /// <summary>
    /// This class provides extension methods for <see cref="EncryptionContainer"/>.
    /// </summary>
    public static class EncryptionContainerExtensions
    {
        /// <summary>
        /// Get container with <see cref="Encryptor"/> for performing operations using client-side encryption.
        /// </summary>
        /// <param name="container">Regular cosmos container.</param>
        /// <param name="encryptor">Provider that allows encrypting and decrypting data.</param>
        /// <returns>Container to perform operations supporting client-side encryption / decryption.</returns>
        public static Container WithEncryptor(
            this Container container,
            Encryptor encryptor)
        {
            return new EncryptionContainer(
                container,
                encryptor);
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// Configures the specified <see cref="Container"/> to use streaming JSON processing by default.
        /// </summary>
        /// <param name="container">The <see cref="Container"/> instance to configure. Must be an <see cref="EncryptionContainer"/>.</param>
        /// <returns>The configured <see cref="EncryptionContainer"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="container"/> is not an <see cref="EncryptionContainer"/>.</exception>
        /// <remarks>
        /// <para>
        /// Streaming JSON processing uses pooled <c>ArrayPool&lt;byte&gt;</c> buffers to reduce allocations on the
        /// decrypt path. When the result type is <see cref="DecryptableItem"/>, individual items are decrypted
        /// lazily inside <see cref="DecryptableItem.GetItemAsync{T}"/> and hold a rented buffer until the item is
        /// disposed.
        /// </para>
        /// <para>
        /// <strong>Per-call opt-in.</strong> This method sets the streaming processor as the container-wide default.
        /// To opt in (or out) on an individual feed call instead, use the strongly-typed
        /// <c>WithEncryptionJsonProcessor</c> extension on that call's request options:
        /// <code language="c#">
        /// <![CDATA[
        /// QueryRequestOptions requestOptions = new QueryRequestOptions()
        ///     .WithEncryptionJsonProcessor(JsonProcessor.Stream);
        /// ]]>
        /// </code>
        /// The per-call selection takes precedence over the container default and works with
        /// <c>QueryRequestOptions</c>, <c>ChangeFeedRequestOptions</c>, and <c>ReadManyRequestOptions</c>. For
        /// LINQ-sourced iterators, use the <c>ToEncryptionFeedIterator</c> / <c>ToEncryptionStreamIterator</c>
        /// overloads that accept a <see cref="JsonProcessor"/>.
        /// </para>
        /// <para>
        /// <strong>Disposal contract for <c>FeedResponse&lt;DecryptableItem&gt;</c>.</strong> The <c>FeedResponse&lt;T&gt;</c>
        /// returned from <c>FeedIterator&lt;DecryptableItem&gt;.ReadNextAsync</c> implements <see cref="IAsyncDisposable"/>
        /// at runtime, but the compile-time return type does not advertise it. Callers SHOULD cast each page to
        /// <see cref="IAsyncDisposable"/> and dispose it (typically in a <c>finally</c> block) so that any items the
        /// caller skipped, did not enumerate, or did not call <c>GetItemAsync</c> on promptly return their pooled
        /// buffers to the pool. Disposal is the prompt path; if it is missed, a finalizer on the underlying pooled
        /// stream still returns and zeroes the buffer when the page is garbage-collected, so a missed dispose degrades
        /// to a delayed cleanup rather than a permanent pool leak or lingering plaintext. See the example on
        /// <see cref="DecryptableItem"/> for the recommended pattern.
        /// </para>
        /// </remarks>
        public static Container UseStreamingJsonProcessingByDefault(this Container container)
        {
            if (container is not EncryptionContainer encryptionContainer)
            {
                throw new ArgumentException(
                    $"{nameof(UseStreamingJsonProcessingByDefault)} is only supported with {nameof(EncryptionContainer)}.",
                    nameof(container));
            }

            encryptionContainer.UseStreamingJsonProcessingByDefault();

            return encryptionContainer;
        }
#endif

        /// <summary>
        /// This method gets the FeedIterator from LINQ IQueryable to execute query asynchronously.
        /// This will create the fresh new FeedIterator when called which will support decryption.
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="container">the encryption container.</param>
        /// <param name="query">the IQueryable{T} to be converted.</param>
        /// <returns>An iterator to go through the items.</returns>
        /// <remarks>
        /// On .NET 8+, this overload uses the container's default JSON processor (see
        /// <c>UseStreamingJsonProcessingByDefault</c>). To choose the processor per call on the LINQ path,
        /// use the <c>ToEncryptionFeedIterator(container, query, JsonProcessor)</c> overload that accepts a
        /// <c>JsonProcessor</c>.
        /// </remarks>
        /// <example>
        /// This example shows how to get FeedIterator from LINQ.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// IOrderedQueryable<ToDoActivity> linqQueryable = this.container.GetItemLinqQueryable<ToDoActivity>();
        /// FeedIterator setIterator = this.container.ToEncryptionFeedIterator<ToDoActivity>(linqQueryable);
        /// ]]>
        /// </code>
        /// </example>
        public static FeedIterator<T> ToEncryptionFeedIterator<T>(
            this Container container,
            IQueryable<T> query)
        {
            if (container is not EncryptionContainer encryptionContainer)
            {
                throw new ArgumentOutOfRangeException(nameof(query), $"{nameof(ToEncryptionFeedIterator)} is only supported with {nameof(EncryptionContainer)}.");
            }

            return new EncryptionFeedIterator<T>(
                (EncryptionFeedIterator)encryptionContainer.ToEncryptionStreamIterator(query),
                encryptionContainer.ResponseFactory,
                encryptionContainer.Encryptor,
                encryptionContainer.CosmosSerializer,
                encryptionContainer.DefaultJsonProcessor);
        }

        /// <summary>
        /// This method gets the FeedIterator from LINQ IQueryable to execute query asynchronously.
        /// This will create the fresh new FeedIterator when called which will support decryption.
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="container">the encryption container.</param>
        /// <param name="query">the IQueryable{T} to be converted.</param>
        /// <returns>An iterator to go through the items.</returns>
        /// <remarks>
        /// On .NET 8+, this overload uses the container's default JSON processor (see
        /// <c>UseStreamingJsonProcessingByDefault</c>). To choose the processor per call on the LINQ path,
        /// use the <c>ToEncryptionStreamIterator(container, query, JsonProcessor)</c> overload that accepts a
        /// <c>JsonProcessor</c>.
        /// </remarks>
        /// <example>
        /// This example shows how to get FeedIterator from LINQ.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// IOrderedQueryable<ToDoActivity> linqQueryable = this.container.GetItemLinqQueryable<ToDoActivity>();
        /// FeedIterator setIterator = this.container.ToEncryptionStreamIterator<ToDoActivity>(linqQueryable);
        /// ]]>
        /// </code>
        /// </example>
        public static FeedIterator ToEncryptionStreamIterator<T>(
            this Container container,
            IQueryable<T> query)
        {
            if (container is not EncryptionContainer encryptionContainer)
            {
                throw new ArgumentOutOfRangeException(nameof(query), $"{nameof(ToEncryptionStreamIterator)} is only supported with {nameof(EncryptionContainer)}.");
            }

            return new EncryptionFeedIterator(
                query.ToStreamIterator(),
                encryptionContainer.Encryptor,
                encryptionContainer.DefaultJsonProcessor);
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// Gets a typed FeedIterator from a LINQ IQueryable, decrypting results using the specified
        /// <see cref="JsonProcessor"/> for this call (overriding the container default).
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="container">the encryption container.</param>
        /// <param name="query">the IQueryable{T} to be converted.</param>
        /// <param name="jsonProcessor">The JSON processor to use when decrypting the results.</param>
        /// <returns>An iterator to go through the items.</returns>
        public static FeedIterator<T> ToEncryptionFeedIterator<T>(
            this Container container,
            IQueryable<T> query,
            JsonProcessor jsonProcessor)
        {
            if (container is not EncryptionContainer encryptionContainer)
            {
                throw new ArgumentOutOfRangeException(nameof(query), $"{nameof(ToEncryptionFeedIterator)} is only supported with {nameof(EncryptionContainer)}.");
            }

            return new EncryptionFeedIterator<T>(
                (EncryptionFeedIterator)encryptionContainer.ToEncryptionStreamIterator(query, jsonProcessor),
                encryptionContainer.ResponseFactory,
                encryptionContainer.Encryptor,
                encryptionContainer.CosmosSerializer,
                jsonProcessor);
        }

        /// <summary>
        /// Gets a stream FeedIterator from a LINQ IQueryable, decrypting results using the specified
        /// <see cref="JsonProcessor"/> for this call (overriding the container default).
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="container">the encryption container.</param>
        /// <param name="query">the IQueryable{T} to be converted.</param>
        /// <param name="jsonProcessor">The JSON processor to use when decrypting the results.</param>
        /// <returns>An iterator to go through the items.</returns>
        public static FeedIterator ToEncryptionStreamIterator<T>(
            this Container container,
            IQueryable<T> query,
            JsonProcessor jsonProcessor)
        {
            if (container is not EncryptionContainer encryptionContainer)
            {
                throw new ArgumentOutOfRangeException(nameof(query), $"{nameof(ToEncryptionStreamIterator)} is only supported with {nameof(EncryptionContainer)}.");
            }

            return new EncryptionFeedIterator(
                query.ToStreamIterator(),
                encryptionContainer.Encryptor,
                jsonProcessor);
        }
#endif
    }
}
