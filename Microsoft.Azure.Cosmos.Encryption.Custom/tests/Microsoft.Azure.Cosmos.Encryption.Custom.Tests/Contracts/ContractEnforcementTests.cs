namespace Microsoft.Azure.Cosmos.Encryption.Tests.Contracts
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestCategory("Windows")]
    [TestCategory("UpdateContract")]
    [TestClass]
    public class ContractEnforcementTests
    {
        [TestMethod]
        public void ContractChanges()
        {
            Cosmos.Tests.Contracts.ContractEnforcement.ValidateContractContainBreakingChanges(
                dllName: "Microsoft.Azure.Cosmos.Encryption.Custom",
                baselinePath: "DotNetSDKEncryptionCustomAPI.json",
                breakingChangesPath: "DotNetSDKEncryptionCustomAPIChanges.json");
        }
    }
}
