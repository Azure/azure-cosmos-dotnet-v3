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
        /// Initializes and Caches the Client Encryption Policy configured for the container.
        /// </summary>
        /// <param name="container">MdeContainer.</param>
        /// <param name="cancellationToken"> cancellation token </param>
        /// <returns>Container to perform operations supporting client-side encryption / decryption.</returns>
        public static async Task<Container> InitializeEncryptionAsync(
            this Container container,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (container is MdeContainer mdeContainer)
            {
                EncryptionCosmosClient encryptionCosmosClient = mdeContainer.EncryptionCosmosClient;
                await encryptionCosmosClient.GetOrAddClientEncryptionPolicyAsync(container, cancellationToken, false);
                return mdeContainer;
            }
            else
            {
                throw new InvalidOperationException($"Invalid {container} used for this operation");
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
        /// FeedIterator setIterator = this.container.ToMdeEncryptionStreamIterator<ToDoActivity>(linqQueryable);
        /// ]]>
        /// </code>
        /// </example>
        public static FeedIterator<T> ToMdeEncryptionFeedIterator<T>(
            this Container container,
            IQueryable<T> query)
        {
            if (!(container is MdeContainer mdeContainer))
            {
                throw new ArgumentOutOfRangeException(nameof(query), $"{nameof(ToMdeEncryptionFeedIterator)} is only supported with {nameof(MdeContainer)}.");
            }

            return new MdeEncryptionFeedIterator<T>(
                (MdeEncryptionFeedIterator)mdeContainer.ToMdeEncryptionStreamIterator(query),
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
        /// FeedIterator setIterator = this.container.ToMdeEncryptionStreamIterator<ToDoActivity>(linqQueryable);
        /// ]]>
        /// </code>
        /// </example>
        public static FeedIterator ToMdeEncryptionStreamIterator<T>(
            this Container container,
            IQueryable<T> query)
        {
            if (!(container is MdeContainer mdeContainer))
            {
                throw new ArgumentOutOfRangeException(nameof(query), $"{nameof(ToMdeEncryptionStreamIterator)} is only supported with {nameof(MdeContainer)}.");
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
        public static PermissionProperties GetPermissionPropertiesForMdeContainer(
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
                throw new ArgumentOutOfRangeException($"{nameof(GetPermissionPropertiesForMdeContainer)} is only supported with {nameof(MdeContainer)}.");
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
        public static PermissionProperties GetPermissionPropertiesForMdeContainer(
            this Container container,
            string id,
            PermissionMode permissionMode,
            PartitionKey resourcePartitionKey,
            string itemId)
        {
            if (container is MdeContainer mdeContainer)
            {
                return new PermissionProperties(id, permissionMode, mdeContainer.Container, resourcePartitionKey, itemId);
            }
            else
            {
                throw new ArgumentOutOfRangeException($"{nameof(GetPermissionPropertiesForMdeContainer)} is only supported with {nameof(MdeContainer)}.");
            }
        }
    }
}
