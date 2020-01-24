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
            JToken summary = jObject["Summary"];
            Assert.IsTrue(summary["UserAgent"].ToString().Contains("cosmos-netstandard-sdk"), "Diagnostics should have user agent string");
            Assert.AreEqual("[]", jObject["Context"].ToString());

            // Test all the different operations on diagnostics context
            cosmosDiagnostics.Summary.AddSdkRetry(TimeSpan.FromSeconds(42));
            using (cosmosDiagnostics.CreateOverallScope("OverallScope"))
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
                using (cosmosDiagnostics.CreateScope("ValidateScope"))
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }

            cosmosDiagnostics.Summary.SetSdkUserAgent("MyCustomUserAgentString");

            string result = cosmosDiagnostics.ToString();

            string regex = @"\{""Summary"":\{""StartUtc"":"".+Z"",""ElapsedTime"":""00:00:.+"",""UserAgent"":""MyCustomUserAgentString"",""RetryCount"":1,""RetryBackOffTime"":""00:00:42""\},""Context"":\[\{""Id"":""OverallScope"",""ElapsedTime"":""00:00:.+""\},\{""Id"":""ValidateScope"",""ElapsedTime"":""00:00:.+""\}\]\}";

            Assert.IsTrue(Regex.IsMatch(result, regex), result);

            JToken jToken = JToken.Parse(result);
            TimeSpan total = jToken["Summary"]["ElapsedTime"].ToObject<TimeSpan>();
            Assert.IsTrue(total > TimeSpan.FromSeconds(2));
            TimeSpan overalScope = jToken["Context"][0]["ElapsedTime"].ToObject<TimeSpan>();
            Assert.IsTrue(total == overalScope);
            TimeSpan innerScope = jToken["Context"][1]["ElapsedTime"].ToObject<TimeSpan>();
            Assert.IsTrue(innerScope > TimeSpan.FromSeconds(1));
        }

        [TestMethod]
        public void ValidateDiagnosticsAppendContext()
        {
            CosmosDiagnosticsContext cosmosDiagnostics = new CosmosDiagnosticsContext();

            // Test all the different operations on diagnostics context
            cosmosDiagnostics.Summary.AddSdkRetry(TimeSpan.FromSeconds(42));
            using (cosmosDiagnostics.CreateScope("ValidateScope"))
            {
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }

            cosmosDiagnostics.Summary.SetSdkUserAgent("MyCustomUserAgentString");

            CosmosDiagnosticsContext cosmosDiagnostics2 = new CosmosDiagnosticsContext();

            using (cosmosDiagnostics.CreateScope("CosmosDiagnostics2Scope"))
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            }

            cosmosDiagnostics2.Append(cosmosDiagnostics);

            string diagnostics = cosmosDiagnostics2.ToString();
            Assert.IsTrue(diagnostics.Contains("MyCustomUserAgentString"));
            Assert.IsTrue(diagnostics.Contains("ValidateScope"));
            Assert.IsTrue(diagnostics.Contains("CosmosDiagnostics2Scope"));
        }
    }
}
