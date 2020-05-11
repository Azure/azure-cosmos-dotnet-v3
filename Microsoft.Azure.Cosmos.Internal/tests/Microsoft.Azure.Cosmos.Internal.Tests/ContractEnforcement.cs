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

            string previewLocalJsonNoOfficialContract = this.GetPreviewContractWithoutOfficialContract(previewLocalJObject, officialBaselineJObject);
            Assert.IsNotNull(previewLocalJsonNoOfficialContract);

            string baselinePreviewJson = ContractEnforcementSharedHelper.GetBaselineContract(BaselinePath);
            File.WriteAllText($"{BreakingChangesPath}", previewLocalJsonNoOfficialContract);

            Assert.IsFalse(
                ContractEnforcementSharedHelper.CompareAndTraceJson(baselinePreviewJson, previewLocalJsonNoOfficialContract),
                $@"Public API has changed. If this is expected, then refresh {BaselinePath} with {Environment.NewLine} Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/testbaseline.cmd /update after this test is run locally. To see the differences run testbaselines.cmd /diff"
            );
        }

        private string GetPreviewContractWithoutOfficialContract(JObject previewContract, JObject officialContract)
        {
            this.GetPreviewContractHelper(previewContract, officialContract);
            return previewContract.ToString();
        }

        private void GetPreviewContractHelper(JObject previewContract, JObject officialContract)
        {
            string p = previewContract.ToString();
            string o = officialContract.ToString();
            foreach (KeyValuePair<string, JToken> token in officialContract)
            {
                JToken previewLocalToken = previewContract[token.Key];
                if (previewLocalToken != null)
                {
                    if (JToken.DeepEquals(previewLocalToken, token.Value))
                    {
                        previewContract.Remove(token.Key);
                    }
                    else if (previewLocalToken.Type == JTokenType.Object && token.Value.Type == JTokenType.Object)
                    {
                        this.GetPreviewContractHelper(previewLocalToken as JObject, token.Value as JObject);
                    }
                }
            }
        }
    }
}
