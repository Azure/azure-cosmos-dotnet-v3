namespace Microsoft.Azure.Cosmos.FaultInjection.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.FaultInjection.Tests.Utils;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class FaultInjectionUnitTests
    {
        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests Fault Injection Rule Builder classes")]
        public void FaultInjectionBuilderTests()
        {

            string ruleId = "rule_id";
            FaultInjectionCondition faultInjectionCondition = new FaultInjectionConditionBuilder()
                .WithOperationType(FaultInjectionOperationType.CreateItem)
                .WithConnectionType(FaultInjectionConnectionType.Direct)
                .WithRegion("East US")
                .WithEndpoint(
                    new FaultInjectionEndpointBuilder(
                        databaseName: "db", 
                        containerName: "col",
                        feedRange: FeedRange.FromPartitionKey(new Cosmos.PartitionKey("test")))
                    .WithReplicaCount(3)
                    .WithIncludePrimary(true)
                    .Build())
                .Build();
            FaultInjectionRule faultInjectionRule = new FaultInjectionRuleBuilder(
                id: ruleId,
                condition: faultInjectionCondition,
                result: FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ConnectionDelay)
                    .WithDelay(TimeSpan.FromSeconds(6))
                    .WithTimes(1)
                    .WithSuppressServiceRequest(true)
                    .Build()
                )
                .WithDuration(TimeSpan.FromSeconds(10))
                .WithStartDelay(TimeSpan.FromSeconds(5))
                .WithHitLimit(3)
                .Build();

            //Test FaultInjectionRule
            Assert.AreEqual(TimeSpan.FromSeconds(10), faultInjectionRule.GetDuration());
            Assert.AreEqual(TimeSpan.FromSeconds(5), faultInjectionRule.GetStartDelay());
            Assert.AreEqual(3, faultInjectionRule.GetHitLimit());
            Assert.AreEqual(ruleId, faultInjectionRule.GetId());
            Assert.IsTrue(faultInjectionRule.IsEnabled());
            
            faultInjectionRule.Disable();
            Assert.IsFalse(faultInjectionRule.IsEnabled());
            faultInjectionRule.Enable();
            Assert.IsTrue(faultInjectionRule.IsEnabled());

            Assert.AreEqual(0, faultInjectionRule.GetHitCount());

            //Test FaultInjectionCondition
            Assert.AreEqual(FaultInjectionOperationType.CreateItem, faultInjectionRule.GetCondition().GetOperationType());
            Assert.AreEqual(FaultInjectionConnectionType.Direct, faultInjectionRule.GetCondition().GetConnectionType());
            Assert.AreEqual("East US", faultInjectionRule.GetCondition().GetRegion());

            //Test FaultInjectionEndpoint
            Cosmos.PartitionKey test = new Cosmos.PartitionKey("test");
            Assert.AreEqual(FeedRange.FromPartitionKey(test).ToString(), faultInjectionRule.GetCondition().GetEndpoint().GetFeedRange().ToString());
            Assert.AreEqual("dbs/db/colls/col", faultInjectionRule.GetCondition().GetEndpoint().GetResoureName());
            Assert.AreEqual(3, faultInjectionRule.GetCondition().GetEndpoint().GetReplicaCount());
            Assert.IsTrue(faultInjectionRule.GetCondition().GetEndpoint().IsIncludePrimary());

            //Test FaultInjectionResult
            Assert.AreEqual(FaultInjectionServerErrorType.ConnectionDelay, 
                ((FaultInjectionServerErrorResult)faultInjectionRule.GetResult()).GetServerErrorType());
            Assert.AreEqual(TimeSpan.FromSeconds(6), ((FaultInjectionServerErrorResult)faultInjectionRule.GetResult()).GetDelay());
            Assert.AreEqual(1, ((FaultInjectionServerErrorResult)faultInjectionRule.GetResult()).GetTimes());
            Assert.IsTrue(((FaultInjectionServerErrorResult)faultInjectionRule.GetResult()).GetSuppressServiceRequests());

        }

        [TestMethod]
        public async Task MyTestMethod()
        {
            string tooManyRequestsRuleId = "tooManyRequestsRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule tooManyRequestsRule = new FaultInjectionRuleBuilder(
                id: tooManyRequestsRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.TooManyRequests)
                        .WithResponseHeaders(new Dictionary<string, string> { { WFConstants.BackendHeaders.LSN, "-1" }, { WFConstants.BackendHeaders.LocalLSN, "-1" } })
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            List<FaultInjectionRule> ruleList = new List<FaultInjectionRule> { tooManyRequestsRule };
            FaultInjector faultInjector = new FaultInjector(ruleList);

            tooManyRequestsRule.Disable();

            using CosmosClient client = TestCommon.CreateCosmosClient(false, faultInjector, false);

            Container container = client.GetContainer("database", "col");

            JObject item = JObject.FromObject(new { id = Guid.NewGuid().ToString(), _partitionKey = Guid.NewGuid().ToString() });
            await container.CreateItemAsync(item);

            try
            {
                List<Task> tasks = new List<Task>();
                for (int i = 0; i < 30000; i++)
                {
                    tasks.Add(container.ReadItemAsync<JObject>((string)item["id"], new Cosmos.PartitionKey((string)item["_partitionKey"])));
                }

                await Task.WhenAll(tasks);
            }
            catch (CosmosException ex)
            {
                Logger.LogMessage("{0}", ex.Message);
                Logger.LogMessage("{0}", ex.Diagnostics);
                Assert.AreEqual(System.Net.HttpStatusCode.TooManyRequests, ex.StatusCode);
            }

            //DatabaseResponse database = await client.CreateDatabaseIfNotExistsAsync("testDb");

            //Container container = await database.Database.CreateContainerIfNotExistsAsync("test", "/pk");

            //JObject item = JObject.FromObject(new { id = Guid.NewGuid().ToString(), pk = Guid.NewGuid().ToString() });
            //await container.CreateItemAsync(item);

            //try
            //{
            //    tooManyRequestsRule.Enable();

            //    await container.ReadItemAsync<JObject>((string)item["id"], new Cosmos.PartitionKey((string)item["pk"]));
            //    Assert.Fail("Should have failed");
            //}
            //catch (CosmosException ex)
            //{
            //    Logger.LogMessage("{0}", ex.Message);
            //    Logger.LogMessage("{0}", ex.Diagnostics);
            //    Assert.AreEqual(System.Net.HttpStatusCode.TooManyRequests, ex.StatusCode);
            //}
        }
    }
}