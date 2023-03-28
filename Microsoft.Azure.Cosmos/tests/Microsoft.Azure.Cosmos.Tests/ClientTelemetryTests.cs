﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using HdrHistogram;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.Telemetry;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Telemetry.Models;

    /// <summary>
    /// Tests for <see cref="ClientTelemetry"/>.
    /// </summary>
    [TestClass]
    public class ClientTelemetryTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            Environment.SetEnvironmentVariable(ClientTelemetryOptions.EnvPropsClientTelemetryEnabled, null);
        }

        [TestMethod]
        public void CheckMetricsAggregationLogic()
        {
            MetricInfo metrics = new MetricInfo("metricsName", "unitName");

            LongConcurrentHistogram histogram = new LongConcurrentHistogram(1,
                   long.MaxValue,
                   5);

            histogram.RecordValue(10);
            histogram.RecordValue(20);
            histogram.RecordValue(30);
            histogram.RecordValue(40);

            metrics.SetAggregators(histogram);

            Assert.AreEqual(40, metrics.Max);
            Assert.AreEqual(10, metrics.Min);
            Assert.AreEqual(4, metrics.Count);
            Assert.AreEqual(25, metrics.Mean);

            Assert.AreEqual(20, metrics.Percentiles[ClientTelemetryOptions.Percentile50]);
            Assert.AreEqual(40, metrics.Percentiles[ClientTelemetryOptions.Percentile90]);
            Assert.AreEqual(40, metrics.Percentiles[ClientTelemetryOptions.Percentile95]);
            Assert.AreEqual(40, metrics.Percentiles[ClientTelemetryOptions.Percentile99]);
            Assert.AreEqual(40, metrics.Percentiles[ClientTelemetryOptions.Percentile999]);
        }

        [TestMethod]
        public void CheckMetricsAggregationLogicWithAdjustment()
        {
            MetricInfo metrics = new MetricInfo("metricsName", "unitName");
            long adjustmentFactor = 1000;

            LongConcurrentHistogram histogram = new LongConcurrentHistogram(1,
                          long.MaxValue,
                          5);

            histogram.RecordValue(10 * adjustmentFactor);
            histogram.RecordValue(20 * adjustmentFactor);
            histogram.RecordValue(30 * adjustmentFactor);
            histogram.RecordValue(40 * adjustmentFactor);

            metrics.SetAggregators(histogram, adjustmentFactor);

            Assert.AreEqual(40, metrics.Max);
            Assert.AreEqual(10, metrics.Min);
            Assert.AreEqual(4, metrics.Count);

            Assert.AreEqual(25, metrics.Mean);

            Assert.AreEqual(20, metrics.Percentiles[ClientTelemetryOptions.Percentile50]);
            Assert.AreEqual(40, metrics.Percentiles[ClientTelemetryOptions.Percentile90]);
            Assert.AreEqual(40, metrics.Percentiles[ClientTelemetryOptions.Percentile95]);
            Assert.AreEqual(40, metrics.Percentiles[ClientTelemetryOptions.Percentile99]);
            Assert.AreEqual(40, metrics.Percentiles[ClientTelemetryOptions.Percentile999]);
        }

        [TestMethod]
        public void CheckJsonSerializerContract()
        {
            string json = JsonConvert.SerializeObject(new ClientTelemetryProperties(clientId: "clientId",
                processId: "",
                userAgent: null,
                connectionMode: ConnectionMode.Direct,
                preferredRegions: null,
                aggregationIntervalInSec: 10), ClientTelemetryOptions.JsonSerializerSettings);
            Assert.AreEqual("{\"clientId\":\"clientId\",\"processId\":\"\",\"connectionMode\":\"DIRECT\",\"aggregationIntervalInSec\":10,\"systemInfo\":[]}", json);
        }

        [TestMethod]
        public void CheckJsonSerializerContractWithPreferredRegions()
        {
            List<string> preferredRegion = new List<string>
            {
                "region1"
            };
            string json = JsonConvert.SerializeObject(new ClientTelemetryProperties(clientId: "clientId",
                processId: "",
                userAgent: null,
                connectionMode: ConnectionMode.Direct,
                preferredRegions: preferredRegion,
                aggregationIntervalInSec: 1), ClientTelemetryOptions.JsonSerializerSettings);
            Assert.AreEqual("{\"clientId\":\"clientId\",\"processId\":\"\",\"connectionMode\":\"DIRECT\",\"preferredRegions\":[\"region1\"],\"aggregationIntervalInSec\":1,\"systemInfo\":[]}", json);
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void CheckMisconfiguredTelemetry_should_fail()
        {
            Environment.SetEnvironmentVariable(ClientTelemetryOptions.EnvPropsClientTelemetryEnabled, "non-boolean");
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
        }

        [TestMethod]
        [DataRow(200, 0 ,1, false)]
        [DataRow(404, 0, 1, false)]
        [DataRow(404, 1002, 1, true)]
        [DataRow(409, 0, 1, false)]
        [DataRow(409, 1002, 1, true)]
        [DataRow(503, 2001, 1, true)]
        [DataRow(200, 0, 6, true)]
        public void CheckEligibleStatistics(int statusCode, int subStatusCode, int latencyInMs, bool expectedFlag)
        {
            Assert.AreEqual(expectedFlag, ClientTelemetryOptions.IsEligible(statusCode, subStatusCode, TimeSpan.FromMilliseconds(latencyInMs)));
        }
    }
}
