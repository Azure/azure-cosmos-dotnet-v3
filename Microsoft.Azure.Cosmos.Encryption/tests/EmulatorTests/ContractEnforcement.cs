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

        [TestMethod]
        public void ContractChanges()
        {
            string currentJson = ContractEnforcementSharedHelper.GetCurrentContract(
                "Microsoft.Azure.Cosmos.Encryption");

            string baselineJson = ContractEnforcementSharedHelper.GetBaselineContract(BaselinePath);
            File.WriteAllText($"{BreakingChangesPath}", currentJson);

            ContractEnforcementSharedHelper.CompareAndTraceJson(baselineJson, currentJson);
        }
    }
}
