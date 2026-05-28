namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestCategory("Windows")]
    [TestCategory("UpdateContract")]
    [TestClass]
    public class ContractEnforcementTests
    {
        /// <summary>
        /// This test validates the public API surface against a baseline contract.
        /// 
        /// IMPORTANT: Because tests run on multiple .NET versions (net6.0 and net8.0),
        /// the contract validation uses framework-specific baselines to ensure consistency:
        /// 
        /// - When running on net6.0: validates against DotNetSDKEncryptionAPI.net6.json
        /// - When running on net8.0: validates against DotNetSDKEncryptionAPI.net8.json
        /// 
        /// To update baselines, run: UpdateContracts.ps1 from the repository root.
        /// This script runs tests on BOTH net6.0 and net8.0 to generate both baselines.
        /// </summary>
        [TestMethod]
        public void ContractChanges()
        {
            Cosmos.Tests.Contracts.ContractEnforcement.ValidateContract(
                dllName: "Microsoft.Azure.Cosmos.Encryption",
                contractType: Cosmos.Tests.Contracts.ContractType.Standard,
                baselinePattern: "DotNetSDKEncryptionAPI",
                breakingChangesPattern: "DotNetSDKEncryptionAPIChanges");
        }
    }
}
