namespace Microsoft.Azure.Cosmos.Tests.Contracts
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Xml;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    
    [TestCategory("Windows")]
    [TestCategory("UpdateContract")]
    [TestClass]
    public class ContractEnforcementTests
    {
        private const string DllName = "Microsoft.Azure.Cosmos.Client";
        private const string OfficialBaselinePath = "DotNetSDKAPI.json";
        private const string OfficialTelemetryBaselinePath = "DotNetSDKTelemetryAPI.json";

#if PREVIEW
        [TestMethod]
        public void PreviewContractChanges()
        {
            ContractEnforcement.ValidatePreviewContractContainBreakingChanges(
                dllName: DllName,
                officialBaselinePath: OfficialBaselinePath,
                previewBaselinePath: "DotNetPreviewSDKAPI.json",
                previewBreakingChangesPath: "DotNetPreviewSDKAPIChanges.json");
        }
#else
        [TestMethod]
        public void ContractChanges()
        {
            ContractEnforcement.ValidateContractContainBreakingChanges(
                dllName: DllName,
                baselinePath: OfficialBaselinePath,
                breakingChangesPath: "DotNetSDKAPIChanges.json");
        }

        [TestMethod]
        public void TelemetryContractChanges()
        {
            ContractEnforcement.ValidateTelemetryContractContainBreakingChanges(
                dllName: DllName,
                baselinePath: OfficialTelemetryBaselinePath,
                breakingChangesPath: "DotNetSDKTelemetryAPIChanges.json");
        }
#endif

        [TestMethod]
        public void UniqueKeyUnsealed()
        {
            Assert.IsFalse(typeof(UniqueKey).IsSealed);
        }

        [TestMethod]
        public void ValdatePacakgeVersions()
        {
            const String VersionFile = "Directory.Build.props";

            XmlDocument xmldoc = new XmlDocument();
            xmldoc.LoadXml(File.ReadAllText(VersionFile));

            XmlNode propertyGroupNode = xmldoc.DocumentElement["PropertyGroup"];

            string officialVersionText = propertyGroupNode?["ClientOfficialVersion"]?.InnerText;
            string previewVersionText = propertyGroupNode?["ClientPreviewVersion"]?.InnerText;

            Trace.TraceInformation($"Official Version: {officialVersionText}");
            Trace.TraceInformation($"Preview Version: {previewVersionText}");

            Version officialVersion = new Version(officialVersionText);
            Version previewVersion = new Version(previewVersionText);

            // Preview minor version should always be one ahead of the official version
            Assert.AreEqual(officialVersion.Major, previewVersion.Major, $"{officialVersionText} {previewVersionText}");
            Assert.AreEqual(officialVersion.Minor + 1, previewVersion.Minor, $"{officialVersionText} {previewVersionText}");
            Assert.AreEqual(officialVersion.Build, previewVersion.Build, $"{officialVersionText} {previewVersionText}");
        }
    }
}
