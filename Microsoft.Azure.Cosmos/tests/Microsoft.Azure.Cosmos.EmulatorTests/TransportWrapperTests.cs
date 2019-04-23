//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TransportWrapperTests
    {
        [TestMethod]
        public async Task TransportInterceptorContractTest()
        {
            CosmosClient cosmosClient = TestCommon.CreateCosmosClient(
                builder =>
                {
                    builder.UseTransportClientHandlerFactory(transportClient => new TransportClientWrapper(transportClient, TransportWrapperTests.Interceptor));
                });

            CosmosDatabase database = await cosmosClient.Databases.CreateDatabaseAsync(Guid.NewGuid().ToString());
            CosmosContainer container = await database.Containers.CreateContainerAsync(Guid.NewGuid().ToString(), "/id");

            string id1 = Guid.NewGuid().ToString();
            TestPayload payload1 = await container.Items.CreateItemAsync<TestPayload>(id1, new TestPayload { id = id1 });
            payload1 = await container.Items.ReadItemAsync<TestPayload>(id1, id1);
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

        private class TestPayload
        {
            public string id { get; set; }
        }

        private class TransportClientWrapper : TransportClient
        {
            private readonly TransportClient baseClient;
            private readonly Action<Uri, ResourceOperation, DocumentServiceRequest> interceptor;

            internal TransportClientWrapper(
                TransportClient client,
                Action<Uri, ResourceOperation, DocumentServiceRequest> interceptor)
            {
                Debug.Assert(client != null);
                Debug.Assert(interceptor != null);

                this.baseClient = client;
                this.interceptor = interceptor;
            }

            internal override Task<StoreResponse> InvokeStoreAsync(
                Uri physicalAddress,
                ResourceOperation resourceOperation,
                DocumentServiceRequest request)
            {
                interceptor(physicalAddress, resourceOperation, request);

                return this.baseClient.InvokeStoreAsync(physicalAddress, resourceOperation, request);
            }
        }
    }
}
