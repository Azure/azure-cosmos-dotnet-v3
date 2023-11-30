//namespace Microsoft.Azure.Cosmos.FaultInjection.Tests
//{
//    using System;
//    using System.Threading.Tasks;
//    using Microsoft.Azure.Cosmos.FaultInjection.Tests.Utils;
//    using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;

//    [TestClass]
//    public class FaultInjectionServerErrorRuleDirectModeTests
//    {
//        private const int Timeout = 6000;
//        private const string FaultInjectionRuleNonApplicableAddress = "Addresses mismatch";
//        private const string FaultInjectionRuleNonApplicableOperationType = "OperationType mismatch";
//        private const string FaultInjectionRuleNonApplicableRegionEndpoint = "RegionEndpoint mismatch";
//        private const string FaultInjectionRuleNonApplicabeRegionHitLimit = "Hit Limit reached";

//        private CosmosClient client = null;
//        private Cosmos.Database database = null;
//        private Container container = null;
        
//        public async Task Initialize(FaultInjector faultInjector)
//        {
//            this.client = TestCommon.CreateCosmosClient(false, faultInjector);
//            this.database = await this.client.CreateDatabaseAsync("testDb");
//            this.container = await this.database.CreateContainerAsync("testContainer", "/Pk");
//        }
//        [TestCleanup]
//        public async Task Cleanup()
//        {
//            await this.database.DeleteAsync();
//            this.client.Dispose();
//        }

//        [TestMethod]
//        [DataRow(OperationType.Read, DisplayName = "Read")]
//        [DataRow(OperationType.Replace, DisplayName = "Replace")]
//        [DataRow(OperationType.Create, DisplayName = "Create")]
//        [DataRow(OperationType.Delete, DisplayName = "Delete")]
//        [DataRow(OperationType.Query, DisplayName = "Query")]
//        [DataRow(OperationType.Patch, DisplayName = "Patch")]
//        public async Task FaultInjectionServerErrorRule_OperationTypeTest(OperationType operationType)
//        {
//            if (!this.Timeout_FaultInjectionServerErrorRule_OperationTypeTest(operationType).Wait(Timeout))
//            {
//                Assert.Fail("Test timed out");
//            }
//        }

//        private async Task Timeout_FaultInjectionServerErrorRule_OperationTypeTest(OperationType operationType)
//        {
//            //Test Server gone, operation type will be ignored after getting the address
//            string serverGoneRuleId = "serverGoneRule-" + Guid.NewGuid().ToString();
//            FaultInjectionRule serverGoneRule = new FaultInjectionRuleBuilder(serverGoneRuleId)
//                .WithCondition(
//                    new FaultInjectionConditionBuilder()
//                        .WithOperationType(FaultInjectionOperationType.ReadItem)
//                        .Build())
//                .WithResult(
//                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
//                        .WithTimes(1)
//                        .Build())
//                .WithDuration(TimeSpan.FromMinutes(5))
//                .Build();

//            string tooManyRequestsRuleId = "tooManyRequestsRule-" + Guid.NewGuid().ToString();
//            FaultInjectionRule tooManyRequestsRule = new FaultInjectionRuleBuilder(tooManyRequestsRuleId)
//                .WithCondition(
//                    new FaultInjectionConditionBuilder()
//                        .WithOperationType(FaultInjectionOperationType.ReadItem)
//                        .Build())
//                .WithResult(
//                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.TooManyRequests)
//                        .WithTimes(1)
//                        .Build())
//                .WithDuration(TimeSpan.FromMinutes(5))
//                .Build();

//            List<FaultInjectionRule> ruleList = new List<FaultInjectionRule> { serverGoneRule, tooManyRequestsRule };
//            FaultInjector faultInjector = new FaultInjector(ruleList);
//            await this.Initialize(faultInjector);
//            Assert.AreEqual(0, serverGoneRule.GetAddresses().Count);
            
//            try
//            {
//                tooManyRequestsRule.Disable();
//                DatabaseItem item = new DatabaseItem(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
//                CosmosDiagnostics diagnostics = this.PerformDocumentOperation(operationType, item);
//                this.ValidateFaultInjectionRuleApplication(
//                    diagnostics,
//                    operationType,
//                    HttpConstants.StatusCode.Gone,
//                    HttpConstants.SubStatusCodes.ServerGenerated410,
//                    serverGoneRuleId,
//                    true);

//                serverGoneRule.Disable();
//                Assert.AreEqual(0, tooManyRequestsRule.GetAddresses().Count);

//                tooManyRequestsRule.Enable();
//                diagnostics = this.PerformDocumentOperation(operationType, item);
//                if (operationType == OperationType.Read)
//                {
//                    this.ValidateHitCount(tooManyRequestsRule, 1, operationType, ResourceType.Document);
//                }
//                else
//                {
//                    this.ValidateFaultInjectionRuleNotApplied(
//                        diagnostics,
//                        operationType,
//                        FaultInjectionRuleNonApplicableOperationType);
//                }
//            }
//            finally
//            {
//                serverGoneRule.Disable();
//                tooManyRequestsRule.Disable();
//            }
//        }

