//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Utils
{
    using System.Globalization;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;

    internal static class CosmosContainerExtensions
    {
        public static async Task<T> TryGetItemAsync<T>(
            this Container container,
            PartitionKey partitionKey,
            string itemId)
        {
            return await container.ReadItemAsync<T>(
                    itemId,
                    partitionKey)
                    .ConfigureAwait(false);
        }

        public static async Task<ItemResponse<T>> TryCreateItemAsync<T>(
            this Container container,
            object partitionKey,
            T item)
        {
            var response = await container.CreateItemAsync<T>(item).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                // Ignore-- document already exists.
                return null;
            }

            return response;
        }

        public static async Task<T> TryDeleteItemAsync<T>(
            this Container container,
            PartitionKey partitionKey,
            string itemId,
            ItemRequestOptions cosmosItemRequestOptions = null)
        {
            var response = await container.DeleteItemAsync<T>(itemId, partitionKey, cosmosItemRequestOptions).ConfigureAwait(false);

            return response.Resource;
        }

        public static async Task<bool> ItemExistsAsync(
            this Container container,
            PartitionKey partitionKey,
            string itemId)
        {
            var response = await container.ReadItemStreamAsync(
                        itemId,
                        partitionKey)
                        .ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }

        public static async Task<string> GetMonitoredContainerRidAsync(
            this Container monitoredContainer,
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
            this Container monitoredContainer,
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