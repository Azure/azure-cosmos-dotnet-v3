namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Encryption;

    [TestCategory("Windows")]
    [TestCategory("UpdateContract")]
    [TestClass]
    public class ContractEnforcementTests
    {
        [TestMethod]
        public void ContractChanges()
        {
            Cosmos.Tests.Contracts.ContractEnforcement.ValidateContractContainBreakingChanges(
                assembly: typeof(DataEncryptionAlgorithm).Assembly,
                baselinePath: "DotNetSDKEncryptionAPI.json",
                breakingChangesPath: "DotNetSDKEncryptionAPIChanges.json");
        }
    }
}
