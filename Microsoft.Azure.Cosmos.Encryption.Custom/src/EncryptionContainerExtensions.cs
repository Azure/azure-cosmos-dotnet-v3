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
        /// <exception cref="NotSupportedException">Thrown if <paramref name="container"/> is not an <see cref="EncryptionContainer"/>.</exception>
        /// <remarks>
        /// <para>
        /// Streaming JSON processing uses pooled <c>ArrayPool&lt;byte&gt;</c> buffers to reduce allocations on the
        /// decrypt path. When the result type is <see cref="DecryptableItem"/>, individual items are decrypted
        /// lazily inside <see cref="DecryptableItem.GetItemAsync{T}"/> and hold a rented buffer until the item is
        /// disposed.
        /// </para>
        /// <para>
        /// <strong>Disposal contract for <c>FeedResponse&lt;DecryptableItem&gt;</c>.</strong> The <c>FeedResponse&lt;T&gt;</c>
        /// returned from <c>FeedIterator&lt;DecryptableItem&gt;.ReadNextAsync</c> implements <see cref="IAsyncDisposable"/>
        /// at runtime, but the compile-time return type does not advertise it. Callers MUST cast each page to
        /// <see cref="IAsyncDisposable"/> and dispose it (typically in a <c>finally</c> block) so that any items the
        /// caller skipped, did not enumerate, or did not call <c>GetItemAsync</c> on return their pooled buffers to
        /// the pool. Forgetting to dispose leaks <c>ArrayPool</c> rentals and risks leaving plaintext residue in
        /// pooled memory until GC. See the example on <see cref="DecryptableItem"/> for the recommended pattern.
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
    }
}
