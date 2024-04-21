﻿namespace Microsoft.Azure.Cosmos.Tests.Contracts
{
    using System;
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
    }
}
