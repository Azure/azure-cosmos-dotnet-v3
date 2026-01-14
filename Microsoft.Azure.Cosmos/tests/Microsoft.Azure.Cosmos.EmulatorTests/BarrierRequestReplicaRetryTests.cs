//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
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
                });
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
            // Shared state protected by lock
            object stateLock = new object();
            int barrierRequestCount = 0;
            int forceRefreshAddressCacheSeenCount = 0;
            long targetLsn = 0;

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ConsistencyLevel = Cosmos.ConsistencyLevel.Strong,
                TransportClientHandlerFactory = (transport) => new TransportClientWrapper(
                    transport,
                    interceptorAfterResult: (request, storeResponse) =>
                    {
                        lock (stateLock)
                        {
                            // Force barrier request by setting GlobalCommittedLSN behind LSN on write
                            if (storeResponse.StatusCode == HttpStatusCode.Created)
                            {
                                if (targetLsn == 0)
                                {
                                    targetLsn = storeResponse.LSN;
                                    // This triggers barrier request in ConsistencyWriter.ApplySessionToken
                                    storeResponse.Headers.Set(WFConstants.BackendHeaders.NumberOfReadRegions, "2");
                                    storeResponse.Headers.Set(WFConstants.BackendHeaders.GlobalCommittedLSN, "0"); // Behind LSN
                                    storeResponse.Headers.Set(WFConstants.BackendHeaders.LSN, storeResponse.LSN.ToString(CultureInfo.InvariantCulture));
                                }
                            }

                            // Handle barrier (HEAD) requests
                            if (request.OperationType == OperationType.Head)
                            {
                                barrierRequestCount++;

                                if (request.RequestContext != null && request.RequestContext.ForceRefreshAddressCache)
                                {
                                    forceRefreshAddressCacheSeenCount++;
                                }

                                // First barrier request returns 410/1022
                                if (barrierRequestCount == 1)
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
                                // Second barrier request (to primary with ForceRefreshAddressCache) succeeds
                                else if (barrierRequestCount == 2)
                                {
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

            lock (stateLock)
            {
                if (barrierRequestCount > 0)
                {
                    Assert.AreEqual(2, barrierRequestCount, "Should make exactly 2 barrier requests (secondary then primary)");
                    Assert.IsTrue(forceRefreshAddressCacheSeenCount > 0, "Second barrier request should have ForceRefreshAddressCache=true");
                }
                else
                {
                    Assert.Inconclusive("Barrier requests were not triggered. This test requires a multi-region Strong consistency account.");
                }
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

            object stateLock = new object();
            int barrierRequestCount = 0;
            int forceRefreshAddressCacheSeenCount = 0;
            const long targetLsn = 100;
            bool shouldInterceptRead = true;
            string targetItemId = item.id;

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ConsistencyLevel = Cosmos.ConsistencyLevel.Strong,
                TransportClientHandlerFactory = (transport) => new TransportClientWrapper(
                    transport,
                    interceptorAfterResult: (request, storeResponse) =>
                    {
                        lock (stateLock)
                        {
                            // Only manipulate the read response for our specific test item when flag is set
                            if (request.OperationType == OperationType.Read &&
                                storeResponse.StatusCode == HttpStatusCode.OK &&
                                request.ResourceAddress != null &&
                                request.ResourceAddress.Contains(targetItemId) &&
                                shouldInterceptRead)
                            {
                                shouldInterceptRead = false;
                                // This triggers barrier in QuorumReader.ReadQuorumAsync
                                storeResponse.Headers.Set(WFConstants.BackendHeaders.NumberOfReadRegions, "2");
                                storeResponse.Headers.Set(WFConstants.BackendHeaders.LSN, targetLsn.ToString(CultureInfo.InvariantCulture));
                                storeResponse.Headers.Set(WFConstants.BackendHeaders.ItemLSN, targetLsn.ToString(CultureInfo.InvariantCulture));
                                storeResponse.Headers.Set(WFConstants.BackendHeaders.GlobalCommittedLSN, "50"); // Behind LSN
                            }

                            // Handle barrier (HEAD) requests
                            if (request.OperationType == OperationType.Head)
                            {
                                barrierRequestCount++;

                                // Check if ForceRefreshAddressCache is set
                                if (request.RequestContext != null && request.RequestContext.ForceRefreshAddressCache)
                                {
                                    forceRefreshAddressCacheSeenCount++;
                                }

                                // First attempt (quorum replica) fails with 410/1022
                                if (barrierRequestCount == 1)
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
                        }

                        return storeResponse;
                    })
            };

            using CosmosClient client = new CosmosClient(this.connectionString, clientOptions);
            Container testContainer = client.GetContainer(this.database.Id, this.container.Id);

            try
            {
                // Act
                ItemResponse<ToDoActivity> response = await testContainer.ReadItemAsync<ToDoActivity>(
                    item.id,
                    new Cosmos.PartitionKey(item.pk));

                // Assert
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Read should succeed after barrier retry");

                lock (stateLock)
                {
                    if (barrierRequestCount > 0)
                    {
                        Assert.IsTrue(barrierRequestCount >= 2, $"Should make at least 2 barrier requests (had {barrierRequestCount})");
                        Assert.IsTrue(forceRefreshAddressCacheSeenCount > 0, "Retry should have ForceRefreshAddressCache=true");
                    }
                    else
                    {
                        Assert.Inconclusive("Barrier requests were not triggered. This test requires a multi-region Strong consistency account.");
                    }
                }
            }
            catch (CosmosException ex)
            {
                int finalBarrierCount;
                lock (stateLock)
                {
                    finalBarrierCount = barrierRequestCount;
                }
                Assert.Fail($"Unexpected exception: {ex.StatusCode} - {ex.Message}. Barrier requests: {finalBarrierCount}, ForceRefresh seen: {forceRefreshAddressCacheSeenCount > 0}");
            }
        }

        [TestMethod]
        [Owner("aavasthy")]
        [Description("Validates that write barrier fails when primary replica returns 410/1022")]
        [TestCategory("MultiRegion")]
        public async Task WriteBarrier_PrimaryReplicaLeaseNotFound_Fails()
        {
            object stateLock = new object();
            int barrierRequestCount = 0;
            int primaryBarrierCount = 0;
            long targetLsn = 0;

            // Based on ConsistencyWriter code
            const int expectedMaxBarrierRetries = 40;

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ConsistencyLevel = Cosmos.ConsistencyLevel.Strong,
                RequestTimeout = TimeSpan.FromSeconds(5), // Set a shorter timeout to fail faster
                TransportClientHandlerFactory = (transport) => new TransportClientWrapper(
                    transport,
                    interceptorAfterResult: (request, storeResponse) =>
                    {
                        lock (stateLock)
                        {
                            // Force barrier request by setting GlobalCommittedLSN behind LSN on write
                            if (storeResponse.StatusCode == HttpStatusCode.Created)
                            {
                                if (targetLsn == 0)
                                {
                                    targetLsn = storeResponse.LSN;
                                    // This triggers barrier request in ConsistencyWriter.ApplySessionToken
                                    storeResponse.Headers.Set(WFConstants.BackendHeaders.NumberOfReadRegions, "2");
                                    storeResponse.Headers.Set(WFConstants.BackendHeaders.GlobalCommittedLSN, "0"); // Behind LSN
                                    storeResponse.Headers.Set(WFConstants.BackendHeaders.LSN, storeResponse.LSN.ToString(CultureInfo.InvariantCulture));
                                }
                            }

                            // Handle barrier (HEAD) requests
                            if (request.OperationType == OperationType.Head)
                            {
                                barrierRequestCount++;

                                if (request.RequestContext != null && request.RequestContext.ForceRefreshAddressCache)
                                {
                                    primaryBarrierCount++;
                                }

                                // Always return 410/1022 for ALL barrier requests
                                DictionaryNameValueCollection headers = new DictionaryNameValueCollection();
                                headers.Set(WFConstants.BackendHeaders.SubStatus,
                                    ((int)SubStatusCodes.LeaseNotFound).ToString(CultureInfo.InvariantCulture));

                                return new StoreResponse()
                                {
                                    Status = 410,
                                    Headers = headers,
                                    ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes($"Lease not found - attempt {barrierRequestCount}"))
                                };
                            }
                        }

                        return storeResponse;
                    })
            };

            using CosmosClient client = new CosmosClient(this.connectionString, clientOptions);
            Container testContainer = client.GetContainer(this.database.Id, this.container.Id);

            // Act & Assert
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();

            try
            {
                ItemResponse<ToDoActivity> response = await testContainer.CreateItemAsync(
                    item,
                    new Cosmos.PartitionKey(item.pk));

                int finalBarrierCount, finalPrimaryCount;
                lock (stateLock)
                {
                    finalBarrierCount = barrierRequestCount;
                    finalPrimaryCount = primaryBarrierCount;
                }
                Assert.Fail($"Write should have failed when all barrier attempts returned 410/1022. Barrier requests: {finalBarrierCount}, Primary barrier requests: {finalPrimaryCount}");
            }
            catch (CosmosException ex)
            {
                int finalBarrierCount, finalPrimaryCount;
                lock (stateLock)
                {
                    finalBarrierCount = barrierRequestCount;
                    finalPrimaryCount = primaryBarrierCount;
                }

                Assert.IsTrue(
                    ex.StatusCode == HttpStatusCode.Gone ||
                    ex.StatusCode == HttpStatusCode.RequestTimeout ||
                    ex.StatusCode == HttpStatusCode.ServiceUnavailable,
                    $"Expected Gone, RequestTimeout, or ServiceUnavailable but got {ex.StatusCode}");

                Assert.IsTrue(finalBarrierCount > 1, $"Should have attempted multiple barrier requests (had {finalBarrierCount})");

                // Verify at least primary replica barrier attempt was made
                Assert.IsTrue(finalPrimaryCount >= 1, $"Should have attempted at least one primary barrier request (had {finalPrimaryCount})");

                Assert.IsTrue(finalBarrierCount <= expectedMaxBarrierRetries,
                    $"Barrier request count {finalBarrierCount} exceeded expected max {expectedMaxBarrierRetries}");
            }
        }

        [TestMethod]
        [Owner("aavasthy")]
        [Description("Validates that read barrier retries on primary when replica returns 410/1022, and if primary fails, SDK updates region list and retries on next available region")]
        [TestCategory("MultiRegion")]
        public async Task ReadBarrier_ReplicaAndPrimaryFail_RetriesOnNextAvailableRegion()
        {
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
            await this.container.CreateItemAsync(item, new Cosmos.PartitionKey(item.pk));

            object stateLock = new object();
            int barrierRequestCount = 0;
            int primaryBarrierAttempts = 0;
            Dictionary<string, bool> uniqueRegionsContacted = new Dictionary<string, bool>();
            List<string> barrierRequestRegions = new List<string>();
            bool shouldTriggerBarrier = true;
            string targetItemId = item.id;

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ConsistencyLevel = Cosmos.ConsistencyLevel.Strong,
                TransportClientHandlerFactory = (transport) => new TransportClientWrapper(
                    transport,
                    interceptorAfterResult: (request, storeResponse) =>
                    {
                        lock (stateLock)
                        {
                            // Track all regions contacted
                            string currentRegion = request.RequestContext?.LocationEndpointToRoute?.ToString();
                            if (!string.IsNullOrEmpty(currentRegion))
                            {
                                uniqueRegionsContacted[currentRegion] = true;
                            }

                            if (request.OperationType == OperationType.Read &&
                                storeResponse.StatusCode == HttpStatusCode.OK &&
                                request.ResourceAddress?.Contains(targetItemId) == true &&
                                shouldTriggerBarrier)
                            {
                                shouldTriggerBarrier = false;
                                // Force barrier check by setting GlobalCommittedLSN behind LSN
                                storeResponse.Headers.Set(WFConstants.BackendHeaders.NumberOfReadRegions, "2");
                                storeResponse.Headers.Set(WFConstants.BackendHeaders.LSN, "100");
                                storeResponse.Headers.Set(WFConstants.BackendHeaders.ItemLSN, "100");
                                storeResponse.Headers.Set(WFConstants.BackendHeaders.GlobalCommittedLSN, "50"); // Behind LSN - triggers barrier
                            }

                            if (request.OperationType == OperationType.Head)
                            {
                                barrierRequestCount++;
                                barrierRequestRegions.Add(currentRegion ?? "unknown");

                                bool isPrimaryRetry = request.RequestContext?.ForceRefreshAddressCache == true;
                                if (isPrimaryRetry)
                                {
                                    primaryBarrierAttempts++;
                                }

                                Console.WriteLine($"Barrier #{barrierRequestCount}: Region={currentRegion}, IsPrimary={isPrimaryRetry}");

                                // Simulate 410/1022 for first few attempts to force region retry
                                if (barrierRequestCount <= 2)
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
                        }

                        return storeResponse;
                    })
            };

            using CosmosClient client = new CosmosClient(this.connectionString, clientOptions);
            Container testContainer = client.GetContainer(this.database.Id, this.container.Id);

            // Act
            ItemResponse<ToDoActivity> response = await testContainer.ReadItemAsync<ToDoActivity>(
                item.id,
                new Cosmos.PartitionKey(item.pk));

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Read should eventually succeed");

            lock (stateLock)
            {
                if (barrierRequestCount > 0)
                {
                    // Verify replica failure triggers primary retry
                    Assert.IsTrue(barrierRequestCount >= 2,
                        "Should have at least 2 barrier attempts (replica + primary)");

                    Assert.IsTrue(primaryBarrierAttempts >= 1,
                        "Should retry on primary with ForceRefreshAddressCache=true after replica fails");

                    // 3. Verify region failover behavior
                    if (uniqueRegionsContacted.Count > 1)
                    {
                        Assert.IsTrue(barrierRequestCount >= 3,
                            "When failing over to new region, should have additional barrier attempts");
                    }
                }
                else
                {
                    Assert.Inconclusive("Barrier requests were not triggered. This test requires a multi-region Strong consistency account.");
                }
            }
        }
    }
}