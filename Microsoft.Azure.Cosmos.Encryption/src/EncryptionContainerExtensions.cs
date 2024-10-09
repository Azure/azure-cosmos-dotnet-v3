//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Linq;

    /// <summary>
    /// Extension methods for <see cref="Container"/> to support client-side encryption.
    /// </summary>
    [CLSCompliant(false)]
    public static class EncryptionContainerExtensions
    {
        /// <summary>
        /// Warms up the caches for the client encryption policy and the corresponding client encryption keys configured for the container.
        /// This may be used to help improve latencies for the initial requests on the container from the client.
        /// If this is not explicitly invoked, the caches are built when requests needing encryption/decryption are made.
        /// </summary>
        /// <param name="container">Container instance from client supporting encryption.</param>
        /// <param name="cancellationToken">Token for request cancellation.</param>
        /// <returns>Container with encryption-related caches warmed up.</returns>
        /// <example>
        /// This example shows how to get a container with encryption support and warm up the encryption-related caches.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// Azure.Core.TokenCredential tokenCredential = new Azure.Identity.DefaultAzureCredential();
        /// Azure.Core.Cryptography.IKeyEncryptionKeyResolver keyResolver = new Azure.Security.KeyVault.Keys.Cryptography.KeyResolver(tokenCredential);
        /// CosmosClient client = (new CosmosClient(endpoint, authKey)).WithEncryption(keyResolver, KeyEncryptionKeyResolverName.AzureKeyVault);
        /// Container container = await client.GetDatabase("databaseId").GetContainer("containerId").InitializeEncryptionAsync();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// See <see href="https://aka.ms/CosmosClientEncryption">client-side encryption documentation</see> for more details.
        /// </remarks>
        public static async Task<Container> InitializeEncryptionAsync(
            this Container container,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (container is not EncryptionContainer encryptionContainer)
            {
                throw new ArgumentOutOfRangeException($"{nameof(InitializeEncryptionAsync)} requires the use of an encryption-enabled client. Please refer to https://aka.ms/CosmosClientEncryption for more details.");
            }

            await encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: cancellationToken);

            return container;
        }

        /// <summary>
        /// Creates a FeedIterator from a LINQ IQueryable to be able to execute the query asynchronously.
        /// This has support for decrypting results.
        /// </summary>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="container">Container instance from client supporting encryption.</param>
        /// <param name="query">The <see cref="IQueryable{T}" /> to be converted.</param>
        /// <returns>An iterator to go through the results.</returns>
        /// <example>
        /// This example shows how to get a FeedIterator from an IQueryable with support to decrypt results.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// IOrderedQueryable<ToDoActivity> linqQueryable = container.GetItemLinqQueryable<ToDoActivity>();
        /// FeedIterator<ToDoActivity> resultIterator = container.ToEncryptionFeedIterator<ToDoActivity>(linqQueryable);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// See <see href="https://aka.ms/CosmosClientEncryption">client-side encryption documentation</see> for more details.
        /// </remarks>
        public static FeedIterator<T> ToEncryptionFeedIterator<T>(
            this Container container,
            IQueryable<T> query)
        {
            if (container is not EncryptionContainer encryptionContainer)
            {
                throw new ArgumentOutOfRangeException(nameof(query), $"{nameof(ToEncryptionFeedIterator)} requires the use of an encryption-enabled client. Please refer to https://aka.ms/CosmosClientEncryption for more details.");
            }

            return new EncryptionFeedIterator<T>(
                (EncryptionFeedIterator)encryptionContainer.ToEncryptionStreamIterator(query),
                encryptionContainer.ResponseFactory);
        }

        /// <summary>
        /// Creates a FeedIterator from a LINQ IQueryable to be able to execute the query asynchronously.
        /// This has support for decrypting results.
        /// </summary>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="container">Container instance from client supporting encryption.</param>
        /// <param name="query">The <see cref="IQueryable{T}" /> to be converted.</param>
        /// <returns>An iterator to go through the results.</returns>
        /// <example>
        /// This example shows how to get FeedIterator from LINQ.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// IOrderedQueryable<ToDoActivity> linqQueryable = this.container.GetItemLinqQueryable<ToDoActivity>();
        /// FeedIterator resultIterator = this.container.ToEncryptionStreamIterator<ToDoActivity>(linqQueryable);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// See <see href="https://aka.ms/CosmosClientEncryption">client-side encryption documentation</see> for more details.
        /// </remarks>
        public static FeedIterator ToEncryptionStreamIterator<T>(
            this Container container,
            IQueryable<T> query)
        {
            if (container is not EncryptionContainer encryptionContainer)
            {
                throw new ArgumentOutOfRangeException(nameof(query), $"{nameof(ToEncryptionStreamIterator)} requires the use of an encryption-enabled client. Please refer to https://aka.ms/CosmosClientEncryption for more details.");
            }

            return new EncryptionFeedIterator(
                query.ToStreamIterator(),
                encryptionContainer,
                new RequestOptions());
        }

        /// <summary>
        /// Creates an instance of <see cref="QueryDefinition" /> with support to add parameters
        /// corresponding to encrypted properties.
        /// </summary>
        /// <param name="container">Container instance from client supporting encryption.</param>
        /// <param name="queryText">A valid Cosmos SQL query.</param>
        /// <returns>A new instance of <see cref="QueryDefinition" /> with support to add encrypted parameters.</returns>
        /// <example>
        /// This example shows how to create a QueryDefinition with support to add encrypted parameters.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// QueryDefinition queryDefinitionWithEncryptedParameter = container.CreateQueryDefinition(
        ///     "SELECT * FROM c where c.SensitiveProperty = @FirstParameter");
        /// await queryDefinitionWithEncryptedParameter.AddParameterAsync(
        ///     "@FirstParameter",
        ///     "sensitive value",
        ///     "/SensitiveProperty");
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// Only equality comparisons are supported in the filter condition on encrypted properties.
        /// These also require the property being filtered upon to be encrypted using <see cref="EncryptionType.Deterministic"/> encryption.
        /// See <see href="https://aka.ms/CosmosClientEncryption">client-side encryption documentation</see> for more details.
        /// </remarks>
        public static QueryDefinition CreateQueryDefinition(this Container container, string queryText)
        {
            if (string.IsNullOrEmpty(queryText))
            {
                throw new ArgumentNullException(nameof(queryText));
            }

            if (container is not EncryptionContainer)
            {
                throw new ArgumentOutOfRangeException($"{nameof(CreateQueryDefinition)} requires the use of an encryption-enabled client. Please refer to https://aka.ms/CosmosClientEncryption for more details.");
            }

            return new EncryptionQueryDefinition(queryText, container);
        }
    }
}
