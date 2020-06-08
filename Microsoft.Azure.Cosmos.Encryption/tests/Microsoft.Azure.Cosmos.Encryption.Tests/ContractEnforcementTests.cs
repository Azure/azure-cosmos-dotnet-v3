namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    
    [TestCategory("Windows")]
    [TestClass]
    public class ContractEnforcementTests
    {
        private const string BaselinePath = "DotNetSDKEncryptionAPI.json";
        private const string BreakingChangesPath = "DotNetSDKEncryptionAPIChanges.json";

        [TestMethod]
        public void ContractChanges()
        {
            Assert.IsFalse(
                Cosmos.Tests.ContractEnforcement.DoesContractContainBreakingChanges(
                    "Microsoft.Azure.Cosmos.Encryption", 
                    BaselinePath, 
                    BreakingChangesPath),
                $@"Public API has changed. If this is expected, then refresh {BaselinePath} with {Environment.NewLine} Microsoft.Azure.Cosmos.Encryption/tests/Microsoft.Azure.Cosmos.Encryption.Tests/testbaseline.cmd /update after this test is run locally. To see the differences run testbaselines.cmd /diff"
            );
        }
    }
}
