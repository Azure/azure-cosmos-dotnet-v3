namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    
    [TestCategory("Windows")]
    [TestClass]
    public class ContractEnforcementTests
    {
        private const string BaselinePath = "DotNetSDKAPI.json";
        private const string BreakingChangesPath = "DotNetSDKAPIChanges.json";

        [TestMethod]
        public void ContractChanges()
        {
            Assert.IsFalse(
                ContractEnforcement.DoesContractContainBreakingChanges(
                    "Microsoft.Azure.Cosmos.Client",
                    BaselinePath,
                    BreakingChangesPath),
                $@"Public API has changed. If this is expected, then refresh {BaselinePath} with {Environment.NewLine} Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/testbaseline.cmd /update after this test is run locally. To see the differences run testbaselines.cmd /diff"
            );
        }

        [TestMethod]
        public void UniqueKeyUnsealed()
        {
            Assert.IsFalse(typeof(UniqueKey).IsSealed);
        }
    }
}
