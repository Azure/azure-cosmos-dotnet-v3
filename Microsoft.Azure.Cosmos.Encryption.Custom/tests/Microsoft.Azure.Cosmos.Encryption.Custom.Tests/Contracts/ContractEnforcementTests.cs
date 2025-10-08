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
        [TestMethod]
        public void ContractChanges()
        {
            // Resolve a framework-specific baseline if available; otherwise fall back to the generic baseline.
            string baseline = ResolveFrameworkSpecificBaseline(
                baseFileName: "DotNetSDKEncryptionCustomAPI",
                defaultFileName: "DotNetSDKEncryptionCustomAPI.json");

            Cosmos.Tests.Contracts.ContractEnforcement.ValidateContractContainBreakingChanges(
                dllName: "Microsoft.Azure.Cosmos.Encryption.Custom",
                baselinePath: baseline,
                breakingChangesPath: "DotNetSDKEncryptionCustomAPIChanges.json");
        }

        private static string ResolveFrameworkSpecificBaseline(string baseFileName, string defaultFileName)
        {
            string contractsDir = "Contracts";
            int? currentMajorVersion = GetCurrentMajorVersion();
            string[] candidates = {
                currentMajorVersion is null ? null : $"{baseFileName}.net{currentMajorVersion}.json",
                defaultFileName
            };

            string existing = candidates
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => Path.Combine(contractsDir, name))
                .FirstOrDefault(File.Exists);

            // Return just the file name as expected by ContractEnforcement
            return existing != null ? Path.GetFileName(existing) : defaultFileName;
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
