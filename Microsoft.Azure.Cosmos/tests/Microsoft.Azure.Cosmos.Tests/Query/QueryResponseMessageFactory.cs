//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Documents;

    internal static class QueryResponseMessageFactory
    {
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
            MemoryStream memoryStream = (MemoryStream)MockCosmosUtil.Serializer.ToStream<IList<ToDoItem>>(items);
            long responseLengthBytes = memoryStream.Length;

            IJsonNavigator jsonNavigator = JsonNavigator.Create(memoryStream.ToArray());
            IJsonNavigatorNode jsonNavigatorNode = jsonNavigator.GetRootNode();
            CosmosArray cosmosArray = CosmosArray.Create(jsonNavigator, jsonNavigatorNode);

            double requestCharge = 42;
            string activityId = Guid.NewGuid().ToString();
            CosmosDiagnosticsContext diagnosticsContext = new CosmosDiagnosticsContextCore();
            diagnosticsContext.AddDiagnosticsInternal(new PointOperationStatistics(
                activityId: Guid.NewGuid().ToString(),
                statusCode: HttpStatusCode.OK,
                subStatusCode: SubStatusCodes.Unknown,
                responseTimeUtc: DateTime.UtcNow,
                requestCharge: requestCharge,
                errorMessage: null,
                method: HttpMethod.Post,
                requestUri: new Uri("http://localhost.com"),
                requestSessionToken: null,
                responseSessionToken: null));
            IReadOnlyCollection<QueryPageDiagnostics> diagnostics = new List<QueryPageDiagnostics>()
            {
                new QueryPageDiagnostics(
                    Guid.NewGuid(),
                    "0",
                    "SomeQueryMetricText",
                    "SomeIndexUtilText",
                    diagnosticsContext)
            };

            QueryResponseCore message = QueryResponseCore.CreateSuccess(
                    result: cosmosArray,
                    continuationToken: continuationToken,
                    disallowContinuationTokenMessage: null,
                    activityId: activityId,
                    requestCharge: requestCharge,
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
                using (StreamReader sr = new StreamReader(SerializeForOrderByQuery(items)))
                {
                    json = sr.ReadToEnd();
                }
            }
            else
            {
                memoryStream = (MemoryStream)MockCosmosUtil.Serializer.ToStream<IList<ToDoItem>>(items);
            }

            long responseLengthBytes = memoryStream.Length;

            IJsonNavigator jsonNavigator = JsonNavigator.Create(memoryStream.ToArray());
            IJsonNavigatorNode jsonNavigatorNode = jsonNavigator.GetRootNode();
            CosmosArray cosmosArray = CosmosArray.Create(jsonNavigator, jsonNavigatorNode);
            CosmosDiagnosticsContext diagnosticsContext = new CosmosDiagnosticsContextCore();
            diagnosticsContext.AddDiagnosticsInternal(new PointOperationStatistics(
                activityId: Guid.NewGuid().ToString(),
                statusCode: HttpStatusCode.OK,
                subStatusCode: SubStatusCodes.Unknown,
                responseTimeUtc: DateTime.UtcNow,
                requestCharge: 4,
                errorMessage: null,
                method: HttpMethod.Post,
                requestUri: new Uri("http://localhost.com"),
                requestSessionToken: null,
                responseSessionToken: null));
            IReadOnlyCollection<QueryPageDiagnostics> diagnostics = new List<QueryPageDiagnostics>()
            {
                new QueryPageDiagnostics(
                    Guid.NewGuid(),
                    "0",
                    "SomeQueryMetricText",
                    "SomeIndexUtilText",
                    diagnosticsContext)
            };

            QueryResponseCore message = QueryResponseCore.CreateSuccess(
                    result: cosmosArray,
                    requestCharge: 4,
                    activityId: Guid.NewGuid().ToString(),
                    responseLengthBytes: responseLengthBytes,
                    disallowContinuationTokenMessage: null,
                    continuationToken: continuationToken);

            return message;
        }

        public static QueryResponse<TItem> CreateQueryResponse<TItem>(
            QueryResponse queryResponse)
        {
            return QueryResponse<TItem>.CreateResponse<TItem>(queryResponse, MockCosmosUtil.Serializer);
        }

        public static QueryResponseCore CreateFailureResponse(
            HttpStatusCode httpStatusCode,
            SubStatusCodes subStatusCodes,
            string errorMessage)
        {
            string acitivityId = Guid.NewGuid().ToString();
            CosmosDiagnosticsContext diagnosticsContext = new CosmosDiagnosticsContextCore();
            diagnosticsContext.AddDiagnosticsInternal(new PointOperationStatistics(
                acitivityId,
                System.Net.HttpStatusCode.Gone,
                subStatusCode: SubStatusCodes.PartitionKeyRangeGone,
                responseTimeUtc: DateTime.UtcNow,
                requestCharge: 10.4,
                errorMessage: null,
                method: HttpMethod.Post,
                requestUri: new Uri("http://localhost.com"),
                requestSessionToken: null,
                responseSessionToken: null));
            IReadOnlyCollection<QueryPageDiagnostics> diagnostics = new List<QueryPageDiagnostics>()
            {
                new QueryPageDiagnostics(
                    Guid.NewGuid(),
                    "0",
                    "SomeQueryMetricText",
                    "SomeIndexUtilText",
                    diagnosticsContext)
            };

            QueryResponseCore splitResponse = QueryResponseCore.CreateFailure(
               statusCode: httpStatusCode,
               subStatusCodes: subStatusCodes,
               cosmosException: CosmosExceptionFactory.Create(
                   statusCode: httpStatusCode,
                   subStatusCode: (int)subStatusCodes,
                   message: errorMessage,
                   stackTrace: new System.Diagnostics.StackTrace().ToString(),
                   activityId: acitivityId,
                   requestCharge: 10.4,
                   retryAfter: default,
                   headers: default,
                   diagnosticsContext: diagnosticsContext,
                   error: default,
                   innerException: default),
               requestCharge: 10.4,
               activityId: acitivityId);

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

            return (MemoryStream)MockCosmosUtil.Serializer.ToStream<OrderByReturnStructure[]>(payload);
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
