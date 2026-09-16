namespace Microsoft.Azure.Cosmos.Encryption.Tests.Contracts
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
        /// IMPORTANT: Because the library multi-targets (netstandard2.0 + net8.0) and uses
        /// conditional compilation (#if NET8_0_OR_GREATER), the API surface differs between
        /// .NET versions. Therefore:
        /// 
        /// - When running on net6.0: validates against DotNetSDKEncryptionCustomAPI.net6.json
        /// - When running on net8.0: validates against DotNetSDKEncryptionCustomAPI.net8.json
        /// 
        /// There is NO generic fallback - each supported framework MUST have its own baseline.
        /// 
        /// To update baselines, run: UpdateContracts.ps1 from the repository root.
        /// This script runs tests on BOTH net6.0 and net8.0 to generate both baselines.
        /// </summary>
        [TestMethod]
        public void ContractChanges()
        {
            Cosmos.Tests.Contracts.ContractEnforcement.ValidateContract(
                dllName: "Microsoft.Azure.Cosmos.Encryption.Custom",
                contractType: Cosmos.Tests.Contracts.ContractType.Standard,
                baselinePattern: "DotNetSDKEncryptionCustomAPI",
                breakingChangesPattern: "DotNetSDKEncryptionCustomAPIChanges");
        }
    }
}
