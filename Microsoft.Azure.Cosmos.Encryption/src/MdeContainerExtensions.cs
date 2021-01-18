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
    /// This class provides extension methods for <see cref="Container"/>.
    /// </summary>
    public static class MdeContainerExtensions
    {
        /// <summary>
        /// Initializes and Caches the Client Encryption Policy and the corresponding keys configured for the container.
        /// All the keys configured as per the Client Encryption Policy for the container must be created before its used in the policy.
        /// </summary>
        /// <param name="container">MdeContainer.</param>
        /// <param name="cancellationToken"> cancellation token </param>
        /// <returns>Container to perform operations supporting client-side encryption / decryption.</returns>
        /// <example>
        /// This example shows how to get a Container with Encryption support and Initialize it with InitializeEncryptionAsync which allows for pre-fetching the
        /// encryption policy and the encryption keys for caching.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClient cosmosClient = new CosmosClient();
        /// cosmosClient.WithEncryption();
        /// containerWithEncryption = await this.cosmosDatabase.GetContainer("id").InitializeEncryptionAsync();
        /// ]]>
        /// </code>
        /// </example>
        public static async Task<Container> InitializeEncryptionAsync(
            this Container container,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (container is MdeContainer mdeContainer)
            {
                EncryptionCosmosClient encryptionCosmosClient = mdeContainer.EncryptionCosmosClient;
                ClientEncryptionPolicy clientEncryptionPolicy = await encryptionCosmosClient.GetClientEncryptionPolicyAsync(
                    container: container,
                    cancellationToken: cancellationToken,
                    shouldForceRefresh: false);

                if (clientEncryptionPolicy != null)
                {
                    foreach (string clientEncryptionKeyId in clientEncryptionPolicy.IncludedPaths.Select(p => p.ClientEncryptionKeyId).Distinct())
                    {
                        CachedClientEncryptionProperties cachedClientEncryptionProperties = await mdeContainer.EncryptionCosmosClient.GetClientEncryptionKeyPropertiesAsync(
                                clientEncryptionKeyId: clientEncryptionKeyId,
                                container: container,
                                cancellationToken: cancellationToken,
                                shouldForceRefresh: false);
                    }
                }

                return mdeContainer;
            }
            else
            {
                throw new InvalidOperationException($"Invalid {container} used for this operation.This operation requires the use of an encryption - enabled client. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
            }
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
        public static FeedIterator<T> ToEncryptionFeedIterator<T>(
            this Container container,
            IQueryable<T> query)
        {
            if (container is not MdeContainer mdeContainer)
            {
                throw new ArgumentOutOfRangeException(nameof(query), $"{nameof(ToEncryptionFeedIterator)} requires the use of an encryption - enabled client. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
            }

            return new MdeEncryptionFeedIterator<T>(
                (MdeEncryptionFeedIterator)mdeContainer.ToEncryptionStreamIterator(query),
                mdeContainer.ResponseFactory);
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
            if (container is not MdeContainer mdeContainer)
            {
                throw new ArgumentOutOfRangeException(nameof(query), $"{nameof(ToEncryptionStreamIterator)} requires the use of an encryption - enabled client. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
            }

            return new MdeEncryptionFeedIterator(
                query.ToStreamIterator(),
                mdeContainer.MdeEncryptionProcessor);
        }

        /// <summary>
        /// Initialize a new instance of the Microsoft.Azure.Cosmos.PermissionProperties
        /// with permission to Microsoft.Azure.Cosmos.Container.
        /// </summary>
        /// <param name="container"> The encryption container. </param>
        /// <param name="id"> The permission id. </param>
        /// <param name="permissionMode"> The Microsoft.Azure.Cosmos.PermissionProperties.PermissionMode. </param>
        /// <param name="resourcePartitionKey"> (Optional) The partition key value for the permission in the Azure Cosmos DB service. </param>
        /// <returns> PermissionProperties for the Container </returns>
        public static PermissionProperties GetPermissionPropertiesForEncryptionContainer(
            this Container container,
            string id,
            PermissionMode permissionMode,
            PartitionKey? resourcePartitionKey = null)
        {
            if (container is MdeContainer mdeContainer)
            {
                return new PermissionProperties(id, permissionMode, mdeContainer.Container, resourcePartitionKey);
            }
            else
            {
                throw new ArgumentOutOfRangeException($"{nameof(GetPermissionPropertiesForEncryptionContainer)} requires the use of an encryption - enabled client. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
            }
        }

        /// <summary>
        /// Initialize a new instance of the Microsoft.Azure.Cosmos.PermissionProperties
        /// with permission to Cosmos item.
        /// </summary>
        /// <param name="container"> The encryption container. </param>
        /// <param name="id"> The permission id. </param>
        /// <param name="permissionMode"> The Microsoft.Azure.Cosmos.PermissionProperties.PermissionMode. </param>
        /// <param name="resourcePartitionKey"> The partition key value for the permission in the Azure Cosmos DB service </param>
        /// <param name="itemId">  The cosmos item id </param>
        /// <returns> PermissionProperties for the Container </returns>
        public static PermissionProperties GetPermissionPropertiesForEncryptionContainer(
            this Container container,
            string id,
            PermissionMode permissionMode,
            PartitionKey resourcePartitionKey,
            string itemId)
        {
            if (container is MdeContainer mdeContainer)
            {
                return new PermissionProperties(
                    id,
                    permissionMode,
                    mdeContainer.Container,
                    resourcePartitionKey,
                    itemId);
            }
            else
            {
                throw new ArgumentOutOfRangeException($"{nameof(GetPermissionPropertiesForEncryptionContainer)} requires the use of an encryption - enabled client. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
            }
        }

        /// <summary>
        /// Create a Microsoft.Azure.Cosmos.QueryDefinition with encryption support.
        /// </summary>
        /// <param name="container"> The encryption container.</param>
        /// <param name="queryText"> A valid Cosmos SQL query "Select * from test t" </param>
        /// <returns> Microsoft.Azure.Cosmos.QueryDefinition </returns>
        /// <example>
        /// This example shows how to get a QueryDefinition with Encryption Support.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// containerWithEncryption = await this.cosmosDatabase.GetContainer("id").InitializeEncryptionAsync();
        /// QueryDefinition withEncryptedParameter = containerWithEncryption.CreateQueryDefinition(
        ///     "SELECT * FROM c where c.PropertyName = @PropertyValue");
        /// ]]>
        /// </code>
        /// </example>
        public static QueryDefinition CreateQueryDefinition(this Container container, string queryText)
        {
            if (string.IsNullOrEmpty(queryText))
            {
                throw new ArgumentNullException(nameof(queryText));
            }

            if (container is not MdeContainer)
            {
                throw new ArgumentOutOfRangeException($"{nameof(CreateQueryDefinition)} requires the use of an encryption - enabled client. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
            }

            return new EncryptionQueryDefinition(queryText, container);
        }
    }
}
