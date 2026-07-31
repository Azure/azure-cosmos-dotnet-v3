// -------------------------------------------------------------------
// Barrier Timeout Sub-Status Code Emulator Tests
// -------------------------------------------------------------------
// Validates that when a barrier retry loop times out (E2E request
// timeout), the resulting 408 exception carries the correct
// barrier-specific sub-status code (21006, 21012 or 21014) instead of 0.
//
// Uses TransportClientWrapper.interceptorAfterResult to intercept
// transport responses and keep GlobalCommittedLSN behind LSN,
// forcing the SDK into a barrier retry loop that times out.
//
// Prerequisites:
//   - Set COSMOSDB_MULTI_REGION env var for multi-region account
// -------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.TransportClientHelper;

    [TestClass]
    [TestCategory("MultiRegion")]
    public class BarrierTimeoutSubStatusTests
    {
        private string connectionString;
        private Cosmos.CosmosClient cosmosClient;
        private Cosmos.Database database;
        private Cosmos.Container container;

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.connectionString = ConfigurationManager.GetEnvironmentVariable<string>("COSMOSDB_MULTI_REGION", string.Empty);
            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_MULTI_REGION to run the tests");
            }

            this.cosmosClient = new Cosmos.CosmosClient(
                this.connectionString,
                new Cosmos.CosmosClientOptions
                {
                    ConnectionMode = Cosmos.ConnectionMode.Direct,
                    ConsistencyLevel = Cosmos.ConsistencyLevel.Strong
                });

            string uniqueDbName = "BarrierTimeoutDb_" + Guid.NewGuid().ToString("N")[..8];
            this.database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(uniqueDbName);

            string uniqueContainerName = "BarrierTimeoutContainer";
            this.container = await this.database.CreateContainerIfNotExistsAsync(
                new Cosmos.ContainerProperties(uniqueContainerName, "/pk"));
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (this.database != null)
            {
                try { await this.database.DeleteAsync(); } catch { }
            }

            this.cosmosClient?.Dispose();
        }

        /// <summary>
        /// Forces a global strong write barrier E2E timeout by intercepting transport
        /// responses so GlobalCommittedLSN always lags behind LSN, combined with
        /// a short RequestTimeout. The barrier loop times out → 408 / 21006.
        /// The unit test <c>GlobalStrongWriteBarrierRetriesExhausted_Returns503WithSubStatus</c>
        /// in <c>StoreReaderTest.cs</c> covers the corresponding 503 retry-exhaustion path.
        /// </summary>
        [TestMethod]
        [TestCategory("MultiRegion")]
        [Owner("trivediyash")]
        [Description("Validates 408/21006 when global strong write barrier times out")]
        public async Task WriteBarrier_Timeout_Returns408With21006()
        {
            int barrierRequestCount = 0;
            bool writeIntercepted = false;
            object stateLock = new();

            CosmosClient testClient = new Cosmos.Fluent.CosmosClientBuilder(this.connectionString)
                    .WithConsistencyLevel(Cosmos.ConsistencyLevel.Strong)
                    .WithRequestTimeout(TimeSpan.FromSeconds(10))
                    .WithTransportClientHandlerFactory(transport => new TransportClientWrapper(
                        transport,
                        interceptorAfterResult: (request, storeResponse) =>
                        {
                            lock (stateLock)
                            {
                                // Intercept the write response (201 Created):
                                // Lower GlobalCommittedLSN below LSN and set NumberOfReadRegions > 0
                                // to force the SDK into the barrier retry loop.
                                if (storeResponse.StatusCode == HttpStatusCode.Created
                                    && request.ResourceType == ResourceType.Document
                                    && !writeIntercepted)
                                {
                                    writeIntercepted = true;
                                    long lsn = storeResponse.LSN;
                                    long behindLsn = Math.Max(0, lsn - 3);

                                    storeResponse.Headers.Set(
                                        WFConstants.BackendHeaders.NumberOfReadRegions, "2");
                                    storeResponse.Headers.Set(
                                        WFConstants.BackendHeaders.GlobalCommittedLSN,
                                        behindLsn.ToString(CultureInfo.InvariantCulture));

                                    Trace.TraceInformation(
                                        $"[Interceptor] Write: LSN={lsn}, GlobalCommittedLSN={behindLsn} → barrier triggered");
                                }

                                // Intercept barrier HEAD requests:
                                // Always return GlobalCommittedLSN=0 so barrier is never satisfied.
                                if (request.OperationType == OperationType.Head
                                    && writeIntercepted)
                                {
                                    barrierRequestCount++;

                                    storeResponse.Headers.Set(
                                        WFConstants.BackendHeaders.GlobalCommittedLSN, "0");
                                    storeResponse.Headers.Set(
                                        WFConstants.BackendHeaders.NumberOfReadRegions, "2");
                                }
                            }

                            return storeResponse;
                        }))
                    .Build();

            using (testClient)
            {
                Container testContainer = testClient.GetContainer(
                    this.database.Id, this.container.Id);

                try
                {
                    var item = new
                    {
                        id = Guid.NewGuid().ToString(),
                        pk = "barrier-timeout-test"
                    };

                    await testContainer.CreateItemAsync(
                        item,
                        new Cosmos.PartitionKey("barrier-timeout-test"));

                    Assert.Fail("Expected CosmosException with 408 was not thrown");
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.RequestTimeout)
                {
                    Trace.TraceInformation(
                        $"Got 408: SubStatusCode={ex.SubStatusCode}, " +
                        $"BarrierHEADs={barrierRequestCount}");

                    Assert.AreEqual(
                        21006,
                        ex.SubStatusCode,
                        $"Expected SubStatusCode 21006 (Server_GlobalStrongWriteBarrierNotMet) " +
                        $"but got {ex.SubStatusCode}. Barrier HEAD requests: {barrierRequestCount}");
                }
            }
        }

        /// <summary>
        /// Forces a global strong read barrier E2E timeout by intercepting transport
        /// responses so the read replica's GlobalCommittedLSN always lags behind LSN.
        /// The barrier retry loop in <c>QuorumReader</c> then times out → 408 / 21014.
        /// </summary>
        [TestMethod]
        [TestCategory("MultiRegion")]
        [Owner("trivediyash")]
        [Description("Validates 408/21014 when global strong read barrier times out")]
        public async Task ReadBarrier_Timeout_Returns408With21014()
        {
            // Seed an item first (without interception) so the read has something to fetch.
            string itemId = Guid.NewGuid().ToString();
            const string pk = "read-barrier-timeout-test";
            await this.container.CreateItemAsync(
                new { id = itemId, pk },
                new Cosmos.PartitionKey(pk));

            int barrierRequestCount = 0;
            bool readIntercepted = false;
            object stateLock = new();

            CosmosClient testClient = new Cosmos.Fluent.CosmosClientBuilder(this.connectionString)
                    .WithConsistencyLevel(Cosmos.ConsistencyLevel.Strong)
                    .WithRequestTimeout(TimeSpan.FromSeconds(10))
                    .WithTransportClientHandlerFactory(transport => new TransportClientWrapper(
                        transport,
                        interceptorAfterResult: (request, storeResponse) =>
                        {
                            lock (stateLock)
                            {
                                // Intercept the initial read response (200 OK):
                                // Lower GlobalCommittedLSN below LSN and set NumberOfReadRegions > 0
                                // so the SDK enters the global strong read barrier retry loop.
                                if (storeResponse.StatusCode == HttpStatusCode.OK
                                    && request.OperationType == OperationType.Read
                                    && request.ResourceType == ResourceType.Document
                                    && !readIntercepted)
                                {
                                    readIntercepted = true;
                                    long lsn = storeResponse.LSN;
                                    long behindLsn = Math.Max(0, lsn - 3);

                                    storeResponse.Headers.Set(
                                        WFConstants.BackendHeaders.NumberOfReadRegions, "2");
                                    storeResponse.Headers.Set(
                                        WFConstants.BackendHeaders.GlobalCommittedLSN,
                                        behindLsn.ToString(CultureInfo.InvariantCulture));

                                    Trace.TraceInformation(
                                        $"[Interceptor] Read: LSN={lsn}, GlobalCommittedLSN={behindLsn} → read barrier triggered");
                                }

                                // Intercept barrier HEAD requests: keep GlobalCommittedLSN behind so the
                                // barrier is never satisfied and the retry loop exhausts against the timeout.
                                if (request.OperationType == OperationType.Head
                                    && readIntercepted)
                                {
                                    barrierRequestCount++;

                                    storeResponse.Headers.Set(
                                        WFConstants.BackendHeaders.GlobalCommittedLSN, "0");
                                    storeResponse.Headers.Set(
                                        WFConstants.BackendHeaders.NumberOfReadRegions, "2");
                                }
                            }

                            return storeResponse;
                        }))
                    .Build();

            using (testClient)
            {
                Container testContainer = testClient.GetContainer(
                    this.database.Id, this.container.Id);

                try
                {
                    await testContainer.ReadItemAsync<dynamic>(
                        itemId,
                        new Cosmos.PartitionKey(pk));

                    Assert.Fail("Expected CosmosException with 408 was not thrown");
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.RequestTimeout)
                {
                    Trace.TraceInformation(
                        $"Got 408: SubStatusCode={ex.SubStatusCode}, " +
                        $"BarrierHEADs={barrierRequestCount}");

                    Assert.AreEqual(
                        21014,
                        ex.SubStatusCode,
                        $"Expected SubStatusCode 21014 (Server_ReadBarrierFailed) " +
                        $"but got {ex.SubStatusCode}. Barrier HEAD requests: {barrierRequestCount}");
                }
            }
        }

        /// <summary>
        /// Same pattern but for N-Region Synchronous Commit barrier.
        /// Intercepts responses to set GlobalNRegionCommittedGLSN behind LSN
        /// with EnableNRegionSynchronousCommit behavior.
        /// Expects: 408 / 21012 (Server_NRegionCommitWriteBarrierNotMet).
        ///
        /// Uses an HTTP interceptor to inject EnableNRegionSynchronousCommit=true into
        /// the AccountProperties response so the account does not need to actually have
        /// the feature flag flipped on.
        /// </summary>
        [TestMethod]
        [TestCategory("MultiRegion")]
        [Owner("trivediyash")]
        [Description("Validates 408/21012 when N-Region commit barrier times out")]
        public async Task WriteBarrier_NRegionTimeout_Returns408With21012()
        {
            int barrierRequestCount = 0;
            bool writeIntercepted = false;
            object stateLock = new();

            HttpClientHandlerHelper httpHandler = new HttpClientHandlerHelper
            {
                ResponseIntercepter = async (response, request) =>
                {
                    if ((request.RequestUri.AbsolutePath.EndsWith("/", StringComparison.OrdinalIgnoreCase)
                        || request.RequestUri.AbsolutePath.Equals(string.Empty))
                        && response.IsSuccessStatusCode)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        Newtonsoft.Json.Linq.JObject accountJson = Newtonsoft.Json.Linq.JObject.Parse(content);
                        accountJson[Constants.Properties.EnableNRegionSynchronousCommit] = true;
                        response.Content = new StringContent(
                            accountJson.ToString(), System.Text.Encoding.UTF8, "application/json");
                    }
                    return response;
                }
            };

            CosmosClient testClient = new Cosmos.Fluent.CosmosClientBuilder(this.connectionString)
                    .WithConsistencyLevel(Cosmos.ConsistencyLevel.Session)
                    .WithHttpClientFactory(() => new HttpClient(httpHandler))
                    .WithTransportClientHandlerFactory(transport => new TransportClientWrapper(
                        transport,
                        interceptorAfterResult: (request, storeResponse) =>
                        {
                            lock (stateLock)
                            {
                                if (storeResponse.StatusCode == HttpStatusCode.Created
                                    && request.ResourceType == ResourceType.Document
                                    && !writeIntercepted)
                                {
                                    writeIntercepted = true;
                                    long lsn = storeResponse.LSN;

                                    // Simulate N-Region barrier: set GlobalNRegionCommittedGLSN behind
                                    storeResponse.Headers.Set(
                                        WFConstants.BackendHeaders.NumberOfReadRegions, "2");
                                    storeResponse.Headers.Set(
                                        WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN,
                                        (lsn - 3).ToString(CultureInfo.InvariantCulture));
                                    // GlobalCommittedLSN can be caught up — only NRegion matters
                                    storeResponse.Headers.Set(
                                        WFConstants.BackendHeaders.GlobalCommittedLSN,
                                        lsn.ToString(CultureInfo.InvariantCulture));
                                }

                                if (request.OperationType == OperationType.Head
                                    && writeIntercepted)
                                {
                                    barrierRequestCount++;
                                    storeResponse.Headers.Set(
                                        WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "0");
                                    storeResponse.Headers.Set(
                                        WFConstants.BackendHeaders.NumberOfReadRegions, "2");
                                }
                            }

                            return storeResponse;
                        }))
                    .Build();

            using (testClient)
            {
                Container testContainer = testClient.GetContainer(
                    this.database.Id, this.container.Id);

                try
                {
                    var item = new
                    {
                        id = Guid.NewGuid().ToString(),
                        pk = "nregion-barrier-timeout"
                    };

                    await testContainer.CreateItemAsync(
                        item,
                        new Cosmos.PartitionKey("nregion-barrier-timeout"));

                    Assert.Fail("Expected CosmosException with 408 was not thrown");
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.RequestTimeout)
                {
                    Assert.AreEqual(
                        21012,
                        ex.SubStatusCode,
                        $"Expected SubStatusCode 21012 (Server_NRegionCommitWriteBarrierNotMet) " +
                        $"but got {ex.SubStatusCode}. Barrier HEADs: {barrierRequestCount}");
                }
            }
        }
    }
}
