namespace Microsoft.Azure.Cosmos.FaultInjection.Tests
{
    using System;

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
                        feedRange: FeedRange.FromPartitionKey(new PartitionKey("test")))
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
            PartitionKey test = new PartitionKey("test");
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
    }
}