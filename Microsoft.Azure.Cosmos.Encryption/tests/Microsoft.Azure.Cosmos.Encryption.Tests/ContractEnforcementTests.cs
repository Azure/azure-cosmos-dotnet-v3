namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    
    [TestCategory("Windows")]
    [TestCategory("UpdateContract")]
    [TestClass]
    public class ContractEnforcementTests
    {
        private const string DllName = "Microsoft.Azure.Cosmos.Encryption";
        private const string OfficialBaselinePath = "DotNetSDKEncryptionAPI.json";

        [TestMethod]
        public void ContractChanges()
        {
            Cosmos.Tests.ContractEnforcement.ValidateContractContainBreakingChanges(
                dllName: DllName,
                baselinePath: OfficialBaselinePath,
                breakingChangesPath: "DotNetSDKEncryptionAPIChanges.json");
        }
    }
}
