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
            // Select baseline by TFM to account for conditional members in net8 builds.
#if NET8_0_OR_GREATER
            const string baseline = "DotNetSDKEncryptionCustomAPI.net8.json";
#else
            const string baseline = "DotNetSDKEncryptionCustomAPI.json";
#endif

            Cosmos.Tests.Contracts.ContractEnforcement.ValidateContractContainBreakingChanges(
                dllName: "Microsoft.Azure.Cosmos.Encryption.Custom",
                baselinePath: baseline,
                breakingChangesPath: "DotNetSDKEncryptionCustomAPIChanges.json");
        }
    }
}
