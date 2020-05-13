namespace Microsoft.Azure.Cosmos.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestCategory("Windows")]
    [TestClass]
    public class ContractEnforcement
    {
        [TestMethod]
        public void ContractChanges()
        {
            ContractEnforcementSharedHelper.ValidateContractContainBreakingChanges(
                dllName: "Microsoft.Azure.Cosmos.Client",
                baselinePath: "DotNetSDKAPI.json",
                currentChangesPath: "CurrentDotNetSDKAPI.json");
        }

        [TestMethod]
        public void UniqueKeyUnsealed()
        {
            Assert.IsFalse(typeof(UniqueKey).IsSealed);
        }
    }
}
