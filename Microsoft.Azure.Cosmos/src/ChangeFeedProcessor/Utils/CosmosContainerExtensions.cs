//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Utils
{
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

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
    }
}