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
            CosmosDiagnosticsContext cosmosDiagnostics = new CosmosDiagnosticsContextCore(userClientRequestId: null);
            string diagnostics = cosmosDiagnostics.ToString();

            //Test the default user agent string
            JObject jObject = JObject.Parse(diagnostics);
            JToken summary = jObject["Summary"];
            Assert.IsTrue(summary["UserAgent"].ToString().Contains("cosmos-netstandard-sdk"), "Diagnostics should have user agent string");
            Assert.AreEqual("[]", jObject["Context"].ToString());

            // Test all the different operations on diagnostics context
            using (cosmosDiagnostics.CreateOverallScope("OverallScope"))
            {
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

            string regex = @"\{""Summary"":\{""StartUtc"":"".+Z"",""TotalElapsedTime"":""00:00:.+"",""UserAgent"":""MyCustomUserAgentString"",""TotalRequestCount"":2,""FailedRequestCount"":1\},""Context"":\[\{""Id"":""OverallScope"",""ElapsedTime"":""00:00:0.+""\},\{""Id"":""ValidateScope"",""ElapsedTime"":""00:00:0.+""\},\{""Id"":""PointOperationStatistics"",""ActivityId"":""692ab2f2-41ba-486b-aad7-8c7c6c52379f"",""ResponseTimeUtc"":"".+Z"",""StatusCode"":429,""SubStatusCode"":0,""RequestCharge"":42.0,""RequestUri"":""http://MockUri.com"",""RequestSessionToken"":null,""ResponseSessionToken"":null\},\{""Id"":""SuccessScope"",""ElapsedTime"":""00:00:.+""\},\{""Id"":""PointOperationStatistics"",""ActivityId"":""de09baab-71a4-4897-a163-470711c93ed3"",""ResponseTimeUtc"":"".+Z"",""StatusCode"":200,""SubStatusCode"":0,""RequestCharge"":42.0,""RequestUri"":""http://MockUri.com"",""RequestSessionToken"":null,""ResponseSessionToken"":null\}\]\}";
            Assert.IsTrue(Regex.IsMatch(result, regex), result);

            JToken jToken = JToken.Parse(result);
            TimeSpan total = jToken["Summary"]["TotalElapsedTime"].ToObject<TimeSpan>();
            Assert.IsTrue(total > TimeSpan.FromSeconds(2));
            TimeSpan overalScope = jToken["Context"][0]["ElapsedTime"].ToObject<TimeSpan>();
            Assert.IsTrue(total == overalScope);
            TimeSpan innerScope = jToken["Context"][1]["ElapsedTime"].ToObject<TimeSpan>();
            Assert.IsTrue(innerScope > TimeSpan.FromSeconds(1));
        }

        [TestMethod]
        public void ValidateDiagnosticsAppendContext()
        {
            CosmosDiagnosticsContext cosmosDiagnostics = new CosmosDiagnosticsContextCore(userClientRequestId: null);

            // Test all the different operations on diagnostics context
            using (cosmosDiagnostics.CreateScope("ValidateScope"))
            {
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }

            cosmosDiagnostics.SetSdkUserAgent("MyCustomUserAgentString");

            CosmosDiagnosticsContext cosmosDiagnostics2 = new CosmosDiagnosticsContextCore(userClientRequestId: null);

            using (cosmosDiagnostics.CreateScope("CosmosDiagnostics2Scope"))
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            }

            cosmosDiagnostics2.AddDiagnosticsInternal(cosmosDiagnostics);

            string diagnostics = cosmosDiagnostics2.ToString();
            Assert.IsTrue(diagnostics.Contains("MyCustomUserAgentString"));
            Assert.IsTrue(diagnostics.Contains("ValidateScope"));
            Assert.IsTrue(diagnostics.Contains("CosmosDiagnostics2Scope"));
        }

        [TestMethod]
        public void ValidateClientSideRequestStatisticsToString()
        {
            // Verify that API using the interface get the older v2 string
            CosmosDiagnosticsContext diagnosticsContext = new CosmosDiagnosticsContextCore(userClientRequestId: null);
            diagnosticsContext.OverallClientRequestTime.Stop();

            CosmosClientSideRequestStatistics clientSideRequestStatistics = new CosmosClientSideRequestStatistics(diagnosticsContext);
            string noInfo = clientSideRequestStatistics.ToString();
            Assert.IsNotNull(noInfo);

            StringBuilder stringBuilder = new StringBuilder();
            clientSideRequestStatistics.AppendToBuilder(stringBuilder);
            string noInfoStringBuilder = stringBuilder.ToString();
            Assert.IsNotNull(noInfoStringBuilder);

            Assert.AreEqual(noInfo, noInfoStringBuilder);

            string id = clientSideRequestStatistics.RecordAddressResolutionStart(new Uri("https://testuri"));
            clientSideRequestStatistics.RecordAddressResolutionEnd(id);

            clientSideRequestStatistics.RecordResponse(
                new Documents.DocumentServiceRequest(
                    operationType: Documents.OperationType.Read,
                    resourceIdOrFullName: null,
                    resourceType: Documents.ResourceType.Database,
                    body: null,
                    headers: null,
                    isNameBased: false,
                    authorizationTokenType: Documents.AuthorizationTokenType.PrimaryMasterKey),
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
            Assert.IsTrue(statistics.Contains("\"UserAgent\":\"cosmos-netstandard-sdk"));
            Assert.IsTrue(statistics.Contains("UsingLocalLSN: True, TransportException: null"));
            Assert.IsTrue(statistics.Contains("AddressResolutionStatistics\",\"StartTimeUtc"));
        }
    }
}
