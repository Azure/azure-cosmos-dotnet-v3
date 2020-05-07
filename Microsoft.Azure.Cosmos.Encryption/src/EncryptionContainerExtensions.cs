//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Linq;

    /// <summary>
    /// This class provides extension methods for Encryption Container.
    /// </summary>
    public static class EncryptionContainerExtensions
    {
        /// <summary>
        /// Get container for performing operations using client-side encryption.
        /// </summary>
        /// <param name="container">Regular cosmos container.</param>
        /// <param name="encryptor">Provider that allows encrypting and decrypting data.</param>
        /// <returns></returns>
        public static Container WithEncryptor(
            this Container container,
            Encryptor encryptor)
        {
            return new EncryptionContainer(
                container, 
                encryptor);
        }

        /// <summary>
        /// This method gets the FeedIterator from LINQ IQueryable to execute query asynchronously.
        /// This will create the fresh new FeedIterator when called which will support decryption.
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="container">the encryption container.</param>
        /// <param name="query">the IQueryable{T} to be converted.</param>
        /// <param name="queryRequestOptions">optional QueryRequestOptions for passing DecryptionResultHandler.</param>
        /// <returns>An iterator to go through the items.</returns>
        /// <example>
        /// This example shows how to get FeedIterator from LINQ.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// IOrderedQueryable<ToDoActivity> linqQueryable = this.container.GetItemLinqQueryable<ToDoActivity>();
        /// FeedIterator setIterator = EncryptionContainerExtensions.ToEncryptionFeedIterator<ToDoActivity>(this.container, linqQueryable);
        /// ]]>
        /// </code>
        /// </example>
        public static FeedIterator<T> ToEncryptionFeedIterator<T>(
            this Container container, 
            IQueryable<T> query,
            QueryRequestOptions queryRequestOptions = null)
        {
            if (!(container is EncryptionContainer encryptionContainer))
            {
                throw new ArgumentOutOfRangeException(nameof(query), "ToEncryptionFeedIterator is only supported with EncryptionContainer.");
            }

            return new EncryptionFeedIterator<T>(
                encryptionContainer.ToEncryptionStreamIterator(
                    query,
                    queryRequestOptions),
                encryptionContainer.responseFactory);
        }

        /// <summary>
        /// This method gets the FeedIterator from LINQ IQueryable to execute query asynchronously.
        /// This will create the fresh new FeedIterator when called which will support decryption.
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="container">the encryption container.</param>
        /// <param name="query">the IQueryable{T} to be converted.</param>
        /// <param name="queryRequestOptions">optional QueryRequestOptions for passing DecryptionResultHandler.</param>
        /// <returns>An iterator to go through the items.</returns>
        /// <example>
        /// This example shows how to get FeedIterator from LINQ.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// IOrderedQueryable<ToDoActivity> linqQueryable = this.container.GetItemLinqQueryable<ToDoActivity>();
        /// FeedIterator setIterator = EncryptionContainerExtensions.ToEncryptionStreamIterator<ToDoActivity>(this.container, linqQueryable);
        /// ]]>
        /// </code>
        /// </example>
        public static FeedIterator ToEncryptionStreamIterator<T>(
            this Container container, 
            IQueryable<T> query,
            QueryRequestOptions queryRequestOptions = null)
        {
            if (!(container is EncryptionContainer encryptionContainer))
            {
                throw new ArgumentOutOfRangeException(nameof(query), "ToEncryptionStreamIterator is only supported with EncryptionContainer.");
            }

            Action<DecryptionResult> decryptionResultHandler;
            if (queryRequestOptions is EncryptionQueryRequestOptions encryptionQueryRequestOptions)
            {
                decryptionResultHandler = encryptionQueryRequestOptions.DecryptionResultHandler;
            }
            else
            {
                decryptionResultHandler = null;
            }

            return new EncryptionFeedIterator(
                query.ToStreamIterator(),
                encryptionContainer.encryptor,
                decryptionResultHandler);
        }
    }
}
