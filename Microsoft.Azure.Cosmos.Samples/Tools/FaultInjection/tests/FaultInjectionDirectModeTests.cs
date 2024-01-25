namespace Microsoft.Azure.Cosmos.FaultInjection.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.FaultInjection.Tests.Utils;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class FaultInjectionDirectModeTests
    {
        private const int Timeout = 60000;

        private CosmosClient? client;
        private Cosmos.Database? database;
        private Container? container;

        public async Task Initialize(FaultInjector faultInjector, bool multiRegion)
        {
            this.client = TestCommon.CreateCosmosClient(false, faultInjector, multiRegion);
            this.database = await this.client.CreateDatabaseIfNotExistsAsync("testDb");

            ContainerProperties containerProperties = new ContainerProperties
            {
                Id = "testContainer",
                PartitionKeyPath = "/Pk"
            };
            this.container = await this.database.CreateContainerIfNotExistsAsync(containerProperties);
        }

        public async Task Initialize(bool multiRegion)
        {
            this.client = TestCommon.CreateCosmosClient(false, multiRegion);
            this.database = await this.client.CreateDatabaseIfNotExistsAsync("testDb");

            ContainerProperties containerProperties = new ContainerProperties
            {
                Id = "testContainer",
                PartitionKeyPath = "/Pk"
            };
            this.container = await this.database.CreateContainerIfNotExistsAsync(containerProperties);
        }

        public async Task InitilizePreferredRegionsClient(FaultInjector faultInjector, List<string> preferredRegionList, bool multiRegion)
        {
            this.client = TestCommon.CreateCosmosClient(false, faultInjector, multiRegion, preferredRegionList);
            this.database = await this.client.CreateDatabaseIfNotExistsAsync("testDb");

            ContainerProperties containerProperties = new ContainerProperties
            {
                Id = "testContainer",
                PartitionKeyPath = "/Pk"
            };
            this.container = await this.database.CreateContainerIfNotExistsAsync(containerProperties);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (this.database != null) { await this.database.DeleteAsync(); }
            this.client?.Dispose();
        }

        [TestMethod]

        public void FaultInjectionServerErrorRule_OperationTypeTest()
        {

            List<OperationType> testScenarios = new List<OperationType>
            {
                OperationType.Read,
                OperationType.Replace,
                OperationType.Create,
                OperationType.Delete,
                OperationType.Query,
                OperationType.Patch
            };

            foreach (OperationType operationType in testScenarios)
            {
                if (!this.Timeout_FaultInjectionServerErrorRule_OperationTypeTest(operationType).Wait(Timeout))
                {
                    Assert.Fail("Test timed out");
                }
            }
        }

        private async Task Timeout_FaultInjectionServerErrorRule_OperationTypeTest(OperationType operationType)
        {
            //Test Server gone, operation type will be ignored after getting the address
            string serverGoneRuleId = "serverGoneRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serverGoneRule = new FaultInjectionRuleBuilder(serverGoneRuleId)
                .WithCondition(
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .Build())
                .WithResult(
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            string tooManyRequestsRuleId = "tooManyRequestsRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule tooManyRequestsRule = new FaultInjectionRuleBuilder(tooManyRequestsRuleId)
                .WithCondition(
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .Build())
                .WithResult(
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.TooManyRequests)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            serverGoneRule.Disable();
            tooManyRequestsRule.Disable();

            List<FaultInjectionRule> ruleList = new List<FaultInjectionRule> { serverGoneRule, tooManyRequestsRule };
            FaultInjector faultInjector = new FaultInjector(ruleList);
            await this.Initialize(faultInjector, true);
            Assert.AreEqual(0, serverGoneRule.GetAddresses().Count);

            try
            {
                JObject item = JObject.FromObject(new { id = Guid.NewGuid().ToString(), Pk = Guid.NewGuid().ToString() });
                if (operationType != OperationType.Create)
                {
                    _ = this.container != null
                        ? await this.container.CreateItemAsync(item) : null;
                }

                serverGoneRule.Enable();
                
                CosmosDiagnostics? diagnostics = this.container != null
                    ? await this.PerformDocumentOperation(this.container, operationType, item)
                    : null;
                Assert.IsNotNull(diagnostics);

                this.ValidateFaultInjectionRuleApplication(
                    diagnostics,
                    (int)HttpStatusCode.Gone,
                    (int)SubStatusCodes.Unknown,
                    serverGoneRule);

                serverGoneRule.Disable();

                if (operationType == OperationType.Delete)
                {
                    _ = this.container != null
                        ? await this.container.CreateItemAsync(item) : null;
                }

                if (operationType == OperationType.Create)
                {
                    _ = this.container != null
                        ? await this.container.DeleteItemAsync<JObject>(
                            (string)item["id"],
                            new Cosmos.PartitionKey((string)item["Pk"]))
                        : null;
                }

                Assert.AreEqual(0, tooManyRequestsRule.GetAddresses().Count);

                tooManyRequestsRule.Enable();           
                
                

                diagnostics = this.container != null
                    ? await this.PerformDocumentOperation(this.container, operationType, item)
                    : null;
                Assert.IsNotNull(diagnostics);

                if (operationType == OperationType.Read)
                {
                    this.ValidateHitCount(tooManyRequestsRule, 1);
                }
                else
                {
                    this.ValidateFaultInjectionRuleNotApplied(
                        diagnostics,
                        tooManyRequestsRule);
                }
            }
            finally
            {
                serverGoneRule.Disable();
                tooManyRequestsRule.Disable();
            }
        }

        [TestMethod]
        public void FaultInjectionServerErrorRule_OperationTypeAddressTest()
        {
            List<OperationType> testScenarios = new List<OperationType>
            {
                OperationType.Read,
                OperationType.Replace,
                OperationType.Create,
                OperationType.Delete,
                OperationType.Query,
                OperationType.Patch
            };

            foreach (OperationType operationType in testScenarios)
            {
                if (!this.Timeout_FaultInjectionServerErrorRule_OperationTypeAddressTest(operationType).Wait(Timeout))
                {
                    Assert.Fail("Test timed out");
                }
            }
        }

        private async Task Timeout_FaultInjectionServerErrorRule_OperationTypeAddressTest(OperationType operationType)
        {
            await this.Initialize(true);

            List<string> preferredRegions = new List<string>() { };
            List<string> writeRegions = new List<string>();
            List<string> readRegions;

            GlobalEndpointManager? globalEndpointManager = this.client?.ClientContext.DocumentClient.GlobalEndpointManager;
            if (globalEndpointManager != null)
            {
                (writeRegions, readRegions) = await this.GetReadWriteEndpoints(globalEndpointManager);

                for (int i = 0; i < readRegions?.Count; i++)
                {
                    if (writeRegions != null && writeRegions.Contains(readRegions[i]))
                    {
                        preferredRegions.Add(readRegions[i].ToString());
                    }
                    else
                    {
                        preferredRegions.Insert(0, readRegions[i].ToString());
                    }
                }
            }

            this.client?.Dispose();

            JObject item = JObject.FromObject(new { id = Guid.NewGuid().ToString(), Pk = Guid.NewGuid().ToString() });

            string writeRegionServerGoneRuleId = "writeRegionServerGoneRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule writeRegionServerGoneRule = new FaultInjectionRuleBuilder(writeRegionServerGoneRuleId)
                .WithCondition(
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .Build())
                .WithResult(
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .WithStartDelay(TimeSpan.FromMilliseconds(200))
                .Build();

            string primaryReplicaServerGoneRuleId = "primaryReplicaServerGoneRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule primaryReplicaServerGoneRule = new FaultInjectionRuleBuilder(primaryReplicaServerGoneRuleId)
                .WithCondition(
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .WithEndpoint(
                            new FaultInjectionEndpointBuilder(
                                "testDb", 
                                "testContainer", 
                                FeedRange.FromPartitionKey(new Cosmos.PartitionKey((string)item["Pk"])))
                                .WithReplicaCount(3)
                                .Build())
                        .Build())
                .WithResult(
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .WithStartDelay(TimeSpan.FromMilliseconds(200))
                .Build();

            writeRegionServerGoneRule.Disable();
            primaryReplicaServerGoneRule.Disable();

            List<FaultInjectionRule> ruleList = new List<FaultInjectionRule> { writeRegionServerGoneRule, primaryReplicaServerGoneRule };
            FaultInjector faultInjector = new FaultInjector(ruleList);

            await this.InitilizePreferredRegionsClient(faultInjector, preferredRegions, true);

            _ = this.container != null
                ? await this.container.CreateItemAsync(item) : null;

            globalEndpointManager = ((ChaosInterceptor)faultInjector.GetChaosInterceptor())
                .GetRuleStore()?
                .GetRuleProcessor()?
                .GetGlobalEndpointManager();
            Assert.IsNotNull(globalEndpointManager);

            try
            {
                Assert.AreEqual(writeRegions?.Count + 1, writeRegionServerGoneRule.GetRegionEndpoints().Count);

                writeRegionServerGoneRule.Enable();
                CosmosDiagnostics? diagnostics = this.container != null
                    ? await this.PerformDocumentOperation(this.container, operationType, item) : null;
                Assert.IsNotNull(diagnostics);

                if (OperationTypeExtensions.IsWriteOperation(operationType))
                {
                    this.ValidateHitCount(writeRegionServerGoneRule, 1);
                    this.ValidateFaultInjectionRuleApplication(
                        diagnostics,
                        (int)HttpStatusCode.Gone,
                        (int)SubStatusCodes.Unknown,
                        writeRegionServerGoneRule);
                }
                else
                {
                    this.ValidateFaultInjectionRuleNotApplied(
                        diagnostics,
                        writeRegionServerGoneRule);
                }

                writeRegionServerGoneRule.Disable();
                primaryReplicaServerGoneRule.Enable();

                Assert.AreEqual(globalEndpointManager.WriteEndpoints.Count + 1, primaryReplicaServerGoneRule.GetRegionEndpoints().Count);
                foreach (Uri region in globalEndpointManager.WriteEndpoints)
                {
                    Assert.IsTrue(primaryReplicaServerGoneRule.GetRegionEndpoints().Contains(region));
                }
                Assert.AreEqual(globalEndpointManager.WriteEndpoints.Count, primaryReplicaServerGoneRule.GetAddresses().Count);
            }
            finally
            {
                writeRegionServerGoneRule.Disable();
                primaryReplicaServerGoneRule.Disable();
            }
        }

        [TestMethod]
        public void FaultInjectionServerErrorRule_RegionTest()
        {
            if (!this.Timeout_FaultInjectionServerErrorRule_RegionTest().Wait(Timeout))
            {
                Assert.Fail("Test timed out");
            }
        }

        private async Task Timeout_FaultInjectionServerErrorRule_RegionTest()
        {
            await this.Initialize(true);

            List<string> preferredRegions = new List<string>() { };
            List<string> readRegions;
            ReadOnlyDictionary<string, Uri> readEndpoints = new ReadOnlyDictionary<string, Uri>(new Dictionary<string, Uri>());

            GlobalEndpointManager? globalEndpointManager = this.client?.ClientContext.DocumentClient.GlobalEndpointManager;
            if (globalEndpointManager != null)
            {
                readEndpoints = globalEndpointManager.GetAvailableReadEndpointsByLocation();
                (_, readRegions) = await this.GetReadWriteEndpoints(globalEndpointManager);

                preferredRegions = new List<string>(readRegions);
            }

            this.client?.Dispose();

            string localRegionRuleId = "localRegionRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule localRegionRule = new FaultInjectionRuleBuilder(localRegionRuleId)
                .WithCondition(
                    new FaultInjectionConditionBuilder()
                        .WithRegion(preferredRegions[0])
                        .Build())
                .WithResult(
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            string remoteRegionRuleId = "remoteRegionRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule remoteRegionRule = new FaultInjectionRuleBuilder(remoteRegionRuleId)
                .WithCondition(
                    new FaultInjectionConditionBuilder()
                        .WithRegion(preferredRegions[1])
                        .Build())
                .WithResult(
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            localRegionRule.Disable();
            remoteRegionRule.Disable();

            try
            {
                List<FaultInjectionRule> rules = new List<FaultInjectionRule> { localRegionRule, remoteRegionRule };
                FaultInjector faultInjector = new FaultInjector(rules);
                await this.Initialize(faultInjector, true);

                JObject databaseItem = JObject.FromObject(new { id = Guid.NewGuid().ToString(), Pk = Guid.NewGuid().ToString() });
                _ = this.container != null
                    ? await this.container.CreateItemAsync(databaseItem) : null;

                globalEndpointManager = this.client?.ClientContext.DocumentClient.GlobalEndpointManager;

                if (globalEndpointManager != null)
                {                   
                    Assert.AreEqual(1, localRegionRule.GetRegionEndpoints().Count);
                    Assert.AreEqual(readEndpoints[preferredRegions[0]], localRegionRule.GetRegionEndpoints()[0]);

                    Assert.AreEqual(1, remoteRegionRule.GetRegionEndpoints().Count);
                    Assert.AreEqual(readEndpoints[preferredRegions[1]], remoteRegionRule.GetRegionEndpoints()[0]);
                }

                localRegionRule.Enable();
                remoteRegionRule.Enable();

                CosmosDiagnostics? diagnostics = this.container != null
                    ? await this.PerformDocumentOperation(this.container, OperationType.Read, databaseItem)
                    : null;
                Assert.IsNotNull(diagnostics);

                this.ValidateHitCount(localRegionRule, 1);
                this.ValidateHitCount(remoteRegionRule, 0);
                this.ValidateFaultInjectionRuleApplication(
                    diagnostics,
                    (int)HttpStatusCode.Gone,
                    (int)SubStatusCodes.ServerGenerated410,
                    localRegionRule);
            }
            finally
            {
                localRegionRule.Disable();
                remoteRegionRule.Disable();
            }
        }

        [TestMethod]
        public void FaultInjectionServerErrorRule_PartitionTest()
        {
            if (!this.Timeout_FaultInjectionServerErrorRule_RegionTest().Wait(Timeout))
            {
                Assert.Fail("Test timed out");
            }
        }

        private async Task Timeout_FaultInjectionServerErrorRule_PartitionTest()
        {
            await this.Initialize(true);
            if (this.container != null && this.client != null)
            {
                for (int i = 0; i < 10; i++)
                {
                    await this.container.CreateItemAsync(JObject.FromObject(new { id = Guid.NewGuid().ToString(), Pk = Guid.NewGuid().ToString() }));
                }

                List<FeedRange> feedRanges = (List<FeedRange>)await this.container.GetFeedRangesAsync();
                Assert.IsTrue(feedRanges.Count > 1);

                string query = "SELECT * FROM c";
                QueryRequestOptions queryOptions = new QueryRequestOptions
                {
                    FeedRange = feedRanges[0]
                };

                JObject query0 = (await this.container.GetItemQueryIterator<JObject>(query, requestOptions: queryOptions).ReadNextAsync()).First();

                queryOptions.FeedRange = feedRanges[1];
                JObject query1 = (await this.container.GetItemQueryIterator<JObject>(query, requestOptions: queryOptions).ReadNextAsync()).First();

                this.client?.Dispose();

                string serverErrorFeedRangeRuleId = "serverErrorFeedRangeRule-" + Guid.NewGuid().ToString();
                FaultInjectionRule serverErrorFeedRangeRule = new FaultInjectionRuleBuilder(serverErrorFeedRangeRuleId)
                    .WithCondition(
                        new FaultInjectionConditionBuilder()
                            .WithEndpoint(
                                new FaultInjectionEndpointBuilder("testDb", "testContianer", feedRanges[0])
                                    .Build())
                            .Build())
                    .WithResult(
                        FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.TooManyRequests)
                        .WithTimes(1)
                        .Build())
                .Build();

                serverErrorFeedRangeRule.Disable();

                List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serverErrorFeedRangeRule };
                FaultInjector faultInjector = new FaultInjector(rules);
                await this.Initialize(faultInjector, true);

                GlobalEndpointManager? globalEndpointManager = this.client?.ClientContext.DocumentClient.GlobalEndpointManager;
                List<Uri> readRegions = new List<Uri>();
                if (globalEndpointManager != null) { readRegions = (List<Uri>)globalEndpointManager.ReadEndpoints.AsEnumerable(); }

                Assert.IsTrue(serverErrorFeedRangeRule.GetRegionEndpoints().Count == readRegions.Count);

                foreach (Uri regionEndpoint in readRegions)
                {
                    Assert.IsTrue(serverErrorFeedRangeRule.GetRegionEndpoints().Contains(regionEndpoint));
                }

                Assert.IsTrue(
                    serverErrorFeedRangeRule.GetAddresses().Count >= 3 * readRegions.Count
                    && serverErrorFeedRangeRule.GetAddresses().Count <= 5 * readRegions.Count);

                serverErrorFeedRangeRule.Enable();

                CosmosDiagnostics? diagnostics = this.container != null
                    ? (await this.container.ReadItemAsync<JObject>((string)query0["id"], new Cosmos.PartitionKey((string)query0["Pk"]))).Diagnostics
                    : null;
                Assert.IsNotNull(diagnostics);

                this.ValidateHitCount(serverErrorFeedRangeRule, 1);
                this.ValidateFaultInjectionRuleApplication(
                    diagnostics,
                    (int)StatusCodes.TooManyRequests,
                    (int)SubStatusCodes.Unknown,
                    serverErrorFeedRangeRule);

                try
                {
                    diagnostics = this.container != null
                        ? (await this.container.ReadItemAsync<JObject>((string)query1["id"], new Cosmos.PartitionKey((string)query1["Pk"]))).Diagnostics
                        : null;
                    Assert.IsNotNull(diagnostics);
                    Assert.IsTrue(diagnostics.ToString().Contains("200"));
                    this.ValidateHitCount(serverErrorFeedRangeRule, 1);
                }
                finally
                {
                    serverErrorFeedRangeRule.Disable();
                }
            }          
        }

        [TestMethod]
        public void FaultInjectionServerErrorRule_ServerResponseDelay()
        {
            if (!this.Timeout_FaultInjectionServerErrorRule_ServerResponseDelay().Wait(Timeout))
            {
                Assert.Fail("Test timed out");
            }
        }

        private async Task Timeout_FaultInjectionServerErrorRule_ServerResponseDelay()
        {
            string timeoutRuleId = "timeoutRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule timeoutRule = new FaultInjectionRuleBuilder(timeoutRuleId)
                .WithCondition(
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .Build())
                .WithResult(
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Timeout)
                        .WithDelay(TimeSpan.FromSeconds(6))
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            timeoutRule.Disable();

            await this.Initialize(true);

            JObject createdItem = JObject.FromObject(new { id = Guid.NewGuid().ToString(), Pk = Guid.NewGuid().ToString() });
            ItemResponse<JObject>? itemResponse = this.container != null
                ? await this.container.CreateItemAsync<JObject>(createdItem)
                : null;
            Assert.IsNotNull(itemResponse);

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { timeoutRule });

                CosmosClient testClient = new CosmosClient(
                    accountEndpoint: TestCommon.EndpointMultiRegion,
                    authKeyOrResourceToken: TestCommon.AuthKeyMultiRegion,
                    clientOptions: new CosmosClientOptions()
                    {
                        EnableContentResponseOnWrite = true,
                        ConnectionMode = ConnectionMode.Direct,
                        OpenTcpConnectionTimeout = TimeSpan.FromSeconds(1),
                        ChaosInterceptor = faultInjector.GetChaosInterceptor()
                    });

                Container testContainer = testClient.GetContainer("testDb", "testContainer");
                timeoutRule.Enable();
                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                TimeSpan elapsed;
                ItemResponse<JObject>? readResponse = await testContainer.ReadItemAsync<JObject>(
                    (string)createdItem["id"], 
                    new Cosmos.PartitionKey((string)createdItem["Pk"]));
                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();

                Assert.IsTrue(elapsed.TotalSeconds >= 6);
                Assert.IsNotNull(readResponse);
                this.ValidateHitCount(timeoutRule, 1);
                this.ValidateFaultInjectionRuleApplication(
                    readResponse.Diagnostics,
                    (int)StatusCodes.Gone,
                    (int)SubStatusCodes.TransportGenerated410,
                    timeoutRule);
                testClient.Dispose();
            }
            finally
            {
                timeoutRule.Disable();
            }
        }

        [TestMethod]
        public void FaultInjectionServerErrorRule_ConnectionTimeout()
        {
            if (!this.Timeout_FaultInjectionServerErrorRule_ConnecitonTimeout().Wait(Timeout))
            {
                Assert.Fail("Test timed out");
            }
        }

        private async Task Timeout_FaultInjectionServerErrorRule_ConnecitonTimeout()
        {
            string connectionTimeoutRuleId = "serverConnectionTimeoutRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule connectionTimeoutRule = new FaultInjectionRuleBuilder(connectionTimeoutRuleId)
                .WithCondition(
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .Build())
                .WithResult(
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ConnectionDelay)
                        .WithDelay(TimeSpan.FromSeconds(2))
                        .WithTimes(10)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            connectionTimeoutRule.Disable();
            await this.Initialize(true);

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { connectionTimeoutRule });

                CosmosClient testClient = new CosmosClient(
                    accountEndpoint: TestCommon.EndpointMultiRegion,
                    authKeyOrResourceToken: TestCommon.AuthKeyMultiRegion,
                    clientOptions: new CosmosClientOptions()
                    {
                        EnableContentResponseOnWrite = true,
                        ConnectionMode = ConnectionMode.Direct,
                        OpenTcpConnectionTimeout = TimeSpan.FromSeconds(1),
                        RequestTimeout = TimeSpan.FromSeconds(1),
                        ChaosInterceptor = faultInjector.GetChaosInterceptor()
                    });
                Container testContainer = testClient.GetContainer("testDb", "testContainer");

                JObject createdItem = JObject.FromObject(new { id = Guid.NewGuid().ToString(), Pk = Guid.NewGuid().ToString() });
                connectionTimeoutRule.Enable();
                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                TimeSpan elapsed;
                ItemResponse<JObject>? itemResponse = await testContainer.CreateItemAsync<JObject>(createdItem);
                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();

                Assert.IsTrue(elapsed.TotalSeconds >= 2);
                Assert.IsNotNull(itemResponse);
                Assert.IsTrue(connectionTimeoutRule.GetHitCount() == 1 || connectionTimeoutRule.GetHitCount() == 2);
                testClient.Dispose();
            }
            finally
            {
                connectionTimeoutRule.Disable();
            }
        }

        [TestMethod]
        public void FaultInjectionServerErrorRule_ConnectionDelay()
        {
            if (!this.Timeout_FaultInjectionServerErrorRule_ConnecitonDelay().Wait(Timeout))
            {
                Assert.Fail("Test timed out");
            }
        }

        private async Task Timeout_FaultInjectionServerErrorRule_ConnecitonDelay()
        {
            string connectionDelayRuleId = "serverConnectionDelayRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule connectionDelayRule = new FaultInjectionRuleBuilder(connectionDelayRuleId)
                .WithCondition(
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .Build())
                .WithResult(
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ConnectionDelay)
                        .WithDelay(TimeSpan.FromMilliseconds(100))
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            await this.Initialize(true);

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { connectionDelayRule });

                CosmosClient timeoutClient = new CosmosClient(
                    accountEndpoint: TestCommon.EndpointMultiRegion,
                    authKeyOrResourceToken: TestCommon.AuthKeyMultiRegion,
                    clientOptions: new CosmosClientOptions()
                    {
                        EnableContentResponseOnWrite = true,
                        ConnectionMode = ConnectionMode.Direct,
                        OpenTcpConnectionTimeout = TimeSpan.FromSeconds(1),
                        ChaosInterceptor = faultInjector.GetChaosInterceptor()
                    });
                Container timeoutContainer = timeoutClient.GetContainer("testDb", "testContainer");

                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                TimeSpan elapsed;
                JObject createdItem = JObject.FromObject(new { id = Guid.NewGuid().ToString(), Pk = Guid.NewGuid().ToString() });
                ItemResponse<JObject>? itemResponse = await timeoutContainer.CreateItemAsync<JObject>(createdItem);
                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();

                Assert.IsTrue(elapsed.TotalMilliseconds >= 100);
                timeoutClient.Dispose();

                Assert.IsNotNull(itemResponse);
                Assert.IsTrue(connectionDelayRule.GetHitCount() == 1 || connectionDelayRule.GetHitCount() == 2);
                Assert.IsTrue((int)itemResponse.StatusCode == (int)StatusCodes.Created);
            }
            finally
            {
                connectionDelayRule.Disable();
            }
        }

        [TestMethod]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.Gone, 410, 21005, DisplayName = "Gone")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.InternalServerEror, 500, 0, DisplayName = "InternalServerError")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.RetryWith, 449, 0, DisplayName = "RetryWith")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.TooManyRequests, 429, 0, DisplayName = "TooManyRequests")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.ReadSessionNotAvailable, 404, 1002, DisplayName = "ReadSessionNotAvailable")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.Timeout, 410, 20001, DisplayName = "Timeout")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.PartitionIsMigrating, 410, 1008, DisplayName = "PartitionIsMigrating")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.PartitionIsSplitting, 410, 1007, DisplayName = "PartitionIsSplitting")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.Gone, 410, 21005, DisplayName = "Gone Write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.InternalServerEror, 500, 0, DisplayName = "InternalServerError Write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.RetryWith, 449, 0, DisplayName = "RetryWith Write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.TooManyRequests, 429, 0, DisplayName = "TooManyRequests Write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.ReadSessionNotAvailable, 404, 1002, DisplayName = "ReadSessionNotAvailable Write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.Timeout, 410, 20001, DisplayName = "Timeout Write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.PartitionIsMigrating, 410, 1008, DisplayName = "PartitionIsMigrating Write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.PartitionIsSplitting, 410, 1007, DisplayName = "PartitionIsSplitting Write")]
        public void FaultInjectionServerErrorRule_ServerErrorResponseTest(
            FaultInjectionOperationType faultInjectionOperationType,
            FaultInjectionServerErrorType serverErrorType,
            int errorStatusCode,
            int substatusCode)
        {
            OperationType operationType = faultInjectionOperationType == FaultInjectionOperationType.ReadItem
                ? OperationType.Read
                : OperationType.Create;

            if (!this.Timeout_FaultInjectionServerErrorRule_ServerErrorResponseTest(
                    operationType,
                    faultInjectionOperationType,
                    serverErrorType,
                    errorStatusCode,
                    substatusCode).Wait(Timeout))
            {
                Assert.Fail("Test timed out");
            }
        }

        private async Task Timeout_FaultInjectionServerErrorRule_ServerErrorResponseTest(
            OperationType operationType,
            FaultInjectionOperationType faultInjectionOperationType,
            FaultInjectionServerErrorType serverErrorType,
            int errorStatusCode,
            int substatusCode)
        {
            await this.Initialize(true);

            JObject item = JObject.FromObject(new { id = Guid.NewGuid().ToString(), Pk = Guid.NewGuid().ToString() });
            _ = this.container != null ? await this.container.CreateItemAsync(item) : null;
            this.client?.Dispose();

            string serverErrorResponseRuleId = "serverErrorResponseRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serverErrorResponseRule = new FaultInjectionRuleBuilder(serverErrorResponseRuleId)
                .WithCondition(
                    new FaultInjectionConditionBuilder()
                    .WithOperationType(faultInjectionOperationType)
                    .Build())
                .WithResult(
                    FaultInjectionResultBuilder.GetResultBuilder(serverErrorType)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();
            serverErrorResponseRule.Disable();

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { serverErrorResponseRule });
                await this.Initialize(faultInjector, true);

                serverErrorResponseRule.Enable();
                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                TimeSpan elapsed;
                CosmosDiagnostics? diagnostics = this.container != null
                    ? await this.PerformDocumentOperation(this.container, operationType, item)
                    : null;
                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();

                if (serverErrorType == FaultInjectionServerErrorType.Timeout)
                {
                    Assert.IsTrue(
                        elapsed.TotalSeconds 
                        >= ((ChaosInterceptor)faultInjector.GetChaosInterceptor()).GetRequestTimeout().TotalSeconds);
                }
                Assert.IsNotNull(diagnostics);

                this.ValidateHitCount(serverErrorResponseRule, 1);
                this.ValidateFaultInjectionRuleApplication(
                    diagnostics,
                    errorStatusCode,
                    substatusCode,
                    serverErrorResponseRule);             
            }
            finally
            {
                serverErrorResponseRule.Disable();
            }
        }

        [TestMethod]
        public void FaultInjectionServerErrorRule_HitCountTest()
        {
            this.Timeout_FaultInjectionServerErrorRule_HitCountTest().Wait();
            //if (!this.Timeout_FaultInjectionServerErrorRule_HitCountTest().Wait(Timeout))
            //{
            //    Assert.Fail("Test timed out");
            //}
        }

        private async Task Timeout_FaultInjectionServerErrorRule_HitCountTest()
        {
            string hitCountRuleId = "hitCountRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule hitCountRule = new FaultInjectionRuleBuilder(hitCountRuleId)
                .WithCondition(
                    new FaultInjectionConditionBuilder()
                    .Build())
                .WithResult(
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
                        .WithTimes(1)
                        .Build())
                .WithHitLimit(2)
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();
            hitCountRule.Disable();

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { hitCountRule });

                await this.Initialize(faultInjector, true);

                JObject createdItem = JObject.FromObject(new { id = Guid.NewGuid().ToString(), Pk = Guid.NewGuid().ToString() });

                ItemResponse<JObject>? itemResponse = this.container != null
                    ? await this.container.CreateItemAsync<JObject>(createdItem)
                    : null;
                Assert.IsNotNull(itemResponse);

                CosmosDiagnostics? cosmosDiagnostics;

                hitCountRule.Enable();
                for (int i = 0; i < 3; i++)
                {
                    cosmosDiagnostics = this.container != null
                        ? await this.PerformDocumentOperation(this.container, OperationType.Read, createdItem)
                        : null;
                    Assert.IsNotNull(cosmosDiagnostics);

                    if (i < 2)
                    {
                        this.ValidateFaultInjectionRuleApplication(
                            cosmosDiagnostics,
                            (int)HttpStatusCode.Gone, (int)SubStatusCodes.ServerGenerated410,
                            hitCountRule);
                        this.ValidateHitCount(hitCountRule, i + 1);
                    }
                    else
                    {
                        Assert.IsTrue(cosmosDiagnostics.ToString().Contains("200"));
                        this.ValidateHitCount(hitCountRule, 2);
                    }
                }
            }
            finally
            {
                hitCountRule.Disable();
            }
        }

        [TestMethod]
        public void FaultInjectionServerErrorRule_IncludePrimaryTest()
        {
            if (!this.Timeout_FaultInjectionServerErrorRule_IncludePrimaryTest().Wait(Timeout))
            {
                Assert.Fail("Test timed out");
            }
        }

        private async Task Timeout_FaultInjectionServerErrorRule_IncludePrimaryTest()
        {
            await this.Initialize(false);

            List<FeedRange>? feedRanges = this.container != null
                ? (List<FeedRange>)await this.container.GetFeedRangesAsync() : null;
            Assert.IsTrue(feedRanges != null && feedRanges.Count > 0);

            JObject item = JObject.FromObject(new { id = Guid.NewGuid().ToString(), Pk = Guid.NewGuid().ToString() });

            string includePrimaryServerGoneRuleId = "includePrimaryServerGoneRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule includePrimaryServerGoneRule = new FaultInjectionRuleBuilder(includePrimaryServerGoneRuleId)
                .WithCondition(
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .WithEndpoint(
                            new FaultInjectionEndpointBuilder("testDb", "testContainer", feedRanges[0])
                                .WithReplicaCount(1)
                                .WithIncludePrimary(true)
                                .Build())
                        .Build())
                .WithResult(
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();
            
            includePrimaryServerGoneRule.Disable();
            this.client?.Dispose();

            List<FaultInjectionRule> ruleList = new List<FaultInjectionRule> { includePrimaryServerGoneRule };
            FaultInjector faultInjector = new FaultInjector(ruleList);

            await this.Initialize(faultInjector, false);

            try
            {
                includePrimaryServerGoneRule.Enable();
                CosmosDiagnostics? cosmosDiagnostics = this.container != null
                    ? await this.PerformDocumentOperation(this.container, OperationType.Create, item)
                    : null;
                Assert.IsNotNull(cosmosDiagnostics);

                this.ValidateHitCount(includePrimaryServerGoneRule, 1);
                this.ValidateFaultInjectionRuleApplication(
                    cosmosDiagnostics,
                    (int)HttpStatusCode.Gone,
                    (int)SubStatusCodes.ServerGenerated410,
                    includePrimaryServerGoneRule);

                cosmosDiagnostics = this.container != null
                    ? await this.PerformDocumentOperation(this.container, OperationType.Upsert, item)
                    : null;
                Assert.IsNotNull(cosmosDiagnostics);
                this.ValidateHitCount(includePrimaryServerGoneRule, 2);
                this.ValidateFaultInjectionRuleApplication(
                    cosmosDiagnostics,
                    (int)HttpStatusCode.Gone,
                    (int)SubStatusCodes.ServerGenerated410,
                    includePrimaryServerGoneRule);
            }
            finally
            {
                includePrimaryServerGoneRule.Disable();
            }
        }

        [TestMethod]
        public void FaultInjectionConnectionErrorRule_Test()
        {
            if (!this.Timeout_FaultInjectionConnectionErrorRule_Test().Wait(Timeout))
            {
                Assert.Fail("Test timed out");
            }
        }

        private async Task Timeout_FaultInjectionConnectionErrorRule_Test()
        {
            string ruldId = "connectionErrorRule-close-" + Guid.NewGuid().ToString();
            FaultInjectionRule connectionErrorRule = new FaultInjectionRuleBuilder(ruldId)
                .WithCondition(
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .Build())
                .WithResult(
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionConnectionErrorType.RecieveFailed)
                        .WithInterval(TimeSpan.FromSeconds(1))
                        .WithThreshold(1.0)
                        .Build())
                .WithDuration(TimeSpan.FromSeconds(3))
                .Build();
            
            FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { connectionErrorRule });
            await this.Initialize(faultInjector, true);
            
            FaultInjectionDynamicChannelStore channelStore = ((ChaosInterceptor)faultInjector.GetChaosInterceptor()).GetChannelStore();
            Assert.IsTrue(channelStore.GetAllChannels().Count > 0);

            connectionErrorRule.Enable();
            await Task.Delay(TimeSpan.FromSeconds(2));

            JObject item = JObject.FromObject(new { id = Guid.NewGuid().ToString(), Pk = Guid.NewGuid().ToString() });
            CosmosDiagnostics? cosmosDiagnostics = this.container != null
                    ? await this.PerformDocumentOperation(this.container, OperationType.Create, item)
                    : null;
            Assert.IsNotNull(cosmosDiagnostics);
            Assert.IsTrue(connectionErrorRule.GetHitCount() >= 1);
            int hitCount = (int)connectionErrorRule.GetHitCount();

            await Task.Delay(TimeSpan.FromSeconds(2));

            cosmosDiagnostics = this.container != null
                    ? await this.PerformDocumentOperation(this.container, OperationType.Read, item)
                    : null;
            Assert.IsNotNull(cosmosDiagnostics);
            Assert.IsTrue(connectionErrorRule.GetHitCount() == hitCount);

        }

        private async Task<CosmosDiagnostics> PerformDocumentOperation(Container testContainer, OperationType operationType, JObject item)
        {
            try
            {
                if (operationType == OperationType.Query)
                {
                    QueryRequestOptions queryOptions = new QueryRequestOptions();
                    string query = String.Format("SELECT * FROM c WHERE c.Id = '{0}'", item["id"]);
                    FeedResponse<JObject>? queryResponse = await testContainer.GetItemQueryIterator<JObject>(query, requestOptions: queryOptions).ReadNextAsync();

                    return queryResponse.Diagnostics;
                }

                if (operationType == OperationType.Read
                    || operationType == OperationType.Delete
                    || operationType == OperationType.Replace
                    || operationType == OperationType.Patch
                    || operationType == OperationType.Create
                    || operationType == OperationType.Upsert)
                {
                    if (operationType == OperationType.Read)
                    {
                        return (await testContainer.ReadItemAsync<JObject>((string)item["id"], new Cosmos.PartitionKey((string)item["Pk"]))).Diagnostics;
                    }

                    if (operationType == OperationType.Replace)
                    {
                        return (await testContainer.ReplaceItemAsync<JObject>(
                            item,
                            (string)item["id"],
                            new Cosmos.PartitionKey((string)item["Pk"]))).Diagnostics;
                    }

                    if (operationType == OperationType.Delete)
                    {
                        return (await testContainer.DeleteItemAsync<JObject>((string)item["id"], new Cosmos.PartitionKey((string)item["Pk"]))).Diagnostics;
                    }

                    if (operationType == OperationType.Create)
                    {
                        return (await testContainer.CreateItemAsync<JObject>(item, new Cosmos.PartitionKey((string)item["Pk"]))).Diagnostics;

                    }

                    if (operationType == OperationType.Upsert)
                    {
                        return (await testContainer.UpsertItemAsync<JObject>(item, new Cosmos.PartitionKey((string)item["Pk"]))).Diagnostics;
                    }

                    if (operationType == OperationType.Patch)
                    {                 
                        List<PatchOperation> patchOperations = new List<PatchOperation>
                        {
                            PatchOperation.Add("/" + Guid.NewGuid().ToString(), Guid.NewGuid().ToString())
                        };

                        return (await testContainer.PatchItemAsync<JObject>(
                            (string)item["id"],
                            new Cosmos.PartitionKey((string)item["Pk"]),
                            patchOperations)).Diagnostics;
                    }
                }

                throw new ArgumentException("Invalid Operation Type");
            }
            catch (CosmosException ex)
            {
                return ex.Diagnostics;
            }
        }

        private async Task<(List<string>, List<string>)> GetReadWriteEndpoints(GlobalEndpointManager globalEndpointManager)
        {
            AccountProperties accountProperties = await globalEndpointManager.GetDatabaseAccountAsync();
            List<string> writeRegions = accountProperties.WritableRegions.Select(region => region.Name).ToList();
            List<string> readRegions = accountProperties.ReadableRegions.Select(region => region.Name).ToList();
            return (writeRegions, readRegions);
        }
        private void ValidateHitCount(FaultInjectionRule rule, long expectedHitCount)
        {
            Assert.AreEqual(expectedHitCount, rule.GetHitCount());
        }

        private void ValidateFaultInjectionRuleNotApplied(
            CosmosDiagnostics diagnostics,
            FaultInjectionRule rule)
        {
            string diagnosticsString = diagnostics.ToString();
            Assert.AreEqual(0, rule.GetHitCount());
            Assert.AreEqual(0, diagnostics.GetFailedRequestCount());
            Assert.IsTrue(
                diagnosticsString.Contains("200") 
                || diagnosticsString.Contains("201") 
                || diagnosticsString.Contains("204"));
        }
        private void ValidateFaultInjectionRuleApplication(
            CosmosDiagnostics diagnostics,
            int statusCode,
            int subStatusCode,
            FaultInjectionRule rule)
        {
            string diagnosticsString = diagnostics.ToString();
            Assert.IsTrue(1 <= rule.GetHitCount());
            Assert.IsTrue(1 <= diagnostics.GetFailedRequestCount());
            Assert.IsTrue(diagnosticsString.Contains(statusCode.ToString()));
            Assert.IsTrue(diagnosticsString.Contains(subStatusCode.ToString()));
        }
    }
}
