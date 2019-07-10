//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;
    using Moq;

    internal static class QueryResponseMessageFactory
    {
        private static readonly CosmosSerializer cosmosSerializer = new CosmosJsonSerializerCore();
        public const int SPLIT = -1;

        public static (QueryResponse queryResponse, ReadOnlyCollection<ToDoItem> items) Create(
            string itemIdPrefix,
            string continuationToken,
            string collectionRid,
            int itemCount = 50)
        {
            // Use -1 to represent a split response
            if (itemCount == QueryResponseMessageFactory.SPLIT)
            {
                return CreateSplitResponse(collectionRid);
            }

            ReadOnlyCollection<ToDoItem> items = ToDoItem.CreateItems(itemCount, itemIdPrefix);
            MemoryStream memoryStream = (MemoryStream)cosmosSerializer.ToStream<IList<ToDoItem>>(items);
            long responseLengthBytes = memoryStream.Length;

            IJsonNavigator jsonNavigator = JsonNavigator.Create(memoryStream.ToArray());
            IJsonNavigatorNode jsonNavigatorNode = jsonNavigator.GetRootNode();
            CosmosArray cosmosArray = CosmosArray.Create(jsonNavigator, jsonNavigatorNode);

            Headers headers = new Headers();
            headers.ContinuationToken = continuationToken;
            headers.ActivityId = Guid.NewGuid().ToString();

            QueryResponse message = QueryResponse.CreateSuccess(
                    result: cosmosArray,
                    count: itemCount,
                    responseHeaders: CosmosQueryResponseMessageHeaders.ConvertToQueryHeaders(headers, ResourceType.Document, collectionRid),
                    responseLengthBytes: responseLengthBytes);

            return (message, items);
        }

        public static (QueryResponse queryResponse, ReadOnlyCollection<ToDoItem> items) CreateSplitResponse(string collectionRid)
        {
            QueryResponse splitResponse = QueryResponse.CreateFailure(
               new CosmosQueryResponseMessageHeaders(null, null, ResourceType.Document, collectionRid)
               {
                   SubStatusCode = SubStatusCodes.PartitionKeyRangeGone,
                   ActivityId = Guid.NewGuid().ToString()
               },
               HttpStatusCode.Gone,
               null,
               "Partition split error",
               null);

            return (splitResponse, new List<ToDoItem>().AsReadOnly());
        }
    }
}
