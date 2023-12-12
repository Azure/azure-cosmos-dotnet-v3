namespace Microsoft.Azure.Cosmos.FaultInjection.Tests
{
    using System;

    [TestClass]
    public class FaultInjectionUnitTests
    {
        [TestMethod]
        public void FaultInjectionBuilderTests()
        {

            string ruleId = "rule_id";
            FaultInjectionCondition faultInjectionCondition = new FaultInjectionConditionBuilder()
                .WithOperationType(FaultInjectionOperationType.CreateItem)
                .WithConnectionType(FaultInjectionConnectionType.Direct)
                .Build();
            FaultInjectionRule faultInjectionRule = new FaultInjectionRuleBuilder(ruleId)
                .WithCondition(faultInjectionCondition)
                .WithDuration(TimeSpan.FromSeconds(10))
                .WithResult(FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ConnectionDelay)
                    .WithDelay(TimeSpan.FromSeconds(6))
                    .WithTimes(1)
                    .Build())
                .Build();

            Assert.AreEqual(ruleId, faultInjectionRule.GetId());
            Assert.AreEqual(faultInjectionCondition, faultInjectionRule.GetCondition());
            Assert.AreEqual(TimeSpan.FromSeconds(10), faultInjectionRule.GetDuration());
            Assert.IsNotNull(faultInjectionRule.GetResult());
        }
    }
}