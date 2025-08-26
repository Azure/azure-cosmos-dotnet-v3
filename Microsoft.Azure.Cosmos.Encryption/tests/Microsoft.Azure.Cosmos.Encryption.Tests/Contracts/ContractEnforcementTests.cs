namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    
    [TestCategory("Windows")]
    [TestCategory("UpdateContract")]
    [TestClass]
    public class ContractEnforcementTests
    {
        [TestMethod]
        public void ContractChanges()
        {
            // Pick baseline per target framework to account for runtime-specific signature differences
            string baseline = "DotNetSDKEncryptionAPI.json";
#if NET8_0
            const string net8Baseline = "DotNetSDKEncryptionAPI.net8.json";
            string net8Path = System.IO.Path.Combine("Contracts", net8Baseline);
            if (System.IO.File.Exists(net8Path))
            {
                baseline = net8Baseline;
            }
            else
            {
                Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Fail($"Missing .NET 8 baseline file '{net8Baseline}'. Run UpdateContracts.ps1 to generate it.");
            }
#endif

            Cosmos.Tests.Contracts.ContractEnforcement.ValidateContractContainBreakingChanges(
                dllName: "Microsoft.Azure.Cosmos.Encryption",
                baselinePath: baseline,
                breakingChangesPath: "DotNetSDKEncryptionAPIChanges.json");
        }
    }
}
