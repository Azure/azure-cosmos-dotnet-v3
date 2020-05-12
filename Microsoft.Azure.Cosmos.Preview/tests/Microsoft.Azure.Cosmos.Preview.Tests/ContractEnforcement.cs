namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestCategory("Windows")]
    [TestClass]
    public class ContractEnforcement
    {
        private const string BaselinePath = "DotNetSDKAPI.json";
        private const string BreakingChangesPath = "DotNetSDKAPIChanges.json";
        private const string OfficialBaselinePath = @"OfficialDotNetSDKAPI.json";

        [TestMethod]
        public void ContractChanges()
        {
            string currentJson = ContractEnforcementSharedHelper.GetCurrentContract(
                "Microsoft.Azure.Cosmos.Client");

            JObject previewLocalJObject = JObject.Parse(currentJson);
            JObject officialBaselineJObject = JObject.Parse(File.ReadAllText(OfficialBaselinePath));

            string previewLocalJsonNoOfficialContract = ContractEnforcementSharedHelper.RemoveDuplicateContractElements(
                localContract: previewLocalJObject,
                officialContract: officialBaselineJObject);

            Assert.IsNotNull(previewLocalJsonNoOfficialContract);

            string baselinePreviewJson = ContractEnforcementSharedHelper.GetBaselineContract(BaselinePath);
            File.WriteAllText($"{BreakingChangesPath}", previewLocalJsonNoOfficialContract);

            ContractEnforcementSharedHelper.CompareAndTraceJson(baselinePreviewJson, currentJson);
        }
    }
}
