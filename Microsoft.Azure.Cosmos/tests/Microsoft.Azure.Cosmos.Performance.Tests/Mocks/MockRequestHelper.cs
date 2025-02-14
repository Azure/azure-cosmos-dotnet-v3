//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal static class MockRequestHelper
    {
        internal static readonly byte[] testItemResponsePayload;
        internal static readonly byte[] testItemResponsePayloadBinary;
        internal static readonly byte[] testItemFeedResponsePayload;
        internal static readonly byte[] testItemFeedResponsePayloadBinary;
        internal static readonly BatchResponsePayloadWriter batchResponsePayloadWriter;
        internal static int pagenumber;

        internal static readonly byte[] notFoundPayload = Encoding.ASCII.GetBytes("{\"Errors\":[\"Resource Not Found.Learn more: https:\\/\\/ aka.ms\\/ cosmosdb - tsg - not - found\"]}");

        private static readonly string BinarySerializationFormat = SupportedSerializationFormats.CosmosBinary.ToString();

        static MockRequestHelper()
        {
            MemoryStream ms = new MemoryStream();
            using (FileStream fs = File.OpenRead("samplepayload.json"))
            {
                fs.CopyTo(ms);
                MockRequestHelper.testItemResponsePayload = ms.ToArray();
            }

            testItemResponsePayloadBinary = ConvertOnceToBinary(testItemResponsePayload);

            ms = new MemoryStream();
            using (FileStream fs = File.OpenRead("samplefeedpayload.json"))
            {
                fs.CopyTo(ms);
                MockRequestHelper.testItemFeedResponsePayload = ms.ToArray();
            }

            testItemFeedResponsePayloadBinary = ConvertOnceToBinary(testItemFeedResponsePayload);

            List<TransactionalBatchOperationResult> results = new List<TransactionalBatchOperationResult>
        {
            new TransactionalBatchOperationResult(System.Net.HttpStatusCode.OK)
            {
                ResourceStream = new MemoryStream(MockRequestHelper.testItemFeedResponsePayload, 0, MockRequestHelper.testItemFeedResponsePayload.Length, writable: false, publiclyVisible: true),
                ETag = Guid.NewGuid().ToString()
            }
        };

            batchResponsePayloadWriter = new BatchResponsePayloadWriter(results);
            batchResponsePayloadWriter.PrepareAsync().GetAwaiter().GetResult();

            pagenumber = 0;
        }

        /// <summary>
        /// For mocking a Gateway response
        /// </summary>
        /// <param name="request">The <see cref="DocumentServiceRequest"/> instance.</param>
        /// <returns>A <see cref="DocumentServiceResponse"/> instance, or null if unhandled.</returns>
        public static DocumentServiceResponse GetDocumentServiceResponse(DocumentServiceRequest request)
        {
            StoreResponseNameValueCollection headers = MockRequestHelper.GenerateTestHeaders();

            // We'll store the chosen payload + status code
            byte[] payload = null;
            System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.OK;

            // READ
            if (request.OperationType == OperationType.Read)
            {
                if (request.ResourceAddress.EndsWith(MockedItemBenchmarkHelper.ExistingItemId))
                {
                    payload = testItemResponsePayload;
                    statusCode = System.Net.HttpStatusCode.OK;
                }
                else
                {
                    payload = notFoundPayload;
                    statusCode = System.Net.HttpStatusCode.NotFound;
                }
            }
            // DELETE
            else if (request.OperationType == OperationType.Delete)
            {
                if (request.ResourceAddress.EndsWith(MockedItemBenchmarkHelper.ExistingItemId))
                {
                    payload = testItemResponsePayload;
                    statusCode = System.Net.HttpStatusCode.OK;
                }
                else
                {
                    payload = notFoundPayload;
                    statusCode = System.Net.HttpStatusCode.NotFound;
                }
            }
            // CREATE / REPLACE / UPSERT / PATCH
            else if (request.OperationType == OperationType.Create
                || request.OperationType == OperationType.Replace
                || request.OperationType == OperationType.Upsert
                || request.OperationType == OperationType.Patch)
            {
                payload = testItemResponsePayload;
                statusCode = System.Net.HttpStatusCode.OK;
            }
            // READ FEED
            else if (request.ResourceType == ResourceType.Document &&
                request.OperationType == OperationType.ReadFeed)
            {
                payload = testItemFeedResponsePayload;
                statusCode = System.Net.HttpStatusCode.OK;
            }

            // If still null, it's an unhandled scenario
            if (payload == null)
            {
                return null;
            }

            // Check if binary is requested
            if (request.Headers.Get(HttpConstants.HttpHeaders.SupportedSerializationFormats).Equals(BinarySerializationFormat))
            {
                payload = (request.OperationType == OperationType.ReadFeed)
                    ? testItemFeedResponsePayloadBinary
                    : testItemResponsePayloadBinary;

                headers[HttpConstants.HttpHeaders.SupportedSerializationFormats] = BinarySerializationFormat;
            }

            DocumentServiceResponse response = new DocumentServiceResponse(
                new MemoryStream(payload, 0, payload.Length, writable: false, publiclyVisible: true),
                headers,
                statusCode);

            return response;
        }

        /// <summary>
        /// For mocking a TransportClient response/
        /// </summary>
        /// <param name="request">The <see cref="DocumentServiceRequest"/> instance.</param>
        /// <returns>A <see cref="StoreResponse"/> instance.</returns>
        public static StoreResponse GetStoreResponse(DocumentServiceRequest request)
        {
            StoreResponse response = null;

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
                        ResponseBody = new MemoryStream(testItemFeedResponsePayload, 0, testItemFeedResponsePayload.Length, writable: false, publiclyVisible: true),
                        Status = (int)System.Net.HttpStatusCode.OK,
                        Headers = queryHeaders,
                    };
                }

                return new StoreResponse()
                {
                    ResponseBody = new MemoryStream(testItemFeedResponsePayload, 0, testItemFeedResponsePayload.Length, writable: false, publiclyVisible: true),
                    Status = (int)System.Net.HttpStatusCode.OK,
                    Headers = queryHeaders,
                };
            }

            StoreResponseNameValueCollection headers = MockRequestHelper.GenerateTestHeaders();

            if (request.OperationType == OperationType.Read)
            {
                headers.Add(WFConstants.BackendHeaders.LSN, "1");
                response = request.ResourceAddress.EndsWith(MockedItemBenchmarkHelper.ExistingItemId)
                    ? new StoreResponse()
                    {
                        ResponseBody = new MemoryStream(testItemResponsePayload, 0, testItemResponsePayload.Length, writable: false, publiclyVisible: true),
                        Status = (int)System.Net.HttpStatusCode.OK,
                        Headers = headers,
                    }
                    : new StoreResponse()
                    {
                        ResponseBody = new MemoryStream(notFoundPayload, 0, notFoundPayload.Length, writable: false, publiclyVisible: true),
                        Status = (int)System.Net.HttpStatusCode.NotFound,
                        Headers = headers,
                    };
            }
            else if (request.OperationType == OperationType.Delete)
            {
                response = request.ResourceAddress.EndsWith(MockedItemBenchmarkHelper.ExistingItemId)
                    ? new StoreResponse()
                    {
                        ResponseBody = new MemoryStream(testItemResponsePayload, 0, testItemResponsePayload.Length, writable: false, publiclyVisible: true),
                        Status = (int)System.Net.HttpStatusCode.OK,
                        Headers = headers,
                    }
                    : new StoreResponse()
                    {
                        ResponseBody = new MemoryStream(notFoundPayload, 0, notFoundPayload.Length, writable: false, publiclyVisible: true),
                        Status = (int)System.Net.HttpStatusCode.NotFound,
                        Headers = headers,
                    };
            }
            else if (request.OperationType == OperationType.Create
                || request.OperationType == OperationType.Replace
                || request.OperationType == OperationType.Upsert
                || request.OperationType == OperationType.Patch)
            {
                response = new StoreResponse()
                {
                    ResponseBody = new MemoryStream(testItemResponsePayload, 0, testItemResponsePayload.Length, writable: false, publiclyVisible: true),
                    Status = (int)System.Net.HttpStatusCode.OK,
                    Headers = headers,
                };
            }
            else if (request.ResourceType == ResourceType.Document && request.OperationType == OperationType.ReadFeed)
            {
                headers.ItemCount = "1";
                response = new StoreResponse()
                {
                    ResponseBody = new MemoryStream(testItemFeedResponsePayload, 0, testItemFeedResponsePayload.Length, writable: false, publiclyVisible: true),
                    Status = (int)System.Net.HttpStatusCode.OK,
                    Headers = headers,
                };
            }
            else if (request.OperationType == OperationType.Batch)
            {
                MemoryStream responseContent = batchResponsePayloadWriter.GeneratePayload();
                response = new StoreResponse()
                {
                    ResponseBody = responseContent,
                    Status = (int)System.Net.HttpStatusCode.OK,
                    Headers = headers,
                };
            }

            if (response == null)
            {
                return null;
            }

            if (request.Headers.Get(HttpConstants.HttpHeaders.SupportedSerializationFormats) == BinarySerializationFormat)
            {
                response.ResponseBody.Dispose();

                bool isFeed =
                    (request.ResourceType == ResourceType.Document && request.OperationType == OperationType.ReadFeed)
                    || (request.OperationType == OperationType.Query);

                byte[] binaryPayload = isFeed
                    ? testItemFeedResponsePayloadBinary
                    : testItemResponsePayloadBinary;

                response.ResponseBody = new MemoryStream(binaryPayload, 0, binaryPayload.Length, writable: false, publiclyVisible: true);

                response.Headers[HttpConstants.HttpHeaders.SupportedSerializationFormats] = BinarySerializationFormat;
            }

            return response;
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

        /// <summary>
        /// Deserializes the JSON payload into a ToDoActivity, then re-serializes as binary.
        /// </summary>
        private static byte[] ConvertOnceToBinary(byte[] textPayload)
        {
            using (MemoryStream textStream = new(textPayload))
            {
                CosmosJsonDotNetSerializer textSerializer = new();
                ToDoActivity deserialized = textSerializer.FromStream<ToDoActivity>(textStream)
                    ?? throw new InvalidOperationException("Deserialization returned null.");

                CosmosJsonDotNetSerializer binarySerializer = new(binaryEncodingEnabled: true);
                using (MemoryStream binaryStream = binarySerializer.ToStream(deserialized) as MemoryStream)
                {
                    return binaryStream.ToArray();
                }
            }
        }
    }
}