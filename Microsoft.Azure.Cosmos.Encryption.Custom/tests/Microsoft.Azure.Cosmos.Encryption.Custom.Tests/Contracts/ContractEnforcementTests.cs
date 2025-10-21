namespace Microsoft.Azure.Cosmos.Encryption.Tests.Contracts
{
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Versioning;
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
            int? currentMajorVersion = GetCurrentMajorVersion();
            
            // REQUIRE framework-specific baseline - no fallback to generic contract
            if (!currentMajorVersion.HasValue)
            {
                Assert.Fail("Unable to determine target framework version. " +
                           "Encryption.Custom requires framework-specific contracts (.net6 or .net8).");
            }

            string baseline = $"DotNetSDKEncryptionCustomAPI.net{currentMajorVersion}.json";
            string baselinePath = Path.Combine("Contracts", baseline);

            // Validate that the baseline file exists
            if (!File.Exists(baselinePath))
            {
                Assert.Fail($"Framework-specific baseline file not found: {baselinePath}. " +
                           $"Encryption.Custom requires separate contracts for each target framework (.NET {currentMajorVersion}). " +
                           $"Run UpdateContracts.ps1 from the repository root to generate all required baseline files.");
            }

            // Breaking changes path is always framework-specific
            string breakingChangesPath = $"DotNetSDKEncryptionCustomAPIChanges.net{currentMajorVersion}.json";

            Cosmos.Tests.Contracts.ContractEnforcement.ValidateContractContainBreakingChanges(
                dllName: "Microsoft.Azure.Cosmos.Encryption.Custom",
                baselinePath: baseline,
                breakingChangesPath: breakingChangesPath);
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
