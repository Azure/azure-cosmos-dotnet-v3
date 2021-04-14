//------------------------------------------------------------
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
        [DataRow(true)]
        [DataRow(false)]
        public async Task TransportExceptionValidationTest(bool injectCpuMonitor)
        {
            CosmosClient cosmosClient = TestCommon.CreateCosmosClient(
                builder =>
                {
                    builder.WithTransportClientHandlerFactory(transportClient => new TransportClientWrapper(
                        transportClient,
                        TransportWrapperTests.ThrowTransportExceptionOnItemOperation,
                        injectCpuMonitor));
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
                this.ValidateTransportException(ce, injectCpuMonitor);
            }

            try
            {
                FeedIterator<TestPayload> feedIterator = container.GetItemQueryIterator<TestPayload>("select * from T where T.Random = 19827 ");
                await feedIterator.ReadNextAsync();
                Assert.Fail("Create item should fail with TransportException");
            }
            catch (CosmosException ce)
            {
                this.ValidateTransportException(ce, injectCpuMonitor);
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

        private void ValidateTransportException(CosmosException cosmosException, bool cpuMonitorInjected)
        {
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, cosmosException.StatusCode);
            string message = cosmosException.ToString();
            Assert.IsTrue(message.Contains("TransportException: A client transport error occurred: The connection failed"), "StoreResult Exception is missing");
            string diagnostics = cosmosException.Diagnostics.ToString();
            Assert.IsNotNull(diagnostics);
            Assert.IsTrue(diagnostics.Contains("TransportException: A client transport error occurred: The connection failed"));
            if (cpuMonitorInjected)
            {
                const string cpuHistoryPrefix = "CPU history: (";
                int cpuHistoryIndex = diagnostics.IndexOf(cpuHistoryPrefix);
                Assert.AreNotEqual(-1, cpuHistoryIndex);
                int indexEndOfFirstCpuHistoryElement = diagnostics.IndexOf(
                    ")",
                    cpuHistoryIndex + cpuHistoryPrefix.Length);
                Assert.AreNotEqual(-1, indexEndOfFirstCpuHistoryElement);

                string firstCpuHistoryElement = diagnostics.Substring(
                    cpuHistoryIndex + cpuHistoryPrefix.Length,
                    indexEndOfFirstCpuHistoryElement - cpuHistoryIndex - cpuHistoryPrefix.Length);

                string[] cpuHistoryElementFragments = firstCpuHistoryElement.Split(' ');
                Assert.AreEqual(2, cpuHistoryElementFragments.Length);
                Assert.IsTrue(
                    DateTimeOffset.TryParse(cpuHistoryElementFragments[0], out DateTimeOffset snapshotTime));
                Assert.IsTrue(snapshotTime > DateTimeOffset.UtcNow - TimeSpan.FromMinutes(2));
                Assert.IsTrue(float.TryParse(cpuHistoryElementFragments[1], out float cpuPercentage));
                Assert.IsTrue(cpuPercentage >= 0 && cpuPercentage <= 100);
            }
            else
            {
                Assert.IsTrue(diagnostics.Contains("CPU history: not available"));
            }
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
                requestStatistics.RecordResponse(request, new StoreResult(
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
                    backendRequestDurationInMs: "0"));

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
            private readonly CpuMonitor cpuMonitor;
            private const string emptyCpuLoadHistoryText = "empty";

            internal TransportClientWrapper(
                TransportClient client,
                Action<Uri, ResourceOperation, DocumentServiceRequest> interceptor,
                bool injectCpuMonitor = false)
            {
                Debug.Assert(client != null);
                Debug.Assert(interceptor != null);

                if (injectCpuMonitor)
                {
                    CpuMonitor.OverrideRefreshInterval(TimeSpan.FromMilliseconds(100));
                    this.cpuMonitor = new CpuMonitor();
                    this.cpuMonitor.Start();
                    Stopwatch watch = Stopwatch.StartNew();

                    // Artifically burning some CPU to generate CPU load history
                    CpuLoadHistory cpuLoadHistory = null;
                    while ((cpuLoadHistory = this.cpuMonitor.GetCpuLoad()) == null ||
                        cpuLoadHistory.ToString() == emptyCpuLoadHistoryText)
                    {
                        Task.Delay(10).Wait();
                    }
                }
                this.baseClient = client;
                this.interceptor = interceptor;
            }

            internal override async Task<StoreResponse> InvokeStoreAsync(
                Uri physicalAddress,
                ResourceOperation resourceOperation,
                DocumentServiceRequest request)
            {
                try
                {
                    this.interceptor(physicalAddress, resourceOperation, request);
                }
                catch (DocumentClientException dce)
                {
                    if (this.cpuMonitor != null &&
                        dce.InnerException != null &&
                        dce.InnerException is TransportException te)
                    {
                        te.SetCpuLoad(this.cpuMonitor.GetCpuLoad());
                    }

                    throw;
                }

                StoreResponse response = await this.baseClient.InvokeStoreAsync(physicalAddress, resourceOperation, request);
                return response;
            }

            public override void Dispose()
            {
                if (this.cpuMonitor != null)
                {
                    this.cpuMonitor.Stop();
                    CpuMonitor.OverrideRefreshInterval(
                        TimeSpan.FromSeconds(CpuMonitor.DefaultRefreshIntervalInSeconds));
                }
                base.Dispose();
            }
        }
    }
}
