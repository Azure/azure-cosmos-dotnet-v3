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
                "Microsoft.Azure.Cosmos.Client",
                "DotNetSDKAPI.json",
                "CurrentDotNetSDKAPI.json");
        }

        [TestMethod]
        public void UniqueKeyUnsealed()
        {
            Assert.IsFalse(typeof(UniqueKey).IsSealed);
        }
    }
}
