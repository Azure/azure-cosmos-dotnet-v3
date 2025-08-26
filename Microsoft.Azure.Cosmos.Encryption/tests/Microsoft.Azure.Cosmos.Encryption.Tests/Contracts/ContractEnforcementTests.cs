namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
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
            string baseline = ResolveFrameworkSpecificBaseline(
                baseFileName: "DotNetSDKEncryptionAPI",
                defaultFileName: "DotNetSDKEncryptionAPI.json");

            Cosmos.Tests.Contracts.ContractEnforcement.ValidateContractContainBreakingChanges(
                dllName: "Microsoft.Azure.Cosmos.Encryption",
                baselinePath: baseline,
                breakingChangesPath: "DotNetSDKEncryptionAPIChanges.json");
        }

        private static string ResolveFrameworkSpecificBaseline(string baseFileName, string defaultFileName)
        {
            string contractsDir = "Contracts";
            string tfm = GetCurrentTFM();
            var candidates = new[]
            {
                tfm is null ? null : $"{baseFileName}.{tfm.Split('.')[0]}.json",
                defaultFileName
            };

            string existing = candidates
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => Path.Combine(contractsDir, name))
                .FirstOrDefault(File.Exists);

            return existing != null ? Path.GetFileName(existing) : defaultFileName;
        }

        private static string GetCurrentTFM()
        {
            var attr = Assembly.GetExecutingAssembly().GetCustomAttribute<TargetFrameworkAttribute>();
            if (attr?.FrameworkName == null)
            {
                return null;
            }

            var fx = new FrameworkName(attr.FrameworkName);
            return $"net{fx.Version.Major}.{fx.Version.Minor}";
        }
    }
}
