namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    [TestCategory("Windows")]
    [TestCategory("UpdateContract")]
    [TestClass]
    public class ContractEnforcementTests
    {
        [TestMethod]
        public void ContractChanges()
        {
            Cosmos.Tests.Contracts.ContractEnforcement.ValidateContractContainBreakingChanges(
                dllName: "Microsoft.Azure.Cosmos.Encryption",
                baselinePath: defaultFileName,
                breakingChangesPath: "DotNetSDKEncryptionAPIChanges.json");
        }
    }
}
