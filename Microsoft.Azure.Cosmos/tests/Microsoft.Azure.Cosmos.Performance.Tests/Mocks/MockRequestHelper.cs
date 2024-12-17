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
        internal static readonly byte[] testItemResponsePayload;
        internal static readonly byte[] testItemFeedResponsePayload;
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

            ms = new MemoryStream();
            using (FileStream fs = File.OpenRead("samplefeedpayload.json"))
            {
                fs.CopyTo(ms);
                MockRequestHelper.testItemFeedResponsePayload = ms.ToArray();
            }

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
        /// <returns>A <see cref="DocumentServiceResponse"/> instance.</returns>
        public static DocumentServiceResponse GetDocumentServiceResponse(DocumentServiceRequest request)
        {
            StoreResponseNameValueCollection headers = MockRequestHelper.GenerateTestHeaders();

            DocumentServiceResponse response;
            if (request.OperationType == OperationType.Read)
            {
#pragma warning disable IDE0045 // Convert to conditional expression
                if (request.ResourceAddress.EndsWith(MockedItemBenchmarkHelper.ExistingItemId))
                {
                    response = new DocumentServiceResponse(
                        new MemoryStream(MockRequestHelper.testItemResponsePayload),
                        headers,
                        System.Net.HttpStatusCode.OK
                    );
                }
                else
                {
                    response = new DocumentServiceResponse(
                        new MemoryStream(MockRequestHelper.notFoundPayload),
                        headers,
                        System.Net.HttpStatusCode.NotFound
                    );
                }
#pragma warning restore IDE0045 // Convert to conditional expression
            }
            else if (request.OperationType == OperationType.Delete)
            {
#pragma warning disable IDE0045 // Convert to conditional expression
                if (request.ResourceAddress.EndsWith(MockedItemBenchmarkHelper.ExistingItemId))
                {
                    response = new DocumentServiceResponse(
                        new MemoryStream(MockRequestHelper.testItemResponsePayload),
                        headers,
                        System.Net.HttpStatusCode.OK
                    );
                }
                else
                {
                    response = new DocumentServiceResponse(
                        new MemoryStream(MockRequestHelper.notFoundPayload),
                        headers,
                        System.Net.HttpStatusCode.NotFound
                    );
                }
#pragma warning restore IDE0045 // Convert to conditional expression
            }
            else if (request.OperationType == OperationType.Create
                || request.OperationType == OperationType.Replace
                || request.OperationType == OperationType.Upsert
                || request.OperationType == OperationType.Patch)
            {
                response = new DocumentServiceResponse(
                        new MemoryStream(MockRequestHelper.testItemResponsePayload),
                        headers,
                        System.Net.HttpStatusCode.OK
                    );
            }
            else if (request.ResourceType == ResourceType.Document &&
                request.OperationType == OperationType.ReadFeed)
            {
                response = new DocumentServiceResponse(
                        new MemoryStream(MockRequestHelper.testItemFeedResponsePayload),
                        headers,
                        System.Net.HttpStatusCode.OK
                    );
            }
            else
            {
                // If we reach here, it's an operation we are not explicitly handling
                return null;
            }

            // Check if binary encoding is enabled and this is a supported point operation
            if (ConfigurationManager.IsBinaryEncodingEnabled() && MockRequestHelper.IsPointOperationSupportedForBinaryEncoding(request))
            {
                response = ConvertToBinaryIfNeeded(response);
            }

            return response;
        }

        /// <summary>
        /// For mocking a TransportClient response
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
                }

                response = new StoreResponse()
                {
                    ResponseBody = new MemoryStream(MockRequestHelper.testItemFeedResponsePayload, 0, MockRequestHelper.testItemFeedResponsePayload.Length, writable: false, publiclyVisible: true),
                    Status = (int)System.Net.HttpStatusCode.OK,
                    Headers = queryHeaders,
                };

                // Query isn't a single-point operation like Create/Read/Replace etc., so no binary encoding here
                return response;
            }

            StoreResponseNameValueCollection headers = MockRequestHelper.GenerateTestHeaders();
            if (request.OperationType == OperationType.Read)
            {
                headers.Add(WFConstants.BackendHeaders.LSN, "1");
#pragma warning disable IDE0045 // Convert to conditional expression
                if (request.ResourceAddress.EndsWith(MockedItemBenchmarkHelper.ExistingItemId))
                {
                    response = new StoreResponse()
                    {
                        ResponseBody = new MemoryStream(MockRequestHelper.testItemResponsePayload, 0, MockRequestHelper.testItemResponsePayload.Length, writable: false, publiclyVisible: true),
                        Status = (int)System.Net.HttpStatusCode.OK,
                        Headers = headers,
                    };
                }
                else
                {
                    response = new StoreResponse()
                    {
                        ResponseBody = new MemoryStream(MockRequestHelper.notFoundPayload, 0, MockRequestHelper.notFoundPayload.Length, writable: false, publiclyVisible: true),
                        Status = (int)System.Net.HttpStatusCode.NotFound,
                        Headers = headers,
                    };
                }
#pragma warning restore IDE0045 // Convert to conditional expression
            }
            else if (request.OperationType == OperationType.Delete)
            {
#pragma warning disable IDE0045 // Convert to conditional expression
                if (request.ResourceAddress.EndsWith(MockedItemBenchmarkHelper.ExistingItemId))
                {
                    response = new StoreResponse()
                    {
                        ResponseBody = new MemoryStream(MockRequestHelper.testItemResponsePayload, 0, MockRequestHelper.testItemResponsePayload.Length, writable: false, publiclyVisible: true),
                        Status = (int)System.Net.HttpStatusCode.OK,
                        Headers = headers,
                    };
                }
                else
                {
                    response = new StoreResponse()
                    {
                        ResponseBody = new MemoryStream(MockRequestHelper.notFoundPayload, 0, MockRequestHelper.notFoundPayload.Length, writable: false, publiclyVisible: true),
                        Status = (int)System.Net.HttpStatusCode.NotFound,
                        Headers = headers,
                    };
                }
#pragma warning restore IDE0045 // Convert to conditional expression
            }
            else if (request.OperationType == OperationType.Create
                || request.OperationType == OperationType.Replace
                || request.OperationType == OperationType.Upsert
                || request.OperationType == OperationType.Patch)
            {
                response = new StoreResponse()
                {
                    ResponseBody = new MemoryStream(MockRequestHelper.testItemResponsePayload, 0, MockRequestHelper.testItemResponsePayload.Length, writable: false, publiclyVisible: true),
                    Status = (int)System.Net.HttpStatusCode.OK,
                    Headers = headers,
                };
            }
            else if (request.ResourceType == ResourceType.Document &&
                request.OperationType == OperationType.ReadFeed)
            {
                headers.ItemCount = "1";
                response = new StoreResponse()
                {
                    ResponseBody = new MemoryStream(MockRequestHelper.testItemFeedResponsePayload, 0, MockRequestHelper.testItemFeedResponsePayload.Length, writable: false, publiclyVisible: true),
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

            if (response != null && ConfigurationManager.IsBinaryEncodingEnabled() && MockRequestHelper.IsPointOperationSupportedForBinaryEncoding(request))
            {
                response = ConvertToBinaryIfNeeded(response);
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

        // Converts the response payload into binary if needed
        // This simulates binary serialization using the same approach as in MockedItemBenchmarkHelper.
        private static DocumentServiceResponse ConvertToBinaryIfNeeded(DocumentServiceResponse response)
        {
            byte[] originalPayload = ReadAllBytes(response.ResponseBody);
            byte[] binaryPayload = ReSerializeToBinary(originalPayload);

            // Replace stream with binary version
            response.ResponseBody.Dispose();
            response.ResponseBody = new MemoryStream(binaryPayload, writable: false);

            // Add binary serialization header
            response.Headers[HttpConstants.HttpHeaders.SupportedSerializationFormats] = BinarySerializationFormat;

            return response;
        }

        private static StoreResponse ConvertToBinaryIfNeeded(StoreResponse response)
        {
            byte[] originalPayload = ReadAllBytes(response.ResponseBody);
            byte[] binaryPayload = ReSerializeToBinary(originalPayload);

            // Replace stream with binary version
            response.ResponseBody.Dispose();
            response.ResponseBody = new MemoryStream(binaryPayload, writable: false);

            // Add binary serialization header
            response.Headers[HttpConstants.HttpHeaders.SupportedSerializationFormats] = BinarySerializationFormat;

            return response;
        }

        private static byte[] ReSerializeToBinary(byte[] textPayload)
        {
            // Deserialize from JSON text
            using (MemoryStream ms = new MemoryStream(textPayload))
            using (StreamReader sr = new StreamReader(ms))
            using (Newtonsoft.Json.JsonReader jr = new JsonTextReader(sr))
            {
                Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
                // Assuming the payload matches the ToDoActivity schema
                ToDoActivity deserialized = serializer.Deserialize<ToDoActivity>(jr);

                using (CosmosDBToNewtonsoftWriter writer = new CosmosDBToNewtonsoftWriter(JsonSerializationFormat.Binary))
                {
                    serializer.Serialize(writer, deserialized);
                    return writer.GetResult().ToArray();
                }
            }
        }

        private static byte[] ReadAllBytes(Stream stream)
        {
            if (stream is MemoryStream ms && ms.Position == 0)
            {
                return ms.ToArray();
            }

            using (MemoryStream copy = new MemoryStream())
            {
                stream.CopyTo(copy);
                return copy.ToArray();
            }
        }

        private static bool IsPointOperationSupportedForBinaryEncoding(DocumentServiceRequest request)
        {
            return request.ResourceType == ResourceType.Document
                && (request.OperationType == OperationType.Create
                    || request.OperationType == OperationType.Replace
                    || request.OperationType == OperationType.Delete
                    || request.OperationType == OperationType.Read
                    || request.OperationType == OperationType.Upsert);
        }
    }
}