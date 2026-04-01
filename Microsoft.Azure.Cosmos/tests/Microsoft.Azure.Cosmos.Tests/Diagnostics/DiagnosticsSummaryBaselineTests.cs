//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using static Microsoft.Azure.Cosmos.Tracing.TraceData.ClientSideRequestStatisticsTraceDatum;

    /// <summary>
    /// Baseline tests that validate the exact JSON schema produced by DiagnosticsSummaryWriter.
    /// These tests ensure the summary output structure does not change accidentally across releases.
    /// </summary>
    [TestClass]
    public class DiagnosticsSummaryBaselineTests
    {
        /// <summary>
        /// Validates the exact set of top-level fields in a single-request summary.
        /// This catches any accidental field additions/removals/renames.
        /// </summary>
        [TestMethod]
        public void Baseline_SingleRequest_SchemaFields()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            DateTime fixedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.Ok, SubStatusCodes.Unknown, 5.0, 10, fixedTime);

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            JObject summaryObj = (JObject)parsed["Summary"];

            // Top-level summary fields (exact set)
            HashSet<string> expectedTopLevelFields = new HashSet<string>
            {
                "DiagnosticsVerbosity",
                "TotalDurationMs",
                "TotalRequestCharge",
                "TotalRequestCount",
                "RegionsSummary"
            };

            HashSet<string> actualFields = new HashSet<string>(summaryObj.Properties().Select(p => p.Name));
            Assert.IsTrue(expectedTopLevelFields.SetEquals(actualFields),
                $"Summary top-level fields mismatch. Expected: [{string.Join(", ", expectedTopLevelFields.OrderBy(x => x))}], " +
                $"Actual: [{string.Join(", ", actualFields.OrderBy(x => x))}]");
        }

        /// <summary>
        /// Validates the exact set of fields in a region summary entry.
        /// </summary>
        [TestMethod]
        public void Baseline_RegionSummary_SchemaFields()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            DateTime fixedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.Ok, SubStatusCodes.Unknown, 5.0, 10, fixedTime);

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            JObject region = (JObject)parsed["Summary"]["RegionsSummary"][0];

            // Single-request region has: Region, RequestCount, TotalRequestCharge, First
            HashSet<string> expectedFields = new HashSet<string>
            {
                "Region",
                "RequestCount",
                "TotalRequestCharge",
                "First"
            };

            HashSet<string> actualFields = new HashSet<string>(region.Properties().Select(p => p.Name));
            Assert.IsTrue(expectedFields.SetEquals(actualFields),
                $"Region fields mismatch. Expected: [{string.Join(", ", expectedFields.OrderBy(x => x))}], " +
                $"Actual: [{string.Join(", ", actualFields.OrderBy(x => x))}]");
        }

        /// <summary>
        /// Validates the exact set of fields in a request entry detail.
        /// </summary>
        [TestMethod]
        public void Baseline_RequestEntryDetail_SchemaFields()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            DateTime fixedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            AddStoreResponseStatistic(trace, "West US 2", StatusCodes.Ok, SubStatusCodes.Unknown, 5.0, 10, fixedTime);

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            JObject first = (JObject)parsed["Summary"]["RegionsSummary"][0]["First"];

            HashSet<string> expectedFields = new HashSet<string>
            {
                "StatusCode",
                "SubStatusCode",
                "RequestCharge",
                "DurationMs",
                "Region",
                "Endpoint",
                "RequestStartTimeUtc",
                "OperationType",
                "ResourceType"
            };

            HashSet<string> actualFields = new HashSet<string>(first.Properties().Select(p => p.Name));
            Assert.IsTrue(expectedFields.SetEquals(actualFields),
                $"Request entry fields mismatch. Expected: [{string.Join(", ", expectedFields.OrderBy(x => x))}], " +
                $"Actual: [{string.Join(", ", actualFields.OrderBy(x => x))}]");
        }

        /// <summary>
        /// Validates the exact set of fields in an aggregated group.
        /// </summary>
        [TestMethod]
        public void Baseline_AggregatedGroup_SchemaFields()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            DateTime fixedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            AddStoreResponseStatistic(trace, "R1", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0, 1, fixedTime);
            AddStoreResponseStatistic(trace, "R1", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0, 10, fixedTime.AddMilliseconds(100));
            AddStoreResponseStatistic(trace, "R1", StatusCodes.Ok, SubStatusCodes.Unknown, 5, 5, fixedTime.AddMilliseconds(200));

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            JObject group = (JObject)parsed["Summary"]["RegionsSummary"][0]["AggregatedGroups"][0];

            HashSet<string> expectedFields = new HashSet<string>
            {
                "StatusCode",
                "SubStatusCode",
                "Count",
                "TotalRequestCharge",
                "MinDurationMs",
                "MaxDurationMs",
                "P50DurationMs",
                "AvgDurationMs"
            };

            HashSet<string> actualFields = new HashSet<string>(group.Properties().Select(p => p.Name));
            Assert.IsTrue(expectedFields.SetEquals(actualFields),
                $"Aggregated group fields mismatch. Expected: [{string.Join(", ", expectedFields.OrderBy(x => x))}], " +
                $"Actual: [{string.Join(", ", actualFields.OrderBy(x => x))}]");
        }

        /// <summary>
        /// Validates the exact set of fields in a truncated summary.
        /// </summary>
        [TestMethod]
        public void Baseline_TruncatedSummary_SchemaFields()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            DateTime fixedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            for (int r = 0; r < 20; r++)
            {
                for (int i = 0; i < 10; i++)
                {
                    AddStoreResponseStatistic(trace, $"Region {r}", StatusCodes.TooManyRequests,
                        SubStatusCodes.Unknown, 0.0, 5 + i, fixedTime.AddMilliseconds(r * 1000 + i * 100));
                }
            }

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 512);
            JObject parsed = JObject.Parse(summary);
            JObject summaryObj = (JObject)parsed["Summary"];

            HashSet<string> expectedFields = new HashSet<string>
            {
                "DiagnosticsVerbosity",
                "TotalDurationMs",
                "TotalRequestCount",
                "TotalRequestCharge",
                "Truncated",
                "Message"
            };

            HashSet<string> actualFields = new HashSet<string>(summaryObj.Properties().Select(p => p.Name));
            Assert.IsTrue(expectedFields.SetEquals(actualFields),
                $"Truncated summary fields mismatch. Expected: [{string.Join(", ", expectedFields.OrderBy(x => x))}], " +
                $"Actual: [{string.Join(", ", actualFields.OrderBy(x => x))}]");
        }

        /// <summary>
        /// Validates the full region summary schema when both First, Last, and AggregatedGroups are present.
        /// </summary>
        [TestMethod]
        public void Baseline_FullRegionSummary_SchemaFields()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            DateTime fixedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            AddStoreResponseStatistic(trace, "R1", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0, 5, fixedTime);
            AddStoreResponseStatistic(trace, "R1", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0, 10, fixedTime.AddMilliseconds(100));
            AddStoreResponseStatistic(trace, "R1", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0, 15, fixedTime.AddMilliseconds(200));
            AddStoreResponseStatistic(trace, "R1", StatusCodes.Ok, SubStatusCodes.Unknown, 5, 12, fixedTime.AddMilliseconds(300));

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            JObject region = (JObject)parsed["Summary"]["RegionsSummary"][0];

            HashSet<string> expectedFields = new HashSet<string>
            {
                "Region",
                "RequestCount",
                "TotalRequestCharge",
                "First",
                "Last",
                "AggregatedGroups"
            };

            HashSet<string> actualFields = new HashSet<string>(region.Properties().Select(p => p.Name));
            Assert.IsTrue(expectedFields.SetEquals(actualFields),
                $"Full region fields mismatch. Expected: [{string.Join(", ", expectedFields.OrderBy(x => x))}], " +
                $"Actual: [{string.Join(", ", actualFields.OrderBy(x => x))}]");
        }

        /// <summary>
        /// Validates field types are correct (numbers are numbers, strings are strings, etc.).
        /// </summary>
        [TestMethod]
        public void Baseline_FieldTypes_Consistent()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            DateTime fixedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            AddStoreResponseStatistic(trace, "R1", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0, 5, fixedTime);
            AddStoreResponseStatistic(trace, "R1", StatusCodes.TooManyRequests, SubStatusCodes.Unknown, 0, 10, fixedTime.AddMilliseconds(100));
            AddStoreResponseStatistic(trace, "R1", StatusCodes.Ok, SubStatusCodes.Unknown, 5, 12, fixedTime.AddMilliseconds(200));

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            JObject summaryObj = (JObject)parsed["Summary"];

            // Top-level types
            Assert.AreEqual(JTokenType.String, summaryObj["DiagnosticsVerbosity"].Type);
            Assert.IsTrue(summaryObj["TotalDurationMs"].Type == JTokenType.Float
                || summaryObj["TotalDurationMs"].Type == JTokenType.Integer,
                "TotalDurationMs should be numeric");
            Assert.IsTrue(summaryObj["TotalRequestCharge"].Type == JTokenType.Float
                || summaryObj["TotalRequestCharge"].Type == JTokenType.Integer,
                "TotalRequestCharge should be numeric");
            Assert.AreEqual(JTokenType.Integer, summaryObj["TotalRequestCount"].Type);
            Assert.AreEqual(JTokenType.Array, summaryObj["RegionsSummary"].Type);

            // Region types
            JObject region = (JObject)summaryObj["RegionsSummary"][0];
            Assert.AreEqual(JTokenType.String, region["Region"].Type);
            Assert.AreEqual(JTokenType.Integer, region["RequestCount"].Type);
            Assert.IsTrue(region["TotalRequestCharge"].Type == JTokenType.Float
                || region["TotalRequestCharge"].Type == JTokenType.Integer,
                "TotalRequestCharge should be numeric");
            Assert.AreEqual(JTokenType.Object, region["First"].Type);
            Assert.AreEqual(JTokenType.Object, region["Last"].Type);

            // Request entry types
            JObject first = (JObject)region["First"];
            Assert.AreEqual(JTokenType.Integer, first["StatusCode"].Type);
            Assert.AreEqual(JTokenType.Integer, first["SubStatusCode"].Type);
            Assert.IsTrue(first["RequestCharge"].Type == JTokenType.Float
                || first["RequestCharge"].Type == JTokenType.Integer,
                "RequestCharge should be numeric");
            Assert.IsTrue(first["DurationMs"].Type == JTokenType.Float
                || first["DurationMs"].Type == JTokenType.Integer,
                "DurationMs should be numeric");
            Assert.AreEqual(JTokenType.String, first["Region"].Type);
        }

        /// <summary>
        /// Validates that the DiagnosticsVerbosity field always has the value "Summary".
        /// </summary>
        [TestMethod]
        public void Baseline_DiagnosticsVerbosityField_AlwaysSummary()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 8192);
            JObject parsed = JObject.Parse(summary);
            Assert.AreEqual("Summary", parsed["Summary"]["DiagnosticsVerbosity"].Value<string>());
        }

        /// <summary>
        /// Validates that the truncated summary has the correct Truncated and Message fields.
        /// </summary>
        [TestMethod]
        public void Baseline_TruncatedMessage_Content()
        {
            using ITrace trace = Trace.GetRootTrace("ReadItemAsync");
            DateTime fixedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            for (int r = 0; r < 20; r++)
            {
                for (int i = 0; i < 10; i++)
                {
                    AddStoreResponseStatistic(trace, $"Region {r}", StatusCodes.TooManyRequests,
                        SubStatusCodes.Unknown, 0.0, 5, fixedTime.AddMilliseconds(r * 1000 + i * 100));
                }
            }

            string summary = DiagnosticsSummaryWriter.WriteSummary(trace, 512);
            JObject parsed = JObject.Parse(summary);
            JObject summaryObj = (JObject)parsed["Summary"];

            Assert.IsTrue(summaryObj["Truncated"].Value<bool>());
            Assert.AreEqual(JTokenType.Boolean, summaryObj["Truncated"].Type);
            string message = summaryObj["Message"].Value<string>();
            Assert.IsTrue(message.Contains("truncated", StringComparison.OrdinalIgnoreCase),
                $"Truncation message should mention 'truncated'. Actual: {message}");
            Assert.IsTrue(message.Contains("Detailed", StringComparison.OrdinalIgnoreCase),
                $"Truncation message should mention 'Detailed' mode. Actual: {message}");
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
            ClientSideRequestStatisticsTraceDatum datum = GetOrCreateDatum(trace);

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
    }
}
