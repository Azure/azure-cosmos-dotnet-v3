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
            string previewSuffixText = propertyGroupNode?["ClientPreviewSuffixVersion"]?.InnerText;

            Logger.LogLine($"Official Version: {officialVersionText}");
            Logger.LogLine($"Preview Version: {previewVersionText}");
            Logger.LogLine($"PreviewSuffix Suffix: {previewSuffixText}");

            this.ValdateSDKVersionsUtil(officialVersionText, previewVersionText, previewSuffixText);
        }

        [TestMethod]
        [DataRow("4.0.0", "4.1.0", "preview.0", false)]
        [DataRow("4.0.1", "4.1.0", "preview.1", false)]
        // Invalida pattern's
        [DataRow("4.0.0", "4.0.0", "preview.0", true)]
        [DataRow("4.0.0", "4.1.0", "preview.1", true)]
        [DataRow("4.2.0", "4.1.0", "preview.0", true)]
        public void ValdateSDKVersions(string officialVersionText,
            string previewVersionText,
            string previewSuffixText,
            bool failureExpected)
        {
            if (failureExpected)
            {
                Assert.ThrowsException<AssertFailedException>(() => this.ValdateSDKVersionsUtil(officialVersionText, previewVersionText, previewSuffixText));
            }
            else
            {
                this.ValdateSDKVersionsUtil(officialVersionText, previewVersionText, previewSuffixText);
            }
        }

        private void ValdateSDKVersionsUtil(string officialVersionText,
            string previewVersionText,
            string previewSuffixText)
        {
            Version officialVersion = new Version(officialVersionText);
            Version previewVersion = new Version(previewVersionText);

            string debugText = $"{officialVersionText} {previewVersionText} {previewSuffixText}";

            string[] peviewSuffixSplits = previewSuffixText.Split('.');
            Assert.AreEqual(2, peviewSuffixSplits.Length, $"{debugText}");

            // Preview minor version should always be one ahead of the official version
            Assert.AreEqual(officialVersion.Major, previewVersion.Major, $"{debugText}");
            Assert.AreEqual(officialVersion.Minor + 1, previewVersion.Minor, $"{debugText}");
            Assert.AreEqual(0, previewVersion.Build, $"{debugText}");

            Assert.AreEqual(officialVersion.Build, int.Parse(peviewSuffixSplits[1]), $"{debugText}");
            Assert.AreEqual("preview", peviewSuffixSplits[0], false, $"{debugText}");
        }
    }
}