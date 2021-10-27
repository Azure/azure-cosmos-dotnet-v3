﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using CpuMonitor = Microsoft.Azure.Documents.Rntbd.CpuMonitor;
    using CpuLoadHistory = Microsoft.Azure.Documents.Rntbd.CpuLoadHistory;

    [TestClass]
    public class TransportWrapperTests
    {
        [TestMethod]
        public async Task TransportInterceptorContractTest()
        {
            CosmosClient cosmosClient = TestCommon.CreateCosmosClient(
                builder =>
                {
                    builder.WithTransportClientHandlerFactory(transportClient => new TransportClientWrapper(transportClient, TransportWrapperTests.Interceptor));
                });

            Cosmos.Database database = await cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());
            Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id");

            string id1 = Guid.NewGuid().ToString();
            TestPayload payload1 = await container.CreateItemAsync<TestPayload>(new TestPayload { id = id1 });
            payload1 = await container.ReadItemAsync<TestPayload>(id1, new Cosmos.PartitionKey(id1));
        }

        [TestMethod]
        public async Task TransportExceptionValidationTest()
        {
            CosmosClient cosmosClient = TestCommon.CreateCosmosClient(
                builder =>
                {
                    builder.WithTransportClientHandlerFactory(transportClient => new TransportClientWrapper(
                        transportClient,
                        TransportWrapperTests.ThrowTransportExceptionOnItemOperation));
                });

            Cosmos.Database database = await cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());
            Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id");

            try
            {
                TestPayload payload1 = await container.CreateItemAsync<TestPayload>(new TestPayload { id = "bad" }, new Cosmos.PartitionKey("bad"));
                Assert.Fail("Create item should fail with TransportException");
            }
            catch (CosmosException ce)
            {
                this.ValidateTransportException(ce);
            }

            try
            {
                FeedIterator<TestPayload> feedIterator = container.GetItemQueryIterator<TestPayload>("select * from T where T.Random = 19827 ");
                await feedIterator.ReadNextAsync();
                Assert.Fail("Create item should fail with TransportException");
            }
            catch (CosmosException ce)
            {
                this.ValidateTransportException(ce);
            }

            using (ResponseMessage responseMessage = await container.CreateItemStreamAsync(
                TestCommon.SerializerCore.ToStream(new TestPayload { id = "bad" }),
                new Cosmos.PartitionKey("bad")))
            {
                this.ValidateTransportException(responseMessage);
            }

            FeedIterator streamIterator = container.GetItemQueryStreamIterator("select * from T where T.Random = 19827 ");
            using (ResponseMessage responseMessage = await streamIterator.ReadNextAsync())
            {
                this.ValidateTransportException(responseMessage);
            }   
        }

        private void ValidateTransportException(CosmosException cosmosException)
        {
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, cosmosException.StatusCode);
            string message = cosmosException.ToString();
            Assert.IsTrue(message.Contains("TransportException: A client transport error occurred: The connection failed"), "StoreResult Exception is missing");
            string diagnostics = cosmosException.Diagnostics.ToString();
            Assert.IsNotNull(diagnostics);
            Assert.IsTrue(diagnostics.Contains("TransportException: A client transport error occurred: The connection failed"));
        }

        private void ValidateTransportException(ResponseMessage responseMessage)
        {
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, responseMessage.StatusCode);
            string message = responseMessage.ErrorMessage;
            Assert.AreEqual(message, responseMessage.CosmosException.Message);
            Assert.IsTrue(message.Contains("ServiceUnavailable (503); Substatus: 0; ActivityId:"));
            Assert.IsTrue(message.Contains("Reason: (Message: Channel is closed"), "Should contain exception message");
            string diagnostics = responseMessage.Diagnostics.ToString();
            Assert.IsNotNull(diagnostics);
            Assert.IsTrue(diagnostics.Contains("TransportException: A client transport error occurred: The connection failed"));
        }

        private static void Interceptor(
            Uri physicalAddress,
            ResourceOperation resourceOperation,
            DocumentServiceRequest request)
        {
            if (request.RequestContext?.RegionName != null)
            {
                Trace.TraceInformation($"Got {request.RequestContext?.RegionName} as region name for {physicalAddress}");
            }

            if (resourceOperation.resourceType == ResourceType.Document)
            {
                Assert.IsNotNull(request.RequestContext.RegionName);
                if (resourceOperation.operationType == OperationType.Create)
                {
                    Assert.IsTrue(request.RequestContext.ClientRequestStatistics.ContactedReplicas.Count > 1);
                }
                else
                {
                    Assert.AreEqual(0, request.RequestContext.ClientRequestStatistics.ContactedReplicas.Count);
                }
            }
        }

        private static void ThrowTransportExceptionOnItemOperation(
                Uri physicalAddress,
                ResourceOperation resourceOperation,
                DocumentServiceRequest request)
        {
            if (request.ResourceType == ResourceType.Document)
            {
                TransportException transportException = new TransportException(
                    errorCode: TransportErrorCode.ConnectionBroken,
                    innerException: null,
                    activityId: Guid.NewGuid(),
                    requestUri: physicalAddress,
                    sourceDescription: "SourceDescription",
                    userPayload: true,
                    payloadSent: false);

                DocumentClientException documentClientException = new DocumentClientException(
                    message: "Exception",
                    innerException: transportException,
                    statusCode: System.Net.HttpStatusCode.Gone);
                IClientSideRequestStatistics requestStatistics = request.RequestContext.ClientRequestStatistics;
                requestStatistics.RecordResponse(
                    request, 
                    new StoreResult(
                        storeResponse: null,
                        exception: documentClientException,
                        partitionKeyRangeId: "PkRange",
                        lsn: 42,
                        quorumAckedLsn: 4242,
                        requestCharge: 9000.42,
                        currentReplicaSetSize: 3,
                        currentWriteQuorum: 4,
                        isValid: true,
                        storePhysicalAddress: physicalAddress,
                        globalCommittedLSN: 2,
                        numberOfReadRegions: 1,
                        itemLSN: 5,
                        sessionToken: null,
                        usingLocalLSN: true,
                        activityId: Guid.NewGuid().ToString(),
                        backendRequestDurationInMs: "0",
                        retryAfterInMs: "42",
                        transportRequestStats: new TransportRequestStats()),
                    DateTime.MinValue,
                    DateTime.MaxValue);

                throw Documents.Rntbd.TransportExceptions.GetServiceUnavailableException(physicalAddress, Guid.NewGuid(),
                    transportException);
            }
        }

        private class TestPayload
        {
            public string id { get; set; }
        }

        internal class TransportClientWrapper : TransportClient
        {
            private readonly TransportClient baseClient;
            private readonly Action<Uri, ResourceOperation, DocumentServiceRequest> interceptor;
            private const string emptyCpuLoadHistoryText = "empty";

            internal TransportClientWrapper(
                TransportClient client,
                Action<Uri, ResourceOperation, DocumentServiceRequest> interceptor)
            {
                Debug.Assert(client != null);
                Debug.Assert(interceptor != null);


                this.baseClient = client;
                this.interceptor = interceptor;
            }

            internal override async Task<StoreResponse> InvokeStoreAsync(
                Uri physicalAddress,
                ResourceOperation resourceOperation,
                DocumentServiceRequest request)
            {
                this.interceptor(physicalAddress, resourceOperation, request);

                StoreResponse response = await this.baseClient.InvokeStoreAsync(physicalAddress, resourceOperation, request);
                return response;
            }

            public override void Dispose()
            {
                base.Dispose();
            }
        }
    }
}
