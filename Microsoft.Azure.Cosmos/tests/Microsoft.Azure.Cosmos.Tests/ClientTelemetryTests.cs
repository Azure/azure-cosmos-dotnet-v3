//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using HdrHistogram;
    using System.Net.Http;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.Telemetry;
    using System.Collections.Generic;
    using System.Xml.Serialization;

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
                   Int64.MaxValue,
                   5);

            histogram.RecordValue((long)10);
            histogram.RecordValue((long)20);
            histogram.RecordValue((long)30);
            histogram.RecordValue((long)40);

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
                         Int64.MaxValue,
                         5);

            histogram.RecordValue((long)(10 * adjustmentFactor));
            histogram.RecordValue((long)(20 * adjustmentFactor));
            histogram.RecordValue((long)(30 * adjustmentFactor));
            histogram.RecordValue((long)(40 * adjustmentFactor));

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
            string json = JsonConvert.SerializeObject(new ClientTelemetryProperties("clientId", "", null, ConnectionMode.Direct, null, 10), ClientTelemetryOptions.JsonSerializerSettings);
            Assert.AreEqual("{\"clientId\":\"clientId\",\"processId\":\"\",\"connectionMode\":\"DIRECT\",\"aggregationIntervalInSec\":10,\"systemInfo\":[]}", json);
        }

        [TestMethod]
        public void CheckJsonSerializerContractWithPreferredRegions()
        {
            List<string> preferredRegion = new List<string>
            {
                "region1"
            };
            string json = JsonConvert.SerializeObject(new ClientTelemetryProperties("clientId", "", null, ConnectionMode.Direct, preferredRegion, 1), ClientTelemetryOptions.JsonSerializerSettings);
            Assert.AreEqual("{\"clientId\":\"clientId\",\"processId\":\"\",\"connectionMode\":\"DIRECT\",\"preferredRegions\":[\"region1\"],\"aggregationIntervalInSec\":1,\"systemInfo\":[]}", json);
        }

        [TestMethod]
        [ExpectedException(typeof(System.FormatException))]
        public void CheckMisconfiguredTelemetry_should_fail()
        {
            Environment.SetEnvironmentVariable(ClientTelemetryOptions.EnvPropsClientTelemetryEnabled, "non-boolean");
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
        }
    }
}
