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
                dllName: "Microsoft.Azure.Cosmos.Encryption",
                baselinePath: "EncryptionSDKAPI.json",
                currentChangesPath: "CurrentEncryptionSDKAPI.json");
        }
    }
}
