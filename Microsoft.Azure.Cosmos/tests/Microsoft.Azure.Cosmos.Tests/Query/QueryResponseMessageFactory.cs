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

        public static (QueryResponse queryResponse, ReadOnlyCollection<ToDoItem> items) Create(
            string itemIdPrefix,
            string continuationToken,
            int itemCount = 50)
        {
            ReadOnlyCollection<ToDoItem> items = ToDoItem.CreateItems(itemCount, itemIdPrefix);
            MemoryStream memoryStream = (MemoryStream)cosmosSerializer.ToStream<IList<ToDoItem>>(items);
            long responseLengthBytes = memoryStream.Length;

            IJsonNavigator jsonNavigator = JsonNavigator.Create(memoryStream.ToArray());
            IJsonNavigatorNode jsonNavigatorNode = jsonNavigator.GetRootNode();
            CosmosArray cosmosArray = CosmosArray.Create(jsonNavigator, jsonNavigatorNode);

            Headers headers = new Headers();
            headers.Continuation = continuationToken;
            headers.ActivityId = Guid.NewGuid().ToString();

            QueryResponse message = QueryResponse.CreateSuccess(
                    result: cosmosArray,
                    count: itemCount,
                    responseHeaders: CosmosQueryResponseMessageHeaders.ConvertToQueryHeaders(headers, ResourceType.Document, "CollectionRid"),
                    responseLengthBytes: responseLengthBytes);

            return (message, items);
        }
    }
}
