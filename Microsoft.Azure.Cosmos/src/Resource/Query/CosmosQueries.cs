//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    internal class CosmosQueries
    {
        private readonly CosmosClient _client;
        internal readonly IDocumentQueryClient DocumentClient;

        internal CosmosQueries(CosmosClient client, IDocumentQueryClient documentClient)
        {
            this._client = client;
            this.DocumentClient = documentClient;
        }

        internal IDocumentClientRetryPolicy GetRetryPolicy()
        {
            return this.DocumentClient.ResetSessionTokenRetryPolicy.GetRequestPolicy();
        }

        internal Task<CollectionCache> GetCollectionCacheAsync()
        {
            return this.DocumentClient.GetCollectionCacheAsync();
        }

        internal Task<IRoutingMapProvider> GetRoutingMapProviderAsync()
        {
            return this.DocumentClient.GetRoutingMapProviderAsync();
        } 

        internal Task<QueryPartitionProvider> GetQueryPartitionProviderAsync(CancellationToken cancellationToken)
        {
            return this.DocumentClient.GetQueryPartitionProviderAsync(cancellationToken);
        }

        internal async Task<FeedResponse<CosmosElement>> ExecuteItemQueryAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosQueryRequestOptions requestOptions,
            SqlQuerySpec sqlQuerySpec,
            Action<CosmosRequestMessage> requestEnricher,
            CancellationToken cancellationToken)
        {
            CosmosResponseMessage message = await ExecUtils.ProcessResourceOperationStreamAsync(
                this._client,
                resourceUri,
                resourceType,
                operationType,
                requestOptions,
                null,
                this._client.CosmosJsonSerializer.ToStream<SqlQuerySpec>(sqlQuerySpec),
                requestEnricher,
                cancellationToken);

            return GetFeedResponse(requestOptions, resourceType, message);
        }

        internal Task<CosmosResponseMessage> ReadFeedAsync(CosmosRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult<CosmosResponseMessage>(null);
        }

        internal Task<Documents.ConsistencyLevel> GetDefaultConsistencyLevelAsync()
        {
            return this.DocumentClient.GetDefaultConsistencyLevelAsync();
        }

        internal Task<Documents.ConsistencyLevel?> GetDesiredConsistencyLevelAsync()
        {
            return this.DocumentClient.GetDesiredConsistencyLevelAsync();
        }

        internal Task EnsureValidOverwrite(Documents.ConsistencyLevel desiredConsistencyLevel)
        {
            return this.DocumentClient.EnsureValidOverwrite(desiredConsistencyLevel);
        }

        internal Task<PartitionKeyRangeCache> GetPartitionKeyRangeCache()
        {
            return this.DocumentClient.GetPartitionKeyRangeCache();
        }

        private FeedResponse<CosmosElement> GetFeedResponse(
            CosmosQueryRequestOptions requestOptions,
            ResourceType resourceType,
            CosmosResponseMessage cosmosResponseMessage)
        {
            using (cosmosResponseMessage)
            {
                // Execute the callback an each element of the page
                // For example just could get a response like this
                // {
                //    "_rid": "qHVdAImeKAQ=",
                //    "Documents": [{
                //        "id": "03230",
                //        "_rid": "qHVdAImeKAQBAAAAAAAAAA==",
                //        "_self": "dbs\/qHVdAA==\/colls\/qHVdAImeKAQ=\/docs\/qHVdAImeKAQBAAAAAAAAAA==\/",
                //        "_etag": "\"410000b0-0000-0000-0000-597916b00000\"",
                //        "_attachments": "attachments\/",
                //        "_ts": 1501107886
                //    }],
                //    "_count": 1
                // }
                // And you should execute the callback on each document in "Documents".
                MemoryStream memoryStream = new MemoryStream();
                cosmosResponseMessage.Content.CopyTo(memoryStream);
                long responseLengthBytes = memoryStream.Length;
                byte[] content = memoryStream.ToArray();
                IJsonNavigator jsonNavigator = null;

                // Use the users custom navigator first. If it returns null back try the
                // internal navigator.
                if (requestOptions.CosmosSerializationOptions != null)
                {
                    jsonNavigator = requestOptions.CosmosSerializationOptions.CreateCustomNavigatorCallback(content);
                    if (jsonNavigator == null)
                    {
                        throw new InvalidOperationException("The CosmosSerializationOptions did not return a JSON navigator.");
                    }
                }
                else
                {
                    jsonNavigator = JsonNavigator.Create(content);
                }

                string resourceName = this.GetRootNodeName(resourceType);

                if (!jsonNavigator.TryGetObjectProperty(
                    jsonNavigator.GetRootNode(),
                    resourceName,
                    out ObjectProperty objectProperty))
                {
                    throw new InvalidOperationException($"Response Body Contract was violated. QueryResponse did not have property: {resourceName}");
                }

                IJsonNavigatorNode cosmosElements = objectProperty.ValueNode;
                if (!(CosmosElement.Dispatch(
                    jsonNavigator,
                    cosmosElements) is CosmosArray cosmosArray))
                {
                    throw new InvalidOperationException($"QueryResponse did not have an array of : {resourceName}");
                }

                int itemCount = cosmosArray.Count;
                return new FeedResponse<CosmosElement>(
                    result: cosmosArray,
                    count: itemCount,
                    responseHeaders: cosmosResponseMessage.Headers.CosmosMessageHeaders,
                    responseLengthBytes: responseLengthBytes);
            }
        }

        private string GetRootNodeName(ResourceType resourceType)
        {
            switch (resourceType)
            {
                case Documents.ResourceType.Collection:
                    return "DocumentCollections";
                default:
                    return resourceType.ToResourceTypeString() + "s";
            }
        }
    }
}
