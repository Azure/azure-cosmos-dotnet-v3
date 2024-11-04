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
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
            if (container.Database.Client.ClientOptions.UseSystemTextJsonSerializerWithOptions is not null)
            {
                return new EncryptionContainerStream(container, encryptor);
            }
#endif

            return new EncryptionContainer(
                container,
                encryptor);
        }

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        /// <summary>
        /// Get container with <see cref="Encryptor"/> for performing operations using client-side encryption.
        /// </summary>
        /// <param name="container">Regular cosmos container.</param>
        /// <param name="encryptor">Provider that allows encrypting and decrypting data.</param>
        /// <param name="jsonProcessor">Json Processor used for the container.</param>
        /// <returns>Container to perform operations supporting client-side encryption / decryption.</returns>
        public static Container WithEncryptor(
            this Container container,
            Encryptor encryptor,
            JsonProcessor jsonProcessor)
        {
            return jsonProcessor switch
            {
                JsonProcessor.Stream => new EncryptionContainerStream(container, encryptor),
                JsonProcessor.Newtonsoft => new EncryptionContainer(container, encryptor),
                _ => throw new NotSupportedException($"Json Processor {jsonProcessor} is not supported.")
            };
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
            return container switch
            {
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
                EncryptionContainerStream encryptionContainerStream => new EncryptionFeedIteratorStream<T>(
                    (EncryptionFeedIteratorStream)encryptionContainerStream.ToEncryptionStreamIterator(query),
                    encryptionContainerStream.ResponseFactory),
#endif
                EncryptionContainer encryptionContainer => new EncryptionFeedIterator<T>(
                    (EncryptionFeedIterator)encryptionContainer.ToEncryptionStreamIterator(query),
                    encryptionContainer.ResponseFactory),

                _ => throw new ArgumentOutOfRangeException(nameof(container), $"Container type {container.GetType().Name} is not supported.")

            };
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
            return container switch
            {
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
                EncryptionContainerStream encryptionContainerStream => new EncryptionFeedIteratorStream(
                    query.ToStreamIterator(),
                    encryptionContainerStream.Encryptor,
                    encryptionContainerStream.CosmosSerializer,
                    new MemoryStreamManager()),
#endif
                EncryptionContainer encryptionContainer => new EncryptionFeedIterator(
                    query.ToStreamIterator(),
                    encryptionContainer.Encryptor,
                    encryptionContainer.CosmosSerializer),
                _ => throw new ArgumentOutOfRangeException(nameof(container), $"Container type {container.GetType().Name} is not supported.")
            };
        }
    }
}
