//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Utils
{
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Tracing;

    internal static class CosmosContainerExtensions
    {
        public static readonly CosmosSerializerCore DefaultJsonSerializer = new CosmosSerializerCore();

        public static async Task<T> TryGetItemAsync<T>(
            this Container container,
            PartitionKey partitionKey,
            string itemId)
        {
            using (ResponseMessage responseMessage = await container.ReadItemStreamAsync(
                    itemId,
                    partitionKey)
                    .ConfigureAwait(false))
            {
                responseMessage.EnsureSuccessStatusCode();
                return CosmosContainerExtensions.DefaultJsonSerializer.FromStream<T>(responseMessage.Content);
            }
        }

        public static async Task<ItemResponse<T>> TryCreateItemAsync<T>(
            this Container container,
            PartitionKey partitionKey,
            T item)
        {
            using (Stream itemStream = CosmosContainerExtensions.DefaultJsonSerializer.ToStream<T>(item))
            {
                using (ResponseMessage response = await container.CreateItemStreamAsync(itemStream, partitionKey).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.Conflict)
                    {
                        // Ignore-- document already exists.
                        return null;
                    }

                    response.EnsureSuccessStatusCode();

                    return new ItemResponse<T>(
                        response.StatusCode, 
                        response.Headers, 
                        CosmosContainerExtensions.DefaultJsonSerializer.FromStream<T>(response.Content), 
                        response.Trace);
                }
            }
        }

        public static async Task<ItemResponse<T>> TryReplaceItemAsync<T>(
            this Container container,
            string itemId,
            T item,
            PartitionKey partitionKey,
            ItemRequestOptions itemRequestOptions)
        {
            using (Stream itemStream = CosmosContainerExtensions.DefaultJsonSerializer.ToStream<T>(item))
            {
                using (ResponseMessage response = await container.ReplaceItemStreamAsync(itemStream, itemId, partitionKey, itemRequestOptions).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    return new ItemResponse<T>(
                        response.StatusCode, 
                        response.Headers, 
                        CosmosContainerExtensions.DefaultJsonSerializer.FromStream<T>(response.Content), 
                        response.Trace);
                }
            }
        }

        public static async Task<bool> TryDeleteItemAsync<T>(
            this Container container,
            PartitionKey partitionKey,
            string itemId,
            ItemRequestOptions cosmosItemRequestOptions = null)
        {
            using (ResponseMessage response = await container.DeleteItemStreamAsync(itemId, partitionKey, cosmosItemRequestOptions).ConfigureAwait(false))
            {
                return response.IsSuccessStatusCode;
            }
        }

        public static async Task<bool> ItemExistsAsync(
            this Container container,
            PartitionKey partitionKey,
            string itemId)
        {
            ResponseMessage response = await container.ReadItemStreamAsync(
                        itemId,
                        partitionKey)
                        .ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }

        public static async Task<string> GetMonitoredDatabaseAndContainerRidAsync(
            this Container monitoredContainer,
            CancellationToken cancellationToken = default)
        {
            string containerRid = await ((ContainerInternal)monitoredContainer).GetCachedRIDAsync(
                forceRefresh: false,
                NoOpTrace.Singleton,
                cancellationToken: cancellationToken);
            string databaseRid = await ((DatabaseInternal)((ContainerInternal)monitoredContainer).Database).GetRIDAsync(cancellationToken);
            return $"{databaseRid}_{containerRid}";
        }

        public static string GetLeasePrefix(
            this Container monitoredContainer,
            string leasePrefix,
            string monitoredDatabaseAndContainerRid)
        {
            string optionsPrefix = leasePrefix ?? string.Empty;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}{1}_{2}",
                optionsPrefix,
                ((ContainerInternal)monitoredContainer).ClientContext.Client.Endpoint.Host,
                monitoredDatabaseAndContainerRid);
        }
    }
}