//        [TestMethod]
//        [DataRow(OperationType.Read, DisplayName = "Read")]
//        [DataRow(OperationType.Replace, DisplayName = "Replace")]
//        [DataRow(OperationType.Create, DisplayName = "Create")]
//        [DataRow(OperationType.Delete, DisplayName = "Delete")]
//        [DataRow(OperationType.Query, DisplayName = "Query")]
//        [DataRow(OperationType.Patch, DisplayName = "Patch")]
//        public async Task FaultInjectionServerErrorRule_OperationTypeAddressTest(OperationType operationType)
//        {
//            if (!this.Timeout_FaultInjectionServerErrorRule_OperationTypeAddressTest(operationType).Wait(Timeout))
//            {
//                Assert.Fail("Test timed out");
//            }
//        }

//        private async Task Timeout_FaultInjectionServerErrorRule_OperationTypeAddressTest(OperationType operationType)
//        {
//            DatabaseItem item = new DatabaseItem(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

//            string writeRegionServerGoneRuleId = "writeRegionServerGoneRule-" + Guid.NewGuid().ToString();
//            FaultInjectionRule writeRegionServerGoneRule = new FaultInjectionRuleBuilder(writeRegionServerGoneRuleId)
//                .WithCondition(
//                    new FaultInjectionConditionBuilder()
//                        .WithOperationType(FaultInjectionOperationType.CreateItem)
//                        .Build())
//                .WithResult(
//                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
//                        .WithTimes(1)
//                        .Build())
//                .WithDuration(TimeSpan.FromMinutes(5))
//                .WithStartDelay(TimeSpan.FromMilliseconds(200))
//                .Build();

//            string primaryReplicaServerGoneRuleId = "primaryReplicaServerGoneRule-" + Guid.NewGuid().ToString();
//            FaultInjectionRule primaryReplicaServerGoneRule = new FaultInjectionRuleBuilder(primaryReplicaServerGoneRuleId)
//                .WithCondition(
//                    new FaultInjectionConditionBuilder()
//                        .WithOperationType(FaultInjectionOperationType.CreateItem)
//                        .WithEndpoint(
//                            new FaultInjectionEndpointBuilder(FeedRange.FromPartitionKey(new PartitionKey(item.Pk)))
//                                .WithReplicaCount(3)
//                                .Build())
//                        .Build())
//                .WithResult(
//                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
//                        .WithTimes(1)
//                        .Build())
//                .WithDuration(TimeSpan.FromMinutes(5))
//                .WithStartDelay(TimeSpan.FromMilliseconds(200))
//                .Build();
//        }

//        public record DatabaseItem(
//           string Id,
//           string Pk);

//        private async Task<CosmosDiagnostics> PerformDocumentOperation(OperationType operationType, DatabaseItem item)
//        {
//            try
//            {
//                if (operationType == operationType.Query)
//                {
//                    CosmosQueryRequestOptions queryOptions = new CosmosQueryRequestOptions();
//                    string query = String.Format("SELECT * FROM c WHERE c.Id = '{0}'", item.ID);
//                    FeedResponse<DatabaseItem> queryResponse = await this.container.GetItemQueryIterator<DatabaseItem>(query, requestOptions: queryOptions).ReadNextAsync();
//                    return queryResponse.Diagnostics;
//                }

//                if (operationType == OperationType.Read
//                    || operationType == OperationType.Delete
//                    || operationType == OperationType.Replace
//                    || operationType == OperationType.Patch
//                    || operationType == OperationType.Create
//                    || operationType == OperationType.Upsert)
//                {
//                    if (operationType == OperationType.Read)
//                    {
//                        return await this.container.ReadItemAsync<DatabaseItem>(item.Id, new PartitionKey(item.Pk)).Diagnostics;
//                    }

//                    if (operationType = OperationType.Repleace)
//                    {
//                        return await this.container.ReplaceItemAsync<DatabaseItem>(item, item.Id, new PartitionKey(item.Pk)).Diagnostics;
//                    }

//                    if (operationType = OperationType.Delete)
//                    {
//                        return await this.container.DeleteItemAsync<DatabaseItem>(item.Id, new PartitionKey(item.Pk)).Diagnostics;
//                    }

//                    if (operationType = OperationType.Create)
//                    {
//                        return await this.container.CreateItemAsync<DatabaseItem>(item, new PartitionKey(item.Pk)).Diagnostics;
//                    }

//                    if (operationType = OperationType.Upsert)
//                    {
//                        return await this.container.UpsertItemAsync<DatabaseItem>(item, new PartitionKey(item.Pk)).Diagnostics;
//                    }

//                    if (operationType = OperationType.Patch)
//                    {
//                        return await this.container.PatchItemAsync<DatabaseItem>(item.Id, new PartitionKey(item.Pk), new CosmosPatchOperations().Add("newpath", "newpath")).Diagnostics;
//                    }
//                }

//                throw new ArgumentException("Invalid Operation Type");
//            }
//            catch (CosmosException ex)
//            {
//                return ex.Diagnostics;
//            }
//        }

//        private void ValidateFaultInjectionRuleApplication(
//            CosmosDiagnostics diagnostics,
//            OperatingType operattionType,
//            int statusCode,
//            int subStatusCode,
//            string ruleId,
//            bool canRetryOnInjectedError)
//        {
//            //idk here tbh
//        }
//    }
//}
