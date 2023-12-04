namespace Microsoft.Azure.Cosmos.FaultInjection.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.FaultInjection.Tests.Utils;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;
    using Microsoft.Azure.Documents;
    using System.Net.Security;
    using System.Configuration;

    [TestClass]
    public class FaultInjectionServerErrorRuleDirectModeTests
    {
        private const int Timeout = 6000;
        private const string FaultInjectionRuleNonApplicableOperationType = "OperationType mismatch";

        private CosmosClient client = null;
        private Database database = null;
        private Container container = null;

        public async Task Initialize(FaultInjector faultInjector)
        {
            this.client = TestCommon.CreateCosmosClient(false, faultInjector);
            this.database = await this.client.CreateDatabaseIfNotExistsAsync("testDb");
            
            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId("containerRId");
            containerProperties.Id = "testContainer";
            containerProperties.PartitionKeyPath = "/Pk";
            this.container = await this.database.CreateContainerIfNotExistsAsync(containerProperties);
        }

        public async Task Initialize()
        {
            this.client = TestCommon.CreateCosmosClient(false);
            this.database = await this.client.CreateDatabaseIfNotExistsAsync("testDb");

            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId("containerRId");
            containerProperties.Id = "testContainer";
            containerProperties.PartitionKeyPath = "/Pk";
            this.container = await this.database.CreateContainerIfNotExistsAsync(containerProperties);
        }

        public CosmosClient InitilizePreferredRegionsClient(FaultInjector faultInjector, List<string> preferredRegionList)
        {
            CosmosClient client = TestCommon.CreateCosmosClient(false, faultInjector, preferredRegionList);
            return client;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await this.database.DeleteAsync();
            this.client.Dispose();
        }

        [TestMethod]
        [DataRow(OperationType.Read, DisplayName = "Read")]
        [DataRow(OperationType.Replace, DisplayName = "Replace")]
        [DataRow(OperationType.Create, DisplayName = "Create")]
        [DataRow(OperationType.Delete, DisplayName = "Delete")]
        [DataRow(OperationType.Query, DisplayName = "Query")]
        [DataRow(OperationType.Patch, DisplayName = "Patch")]
        public async Task FaultInjectionServerErrorRule_OperationTypeTest(OperationType operationType)
        {
            if (!this.Timeout_FaultInjectionServerErrorRule_OperationTypeTest(operationType).Wait(Timeout))
            {
                Assert.Fail("Test timed out");
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

            List<FaultInjectionRule> ruleList = new List<FaultInjectionRule> { serverGoneRule, tooManyRequestsRule };
            FaultInjector faultInjector = new FaultInjector(ruleList);
            await this.Initialize(faultInjector);
            Assert.AreEqual(0, serverGoneRule.GetAddresses().Count);

            try
            {
                tooManyRequestsRule.Disable();
                DatabaseItem item = new DatabaseItem(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
                CosmosDiagnostics diagnostics = await this.PerformDocumentOperation(operationType, item);
                this.ValidateFaultInjectionRuleApplication(
                    diagnostics,
                    HttpConstants.StatusCode.Gone,
                    HttpConstants.SubStatusCodes.ServerGenerated410,
                    serverGoneRule,
                    true);

                serverGoneRule.Disable();
                Assert.AreEqual(0, tooManyRequestsRule.GetAddresses().Count);

                tooManyRequestsRule.Enable();
                diagnostics = await this.PerformDocumentOperation(operationType, item);
                if (operationType == OperationType.Read)
                {
                    this.ValidateHitCount(tooManyRequestsRule, 1);
                }
                else
                {
                    this.ValidateFaultInjectionRuleNotApplied(
                        diagnostics,
                        tooManyRequestsRule,
                        FaultInjectionRuleNonApplicableOperationType);
                }
            }
            finally
            {
                serverGoneRule.Disable();
                tooManyRequestsRule.Disable();
            }
        }

        [TestMethod]
        [DataRow(OperationType.Read, DisplayName = "Read")]
        [DataRow(OperationType.Replace, DisplayName = "Replace")]
        [DataRow(OperationType.Create, DisplayName = "Create")]
        [DataRow(OperationType.Delete, DisplayName = "Delete")]
        [DataRow(OperationType.Query, DisplayName = "Query")]
        [DataRow(OperationType.Patch, DisplayName = "Patch")]
        public async Task FaultInjectionServerErrorRule_OperationTypeAddressTest(OperationType operationType)
        {
            if (!this.Timeout_FaultInjectionServerErrorRule_OperationTypeAddressTest(operationType).Wait(Timeout))
            {
                Assert.Fail("Test timed out");
            }
        }

        private async Task Timeout_FaultInjectionServerErrorRule_OperationTypeAddressTest(OperationType operationType)
        {
            CosmosClient tempClient = TestCommon.CreateCosmosClient(false);
            Database tempDatabase = await tempClient.CreateDatabaseIfNotExistsAsync("testDb");
            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId("containerRId");
            containerProperties.Id = "testContainer";
            containerProperties.PartitionKeyPath = "/Pk";
            Container tempContainer = await tempDatabase.CreateContainerIfNotExistsAsync(containerProperties);
            tempClient.Dispose();

            DatabaseItem item = new DatabaseItem(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

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
                            new FaultInjectionEndpointBuilder(FeedRange.FromPartitionKey(new Cosmos.PartitionKey(item.Pk)))
                                .WithReplicaCount(3)
                                .Build(),
                            "containerRId")
                        .Build())
                .WithResult(
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .WithStartDelay(TimeSpan.FromMilliseconds(200))
                .Build();

            List<FaultInjectionRule> ruleList = new List<FaultInjectionRule> { writeRegionServerGoneRule, primaryReplicaServerGoneRule };
            FaultInjector faultInjector = new FaultInjector(ruleList);

            await this.Initialize(faultInjector, preferredRegionList);

            primaryReplicaServerGoneRule.Disable();

            GlobalEndpointManager globalEndpointManager = ((ChaosInterceptor)faultInjector.GetChaosInterceptor())
                .GetRuleStore().Value
                .GetRuleProcessor()
                .GetGlobalEndpointManager();

            // Set preferred locations so read/write requests are routed to different regions
            List<string> preferredRegionList = new List<string>();
            foreach (string region in globalEndpointManager.LocationCache.locationInfo.AvailableReadLocations)
            {
                if (globalEndpointManager.LocationCache.locationInfo.AvailableReadLocations.Contains(region))
                {
                    preferredRegionList.Add(region);
                }
                else
                {
                    preferredRegionList.Insert(0, region);
                }
            }

            CosmosClient preferredRegionsClient = this.InitilizePreferredRegionsClient(faultInjector, preferredRegionList);
            Database preferredRegionsDatabase = preferredRegionsClient.GetDatabase("testDb");
            Container preferredRegionsContainer = preferredRegionsDatabase.GetContainer("testContainer");

            try
            {
                Assert.AreEqual(globalEndpointManager.LocationCache.locationInfo.AvailableReadLocations.Count, writeRegionServerGoneRule.GetRegionEndpoints().Count);

                CosmosDiagnostics diagnostics = await this.PerformDocumentOperation(preferredRegionsContainer, operationType, item);

                if (OperationType.IsWriteOperation(operationType))
                {
                    this.ValidateHitCount(writeRegionServerGoneRule, 1);
                    this.ValidateFaultInjectionRuleApplication(
                        diagnostics,
                        HttpConstants.StatusCode.Gone,
                        HttpConstants.SubStatusCodes.ServerGenerated410,
                        writeRegionServerGoneRule,
                        true);
                }
                else
                {
                    this.ValidateFaultInjectionRuleNotApplied(
                        diagnostics,
                        writeRegionServerGoneRule,
                        FaultInjectionRuleNonApplicableOperationType);
                }

                writeRegionServerGoneRule.Disable();
                primaryReplicaServerGoneRule.Enable();

                Assert.AreEqual(globalEndpointManager.LocationCache.locationInfo.AvailableWriteLocations.Count + 1, primaryReplicaServerGoneRule.GetRegionEndpoints().Count);
                foreach (Uri region in globalEndpointManager.LocationCache.locationInfo.WriteEndpoints)
                {
                    Assert.IsTrue(primaryReplicaServerGoneRule.GetRegionEndpoints().Contains(region));
                }
                Assert.AreEqual(globalEndpointManager.LocationCache.locationInfo.WriteRegions.Count, primaryReplicaServerGoneRule.GetAddresses().Count);                
            }
            finally
            {
                preferredRegionsClient.Dispose();
                writeRegionServerGoneRule.Disable();
                primaryReplicaServerGoneRule.Disable();

            }
        }

        [TestMethod]
        public async Task FaultInjectionServerErrorRule_RegionTest()
        {
            if (!this.Timeout_FaultInjectionServerErrorRule_RegionTest().Wait(Timeout))
            {
                Assert.Fail("Test timed out");
            }
        }

        private async Task Timeout_FaultInjectionServerErrorRule_RegionTest()
        {
            CosmosClient tempClient = TestCommon.CreateCosmosClient(false);
            GlobalEndpointManager globalEndpointManager = tempClient.ClientContext.DocumentClient.GlobalEndpointManager;
            List<string> preferredRegionList = globalEndpointManager.LocationCache.locationInfo.AvailableReadLocations.Keys;

            tempClient.Dispose();
            
            string localRegionRuleId = "localRegionRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule localRegionRule = new FaultInjectionRuleBuilder(localRegionRuleId)
                .WithCondition(
                    new FaultInjectionConditionBuilder()
                        .WithRegion(preferredRegionList[0])
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
                        .WithRegion(preferredRegionList[1])
                        .Build())
                .WithResult(
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
                    .WithTimes(1)
                    .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();
            
            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { localRegionRule, remoteRegionRule };
            FaultInjector faultInjector = new FaultInjector(rules);
            await this.Initialize(faultInjector);

            //figure out emulator with multi regions
            
        }

        [TestMethod]
        public async Task FaultInjectionServerErrorRule_PartitionTest()
        {
            if (!this.Timeout_FaultInjectionServerErrorRule_RegionTest().Wait(Timeout))
            {
                Assert.Fail("Test timed out");
            }
        }

        private async Task Timeout_FaultInjectionServerErrorRule_PartitionTest()
        {
            CosmosClient tempClient = TestCommon.CreateCosmosClient(false);
            Database tempDatabase = await tempClient.CreateDatabaseIfNotExistsAsync("testDb");
            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId("containerRId");
            containerProperties.Id = "testContainer";
            containerProperties.PartitionKeyPath = "/Pk";
            Container tempContainer = await tempDatabase.CreateContainerIfNotExistsAsync(containerProperties);

            for (int i = 0; i <10; i++)
            {
                await tempContainer.CreateItemAsync(new DatabaseItem(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()));
            }

            List<FeedRange> feedRanges = (List<FeedRange>)await tempContainer.GetFeedRangesAsync();
            Assert.IsTrue(feedRanges.Count > 1);

            string query = "SELECT * FROM c";
            QueryRequestOptions queryOptions = new QueryRequestOptions();
            queryOptions.FeedRange = feedRanges[0];

            DatabaseItem query0 = (await tempContainer.GetItemQueryIterator<DatabaseItem>(query, requestOptions: queryOptions).ReadNextAsync()).First();

            queryOptions.FeedRange = feedRanges[1];
            DatabaseItem query1 = (await tempContainer.GetItemQueryIterator<DatabaseItem>(query, requestOptions: queryOptions).ReadNextAsync()).First();

            string serverErrorFeedRangeRuleId = "serverErrorFeedRangeRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serverErrorFeedRangeRule = new FaultInjectionRuleBuilder(serverErrorFeedRangeRuleId)
                .WithCondition(
                    new FaultInjectionConditionBuilder()
                        .WithEndpoint(
                            new FaultInjectionEndpointBuilder(feedRanges[0])
                                .Build(),
                            "containerRId")
                        .Build())
                .WithResult(
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.TooManyRequests)
                    .WithTimes(1)
                    .Build())
                .Build();


            tempClient.Dispose();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule> {serverErrorFeedRangeRule};
            FaultInjector faultInjector = new FaultInjector(rules);
            await this.Initialize(faultInjector);

            GlobalEndpointManager globalEndpointManager = tempClient.ClientContext.DocumentClient.GlobalEndpointManager;
            List<Uri> readRegions = globalEndpointManager.LocationCache.locationInfo.AvailableReadLocations.Values;

            Assert.IsTrue(serverErrorFeedRangeRule.GetRegionEndpoints().Count == readRegions.Count);

            foreach (Uri regionEndpoint in readRegions)
            {
                Assert.IsTrue(serverErrorFeedRangeRule.GetRegionEndpoints().Contains(regionEndpoint));
            }

            Assert.IsTrue(
                serverErrorFeedRangeRule.GetAddresses().Count >= 3 * readRegions.Count
                && serverErrorFeedRangeRule.GetAddresses().Count <= 5 * readRegions.Count);

            CosmosDiagnostics diagnostics = (await this.container.ReadItemAsync<DatabaseItem>(query0.Id, new Cosmos.PartitionKey(query0.Pk))).Diagnostics;

            this.ValidateHitCount(serverErrorFeedRangeRule, 1);
            this.ValidateFaultInjectionRuleApplication(
                diagnostics,
                HttpConstants.StatusCode.TooManyRequests,
                HttpConstants.SubStatusCodes.Unknown,
                serverErrorFeedRangeRule,
                true);

            try
            {
                diagnostics = (await this.container.ReadItemAsync<DatabaseItem>(query1.Id, new Cosmos.PartitionKey(query1.Pk))).Diagnostics;
                Assert.IsTrue(diagnostics.ToString().Contains("200"));
                this.ValidateHitCount(serverErrorFeedRangeRule, 1);
            }
            finally
            {
                serverErrorFeedRangeRule.Disable();
            }
        }

        [TestMethod]
        public async Task FaultInjectionServerErrorRule_ServerResponseDelay()
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

            await this.Initialize();
            
            DatabaseItem createdItem = new DatabaseItem(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
            ItemResponse<DatabaseItem> itemResponse = await this.container.CreateItemAsync<DatabaseItem>(createdItem);
            
            this.client.Dispose();

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { timeoutRule });

                this.client = new CosmosClient(
                    accountEndpoint: ConfigurationManager.AppSettings["GatewayEndpoint"],
                    authKeyOrResourceToken: ConfigurationManager.AppSettings["MasterKey"],
                    clientOptions: new CosmosClientOptions()
                    {
                        EnableContentResponseOnWrite = true,
                        ConnectionMode = ConnectionMode.Direct,
                        OpenTcpConnectionTimeout = TimeSpan.FromSeconds(1),
                        ChaosInterceptor = faultInjector.GetChaosInterceptor()
                    });

                ItemResponse<DatabaseItem> readResponse = await this.container.ReadItemAsync<DatabaseItem>(createdItem.Id, new Cosmos.PartitionKey(createdItem.Pk));
                this.ValidateHitCount(timeoutRule, 1);
                this.ValidateFaultInjectionRuleApplication(
                    readResponse.Diagnostics,
                    HttpConstants.StatusCode.Timeout,
                    HttpConstants.SubStatusCodes.Unknown,
                    timeoutRule);
            }
            finally
            {
                timeoutRule.Disable();
            }
        }

        [TestMethod]
        public async Task FaultInjectionServerErrorRule_ConnectionTimeout()
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
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            await this.Initialize();
            this.client.Dispose();

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { connectionTimeoutRule });

                this.client = new CosmosClient(
                    accountEndpoint: ConfigurationManager.AppSettings["GatewayEndpoint"],
                    authKeyOrResourceToken: ConfigurationManager.AppSettings["MasterKey"],
                    clientOptions: new CosmosClientOptions()
                    {
                        EnableContentResponseOnWrite = true,
                        ConnectionMode = ConnectionMode.Direct,
                        OpenTcpConnectionTimeout = TimeSpan.FromSeconds(1),
                        ChaosInterceptor = faultInjector.GetChaosInterceptor()
                    });

                DatabaseItem createdItem = new DatabaseItem(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
                ItemResponse<DatabaseItem> itemResponse = await this.container.CreateItemAsync<DatabaseItem>(createdItem);

                Assert.IsTrue(connectionTimeoutRule.GetHitCount() == 1 || connectionTimeoutRule.GetHitCount() == 2);
                this.ValidateFaultInjectionRuleApplication(
                    itemResponse.Diagnostics,
                    HttpConstants.StatusCode.Gone,
                    HttpConstants.SubStatusCodes.TransportGenerated410,
                    connectionTimeoutRule);
            }
            finally
            {
                connectionTimeoutRule.Disable();
            }
        }

        [TestMethod]
        public async Task FaultInjectionServerErrorRule_ConnectionDelay()
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

            await this.Initialize();
            this.client.Dispose();

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { connectionDelayRule });

                this.client = new CosmosClient(
                    accountEndpoint: ConfigurationManager.AppSettings["GatewayEndpoint"],
                    authKeyOrResourceToken: ConfigurationManager.AppSettings["MasterKey"],
                    clientOptions: new CosmosClientOptions()
                    {
                        EnableContentResponseOnWrite = true,
                        ConnectionMode = ConnectionMode.Direct,
                        OpenTcpConnectionTimeout = TimeSpan.FromSeconds(1),
                        ChaosInterceptor = faultInjector.GetChaosInterceptor()
                    });

                DatabaseItem createdItem = new DatabaseItem(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
                ItemResponse<DatabaseItem> itemResponse = await this.container.CreateItemAsync<DatabaseItem>(createdItem);

                Assert.IsTrue(connectionDelayRule.GetHitCount() == 1 || connectionDelayRule.GetHitCount() == 2);
                Assert.IsTrue(itemResponse.StatusCode = HttpConstants.StatusCode.Created);
            }
            finally
            {
                connectionDelayRule.Disable();
            }
        }

        [TestMethod]
        [DataRow(OperationType.Read, FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.Gone, 410, 0, DisplayName = "ReadItem Gone")]
        [DataRow(OperationType.Read, FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.InternalServerEror, 500, 0, DisplayName = "ReadItem InternalServerError")]
        [DataRow(OperationType.Read, FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.RetryWith, 449, 0, DisplayName = "ReadItem RetryWith")]
        [DataRow(OperationType.Read, FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.TooManyRequests, 429, 0, DisplayName = "ReadItem TooManyRequests")]
        [DataRow(OperationType.Read, FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.ReadSessionNotAvailable, 404, 1002, DisplayName = "ReadItem ReadSessionNotAvailable")]
        [DataRow(OperationType.Read, FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.Timeout, 408, 0, DisplayName = "ReadItem Timeout")]
        [DataRow(OperationType.Read, FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.PartitionIsMigrating, 410, 1008, DisplayName = "ReadItem PartitionIsMigrating")]
        [DataRow(OperationType.Read, FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.PartitionIsSplitting, 410, 1007, DisplayName = "ReadItem PartitionIsSplitting")]
        [DataRow(OperationType.Create, FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.Gone, 410, 0, DisplayName = "CreateItem Gone")]
        [DataRow(OperationType.Create, FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.InternalServerEror, 500, 0, DisplayName = "CreateItem InternalServerError")]
        [DataRow(OperationType.Create, FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.RetryWith, 449, 0, DisplayName = "CreateItem RetryWith")]
        [DataRow(OperationType.Create, FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.TooManyRequests, 429, 0, DisplayName = "CreateItem TooManyRequests")]
        [DataRow(OperationType.Create, FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.ReadSessionNotAvailable, 404, 1002, DisplayName = "CreateItem ReadSessionNotAvailable")]
        [DataRow(OperationType.Create, FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.Timeout, 408, 0, DisplayName = "CreateItem Timeout")]
        [DataRow(OperationType.Create, FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.PartitionIsMigrating, 410, 1008, DisplayName = "CreateItem PartitionIsMigrating")]
        [DataRow(OperationType.Create, FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.PartitionIsSplitting, 410, 1007, DisplayName = "CreateItem PartitionIsSplitting")]
        public async Task FaultInjectionServerErrorRule_ServerErrorResponseTest(
            OperationType operationType,
            FaultInjectionOperationType faultInjectionOperationType,
            FaultInjectionServerErrorType serverErrorType,
            int errorStatusCode,
            int substatusCode)
        {
            if (!this.Timeout_FaultInjectionServerErrorRule_ServerErrorResponseTest(
                    operationType,
                    faultInjectionOperationType,
                    serverErrorType,
                    canRetry,
                    errorStatusCode,
                    substatusCode).Wait(Timeout))
            {
                Assert.Fail("Test timed out");
            }
        }

        public async Task Timeout_FaultInjectionServerErrorRule_ServerErrorResponseTest(
            OperationType operationType,
            FaultInjectionOperationType faultInjectionOperationType,
            FaultInjectionServerErrorType serverErrorType,
            int errorStatusCode,
            int substatusCode)
        {
            CosmosClient tempClient = TestCommon.CreateCosmosClient(false);
            Database tempDatabase = await tempClient.CreateDatabaseIfNotExistsAsync("testDb");
            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId("containerRId");
            containerProperties.Id = "testContainer";
            containerProperties.PartitionKeyPath = "/Pk";
            Container tempContainer = await tempDatabase.CreateContainerIfNotExistsAsync(containerProperties);

            DatabaseItem item = new DatabaseItem(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
            await tempContainer.CreateItemAsync(item);

            tempClient.Dispose();

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

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { serverErrorResponseRule });
                await this.Initialize(faultInjector);

                CosmosDiagnostics diagnostics = await this.PerformDocumentOperation(operationType, item);

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
        public async Task FaultInjectionServerErrorRule_HitCountTest()
        {
            if (!this.Timeout_FaultInjectionServerErrorRule_HitCountTest().Wait(Timeout))
            {
                Assert.Fail("Test timed out");
            }
        }

        private async Task Timeout_FaultInjectionServerErrorRule_HitCountTest()
        {
            string hitCountRuleId = "hitCountRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule hitCountRule = new FaultInjectionRuleBuilder(hitCountRuleId)
                .WithCondition(
                    new FaultInjectionConditionBuilder()
                    .WithOperationType(FaultInjectionOperationType.ReadItem)
                    .Build())
                .WithResult(
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
                        .WithTimes(1)
                        .Build())
                .WithHitLimit(2)
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { hitCountRule });

                this.Initialize(faultInjector);

                DatabaseItem createdItem = new DatabaseItem(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
                ItemResponse<DatabaseItem> itemResponse = await this.container.CreateItemAsync<DatabaseItem>(createdItem);

                for (int i = 0; i < 3; i++)
                {
                    itemResponse = await this.PerformDocumentOperation(OperationType.Read, createdItem);

                    if (i < 2)
                    {
                        this.ValidateFaultInjectionRuleApplication(
                            itemResponse.Diagnostics,
                            HttpConstants.StatusCode.Gone,
                            HttpConstants.SubStatusCodes.ServerGenerated410,
                            hitCountRule);
                        this.ValidateHitCount(hitCountRule, i + 1);
                    }
                    else
                    {
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
        public async Task FaultInjectionServerErrorRule_IncludePrimaryTest()
        {
            if (!this.Timeout_FaultInjectionServerErrorRule_IncludePrimaryTest().Wait(Timeout))
            {
                Assert.Fail("Test timed out");
            }
        }

        private async Task Timeout_FaultInjectionServerErrorRule_IncludePrimaryTest()
        {
            CosmosClient tempClient = TestCommon.CreateCosmosClient(false);
            Database tempDatabase = await tempClient.CreateDatabaseIfNotExistsAsync("testDb");
            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId("containerRId");
            containerProperties.Id = "testContainer";
            containerProperties.PartitionKeyPath = "/Pk";
            Container tempContainer = await tempDatabase.CreateContainerIfNotExistsAsync(containerProperties);
            tempClient.Dispose();

            List<FeedRange> feedRanges = (List<FeedRange>)await tempContainer.GetFeedRangesAsync();
            Assert.IsTrue(feedRanges.Count > 0);

            DatabaseItem item = new DatabaseItem(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

            string includePrimaryServerGoneRuleId = "includePrimaryServerGoneRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule includePrimaryServerGoneRule = new FaultInjectionRuleBuilder(includePrimaryServerGoneRuleId)
                .WithCondition(
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .WithEndpoint(
                            new FaultInjectionEndpointBuilder(feedRanges[0])
                                .WithReplicaCount(1)
                                .WithIncludePrimary(true)
                                .Build(),
                            "containerRId")
                        .Build())
                .WithResult(
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            List<FaultInjectionRule> ruleList = new List<FaultInjectionRule> { includePrimaryServerGoneRule };
            FaultInjector faultInjector = new FaultInjector(ruleList);

            await this.Initialize(faultInjector);
            
            try
            {
                CosmosDiagnostics cosmosDiagnostics = await this.PerformDocumentOperation(OperationType.Create, item);
                this.ValidateHitCount(includePrimaryServerGoneRule, 1);
                this.ValidateFaultInjectionRuleApplication(
                    cosmosDiagnostics,
                    HttpConstants.StatusCode.Gone,
                    HttpConstants.SubStatusCodes.ServerGenerated410,
                    includePrimaryServerGoneRule);

                CosmosDiagnostics cosmosDiagnostics = await this.PerformDocumentOperation(OperationType.Upsert, item);
                this.ValidateHitCount(includePrimaryServerGoneRule, 2);
                this.ValidateFaultInjectionRuleApplication(
                    cosmosDiagnostics,
                    HttpConstants.StatusCode.Gone,
                    HttpConstants.SubStatusCodes.ServerGenerated410,
                    includePrimaryServerGoneRule);
            }
            finally
            {
                includePrimaryServerGoneRule.Disable();
            }
        }

        public record DatabaseItem(
           string Id,
           string Pk);

        private async Task<CosmosDiagnostics> PerformDocumentOperation(OperationType operationType, DatabaseItem item)
        {
            try
            {
                if (operationType == OperationType.Query)
                {
                    QueryRequestOptions queryOptions = new QueryRequestOptions();
                    string query = String.Format("SELECT * FROM c WHERE c.Id = '{0}'", item.Id);
                    FeedResponse<DatabaseItem> queryResponse = await this.container.GetItemQueryIterator<DatabaseItem>(query, requestOptions: queryOptions).ReadNextAsync();
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
                        return (await this.container.ReadItemAsync<DatabaseItem>(item.Id, new Cosmos.PartitionKey(item.Pk))).Diagnostics;
                    }

                    if (operationType = OperationType.Repleace)
                    {
                        return (await this.container.ReplaceItemAsync<DatabaseItem>(item, item.Id, new Cosmos.PartitionKey(item.Pk))).Diagnostics;
                    }

                    if (operationType = OperationType.Delete)
                    {
                        return (await this.container.DeleteItemAsync<DatabaseItem>(item.Id, new Cosmos.PartitionKey(item.Pk))).Diagnostics;
                    }

                    if (operationType = OperationType.Create)
                    {
                        return (await this.container.CreateItemAsync<DatabaseItem>(item, new Cosmos.PartitionKey(item.Pk))).Diagnostics;
                    }

                    if (operationType = OperationType.Upsert)
                    {
                        return (await this.container.UpsertItemAsync<DatabaseItem>(item, new Cosmos.PartitionKey(item.Pk))).Diagnostics;
                    }

                    if (operationType = OperationType.Patch)
                    {
                        return (await this.container.PatchItemAsync<DatabaseItem>(
                            item.Id, 
                            new Cosmos.PartitionKey(item.Pk), 
                            new[]
                            {
                                PatchOperation.Replace("/Id", item.Id),
                            })).Diagnostics;
                    }
                }

                throw new ArgumentException("Invalid Operation Type");
            }
            catch (CosmosException ex)
            {
                return ex.Diagnostics;
            }
        }

        private void ValidateHitCount(FaultInjectionRule rule, long expectedHitCount)
        {
            Assert.AreEqual(expectedHitCount, rule.GetHitCount());
        }

        private void ValidateFaultInjectionRuleNotApplied(
            CosmosDiagnostics diagnostics,
            FaultInjectionRule rule,
            string failureReason)
        {
            string diagnosticsString = diagnostics.ToString();
            Assert.AreEqual(0, rule.GetHitCount());
            Assert.AreEqual(0, diagnostics.GetFailedRequestCount());
            Assert.IsFalse(diagnosticsString.Contains(failureReason));
            Assert.IsTrue(diagnosticsString.Contains("200") || diagnosticsString.Contains("201"));
        }
        private void ValidateFaultInjectionRuleApplication(
            CosmosDiagnostics diagnostics,
            int statusCode,
            int subStatusCode,
            FaultInjectionRule rule)
        {
            string diagnosticsString = diagnostics.ToString();
            Assert.AreEqual(1, rule.GetHitCount());
            Assert.AreEqual(1, diagnostics.GetFailedRequestCount());
            Assert.IsTrue(diagnosticsString.Contains(rule.GetId()));
            Assert.IsTrue(diagnosticsString.Contains(statusCode.ToString()));
            Assert.IsTrue(diagnosticsString.Contains(subStatusCode.ToString()));
        }
    }
}
