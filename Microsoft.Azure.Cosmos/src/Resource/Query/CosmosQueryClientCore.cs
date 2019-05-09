//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    internal class CosmosQueryClientCore : CosmosQueryClient
    {
        private readonly CosmosClientContext clientContext;
        private readonly CosmosContainerCore cosmosContainerCore;
        internal readonly IDocumentQueryClient DocumentQueryClient;

        internal CosmosQueryClientCore(
            CosmosClientContext clientContext,
            CosmosContainerCore cosmosContainerCore)
        {
            this.clientContext = clientContext ?? throw new ArgumentException(nameof(clientContext));
            this.cosmosContainerCore = cosmosContainerCore ?? throw new ArgumentException(nameof(cosmosContainerCore));
            this.DocumentQueryClient = clientContext.DocumentQueryClient ?? throw new ArgumentException(nameof(clientContext));
        }

        internal override IDocumentClientRetryPolicy GetRetryPolicy()
        {
            return this.DocumentQueryClient.ResetSessionTokenRetryPolicy.GetRequestPolicy();
        }

        internal override Task<CollectionCache> GetCollectionCacheAsync()
        {
            return this.DocumentQueryClient.GetCollectionCacheAsync();
        }

        internal override Task<IRoutingMapProvider> GetRoutingMapProviderAsync()
        {
            return this.DocumentQueryClient.GetRoutingMapProviderAsync();
        }

        internal override Task<QueryPartitionProvider> GetQueryPartitionProviderAsync(CancellationToken cancellationToken)
        {
            return this.DocumentQueryClient.GetQueryPartitionProviderAsync(cancellationToken);
        }

        internal override async Task<CosmosQueryResponse> ExecuteItemQueryAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            QueryRequestOptions requestOptions,
            SqlQuerySpec sqlQuerySpec,
            Action<CosmosRequestMessage> requestEnricher,
            CancellationToken cancellationToken)
        {
            CosmosResponseMessage message = await this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                partitionKey: requestOptions.PartitionKey,
                cosmosContainerCore: this.cosmosContainerCore,
                streamPayload: this.clientContext.JsonSerializer.ToStream<SqlQuerySpec>(sqlQuerySpec),
                requestEnricher: requestEnricher,
                cancellationToken: cancellationToken);

            return this.GetCosmosElementResponse(requestOptions, resourceType, message);
        }

        internal override Task<Documents.ConsistencyLevel> GetDefaultConsistencyLevelAsync()
        {
            return this.DocumentQueryClient.GetDefaultConsistencyLevelAsync();
        }

        internal override Task<Documents.ConsistencyLevel?> GetDesiredConsistencyLevelAsync()
        {
            return this.DocumentQueryClient.GetDesiredConsistencyLevelAsync();
        }

        internal override Task EnsureValidOverwrite(Documents.ConsistencyLevel desiredConsistencyLevel)
        {
            return this.DocumentQueryClient.EnsureValidOverwrite(desiredConsistencyLevel);
        }

        internal override Task<PartitionKeyRangeCache> GetPartitionKeyRangeCache()
        {
            return this.DocumentQueryClient.GetPartitionKeyRangeCache();
        }

        internal override Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesByEpkString(
            string resourceLink,
            string collectionResourceId,
            string effectivePartitionKeyString)
        {
            return this.GetTargetPartitionKeyRanges(
                resourceLink,
                collectionResourceId,
                new List<Range<string>>
                {
                    Range<string>.GetPointRange(effectivePartitionKeyString)
                });
        }

        internal override async Task<List<PartitionKeyRange>> GetTargetPartitionKeyRanges(
            string resourceLink,
            string collectionResourceId,
            List<Range<string>> providedRanges)
        {
            if (string.IsNullOrEmpty(collectionResourceId))
            {
                throw new ArgumentNullException(nameof(collectionResourceId));
            }

            if (providedRanges == null || 
                !providedRanges.Any() || 
                providedRanges.Any(x => x == null))
            {
                throw new ArgumentNullException(nameof(providedRanges));
            }

            IRoutingMapProvider routingMapProvider = await this.GetRoutingMapProviderAsync();

            List<PartitionKeyRange> ranges = await routingMapProvider.TryGetOverlappingRangesAsync(collectionResourceId, providedRanges);
            if (ranges == null && PathsHelper.IsNameBased(resourceLink))
            {
                // Refresh the cache and don't try to re-resolve collection as it is not clear what already
                // happened based on previously resolved collection rid.
                // Return NotFoundException this time. Next query will succeed.
                // This can only happen if collection is deleted/created with same name and client was not restarted
                // in between.
                CollectionCache collectionCache = await this.GetCollectionCacheAsync();
                collectionCache.Refresh(resourceLink);
            }

            if (ranges == null)
            {
                throw new NotFoundException($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}: GetTargetPartitionKeyRanges(collectionResourceId:{collectionResourceId}, providedRanges: {string.Join(",", providedRanges)} failed due to stale cache");
            }

            return ranges;
        }

        internal override bool ByPassQueryParsing()
        {
            return CustomTypeExtensions.ByPassQueryParsing();
        }

        private CosmosQueryResponse GetCosmosElementResponse(
            QueryRequestOptions requestOptions,
            ResourceType resourceType,
            CosmosResponseMessage cosmosResponseMessage)
        {
            using (cosmosResponseMessage)
            {
                if (!cosmosResponseMessage.IsSuccessStatusCode)
                {
                    return CosmosQueryResponse.CreateFailure(
                        CosmosQueryResponseMessageHeaders.ConvertToQueryHeaders(cosmosResponseMessage.Headers),
                        cosmosResponseMessage.StatusCode,
                        cosmosResponseMessage.RequestMessage,
                        cosmosResponseMessage.ErrorMessage,
                        cosmosResponseMessage.Error);
                }

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

                // Use the users custom navigator
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
                return CosmosQueryResponse.CreateSuccess(
                    result: cosmosArray,
                    count: itemCount,
                    responseHeaders: CosmosQueryResponseMessageHeaders.ConvertToQueryHeaders(cosmosResponseMessage.Headers),
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
