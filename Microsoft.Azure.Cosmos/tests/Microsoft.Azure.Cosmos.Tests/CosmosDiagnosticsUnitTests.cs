//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class CosmosDiagnosticsUnitTests
    {
        [TestMethod]
        public void ValidateDiagnosticsContext()
        {
            CosmosDiagnosticsContext cosmosDiagnostics = new CosmosDiagnosticsContextCore();
            cosmosDiagnostics.GetOverallScope().Dispose();
            string diagnostics = cosmosDiagnostics.ToString();

            //Test the default user agent string
            JObject jObject = JObject.Parse(diagnostics);
            JToken summary = jObject["Summary"];
            Assert.IsTrue(summary["UserAgent"].ToString().Contains("cosmos-netstandard-sdk"), "Diagnostics should have user agent string");

            cosmosDiagnostics = new CosmosDiagnosticsContextCore();
            using (cosmosDiagnostics.GetOverallScope())
            {
                // Test all the different operations on diagnostics context
                Thread.Sleep(TimeSpan.FromSeconds(1));
                using (cosmosDiagnostics.CreateScope("ValidateScope"))
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    cosmosDiagnostics.AddDiagnosticsInternal(new PointOperationStatistics(
                        new Guid("692ab2f2-41ba-486b-aad7-8c7c6c52379f").ToString(),
                        (HttpStatusCode)429,
                        Documents.SubStatusCodes.Unknown,
                        DateTime.UtcNow,
                        42,
                        null,
                        HttpMethod.Get,
                        new Uri("http://MockUri.com"),
                        null,
                        null));
                }

                using (cosmosDiagnostics.CreateScope("SuccessScope"))
                {
                    cosmosDiagnostics.AddDiagnosticsInternal(new PointOperationStatistics(
                        new Guid("de09baab-71a4-4897-a163-470711c93ed3").ToString(),
                        HttpStatusCode.OK,
                        Documents.SubStatusCodes.Unknown,
                        DateTime.UtcNow,
                        42,
                        null,
                        HttpMethod.Get,
                        new Uri("http://MockUri.com"),
                        null,
                        null));
                }
            }

            cosmosDiagnostics.SetSdkUserAgent("MyCustomUserAgentString");

            string result = cosmosDiagnostics.ToString();

            string regex = @"\{""DiagnosticVersion"":""2"",""Summary"":\{""StartUtc"":"".+Z"",""TotalElapsedTimeInMs"":.+,""UserAgent"":""MyCustomUserAgentString"",""TotalRequestCount"":2,""FailedRequestCount"":1\},""Context"":\[\{""Id"":""ValidateScope"",""ElapsedTimeInMs"":.+\},\{""Id"":""PointOperationStatistics"",""ActivityId"":""692ab2f2-41ba-486b-aad7-8c7c6c52379f"",""ResponseTimeUtc"":"".+Z"",""StatusCode"":429,""SubStatusCode"":0,""RequestCharge"":42.0,""RequestUri"":""http://MockUri.com"",""RequestSessionToken"":null,""ResponseSessionToken"":null\},\{""Id"":""SuccessScope"",""ElapsedTimeInMs"":.+\},\{""Id"":""PointOperationStatistics"",""ActivityId"":""de09baab-71a4-4897-a163-470711c93ed3"",""ResponseTimeUtc"":"".+Z"",""StatusCode"":200,""SubStatusCode"":0,""RequestCharge"":42.0,""RequestUri"":""http://MockUri.com"",""RequestSessionToken"":null,""ResponseSessionToken"":null\}\]\}";
            Assert.IsTrue(Regex.IsMatch(result, regex), $"regex: {regex} result: {result}");

            JToken jToken = JToken.Parse(result);
            double total = jToken["Summary"]["TotalElapsedTimeInMs"].ToObject<double>();
            Assert.IsTrue(total > TimeSpan.FromSeconds(2).TotalMilliseconds);
            double overalScope = jToken["Context"][0]["ElapsedTimeInMs"].ToObject<double>();
            Assert.IsTrue(overalScope < total);
            Assert.IsTrue(overalScope > TimeSpan.FromSeconds(1).TotalMilliseconds);
            double innerScope = jToken["Context"][2]["ElapsedTimeInMs"].ToObject<double>();
            Assert.IsTrue(innerScope > 0);
        }

        [TestMethod]
        public void ValidateDiagnosticsAppendContext()
        {
            CosmosDiagnosticsContext cosmosDiagnostics = new CosmosDiagnosticsContextCore();
            CosmosDiagnosticsContext cosmosDiagnostics2;

            using (cosmosDiagnostics.GetOverallScope())
            {
                // Test all the different operations on diagnostics context
                using (cosmosDiagnostics.CreateScope("ValidateScope"))
                {
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                }

                cosmosDiagnostics.SetSdkUserAgent("MyCustomUserAgentString");

                cosmosDiagnostics2 = new CosmosDiagnosticsContextCore();
                cosmosDiagnostics2.GetOverallScope().Dispose();

                using (cosmosDiagnostics.CreateScope("CosmosDiagnostics2Scope"))
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(100));
                }

                cosmosDiagnostics2.AddDiagnosticsInternal(cosmosDiagnostics);
            }

            string diagnostics = cosmosDiagnostics2.ToString();
            Assert.IsTrue(diagnostics.Contains("MyCustomUserAgentString"));
            Assert.IsTrue(diagnostics.Contains("ValidateScope"));
            Assert.IsTrue(diagnostics.Contains("CosmosDiagnostics2Scope"));
        }

        [TestMethod]
        public void ValidateClientSideRequestStatisticsToString()
        {
            // Verify that API using the interface get the older v2 string
            CosmosDiagnosticsContext diagnosticsContext = new CosmosDiagnosticsContextCore();
            diagnosticsContext.GetOverallScope().Dispose();

            CosmosClientSideRequestStatistics clientSideRequestStatistics = new CosmosClientSideRequestStatistics(diagnosticsContext);
            string noInfo = clientSideRequestStatistics.ToString();
            Assert.AreEqual("Please see CosmosDiagnostics", noInfo);

            StringBuilder stringBuilder = new StringBuilder();
            clientSideRequestStatistics.AppendToBuilder(stringBuilder);
            string noInfoStringBuilder = stringBuilder.ToString();
            Assert.AreEqual("Please see CosmosDiagnostics", noInfo);

            string id = clientSideRequestStatistics.RecordAddressResolutionStart(new Uri("https://testuri"));
            clientSideRequestStatistics.RecordAddressResolutionEnd(id);

            Documents.DocumentServiceRequest documentServiceRequest = new Documents.DocumentServiceRequest(
                    operationType: Documents.OperationType.Read,
                    resourceIdOrFullName: null,
                    resourceType: Documents.ResourceType.Database,
                    body: null,
                    headers: null,
                    isNameBased: false,
                    authorizationTokenType: Documents.AuthorizationTokenType.PrimaryMasterKey);

            clientSideRequestStatistics.RecordRequest(documentServiceRequest);
            clientSideRequestStatistics.RecordResponse(
                documentServiceRequest,
                new Documents.StoreResult(
                    storeResponse: new Documents.StoreResponse(),
                    exception: null,
                    partitionKeyRangeId: "PkRange",
                    lsn: 42,
                    quorumAckedLsn: 4242,
                    requestCharge: 9000.42,
                    currentReplicaSetSize: 3,
                    currentWriteQuorum: 4,
                    isValid: true,
                    storePhysicalAddress: null,
                    globalCommittedLSN: 2,
                    numberOfReadRegions: 1,
                    itemLSN: 5,
                    sessionToken: null,
                    usingLocalLSN: true));

            string statistics = clientSideRequestStatistics.ToString();
            Assert.AreEqual("Please see CosmosDiagnostics", statistics);
        }


        [TestMethod]
        public void TestUpdatesWhileVisiting()
        {
            CosmosDiagnosticsContext cosmosDiagnostics = new CosmosDiagnosticsContextCore();
            cosmosDiagnostics.CreateScope("FirstScope");

            bool isFirst = true;
            foreach (CosmosDiagnosticsInternal diagnostic in cosmosDiagnostics)
            {
                if (isFirst)
                {
                    cosmosDiagnostics.CreateScope("SecondScope");
                    isFirst = false;
                }
               
                diagnostic.ToString();
            }
        }
    }
}
