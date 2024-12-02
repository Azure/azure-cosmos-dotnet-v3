//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal static class MockRequestHelper
    {
        internal static readonly byte[] testItemResponsePayloadText;
        internal static readonly byte[] testItemResponsePayloadBinary;
        internal static readonly byte[] testItemFeedResponsePayloadText;
        internal static readonly byte[] testItemFeedResponsePayloadBinary;
        internal static readonly BatchResponsePayloadWriter batchResponsePayloadWriter;
        internal static int pagenumber;

        internal static readonly byte[] notFoundPayload = Encoding.ASCII.GetBytes("{\"Errors\":[\"Resource Not Found.Learn more: https://aka.ms/cosmosdb-tsg-not-found\"]}");

        static MockRequestHelper()
        {
            // Load the test item
            string payloadContent = File.ReadAllText("samplepayload.json");
            ToDoActivity testItem = JsonConvert.DeserializeObject<ToDoActivity>(payloadContent);

            // Generate text format payload
            using (MemoryStream ms = new MemoryStream())
            using (StreamWriter sw = new StreamWriter(ms, new UTF8Encoding(false, true), 1024, true))
            using (Newtonsoft.Json.JsonWriter writer = new Newtonsoft.Json.JsonTextWriter(sw))
            {
                writer.Formatting = Newtonsoft.Json.Formatting.None;
                Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
                serializer.Serialize(writer, testItem);
                writer.Flush();
                sw.Flush();
                testItemResponsePayloadText = ms.ToArray();
            }

            // Generate binary format payload
            using (CosmosDBToNewtonsoftWriter writer = new CosmosDBToNewtonsoftWriter(JsonSerializationFormat.Binary))
            {
                writer.Formatting = Newtonsoft.Json.Formatting.None;
                Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
                serializer.Serialize(writer, testItem);
                testItemResponsePayloadBinary = writer.GetResult().ToArray();
            }

            // For testItemFeedResponsePayload
            // Create a feed response with multiple items
            List<ToDoActivity> items = new List<ToDoActivity> { testItem };

            // Generate text format feed payload
            using (MemoryStream ms = new MemoryStream())
            using (StreamWriter sw = new StreamWriter(ms, new UTF8Encoding(false, true), 1024, true))
            using (Newtonsoft.Json.JsonWriter writer = new Newtonsoft.Json.JsonTextWriter(sw))
            {
                writer.Formatting = Newtonsoft.Json.Formatting.None;
                Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
                serializer.Serialize(writer, items);
                writer.Flush();
                sw.Flush();
                testItemFeedResponsePayloadText = ms.ToArray();
            }

            // Generate binary format feed payload
            using (CosmosDBToNewtonsoftWriter writer = new CosmosDBToNewtonsoftWriter(JsonSerializationFormat.Binary))
            {
                writer.Formatting = Newtonsoft.Json.Formatting.None;
                Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
                serializer.Serialize(writer, items);
                testItemFeedResponsePayloadBinary = writer.GetResult().ToArray();
            }

            // Initialize the batch response payload writer with an empty list
            batchResponsePayloadWriter = new BatchResponsePayloadWriter(new List<TransactionalBatchOperationResult>());
            batchResponsePayloadWriter.PrepareAsync().GetAwaiter().GetResult();

            pagenumber = 0;
        }

        /// <summary>
        /// For mocking a Gateway response
        /// </summary>
        /// <param name="request">The <see cref="DocumentServiceRequest"/> instance.</param>
        /// <returns>A <see cref="DocumentServiceResponse"/> instance.</returns>
        public static DocumentServiceResponse GetDocumentServiceResponse(DocumentServiceRequest request)
        {
            StoreResponseNameValueCollection headers = MockRequestHelper.GenerateTestHeaders();

            // Determine response serialization format
            JsonSerializationFormat responseSerializationFormat = GetResponseSerializationFormat(request);

            byte[] responsePayload;
            byte[] feedResponsePayload;

            if (responseSerializationFormat == JsonSerializationFormat.Binary)
            {
                responsePayload = testItemResponsePayloadBinary;
                feedResponsePayload = testItemFeedResponsePayloadBinary;
                headers[HttpConstants.HttpHeaders.ContentSerializationFormat] = ((int)JsonSerializationFormat.Binary).ToString();
            }
            else
            {
                responsePayload = testItemResponsePayloadText;
                feedResponsePayload = testItemFeedResponsePayloadText;
                headers[HttpConstants.HttpHeaders.ContentSerializationFormat] = ((int)JsonSerializationFormat.Text).ToString();
            }

            if (request.OperationType == OperationType.Read)
            {
                if (request.ResourceAddress.EndsWith(MockedItemBenchmarkHelper.ExistingItemId))
                {
                    return new DocumentServiceResponse(
                        new MemoryStream(responsePayload),
                        headers,
                        System.Net.HttpStatusCode.OK
                    );
                }

                return new DocumentServiceResponse(
                    new MemoryStream(MockRequestHelper.notFoundPayload),
                    headers,
                    System.Net.HttpStatusCode.NotFound
                );
            }

            if (request.OperationType == OperationType.Delete)
            {
                if (request.ResourceAddress.EndsWith(MockedItemBenchmarkHelper.ExistingItemId))
                {
                    return new DocumentServiceResponse(
                        new MemoryStream(responsePayload),
                        headers,
                        System.Net.HttpStatusCode.OK
                    );
                }

                return new DocumentServiceResponse(
                    new MemoryStream(MockRequestHelper.notFoundPayload),
                    headers,
                    System.Net.HttpStatusCode.NotFound
                );
            }

            if (request.OperationType == OperationType.Create
                || request.OperationType == OperationType.Replace
                || request.OperationType == OperationType.Upsert
                || request.OperationType == OperationType.Patch)
            {
                return new DocumentServiceResponse(
                        new MemoryStream(responsePayload),
                        headers,
                        System.Net.HttpStatusCode.OK
                    );
            }

            if (request.ResourceType == ResourceType.Document &&
                request.OperationType == OperationType.ReadFeed)
            {
                headers.ItemCount = "1";

                return new DocumentServiceResponse(
                    new MemoryStream(feedResponsePayload),
                    headers,
                    System.Net.HttpStatusCode.OK
                );
            }

            if (request.OperationType == OperationType.Batch)
            {
                MemoryStream responseContent = batchResponsePayloadWriter.GeneratePayload();

                return new DocumentServiceResponse(
                    responseContent,
                    headers,
                    System.Net.HttpStatusCode.OK
                );
            }

            // Return a default response if none of the conditions match
            return new DocumentServiceResponse(
                new MemoryStream(responsePayload),
                headers,
                System.Net.HttpStatusCode.OK
            );
        }

        /// <summary>
        /// For mocking a TransportClient response
        /// </summary>
        /// <param name="request">The <see cref="DocumentServiceRequest"/> instance.</param>
        /// <returns>A <see cref="StoreResponse"/> instance.</returns>
        public static StoreResponse GetStoreResponse(DocumentServiceRequest request)
        {
            // Determine response serialization format
            JsonSerializationFormat responseSerializationFormat = GetResponseSerializationFormat(request);

            byte[] responsePayload;
            byte[] feedResponsePayload;

            if (responseSerializationFormat == JsonSerializationFormat.Binary)
            {
                responsePayload = testItemResponsePayloadBinary;
                feedResponsePayload = testItemFeedResponsePayloadBinary;
            }
            else
            {
                responsePayload = testItemResponsePayloadText;
                feedResponsePayload = testItemFeedResponsePayloadText;
            }

            if (request.ResourceType == ResourceType.Document &&
                request.OperationType == OperationType.Query)
            {
                StoreResponseNameValueCollection queryHeaders = new StoreResponseNameValueCollection()
                {
                    ActivityId = Guid.NewGuid().ToString(),
                    BackendRequestDurationMilliseconds = "1.42",
                    CurrentReplicaSetSize = "1",
                    CurrentWriteQuorum = "1",
                    CurrentResourceQuotaUsage = "documentSize=0;documentsSize=1;documentsCount=1;collectionSize=1;",
                    GlobalCommittedLSN = "-1",
                    ItemCount = "1",
                    LSN = "2540",
                    LocalLSN = "2540",
                    LastStateChangeUtc = "Wed, 18 Aug 2021 20:30:05.117 GMT",
                    MaxResourceQuota = "documentSize=10240;documentsSize=10485760;documentsCount=-1;collectionSize=10485760;",
                    NumberOfReadRegions = "0",
                    OwnerFullName = "dbs/f4ac3cfd-dd38-4adb-b2d2-be97b3efbd1b/colls/2a926112-a26e-4935-ac6a-66df269c890d",
                    OwnerId = "GHRtAJahWQ4=",
                    PartitionKeyRangeId = "0",
                    PendingPKDelete = "false",
                    QueryExecutionInfo = "{\"reverseRidEnabled\":false,\"reverseIndexScan\":false}",
                    QueryMetrics = "totalExecutionTimeInMs=0.78;queryCompileTimeInMs=0.26;queryLogicalPlanBuildTimeInMs=0.04;queryPhysicalPlanBuildTimeInMs=0.18;queryOptimizationTimeInMs=0.01;VMExecutionTimeInMs=0.04;indexLookupTimeInMs=0.00;documentLoadTimeInMs=0.02;systemFunctionExecuteTimeInMs=0.00;userFunctionExecuteTimeInMs=0.00;retrievedDocumentCount=1;retrievedDocumentSize=671;outputDocumentCount=1;outputDocumentSize=720;writeOutputTimeInMs=0.00;indexUtilizationRatio=1.00",
                    QuorumAckedLSN = "2540" ,
                    QuorumAckedLocalLSN = "2540",
                    RequestCharge = "2.27" ,
                    SchemaVersion = "1.12",
                    ServerVersion = " version=2.14.0.0",
                    SessionToken = "0:-1#2540",
                    TransportRequestID = "2",
                    XPRole = "0"
                };

                queryHeaders.ItemCount = "1";

                // Multipage Scenario
                if (!request.Headers.Get(HttpConstants.HttpHeaders.PageSize).Equals("1000"))
                {
                    pagenumber++;

                    // return only 5 pages
                    queryHeaders.Continuation = pagenumber <= 5 ? "dummyToken" : null;

                    if (pagenumber > 5)
                    {
                        pagenumber = 0;
                    }

                    return new StoreResponse()
                    {
                        ResponseBody = new MemoryStream(feedResponsePayload, 0, feedResponsePayload.Length, writable: false, publiclyVisible: true),
                        Status = (int)System.Net.HttpStatusCode.OK,
                        Headers = queryHeaders,
                    };
                }

                return new StoreResponse()
                {
                    ResponseBody = new MemoryStream(feedResponsePayload, 0, feedResponsePayload.Length, writable: false, publiclyVisible: true),
                    Status = (int)System.Net.HttpStatusCode.OK,
                    Headers = queryHeaders,
                };
            }

            StoreResponseNameValueCollection headers = MockRequestHelper.GenerateTestHeaders();

#pragma warning disable IDE0045 // Convert to conditional expression
            if (responseSerializationFormat == JsonSerializationFormat.Binary)
            {
                headers[HttpConstants.HttpHeaders.ContentSerializationFormat] = ((int)JsonSerializationFormat.Binary).ToString();
            }
            else
            {
                headers[HttpConstants.HttpHeaders.ContentSerializationFormat] = ((int)JsonSerializationFormat.Text).ToString();
            }
#pragma warning restore IDE0045 // Convert to conditional expression

            if (request.OperationType == OperationType.Read)
            {
                headers.Add(WFConstants.BackendHeaders.LSN, "1");
                if (request.ResourceAddress.EndsWith(MockedItemBenchmarkHelper.ExistingItemId))
                {
                    return new StoreResponse()
                    {
                        ResponseBody = new MemoryStream(responsePayload, 0, responsePayload.Length, writable: false, publiclyVisible: true),
                        Status = (int)System.Net.HttpStatusCode.OK,
                        Headers = headers,
                    };
                }

                return new StoreResponse()
                {
                    ResponseBody = new MemoryStream(MockRequestHelper.notFoundPayload, 0, MockRequestHelper.notFoundPayload.Length, writable: false, publiclyVisible: true),
                    Status = (int)System.Net.HttpStatusCode.NotFound,
                    Headers = headers,
                };
            }

            if (request.OperationType == OperationType.Delete)
            {
                if (request.ResourceAddress.EndsWith(MockedItemBenchmarkHelper.ExistingItemId))
                {
                    return new StoreResponse()
                    {
                        ResponseBody = new MemoryStream(responsePayload, 0, responsePayload.Length, writable: false, publiclyVisible: true),
                        Status = (int)System.Net.HttpStatusCode.OK,
                        Headers = headers,
                    };
                }

                return new StoreResponse()
                {
                    ResponseBody = new MemoryStream(MockRequestHelper.notFoundPayload, 0, MockRequestHelper.notFoundPayload.Length, writable: false, publiclyVisible: true),
                    Status = (int)System.Net.HttpStatusCode.NotFound,
                    Headers = headers,
                };
            }

            if (request.OperationType == OperationType.Create
                || request.OperationType == OperationType.Replace
                || request.OperationType == OperationType.Upsert
                || request.OperationType == OperationType.Patch)
            {
                return new StoreResponse()
                {
                    ResponseBody = new MemoryStream(responsePayload, 0, responsePayload.Length, writable: false, publiclyVisible: true),
                    Status = (int)System.Net.HttpStatusCode.OK,
                    Headers = headers,
                };
            }

            if (request.ResourceType == ResourceType.Document &&
                request.OperationType == OperationType.ReadFeed)
            {
                headers.ItemCount = "1";
                return new StoreResponse()
                {
                    ResponseBody = new MemoryStream(feedResponsePayload, 0, feedResponsePayload.Length, writable: false, publiclyVisible: true),
                    Status = (int)System.Net.HttpStatusCode.OK,
                    Headers = headers,
                };
            }

            if (request.OperationType == OperationType.Batch)
            {
                MemoryStream responseContent = batchResponsePayloadWriter.GeneratePayload();

                return new StoreResponse()
                {
                    ResponseBody = responseContent,
                    Status = (int)System.Net.HttpStatusCode.OK,
                    Headers = headers,
                };
            }

            // Return a default response if none of the conditions match
            return new StoreResponse()
            {
                ResponseBody = new MemoryStream(responsePayload, 0, responsePayload.Length, writable: false, publiclyVisible: true),
                Status = (int)System.Net.HttpStatusCode.OK,
                Headers = headers,
            };
        }

        private static JsonSerializationFormat GetResponseSerializationFormat(DocumentServiceRequest request)
        {
            string supportedSerializationFormats = request.Headers[HttpConstants.HttpHeaders.SupportedSerializationFormats];

            JsonSerializationFormat responseSerializationFormat = JsonSerializationFormat.Text;

            if (!string.IsNullOrEmpty(supportedSerializationFormats) 
                && supportedSerializationFormats.Contains(SupportedSerializationFormats.CosmosBinary.ToString())
                && request.OperationType.IsPointOperation())
            {
                responseSerializationFormat = JsonSerializationFormat.Binary;
            }

            return responseSerializationFormat;
        }

        private static StoreResponseNameValueCollection GenerateTestHeaders()
        {
            StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
            for (int i = 0; i < 15; i++)
            {
                string random = Guid.NewGuid().ToString();
                headers[random] = random;
            }

            return headers;
        }
    }
}