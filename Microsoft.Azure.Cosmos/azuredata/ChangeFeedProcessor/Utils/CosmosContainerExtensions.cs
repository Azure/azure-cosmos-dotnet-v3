//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.ChangeFeed
{
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos.Serialization;

    internal static class CosmosContainerExtensions
    {
        public static readonly CosmosSerializer DefaultJsonSerializer = CosmosTextJsonSerializer.CreatePropertiesSerializer();

        public static async Task<T> TryGetItemAsync<T>(
            this CosmosContainer container,
            PartitionKey partitionKey,
            string itemId)
        {
            using (Response responseMessage = await container.ReadItemStreamAsync(
                    itemId,
                    partitionKey)
                    .ConfigureAwait(false))
            {
                responseMessage.EnsureSuccessStatusCode();
                return CosmosContainerExtensions.DefaultJsonSerializer.FromStream<T>(responseMessage.ContentStream);
            }
        }

        public static async Task<ItemResponse<T>> TryCreateItemAsync<T>(
            this CosmosContainer container,
            PartitionKey partitionKey,
            T item)
        {
            using (Stream itemStream = CosmosContainerExtensions.DefaultJsonSerializer.ToStream<T>(item))
            {
                using (Response response = await container.CreateItemStreamAsync(itemStream, partitionKey).ConfigureAwait(false))
                {
                    if (response.Status == (int)HttpStatusCode.Conflict)
                    {
                        // Ignore-- document already exists.
                        return null;
                    }

                    return new ItemResponse<T>(response, CosmosContainerExtensions.DefaultJsonSerializer.FromStream<T>(response.ContentStream));
                }
            }
        }

        public static async Task<ItemResponse<T>> TryReplaceItemAsync<T>(
            this CosmosContainer container,
            string itemId,
            T item,
            PartitionKey partitionKey,
            ItemRequestOptions itemRequestOptions)
        {
            using (Stream itemStream = CosmosContainerExtensions.DefaultJsonSerializer.ToStream<T>(item))
            {
                using (Response response = await container.ReplaceItemStreamAsync(itemStream, itemId, partitionKey, itemRequestOptions).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    return new ItemResponse<T>(response, CosmosContainerExtensions.DefaultJsonSerializer.FromStream<T>(response.ContentStream));
                }
            }
        }

        public static async Task<bool> TryDeleteItemAsync<T>(
            this CosmosContainer container,
            PartitionKey partitionKey,
            string itemId,
            ItemRequestOptions cosmosItemRequestOptions = null)
        {
            using (Response response = await container.DeleteItemStreamAsync(itemId, partitionKey, cosmosItemRequestOptions).ConfigureAwait(false))
            {
                return response.IsSuccessStatusCode();
            }
        }

        public static async Task<bool> ItemExistsAsync(
            this CosmosContainer container,
            PartitionKey partitionKey,
            string itemId)
        {
            Response response = await container.ReadItemStreamAsync(
                        itemId,
                        partitionKey)
                        .ConfigureAwait(false);

            return response.IsSuccessStatusCode();
        }

        public static async Task<string> GetMonitoredContainerRidAsync(
            this CosmosContainer monitoredContainer,
            string suggestedMonitoredRid,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!string.IsNullOrEmpty(suggestedMonitoredRid))
            {
                return suggestedMonitoredRid;
            }

            string containerRid = await ((ContainerCore)monitoredContainer).GetRIDAsync(cancellationToken);
            string databaseRid = await ((DatabaseCore)((ContainerCore)monitoredContainer).Database).GetRIDAsync(cancellationToken);
            return $"{databaseRid}_{containerRid}";
        }

        public static string GetLeasePrefix(
            this CosmosContainer monitoredContainer,
            ChangeFeedLeaseOptions changeFeedLeaseOptions,
            string monitoredContainerRid)
        {
            string optionsPrefix = changeFeedLeaseOptions.LeasePrefix ?? string.Empty;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}{1}_{2}",
                optionsPrefix,
                ((ContainerCore)monitoredContainer).ClientContext.Client.Endpoint.Host,
                monitoredContainerRid);
        }
    }
}