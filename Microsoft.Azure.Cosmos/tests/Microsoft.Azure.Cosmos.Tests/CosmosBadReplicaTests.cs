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
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosBadReplicaTests
    {
        [TestMethod]
        [TestCategory("Flaky")]
        [Timeout(30000)]
        [DataRow(true, true, false, DisplayName = "Validate when replica validation is enabled using environment variable.")]
        [DataRow(false, true, false, DisplayName = "Validate when replica validation is disabled using environment variable.")]
        [DataRow(true, false, true, DisplayName = "Validate when replica validation is enabled using cosmos client options.")]
        [DataRow(false, false, true, DisplayName = "Validate when replica validation is disabled using cosmos client options.")]
        public async Task TestGoneFromServiceScenarioAsync(
            bool enableReplicaValidation,
            bool useEnvironmentVariable,
            bool useCosmosClientOptions)
        {
            try
            {
                if (useEnvironmentVariable)
                {
                    Environment.SetEnvironmentVariable(
                        variable: ConfigurationManager.ReplicaConnectivityValidationEnabled,
                        value: enableReplicaValidation.ToString());
                }

                Mock<IHttpHandler> mockHttpHandler = new Mock<IHttpHandler>(MockBehavior.Strict);
                Uri endpoint = MockSetupsHelper.SetupSingleRegionAccount(
                    "mockAccountInfo",
                    consistencyLevel: ConsistencyLevel.Session,
                    mockHttpHandler,
                    out string primaryRegionEndpoint);

                string databaseName = "mockDbName";
                string containerName = "mockContainerName";
                string containerRid = "ccZ1ANCszwk=";
                Documents.ResourceId cRid = Documents.ResourceId.Parse(containerRid);
                MockSetupsHelper.SetupContainerProperties(
                    mockHttpHandler: mockHttpHandler,
                    regionEndpoint: primaryRegionEndpoint,
                    databaseName: databaseName,
                    containerName: containerName,
                    containerRid: containerRid);

                MockSetupsHelper.SetupSinglePartitionKeyRange(
                    mockHttpHandler,
                    primaryRegionEndpoint,
                    cRid,
                    out IReadOnlyList<string> partitionKeyRanges);

                List<string> replicaIds1 = new List<string>()
                {
                    "11111111111111111",
                    "22222222222222222",
                    "33333333333333333",
                    "44444444444444444",
                };

                HttpResponseMessage replicaSet1 = MockSetupsHelper.CreateAddresses(
                    replicaIds1,
                    partitionKeyRanges.First(),
                    "eastus",
                    cRid);

                // One replica changed on the refresh
                List<string> replicaIds2 = new List<string>()
                {
                    "11111111111111111",
                    "22222222222222222",
                    "33333333333333333",
                    "55555555555555555",
                };

                HttpResponseMessage replicaSet2 = MockSetupsHelper.CreateAddresses(
                    replicaIds2,
                    partitionKeyRanges.First(),
                    "eastus",
                    cRid);

                bool delayCacheRefresh = true;
                bool delayRefreshUnblocked = false;
                mockHttpHandler.SetupSequence(x => x.SendAsync(
                    It.Is<HttpRequestMessage>(r => r.RequestUri.ToString().Contains("addresses")), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(replicaSet1))
                    .Returns(async () =>
                    {
                        //block cache refresh to verify bad replica is not visited during refresh
                        while (delayCacheRefresh)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(20));
                        }

                        delayRefreshUnblocked = true;
                        return replicaSet2;
                    });

                int callBack = 0;
                List<Documents.TransportAddressUri> urisVisited = new List<Documents.TransportAddressUri>();
                Mock<Documents.TransportClient> mockTransportClient = new Mock<Documents.TransportClient>(MockBehavior.Strict);
                mockTransportClient.Setup(x => x.InvokeResourceOperationAsync(It.IsAny<Documents.TransportAddressUri>(), It.IsAny<Documents.DocumentServiceRequest>()))
                    .Callback<Documents.TransportAddressUri, Documents.DocumentServiceRequest>((t, _) => urisVisited.Add(t))
                .Returns(() =>
                {
                    callBack++;
                    if (callBack == 1)
                    {
                        throw Documents.Rntbd.TransportExceptions.GetGoneException(
                            new Uri("https://localhost:8081"),
                            Guid.NewGuid(),
                            new Documents.TransportException(Documents.TransportErrorCode.ConnectionBroken,
                            null,
                            Guid.NewGuid(),
                            new Uri("https://localhost:8081"),
                            "Mock",
                            userPayload: true,
                            payloadSent: false));
                    }

                    return Task.FromResult(new Documents.StoreResponse()
                    {
                        Status = 200,
                        Headers = new Documents.Collections.StoreResponseNameValueCollection()
                        {
                            ActivityId = Guid.NewGuid().ToString(),
                            LSN = "12345",
                            PartitionKeyRangeId = "0",
                            GlobalCommittedLSN = "12345",
                            SessionToken = "1#12345#1=12345"
                        },
                        ResponseBody = new MemoryStream()
                    });
                });

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = Cosmos.ConsistencyLevel.Session,
                    HttpClientFactory = () => new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object)),
                    TransportClientHandlerFactory = (original) => mockTransportClient.Object,
                };

                if (useCosmosClientOptions)
                {
                    cosmosClientOptions.EnableAdvancedReplicaSelectionForTcp = enableReplicaValidation;
                }

                using (CosmosClient customClient = new CosmosClient(
                    endpoint.ToString(),
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                    cosmosClientOptions))
                {
                    try
                    {
                        Container container = customClient.GetContainer(databaseName, containerName);

                        for (int i = 0; i < 20; i++)
                        {
                            ResponseMessage response = await container.ReadItemStreamAsync(Guid.NewGuid().ToString(), new Cosmos.PartitionKey(Guid.NewGuid().ToString()));
                            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                        }

                        mockTransportClient.VerifyAll();
                        mockHttpHandler.VerifyAll();

                        mockTransportClient
                            .Setup(x => x.OpenConnectionAsync(It.IsAny<Uri>()))
                            .Returns(Task.CompletedTask);

                        Documents.TransportAddressUri failedReplica = urisVisited.First();

                        // With replica validation enabled in preview mode, the failed replica will be validated as a part of the flow,
                        // and because any subsequent validation/ connection will be successful, the failed replica will now be marked
                        // as connected, thus it will be visited more than once. However, note that when replice validation is disabled,
                        // no validation is done thus the URI will be marked as unhealthy as expected. Therefore the uri will be visited
                        // just once.
                        Assert.IsTrue(
                            enableReplicaValidation
                                ? urisVisited.Any(x => x.Equals(failedReplica))
                                : urisVisited.Count(x => x.Equals(failedReplica)) == 1);

                        urisVisited.Clear();
                        delayCacheRefresh = false;
                        do
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(100));
                        } while (!delayRefreshUnblocked);

                        for (int i = 0; i < 20; i++)
                        {
                            ResponseMessage response = await container.ReadItemStreamAsync(Guid.NewGuid().ToString(), new Cosmos.PartitionKey(Guid.NewGuid().ToString()));
                            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                        }

                        Assert.AreEqual(4, urisVisited.ToHashSet().Count());

                        // Clears all the setups. No network calls should be done on the next operation.
                        mockHttpHandler.Reset();
                        mockTransportClient.Reset();
                    }
                    finally
                    {
                        mockTransportClient.Setup(x => x.Dispose());
                    }
                }
            }
            finally
            {
                if (useEnvironmentVariable)
                {
                    Environment.SetEnvironmentVariable(
                        variable: ConfigurationManager.ReplicaConnectivityValidationEnabled,
                        value: null);
                }
            }
        }
    }
}