//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using static Microsoft.Azure.Cosmos.Tracing.TraceData.ClientSideRequestStatisticsTraceDatum;

    [TestClass]
    public class DiagnosticsSummaryWriterTests
    {
        [TestMethod]
        public void DiagnosticsVerbosity_DefaultIsDetailed()
        {
            Assert.AreEqual(0, (int)DiagnosticsVerbosity.Detailed);
            Assert.AreEqual(DiagnosticsVerbosity.Detailed, default(DiagnosticsVerbosity));
        }

        [TestMethod]
        public void CosmosClientOptions_DiagnosticsVerbosity_DefaultValue()
        {
            CosmosClientOptions options = new CosmosClientOptions();
            Assert.AreEqual(DiagnosticsVerbosity.Detailed, options.DiagnosticsVerbosity);
        }

        [TestMethod]
        public void CosmosClientOptions_MaxSummarySizeBytes_DefaultValue()
        {
            CosmosClientOptions options = new CosmosClientOptions();
            Assert.AreEqual(8192, options.MaxDiagnosticsSummarySizeBytes);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void CosmosClientOptions_MaxSummarySizeBytes_Validation_TooSmall()
        {
            CosmosClientOptions options = new CosmosClientOptions
            {
                MaxDiagnosticsSummarySizeBytes = 2048
            };
        }

        [TestMethod]
        public void CosmosClientOptions_MaxSummarySizeBytes_Validation_MinAllowed()
        {
            CosmosClientOptions options = new CosmosClientOptions
            {
                MaxDiagnosticsSummarySizeBytes = 4096
            };
            Assert.AreEqual(4096, options.MaxDiagnosticsSummarySizeBytes);
        }

        [TestMethod]
        public void ToString_Parameterless_AlwaysDetailed()
        {
            // Parameterless ToString() must always return detailed output
            // regardless of any options setting. We verify structural equivalence
            // (same keys in JSON) rather than exact string match since duration changes.
            using ITrace trace = Trace.GetRootTrace("TestOperation");
            CosmosTraceDiagnostics diagnostics = new CosmosTraceDiagnostics(trace);
            string detailed = diagnostics.ToString();
            string explicitDetailed = diagnostics.ToString(DiagnosticsVerbosity.Detailed);

            JObject parsedDefault = JObject.Parse(detailed);
            JObject parsedExplicit = JObject.Parse(explicitDetailed);

            // Same structure: both have name, Summary, start datetime
            Assert.AreEqual(parsedDefault["name"].ToString(), parsedExplicit["name"].ToString());
            Assert.AreEqual(parsedDefault["start datetime"].ToString(), parsedExplicit["start datetime"].ToString());
        }

        [TestMethod]
        public void ToString_Summary_ProducesValidJson()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.Ok, SubStatusCodes.Unknown, 5.0, 10, DateTime.UtcNow);

            CosmosTraceDiagnostics diagnostics = new CosmosTraceDiagnostics(trace);
            string summary = diagnostics.ToString(DiagnosticsVerbosity.Summary);

            JObject parsed = JObject.Parse(summary);
            Assert.IsNotNull(parsed["Summary"], "Summary object should exist");
            Assert.AreEqual("Summary", parsed["Summary"]["DiagnosticsVerbosity"].ToString());
        }

        [TestMethod]
        public void Summary_SingleRegion_SingleRequest()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.Ok, SubStatusCodes.Unknown, 5.0, 10, DateTime.UtcNow);

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            JObject summaryObj = (JObject)parsed["Summary"];

            Assert.AreEqual(1, summaryObj["TotalRequestCount"].Value<int>());
            Assert.IsNotNull(summaryObj["TotalRequestCharge"]);

            JArray regions = (JArray)summaryObj["RegionsSummary"];
            Assert.AreEqual(1, regions.Count);

            JObject region = (JObject)regions[0];
            Assert.AreEqual("West US 2", region["Region"].ToString());
            Assert.AreEqual(1, region["RequestCount"].Value<int>());
            Assert.IsNotNull(region["First"]);
            Assert.IsNull(region["Last"], "Last should be omitted when only 1 request");
            Assert.IsNull(region["AggregatedGroups"], "No aggregated groups for single request");
        }

        [TestMethod]
        public void Summary_SingleRegion_TwoRequests()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            DateTime baseTime = DateTime.UtcNow;
            AddStoreResponseStatistic(trace, "East US", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0.0, 5, baseTime);
            AddStoreResponseStatistic(trace, "East US", StatusCodes.Ok, SubStatusCodes.Unknown, 5.0, 12, baseTime.AddSeconds(1));

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            JObject summaryObj = (JObject)parsed["Summary"];

            Assert.AreEqual(2, summaryObj["TotalRequestCount"].Value<int>());

            JArray regions = (JArray)summaryObj["RegionsSummary"];
            JObject region = (JObject)regions[0];
            Assert.AreEqual(2, region["RequestCount"].Value<int>());
            Assert.IsNotNull(region["First"]);
            Assert.IsNotNull(region["Last"]);
            Assert.AreEqual((int)StatusCodes.TooManyRequests, region["First"]["StatusCode"].Value<int>());
            Assert.AreEqual((int)StatusCodes.Ok, region["Last"]["StatusCode"].Value<int>());
            Assert.IsNull(region["AggregatedGroups"], "No middle entries for exactly 2 requests");
        }

        [TestMethod]
        public void Summary_SingleRegion_ManyRetries_429()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            DateTime baseTime = DateTime.UtcNow;

            // First request: 429
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0.0, 5, baseTime);

            // 48 middle retries: all 429
            for (int i = 1; i <= 48; i++)
            {
                AddStoreResponseStatistic(trace, "West US 2", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0.0, 3 + i, baseTime.AddMilliseconds(i * 100));
            }

            // Last request: 200 OK
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.Ok, SubStatusCodes.Unknown, 5.0, 12, baseTime.AddSeconds(5));

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            JObject summaryObj = (JObject)parsed["Summary"];

            Assert.AreEqual(50, summaryObj["TotalRequestCount"].Value<int>());

            JArray regions = (JArray)summaryObj["RegionsSummary"];
            Assert.AreEqual(1, regions.Count);

            JObject region = (JObject)regions[0];
            Assert.AreEqual(50, region["RequestCount"].Value<int>());
            Assert.AreEqual((int)StatusCodes.TooManyRequests, region["First"]["StatusCode"].Value<int>());
            Assert.AreEqual((int)StatusCodes.Ok, region["Last"]["StatusCode"].Value<int>());

            JArray groups = (JArray)region["AggregatedGroups"];
            Assert.AreEqual(1, groups.Count, "All middle entries are 429 so 1 aggregated group");

            JObject group = (JObject)groups[0];
            Assert.AreEqual((int)StatusCodes.TooManyRequests, group["StatusCode"].Value<int>());
            Assert.AreEqual(48, group["Count"].Value<int>());
            Assert.IsTrue(group["MinDurationMs"].Value<double>() > 0);
            Assert.IsTrue(group["MaxDurationMs"].Value<double>() >= group["MinDurationMs"].Value<double>());
            Assert.IsTrue(group["P50DurationMs"].Value<double>() > 0);
            Assert.IsTrue(group["AvgDurationMs"].Value<double>() > 0);
        }

        [TestMethod]
        public void Summary_MultiRegion_Failover()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            DateTime baseTime = DateTime.UtcNow;

            // West US 2: 3 requests
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0.0, 5, baseTime);
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0.0, 10, baseTime.AddMilliseconds(100));
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.ServiceUnavailable, SubStatusCodes.Unknown, 0.0, 15, baseTime.AddMilliseconds(200));

            // East US 2: 2 requests
            AddStoreResponseStatistic(trace, "East US 2", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0.0, 8, baseTime.AddMilliseconds(300));
            AddStoreResponseStatistic(trace, "East US 2", StatusCodes.Ok, SubStatusCodes.Unknown, 5.0, 12, baseTime.AddMilliseconds(400));

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            JObject summaryObj = (JObject)parsed["Summary"];

            Assert.AreEqual(5, summaryObj["TotalRequestCount"].Value<int>());
            Assert.AreEqual(2, ((JArray)summaryObj["RegionsSummary"]).Count);

            JObject westRegion = (JObject)summaryObj["RegionsSummary"][0];
            Assert.AreEqual("West US 2", westRegion["Region"].ToString());
            Assert.AreEqual(3, westRegion["RequestCount"].Value<int>());

            JObject eastRegion = (JObject)summaryObj["RegionsSummary"][1];
            Assert.AreEqual("East US 2", eastRegion["Region"].ToString());
            Assert.AreEqual(2, eastRegion["RequestCount"].Value<int>());
        }

        [TestMethod]
        public void Summary_MixedStatusCodes()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            DateTime baseTime = DateTime.UtcNow;

            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0.0, 5, baseTime);
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0.0, 10, baseTime.AddMilliseconds(100));
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.ServiceUnavailable, SubStatusCodes.Unknown, 0.0, 20, baseTime.AddMilliseconds(200));
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.ServiceUnavailable, SubStatusCodes.Unknown, 0.0, 25, baseTime.AddMilliseconds(300));
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.Ok, SubStatusCodes.Unknown, 5.0, 12, baseTime.AddMilliseconds(400));

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            JObject summaryObj = (JObject)parsed["Summary"];

            JArray regions = (JArray)summaryObj["RegionsSummary"];
            JObject region = (JObject)regions[0];
            JArray groups = (JArray)region["AggregatedGroups"];

            Assert.AreEqual(2, groups.Count, "Two distinct status codes in middle entries");
        }

        [TestMethod]
        public void Summary_P50_OddCount()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            DateTime baseTime = DateTime.UtcNow;

            // 5 requests total: first, 3 middle, last
            AddStoreResponseStatistic(trace, "R1", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0, 1, baseTime);
            AddStoreResponseStatistic(trace, "R1", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0, 10, baseTime.AddMilliseconds(100));
            AddStoreResponseStatistic(trace, "R1", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0, 20, baseTime.AddMilliseconds(200));
            AddStoreResponseStatistic(trace, "R1", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0, 30, baseTime.AddMilliseconds(300));
            AddStoreResponseStatistic(trace, "R1", StatusCodes.Ok, SubStatusCodes.Unknown, 5, 5, baseTime.AddMilliseconds(400));

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            JArray groups = (JArray)parsed["Summary"]["RegionsSummary"][0]["AggregatedGroups"];
            JObject group = (JObject)groups[0];

            // Middle entries have durations: 10, 20, 30 (sorted)
            // P50 of 3 items = index (3-1)/2 = 1 → 20
            Assert.AreEqual(10, group["MinDurationMs"].Value<double>());
            Assert.AreEqual(30, group["MaxDurationMs"].Value<double>());
            Assert.AreEqual(20, group["P50DurationMs"].Value<double>());
        }

        [TestMethod]
        public void Summary_P50_EvenCount()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            DateTime baseTime = DateTime.UtcNow;

            // 6 requests total: first, 4 middle, last
            AddStoreResponseStatistic(trace, "R1", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0, 1, baseTime);
            AddStoreResponseStatistic(trace, "R1", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0, 10, baseTime.AddMilliseconds(100));
            AddStoreResponseStatistic(trace, "R1", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0, 20, baseTime.AddMilliseconds(200));
            AddStoreResponseStatistic(trace, "R1", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0, 30, baseTime.AddMilliseconds(300));
            AddStoreResponseStatistic(trace, "R1", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0, 40, baseTime.AddMilliseconds(400));
            AddStoreResponseStatistic(trace, "R1", StatusCodes.Ok, SubStatusCodes.Unknown, 5, 5, baseTime.AddMilliseconds(500));

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            JArray groups = (JArray)parsed["Summary"]["RegionsSummary"][0]["AggregatedGroups"];
            JObject group = (JObject)groups[0];

            // Middle entries have durations: 10, 20, 30, 40 (sorted)
            // P50 of 4 items = index (4-1)/2 = 1 → 20
            Assert.AreEqual(10, group["MinDurationMs"].Value<double>());
            Assert.AreEqual(40, group["MaxDurationMs"].Value<double>());
            Assert.AreEqual(20, group["P50DurationMs"].Value<double>());
        }

        [TestMethod]
        public void Summary_P50_SingleItem()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            DateTime baseTime = DateTime.UtcNow;

            // 3 requests total: first, 1 middle, last
            AddStoreResponseStatistic(trace, "R1", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0, 1, baseTime);
            AddStoreResponseStatistic(trace, "R1", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0, 42, baseTime.AddMilliseconds(100));
            AddStoreResponseStatistic(trace, "R1", StatusCodes.Ok, SubStatusCodes.Unknown, 5, 5, baseTime.AddMilliseconds(200));

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            JArray groups = (JArray)parsed["Summary"]["RegionsSummary"][0]["AggregatedGroups"];
            JObject group = (JObject)groups[0];

            // Single middle entry with duration 42
            Assert.AreEqual(1, group["Count"].Value<int>());
            Assert.AreEqual(42, group["P50DurationMs"].Value<double>());
            Assert.AreEqual(42, group["MinDurationMs"].Value<double>());
            Assert.AreEqual(42, group["MaxDurationMs"].Value<double>());
        }

        [TestMethod]
        public void Summary_SizeEnforcement_UnderLimit()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            AddStoreResponseStatistic(trace, "R1", StatusCodes.Ok, SubStatusCodes.Unknown, 5.0, 10, DateTime.UtcNow);

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            int byteCount = System.Text.Encoding.UTF8.GetByteCount(summary);

            Assert.IsTrue(byteCount <= 8192, $"Summary size {byteCount} should be under limit");
            Assert.IsFalse(summary.Contains("Truncated"), "Should not be truncated");
        }

        [TestMethod]
        public void Summary_SizeEnforcement_OverLimit_Truncated()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            DateTime baseTime = DateTime.UtcNow;

            // Generate many requests across many regions to exceed a tiny limit
            for (int r = 0; r < 20; r++)
            {
                for (int i = 0; i < 10; i++)
                {
                    AddStoreResponseStatistic(
                        trace,
                        $"Region {r}",
                        StatusCodes.TooManyRequests,
                        SubStatusCodes.Unknown,
                        0.0,
                        5 + i,
                        baseTime.AddMilliseconds(r * 1000 + i * 100));
                }
            }

            // Use a very small limit to force truncation
            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 512);
            JObject parsed = JObject.Parse(summary);
            JObject summaryObj = (JObject)parsed["Summary"];

            Assert.IsTrue(summaryObj["Truncated"].Value<bool>(), "Should be truncated");
            Assert.IsNotNull(summaryObj["Message"]);
            Assert.AreEqual(200, summaryObj["TotalRequestCount"].Value<int>());
        }

        [TestMethod]
        public void Summary_EmptyTrace()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            JObject summaryObj = (JObject)parsed["Summary"];

            Assert.AreEqual(0, summaryObj["TotalRequestCount"].Value<int>());
            Assert.AreEqual(0, summaryObj["TotalRequestCharge"].Value<double>());
            Assert.AreEqual(0, ((JArray)summaryObj["RegionsSummary"]).Count);
        }

        [TestMethod]
        public void Detailed_Mode_Unchanged()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.Ok, SubStatusCodes.Unknown, 5.0, 10, DateTime.UtcNow);

            CosmosTraceDiagnostics diagnostics = new CosmosTraceDiagnostics(trace);
            string parameterless = diagnostics.ToString();
            string explicitDetailed = diagnostics.ToString(DiagnosticsVerbosity.Detailed);

            // Verify structural equivalence (duration may differ slightly due to timing)
            JObject parsedDefault = JObject.Parse(parameterless);
            JObject parsedExplicit = JObject.Parse(explicitDetailed);

            Assert.AreEqual(parsedDefault["name"].ToString(), parsedExplicit["name"].ToString());
            Assert.AreEqual(parsedDefault["start datetime"].ToString(), parsedExplicit["start datetime"].ToString());

            // Detailed output should contain the full trace tree structure
            Assert.IsNotNull(parsedDefault["name"]);
            Assert.IsNotNull(parsedDefault["Summary"]);
        }

        [TestMethod]
        public void Summary_RegionOrdering_Deterministic()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            DateTime baseTime = DateTime.UtcNow;

            // Add in chronological order: West first, then East
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.Ok, SubStatusCodes.Unknown, 5.0, 10, baseTime);
            AddStoreResponseStatistic(trace, "East US 2", StatusCodes.Ok, SubStatusCodes.Unknown, 5.0, 10, baseTime.AddMilliseconds(100));

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            JArray regions = (JArray)parsed["Summary"]["RegionsSummary"];

            // Regions should appear in the order they were first encountered chronologically
            Assert.AreEqual("West US 2", regions[0]["Region"].ToString());
            Assert.AreEqual("East US 2", regions[1]["Region"].ToString());
        }

        [TestMethod]
        public void Summary_NullRegion_GroupedAsUnknown()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            AddStoreResponseStatistic(trace, null, StatusCodes.Ok, SubStatusCodes.Unknown, 5.0, 10, DateTime.UtcNow);

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            JArray regions = (JArray)parsed["Summary"]["RegionsSummary"];

            Assert.AreEqual("Unknown", regions[0]["Region"].ToString());
        }

        [TestMethod]
        public void Summary_RequestEntryDetail_HasAllFields()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            DateTime requestTime = new DateTime(2026, 3, 17, 12, 0, 0, DateTimeKind.Utc);
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.Ok, SubStatusCodes.Unknown, 5.0, 10, requestTime);

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            JObject first = (JObject)parsed["Summary"]["RegionsSummary"][0]["First"];

            Assert.AreEqual((int)StatusCodes.Ok, first["StatusCode"].Value<int>());
            Assert.IsNotNull(first["SubStatusCode"]);
            Assert.IsNotNull(first["RequestCharge"]);
            Assert.IsNotNull(first["DurationMs"]);
            Assert.AreEqual("West US 2", first["Region"].ToString());
            Assert.IsNotNull(first["Endpoint"]);
            Assert.IsNotNull(first["RequestStartTimeUtc"]);
            Assert.IsNotNull(first["OperationType"]);
            Assert.IsNotNull(first["ResourceType"]);
        }

        [TestMethod]
        public void CosmosTraceDiagnostics_SummaryCaching()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.Ok, SubStatusCodes.Unknown, 5.0, 10, DateTime.UtcNow);

            CosmosTraceDiagnostics diagnostics = new CosmosTraceDiagnostics(trace);

            // Call ToString(Summary) multiple times — should return same cached instance
            string summary1 = diagnostics.ToString(DiagnosticsVerbosity.Summary);
            string summary2 = diagnostics.ToString(DiagnosticsVerbosity.Summary);

            Assert.AreSame(summary1, summary2, "Summary should be cached via Lazy<string>");
        }

        #region Helpers

        private static void AddStoreResponseStatistic(
            ITrace trace,
            string region,
            StatusCodes statusCode,
            SubStatusCodes subStatusCode,
            double requestCharge,
            double durationMs,
            DateTime requestStartTime)
        {
            // Create or find existing ClientSideRequestStatisticsTraceDatum on the trace
            ClientSideRequestStatisticsTraceDatum datum = GetOrCreateDatum(trace);

            // Create a StoreResult with the desired status code and request charge
            StoreResponse storeResponse = new StoreResponse();
            storeResponse.Status = (int)statusCode;
            storeResponse.Headers = new DictionaryNameValueCollection();
            storeResponse.Headers[HttpConstants.HttpHeaders.RequestCharge] = requestCharge.ToString();
            storeResponse.Headers[WFConstants.BackendHeaders.SubStatus] = ((int)subStatusCode).ToString();

            ReferenceCountedDisposable<StoreResult> storeResultRef = StoreResult.CreateForTesting(storeResponse: storeResponse);

            DateTime responseTime = requestStartTime.AddMilliseconds(durationMs);

            StoreResponseStatistics stats = new StoreResponseStatistics(
                requestStartTime: requestStartTime,
                requestResponseTime: responseTime,
                storeResult: storeResultRef.Target,
                resourceType: ResourceType.Document,
                operationType: OperationType.Read,
                requestSessionToken: null,
                locationEndpoint: new Uri("https://account-" + (region ?? "unknown").Replace(" ", "").ToLower() + ".documents.azure.com"),
                region: region);

            // Use reflection to add to the private storeResponseStatistics list
            FieldInfo field = typeof(ClientSideRequestStatisticsTraceDatum)
                .GetField("storeResponseStatistics", BindingFlags.NonPublic | BindingFlags.Instance);
            List<StoreResponseStatistics> list = (List<StoreResponseStatistics>)field.GetValue(datum);
            list.Add(stats);
        }

        private static ClientSideRequestStatisticsTraceDatum GetOrCreateDatum(ITrace trace)
        {
            const string datumKey = "ClientSideStats";

            if (trace.TryGetDatum(datumKey, out object existing)
                && existing is ClientSideRequestStatisticsTraceDatum existingDatum)
            {
                return existingDatum;
            }

            ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(
                DateTime.UtcNow,
                trace);
            trace.AddDatum(datumKey, datum);
            return datum;
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void WriteSummary_NullTrace_ThrowsArgumentNullException()
        {
            DiagnosticsSummaryWriter.WriteSummary(null, 8192);
        }

        [TestMethod]
        public void ToString_InvalidEnumValue_FallsBackToDetailed()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.Ok, SubStatusCodes.Unknown, 5.0, 10, DateTime.UtcNow);

            CosmosTraceDiagnostics diagnostics = new CosmosTraceDiagnostics(trace);

            // Invalid enum value should fall back to detailed output (same as parameterless ToString)
            string result = diagnostics.ToString((DiagnosticsVerbosity)99);
            JObject parsed = JObject.Parse(result);

            Assert.IsNotNull(parsed["name"], "Invalid verbosity should produce detailed output with trace name");
            Assert.IsNull(parsed["Summary"]?["DiagnosticsVerbosity"],
                "Should not contain Summary.DiagnosticsVerbosity since it is detailed output");
        }

        [TestMethod]
        public void CosmosClientOptions_MaxSummarySizeBytes_CustomValuePropagated()
        {
            CosmosClientOptions options = new CosmosClientOptions
            {
                MaxDiagnosticsSummarySizeBytes = 16384
            };

            Assert.AreEqual(16384, options.MaxDiagnosticsSummarySizeBytes);
        }

        [TestMethod]
        public void CosmosClientOptions_DiagnosticsVerbosity_CanBeSetToSummary()
        {
            CosmosClientOptions options = new CosmosClientOptions
            {
                DiagnosticsVerbosity = DiagnosticsVerbosity.Summary
            };

            Assert.AreEqual(DiagnosticsVerbosity.Summary, options.DiagnosticsVerbosity);
        }

        #endregion
    }
}
