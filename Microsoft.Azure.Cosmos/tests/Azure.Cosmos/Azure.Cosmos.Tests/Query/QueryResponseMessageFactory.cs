//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos.Serialization;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;
    using Moq;

    internal static class QueryResponseMessageFactory
    {
        private static readonly CosmosSerializer cosmosSerializer = CosmosTextJsonSerializer.CreateUserDefaultSerializer();
        public const int SPLIT = -1;

        public static (QueryResponseCore queryResponse, IList<ToDoItem> items) Create(
            string itemIdPrefix,
            string continuationToken,
            string collectionRid,
            int itemCount = 50)
        {
            // Use -1 to represent a split response
            if (itemCount == QueryResponseMessageFactory.SPLIT)
            {
                return (CreateSplitResponse(collectionRid), new List<ToDoItem>().AsReadOnly());
            }

            IList<ToDoItem> items = ToDoItem.CreateItems(itemCount, itemIdPrefix);
            MemoryStream memoryStream = (MemoryStream)cosmosSerializer.ToStream<IList<ToDoItem>>(items);
            long responseLengthBytes = memoryStream.Length;

            IJsonNavigator jsonNavigator = JsonNavigator.Create(memoryStream.ToArray());
            IJsonNavigatorNode jsonNavigatorNode = jsonNavigator.GetRootNode();
            CosmosArray cosmosArray = CosmosArray.Create(jsonNavigator, jsonNavigatorNode);

            double requestCharge = 42;
            string activityId = Guid.NewGuid().ToString();
            IReadOnlyCollection<QueryPageDiagnostics> diagnostics = new List<QueryPageDiagnostics>()
            {
                new QueryPageDiagnostics("0",
                "SomeQueryMetricText",
                "SomeIndexUtilText",
                new PointOperationStatistics(
                    activityId: Guid.NewGuid().ToString(),
                    statusCode: HttpStatusCode.OK,
                    subStatusCode: SubStatusCodes.Unknown,
                    requestCharge: requestCharge,
                    errorMessage: null,
                    method: HttpMethod.Post,
                    requestUri: new Uri("http://localhost.com"),
                    clientSideRequestStatistics: null),
                new SchedulingStopwatch())
            };

            QueryResponseCore message = QueryResponseCore.CreateSuccess(
                    result: cosmosArray,
                    continuationToken: continuationToken,
                    disallowContinuationTokenMessage: null,
                    activityId: activityId,
                    requestCharge: requestCharge,
                    diagnostics: diagnostics,
                    responseLengthBytes: responseLengthBytes);

            return (message, items);
        }

        public static QueryResponseCore CreateQueryResponse(
            IList<ToDoItem> items,
            bool isOrderByQuery,
            string continuationToken,
            string collectionRid)
        {
            MemoryStream memoryStream;
            string json;
            if (isOrderByQuery)
            {
                memoryStream = SerializeForOrderByQuery(items);
                using(StreamReader sr = new StreamReader(SerializeForOrderByQuery(items)))
                {
                    json = sr.ReadToEnd();
                }
            }
            else
            {
                memoryStream = (MemoryStream)cosmosSerializer.ToStream<IList<ToDoItem>>(items);
            }

            long responseLengthBytes = memoryStream.Length;

            IJsonNavigator jsonNavigator = JsonNavigator.Create(memoryStream.ToArray());
            IJsonNavigatorNode jsonNavigatorNode = jsonNavigator.GetRootNode();
            CosmosArray cosmosArray = CosmosArray.Create(jsonNavigator, jsonNavigatorNode);
            IReadOnlyCollection<QueryPageDiagnostics> diagnostics = new List<QueryPageDiagnostics>()
            {
                new QueryPageDiagnostics("0",
                "SomeQueryMetricText",
                "SomeIndexUtilText",
                new PointOperationStatistics(
                    activityId: Guid.NewGuid().ToString(),
                    statusCode: HttpStatusCode.OK,
                    subStatusCode: SubStatusCodes.Unknown,
                    requestCharge: 4,
                    errorMessage: null,
                    method: HttpMethod.Post,
                    requestUri: new Uri("http://localhost.com"),
                    clientSideRequestStatistics: null),
                new SchedulingStopwatch())
            };

            QueryResponseCore message = QueryResponseCore.CreateSuccess(
                    result: cosmosArray,
                    requestCharge: 4,
                    activityId: Guid.NewGuid().ToString(),
                    diagnostics: diagnostics,
                    responseLengthBytes: responseLengthBytes,
                    disallowContinuationTokenMessage: null,
                    continuationToken: continuationToken);

            return message;
        }

        public static QueryResponseCore CreateFailureResponse(
            HttpStatusCode httpStatusCode,
            SubStatusCodes subStatusCodes,
            string errorMessage)
        {
            IReadOnlyCollection<QueryPageDiagnostics> diagnostics = new List<QueryPageDiagnostics>()
            {
                new QueryPageDiagnostics("0",
                "SomeQueryMetricText",
                "SomeIndexUtilText",
                new PointOperationStatistics(
                    Guid.NewGuid().ToString(),
                    System.Net.HttpStatusCode.Gone,
                    subStatusCode: SubStatusCodes.PartitionKeyRangeGone,
                    requestCharge: 10.4,
                    errorMessage: null,
                    method: HttpMethod.Post,
                    requestUri: new Uri("http://localhost.com"),
                    clientSideRequestStatistics: null),
                new SchedulingStopwatch())
            };

            QueryResponseCore splitResponse = QueryResponseCore.CreateFailure(
               statusCode: httpStatusCode,
               subStatusCodes: subStatusCodes,
               errorMessage: errorMessage,
               requestCharge: 10.4,
               activityId: Guid.NewGuid().ToString(),
               diagnostics: diagnostics);

            return splitResponse;
        }

        public static QueryResponseCore CreateFailureToManyRequestResponse()
        {
            // 429 do not have an error message
            return CreateFailureResponse(
                (HttpStatusCode)429,
                SubStatusCodes.Unknown,
                null);
        }

        public static QueryResponseCore CreateSplitResponse(string collectionRid)
        {
            return CreateFailureResponse(
                HttpStatusCode.Gone,
                SubStatusCodes.PartitionKeyRangeGone,
                "Partition split error");
        }

        private static MemoryStream SerializeForOrderByQuery(IList<ToDoItem> items)
        {
            OrderByReturnStructure[] payload = items.Select(item => new OrderByReturnStructure()
            {
                _rid = item._rid,
                payload = item,
                orderByItems = new OrderbyItems[] { new OrderbyItems(item.id) }
            }).ToArray();

            return (MemoryStream)cosmosSerializer.ToStream<OrderByReturnStructure[]>(payload);
        }

        private class OrderByReturnStructure
        {
            public string _rid { get; set; }
            public ToDoItem payload { get; set; }
            public OrderbyItems[] orderByItems { get; set; }
        }

        private class OrderbyItems
        {
            public OrderbyItems(string item)
            {
                this.item = item;
            }

            public string item { get; set; }
        }
    }
}
