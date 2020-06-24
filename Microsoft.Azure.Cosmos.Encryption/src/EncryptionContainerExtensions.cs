//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
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

        public static Container WithPropEncryptor(this Container container, Encryptor encryptor, Dictionary<string, List<string>> toEncrypt)
        {
            List<EncryptionOptions> pencryptionOptions = new List<EncryptionOptions>();
            foreach (KeyValuePair<string, List<string>> entry in toEncrypt)
            {
                pencryptionOptions.Add(
                    new EncryptionOptions()
                    {
                        DataEncryptionKeyId = entry.Key,
                        EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                        PathsToEncrypt = entry.Value
                    }
                ); ;
            }

            return new EncryptionContainer(
           container,
           encryptor, pencryptionOptions);

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
        /// FeedIterator setIterator = this.container.ToEncryptionFeedIterator<ToDoActivity>(linqQueryable);
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
                throw new ArgumentOutOfRangeException(nameof(query), $"{nameof(ToEncryptionFeedIterator)} is only supported with {nameof(EncryptionContainer)}.");
            }

            return new EncryptionFeedIterator<T>(
                (EncryptionFeedIterator)encryptionContainer.ToEncryptionStreamIterator(
                    query,
                    queryRequestOptions),
                encryptionContainer.ResponseFactory);
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
        /// FeedIterator setIterator = this.container.ToEncryptionStreamIterator<ToDoActivity>(linqQueryable);
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
                throw new ArgumentOutOfRangeException(nameof(query), $"{nameof(ToEncryptionStreamIterator)} is only supported with {nameof(EncryptionContainer)}.");
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
                null,
                decryptionResultHandler);
        }
    }
}
