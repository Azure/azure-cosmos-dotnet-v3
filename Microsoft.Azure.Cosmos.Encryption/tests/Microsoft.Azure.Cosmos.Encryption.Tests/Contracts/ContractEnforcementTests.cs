namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System.IO;
    using System.Reflection;
    using System.Runtime.Versioning;
    using VisualStudio.TestTools.UnitTesting;

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
            int? currentMajorVersion = GetCurrentMajorVersion();
            string baseline = currentMajorVersion.HasValue 
                ? $"DotNetSDKEncryptionAPI.net{currentMajorVersion}.json"
                : "DotNetSDKEncryptionAPI.json";
            string breakingChanges = currentMajorVersion.HasValue
                ? $"DotNetSDKEncryptionAPIChanges.net{currentMajorVersion}.json"
                : "DotNetSDKEncryptionAPIChanges.json";

            Cosmos.Tests.Contracts.ContractEnforcement.ValidateContractContainBreakingChanges(
                dllName: "Microsoft.Azure.Cosmos.Encryption",
                baselinePath: baseline,
                breakingChangesPath: breakingChanges);
        }

        private static int? GetCurrentMajorVersion()
        {
            // Read the TFM from the current test assembly TargetFrameworkAttribute
            TargetFrameworkAttribute attr = Assembly.GetExecutingAssembly().GetCustomAttribute<TargetFrameworkAttribute>();
            if (attr?.FrameworkName == null)
            {
                return null;
            }

            // Example: ".NETCoreApp,Version=v8.0" -> 8
            FrameworkName fx = new FrameworkName(attr.FrameworkName);
            return fx.Version.Major;
        }
    }
}
