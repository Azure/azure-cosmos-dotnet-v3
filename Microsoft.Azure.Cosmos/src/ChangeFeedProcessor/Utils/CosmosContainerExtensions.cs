//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

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
            this CosmosContainer container,
            object partitionKey,
            string itemId)
        {
            var response = await container.Items.ReadItemAsync<T>(
                    partitionKey,
                    itemId)
                    .ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default(T);
            }

            return response;
        }

        public static async Task<CosmosItemResponse<T>> TryCreateItemAsync<T>(
            this CosmosContainer container, 
            object partitionKey, 
            T item)
        {
            var response = await container.Items.CreateItemAsync<T>(partitionKey, item).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                // Ignore-- document already exists.
                return null;
            }

            return response;
        }

        public static async Task<T> TryDeleteItemAsync<T>(
            this CosmosContainer container,
            object partitionKey,
            string itemId,
            CosmosItemRequestOptions cosmosItemRequestOptions = null)
        {
            var response = await container.Items.DeleteItemAsync<T>(partitionKey, itemId, cosmosItemRequestOptions).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default(T);
            }

            return response.Resource;
        }

        public static async Task<bool> ItemExistsAsync(
            this CosmosContainer container,
            object partitionKey,
            string itemId)
        {
            var response = await container.Items.ReadItemStreamAsync(
                        partitionKey,
                        itemId)
                        .ConfigureAwait(false);

            return response.IsSuccessStatusCode;
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

            string containerRid = await ((CosmosContainerCore)monitoredContainer).GetRID(cancellationToken);
            string databaseRid = await ((CosmosDatabaseCore)((CosmosContainerCore)monitoredContainer).Database).GetRID(cancellationToken);
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
                monitoredContainer.Client.Configuration.AccountEndPoint.Host,
                monitoredContainerRid);
        }
    }
}