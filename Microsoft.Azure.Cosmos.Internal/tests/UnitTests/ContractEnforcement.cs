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
            ContractEnforcementSharedHelper.ValidateContractContainBreakingChangesExcludeOfficialBaseline(
                dllName: "Microsoft.Azure.Cosmos.Client",
                baselinePath: "InternalDotNetSDKAPI.json",
                currentPath: "CurrentInternalDotNetSDKAPI.json",
                officialBaselinePath: "DotNetSDKAPI.json");
        }
    }
}
