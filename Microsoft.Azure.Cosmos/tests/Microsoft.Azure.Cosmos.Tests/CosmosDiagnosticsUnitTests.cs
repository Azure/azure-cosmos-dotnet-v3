//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class CosmosDiagnosticsUnitTests
    {
        [TestMethod]
        public void ValidateDiagnosticsContext()
        {
            CosmosDiagnosticsContext cosmosDiagnostics = new CosmosDiagnosticsContext();
            string diagnostics = cosmosDiagnostics.ToString();

            //Test the default user agent string
            JObject jObject = JObject.Parse(diagnostics);
            Assert.IsTrue(jObject["UserAgent"].ToString().Contains("cosmos-netstandard-sdk"), "Diagnostics should have user agent string");
            Assert.AreEqual("[]", jObject["ContextList"].ToString());

            // Test all the different operations on diagnostics context
            cosmosDiagnostics.AddJsonAttribute("Test", new { secret = "JsonAttributeTestValue" });
            cosmosDiagnostics.AddSdkRetry(TimeSpan.FromSeconds(42));
            using (cosmosDiagnostics.CreateScope("ValidateScope"))
            {
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }

            cosmosDiagnostics.SetSdkUserAgent("MyCustomUserAgentString");

            string result = cosmosDiagnostics.ToString();
           
            string regex = @"{""RetryCount"":1,""RetryBackoffTimeSpan"":""00:00:42"",""UserAgent"":""MyCustomUserAgentString"",""ContextList"":\[\{""Id"":""Test"",""Value"":{""secret"":""JsonAttributeTestValue""}},{""Id"":""ValidateScope"",""StartTimeUtc"":"".+Z"",""ElapsedTime"":""00:00:.+""\}\]\}";

            Assert.IsTrue(Regex.IsMatch(result, regex), result);
        }
    }
}
