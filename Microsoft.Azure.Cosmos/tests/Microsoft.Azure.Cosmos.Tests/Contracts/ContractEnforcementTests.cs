namespace Microsoft.Azure.Cosmos.Tests.Contracts
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Versioning;
    using System.Xml;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestCategory("Windows")]
    [TestCategory("UpdateContract")]
    [TestClass]
    public class ContractEnforcementTests
    {
        private const string DllName = "Microsoft.Azure.Cosmos.Client";

#if PREVIEW
        [TestMethod]
        public void PreviewContractChanges()
        {
            ContractEnforcement.ValidateContract(
                dllName: DllName,
                contractType: ContractType.Preview,
                baselinePattern: "DotNetPreviewSDKAPI",
                breakingChangesPattern: "DotNetPreviewSDKAPIChanges",
                officialBaselinePattern: "DotNetSDKAPI");
        }
#else
        /// <summary>
        /// This test validates the public API surface against a baseline contract.
        /// 
        /// IMPORTANT: Because tests run on multiple .NET versions (net6.0 and net8.0),
        /// the contract validation uses framework-specific baselines to ensure consistency:
        /// 
        /// - When running on net6.0: validates against DotNetSDKAPI.net6.json
        /// - When running on net8.0: validates against DotNetSDKAPI.net8.json
        /// 
        /// To update baselines, run: UpdateContracts.ps1 from the repository root.
        /// This script runs tests on BOTH net6.0 and net8.0 to generate both baselines.
        /// </summary>
        [TestMethod]
        public void ContractChanges()
        {
            ContractEnforcement.ValidateContract(
                dllName: DllName,
                contractType: ContractType.Standard,
                baselinePattern: "DotNetSDKAPI",
                breakingChangesPattern: "DotNetSDKAPIChanges");
        }

        [TestMethod]
        public void TelemetryContractChanges()
        {
            ContractEnforcement.ValidateContract(
                dllName: DllName,
                contractType: ContractType.Telemetry,
                baselinePattern: "DotNetSDKTelemetryAPI",
                breakingChangesPattern: "DotNetSDKTelemetryAPIChanges");
        }

        /// <summary>
        /// This test validates that the contract enforcement is actually checking for differences
        /// by verifying that mismatched JSON strings are properly detected and result in a failure.
        /// This ensures that the ValidateJsonAreSame method is working correctly.
        /// </summary>
        [TestMethod]
        public void ContractEnforcementDetectsChanges()
        {
            // Arrange: Create two different JSON strings representing API contracts
            string baselineJson = @"{
  ""Subclasses"": {
    ""Microsoft.Azure.Cosmos.Container"": {
      ""Subclasses"": {},
      ""Members"": {
        ""Microsoft.Azure.Cosmos.Container Database"": {
          ""Type"": ""Property"",
          ""Attributes"": [],
          ""MethodInfo"": null
        }
      }
    }
  }
}";

            string modifiedJson = @"{
  ""Subclasses"": {
    ""Microsoft.Azure.Cosmos.Container"": {
      ""Subclasses"": {},
      ""Members"": {
        ""Microsoft.Azure.Cosmos.Container Database"": {
          ""Type"": ""Property"",
          ""Attributes"": [],
          ""MethodInfo"": null
        },
        ""Microsoft.Azure.Cosmos.Container NewProperty"": {
          ""Type"": ""Property"",
          ""Attributes"": [],
          ""MethodInfo"": null
        }
      }
    }
  }
}";

            // Act & Assert: Verify that ValidateJsonAreSame throws when contracts differ
            AssertFailedException exception = Assert.ThrowsException<AssertFailedException>(
                () => ContractEnforcement.ValidateJsonAreSame(baselineJson, modifiedJson));

            // Verify the exception message mentions the contract update script
            Assert.IsTrue(exception.Message.Contains("Public API has changed"),
                $"Expected failure message to mention API changes. Actual message: {exception.Message}");
            Assert.IsTrue(exception.Message.Contains("UpdateContracts.ps1"),
                $"Expected failure message to mention UpdateContracts.ps1. Actual message: {exception.Message}");
        }

        /// <summary>
        /// This test validates that identical JSON contracts pass validation,
        /// ensuring that the contract enforcement doesn't produce false positives.
        /// </summary>
        [TestMethod]
        public void ContractEnforcementAllowsIdenticalContracts()
        {
            // Arrange: Create identical JSON strings
            string json = @"{
  ""Subclasses"": {
    ""Microsoft.Azure.Cosmos.Container"": {
      ""Subclasses"": {},
      ""Members"": {
        ""Microsoft.Azure.Cosmos.Container Database"": {
          ""Type"": ""Property"",
          ""Attributes"": [],
          ""MethodInfo"": null
        }
      }
    }
  }
}";

            // Act & Assert: Verify that identical contracts don't throw
            ContractEnforcement.ValidateJsonAreSame(json, json);
            // If we get here without an exception, the test passes
        }
#endif

        [TestMethod]
        public void UniqueKeyUnsealed()
        {
            Assert.IsFalse(typeof(UniqueKey).IsSealed);
        }

        [TestMethod]
        public void ValdatePackageVersions()
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