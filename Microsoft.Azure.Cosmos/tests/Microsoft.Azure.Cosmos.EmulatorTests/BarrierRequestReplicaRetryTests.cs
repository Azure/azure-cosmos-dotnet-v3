//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.TransportClientHelper;

    [TestClass]
    [TestCategory("MultiRegion")]
    public class BarrierRequestReplicaRetryTests
    {
        private string connectionString;
        private CosmosClient cosmosClient;
        private Cosmos.Database database;
        private Container container;

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.connectionString = ConfigurationManager.GetEnvironmentVariable<string>("COSMOSDB_MULTI_REGION", string.Empty);

            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_MULTI_REGION to run the tests");
            }

            this.cosmosClient = new CosmosClient(
                this.connectionString,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct,
                    ConsistencyLevel = Cosmos.ConsistencyLevel.Strong
                });

            string uniqueDbName = "BarrierTestDb_" + Guid.NewGuid().ToString();
            this.database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(uniqueDbName);

            string uniqueContainerName = "BarrierTestContainer_" + Guid.NewGuid().ToString();
            this.container = await this.database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(uniqueContainerName, "/pk")
                {
                    PartitionKey = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string> { "/pk" } }
                },
                throughput: 10000);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (this.database != null)
            {
                try
                {
                    await this.database.DeleteAsync();
                }
                catch { }
            }

            this.cosmosClient?.Dispose();
        }

        [TestMethod]
        [Owner("aavasthy")]
        [Description("Validates that write barrier requests retry on primary when secondary replica returns 410/1022")]
        [TestCategory("MultiRegion")]
        public async Task WriteBarrier_SecondaryReplicaLeaseNotFound_RetriesOnPrimary()
        {
            int barrierRequestCount = 0;
            bool forceRefreshAddressCacheSeen = false;
            long targetLsn = 0;

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ConsistencyLevel = Cosmos.ConsistencyLevel.Strong,
                TransportClientHandlerFactory = (transport) => new TransportClientWrapper(
                    transport,
                    interceptorAfterResult: (request, storeResponse) =>
                    {
                        // Force barrier request by setting GlobalCommittedLSN behind LSN on write
                        if (storeResponse.StatusCode == HttpStatusCode.Created && targetLsn == 0)
                        {
                            targetLsn = storeResponse.LSN;
                            // This triggers barrier request in ConsistencyWriter.ApplySessionToken
                            storeResponse.Headers.Set(WFConstants.BackendHeaders.NumberOfReadRegions, "2");
                            storeResponse.Headers.Set(WFConstants.BackendHeaders.GlobalCommittedLSN, "0"); // Behind LSN
                            storeResponse.Headers.Set(WFConstants.BackendHeaders.LSN, targetLsn.ToString(CultureInfo.InvariantCulture));
                        }

                        // Handle barrier (HEAD) requests
                        if (request.OperationType == OperationType.Head)
                        {
                            barrierRequestCount++;

                            if (request.RequestContext != null && request.RequestContext.ForceRefreshAddressCache)
                            {
                                forceRefreshAddressCacheSeen = true;
                            }

                            // First barrier request returns 410/1022
                            if (barrierRequestCount == 1)
                            {
                                // Create new StoreResponse with 410/1022
                                DictionaryNameValueCollection headers = new DictionaryNameValueCollection();
                                headers.Set(WFConstants.BackendHeaders.SubStatus,
                                    ((int)SubStatusCodes.LeaseNotFound).ToString(CultureInfo.InvariantCulture));

                                return new StoreResponse()
                                {
                                    Status = 410,
                                    Headers = headers,
                                    ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes("Lease not found"))
                                };
                            }
                            // Second barrier request (to primary with ForceRefreshAddressCache) succeeds
                            else if (barrierRequestCount == 2)
                            {
                                // Return success with proper headers
                                DictionaryNameValueCollection successHeaders = new DictionaryNameValueCollection();
                                successHeaders.Set(WFConstants.BackendHeaders.NumberOfReadRegions, "2");
                                successHeaders.Set(WFConstants.BackendHeaders.GlobalCommittedLSN,
                                    targetLsn.ToString(CultureInfo.InvariantCulture));
                                successHeaders.Set(WFConstants.BackendHeaders.LSN,
                                    targetLsn.ToString(CultureInfo.InvariantCulture));

                                return new StoreResponse()
                                {
                                    Status = 200,
                                    Headers = successHeaders,
                                    ResponseBody = Stream.Null
                                };
                            }
                        }

                        return storeResponse;
                    })
            };

            using CosmosClient client = new CosmosClient(this.connectionString, clientOptions);
            Container testContainer = client.GetContainer(this.database.Id, this.container.Id);

            // Act
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await testContainer.CreateItemAsync(
                item,
                new Cosmos.PartitionKey(item.pk));

            // Assert
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, "Write should succeed after barrier retry");

            if (barrierRequestCount > 0)
            {
                Assert.AreEqual(2, barrierRequestCount, "Should make exactly 2 barrier requests (secondary then primary)");
                Assert.IsTrue(forceRefreshAddressCacheSeen, "Second barrier request should have ForceRefreshAddressCache=true");
            }
            else
            {
                Assert.Inconclusive("Barrier requests were not triggered. This test requires a multi-region Strong consistency account.");
            }
        }

        [TestMethod]
        [Owner("aavasthy")]
        [Description("Validates that read barrier requests retry on primary when quorum replica returns 410/1022")]
        [TestCategory("MultiRegion")]
        public async Task ReadBarrier_QuorumReplicaLeaseNotFound_RetriesOnPrimary()
        {
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
            await this.container.CreateItemAsync(item, new Cosmos.PartitionKey(item.pk));

            int barrierRequestCount = 0;
            bool forceRefreshAddressCacheSeen = false;
            long targetLsn = 100;
            bool shouldInterceptRead = false;
            string targetItemId = item.id;

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ConsistencyLevel = Cosmos.ConsistencyLevel.Strong, 
                TransportClientHandlerFactory = (transport) => new TransportClientWrapper(
                    transport,
                    interceptorAfterResult: (request, storeResponse) =>
                    {
                        // Only manipulate the read response for our specific test item when flag is set
                        if (shouldInterceptRead &&
                            request.OperationType == OperationType.Read &&
                            storeResponse.StatusCode == HttpStatusCode.OK &&
                            request.ResourceAddress != null &&
                            request.ResourceAddress.Contains(targetItemId))
                        {
                            // This triggers barrier in QuorumReader.ReadQuorumAsync
                            storeResponse.Headers.Set(WFConstants.BackendHeaders.NumberOfReadRegions, "2");
                            storeResponse.Headers.Set(WFConstants.BackendHeaders.LSN, targetLsn.ToString(CultureInfo.InvariantCulture));
                            storeResponse.Headers.Set(WFConstants.BackendHeaders.ItemLSN, targetLsn.ToString(CultureInfo.InvariantCulture));
                            storeResponse.Headers.Set(WFConstants.BackendHeaders.GlobalCommittedLSN, "50"); // Behind LSN

                            shouldInterceptRead = false;
                        }

                        // Handle barrier (HEAD) requests
                        if (request.OperationType == OperationType.Head)
                        {
                            barrierRequestCount++;

                            // Check if ForceRefreshAddressCache is set
                            if (request.RequestContext != null && request.RequestContext.ForceRefreshAddressCache)
                            {
                                forceRefreshAddressCacheSeen = true;
                            }

                            // First attempt (quorum replica) fails with 410/1022
                            if (barrierRequestCount == 1 && !forceRefreshAddressCacheSeen)
                            {
                                DictionaryNameValueCollection headers = new DictionaryNameValueCollection();
                                headers.Set(WFConstants.BackendHeaders.SubStatus,
                                    ((int)SubStatusCodes.LeaseNotFound).ToString(CultureInfo.InvariantCulture));

                                return new StoreResponse()
                                {
                                    Status = 410,
                                    Headers = headers,
                                    ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes("Lease not found"))
                                };
                            }
                        }

                        return storeResponse;
                    })
            };

            using CosmosClient client = new CosmosClient(this.connectionString, clientOptions);
            Container testContainer = client.GetContainer(this.database.Id, this.container.Id);

            shouldInterceptRead = true;

            try
            {
                // Act
                ItemResponse<ToDoActivity> response = await testContainer.ReadItemAsync<ToDoActivity>(
                    item.id,
                    new Cosmos.PartitionKey(item.pk));

                // Assert
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Read should succeed after barrier retry");

                if (barrierRequestCount > 0)
                {
                    Assert.IsTrue(barrierRequestCount >= 2, $"Should make at least 2 barrier requests (had {barrierRequestCount})");
                    Assert.IsTrue(forceRefreshAddressCacheSeen, "Retry should have ForceRefreshAddressCache=true");
                }
                else
                {
                    Assert.Inconclusive("Barrier requests were not triggered. This test requires a multi-region Strong consistency account.");
                }
            }
            catch (CosmosException ex)
            {
                Assert.Fail($"Unexpected exception: {ex.StatusCode} - {ex.Message}. Barrier requests: {barrierRequestCount}, ForceRefresh seen: {forceRefreshAddressCacheSeen}");
            }
        }
    }
}