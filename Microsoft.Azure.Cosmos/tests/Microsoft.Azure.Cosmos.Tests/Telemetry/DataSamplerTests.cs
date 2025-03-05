//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DataSamplerTests
    {
        [TestMethod]
        public void TestNetworkRequestSamplerForThreshold()
        {
            int numberOfElementsInEachGroup = ClientTelemetryOptions.NetworkRequestsSampleSizeThreshold;
            int numberOfGroups = 5;

            List<RequestInfo> requestInfoList = new List<RequestInfo>();

            for (int counter = 0; counter < 100; counter++)
            {
                RequestInfo requestInfo = new RequestInfo()
                {
                    DatabaseName = "dbId " + (counter % numberOfGroups), // To repeat similar elements
                    ContainerName = "containerId",
                    Uri = "rntbd://host/partition/replica",
                    StatusCode = 429,
                    SubStatusCode = 1002,
                    Resource = ResourceType.Document.ToResourceTypeString(),
                    Operation = OperationType.Create.ToOperationTypeString(),
                    Metrics = new List<MetricInfo>()
                    {
                        new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit)
                        {
                            Percentiles = new Dictionary<double, double>()
                            {
                                { ClientTelemetryOptions.Percentile50, 10 },
                                { ClientTelemetryOptions.Percentile90, 20 },
                                { ClientTelemetryOptions.Percentile95, 30 },
                                { ClientTelemetryOptions.Percentile99, Random.Shared.Next(1, 100) },
                                { ClientTelemetryOptions.Percentile999, 50 }
                            },
                            Count = Random.Shared.Next(1, 100)
                        }
                    }
                };
                requestInfoList.Add(requestInfo);
            }

            List<RequestInfo> sampleDataByLatency = DataSampler.OrderAndSample(requestInfoList, DataLatencyComparer.Instance);
            Assert.AreEqual(numberOfGroups * numberOfElementsInEachGroup, sampleDataByLatency.Count);

            List<RequestInfo> sampleDataBySampleCount = DataSampler.OrderAndSample(requestInfoList, DataSampleCountComparer.Instance);
            Assert.AreEqual(numberOfGroups * numberOfElementsInEachGroup, sampleDataBySampleCount.Count);
        }

        [TestMethod]
        public void TestNetworkRequestSamplerWithoutData()
        {
            List<RequestInfo> requestInfoList = new List<RequestInfo>();

            Assert.AreEqual(0, DataSampler.OrderAndSample(requestInfoList, DataSampleCountComparer.Instance).Count);
            Assert.AreEqual(0, DataSampler.OrderAndSample(requestInfoList, DataLatencyComparer.Instance).Count);
        }

        [TestMethod]
        public void TestNetworkRequestSamplerForLessThanThresholdSize()
        {
            int numberOfElementsInEachGroup = ClientTelemetryOptions.NetworkRequestsSampleSizeThreshold;
            int numberOfGroups = 3;

            List<RequestInfo> requestInfoList = new List<RequestInfo>();

            for (int counter = 0; counter < 10; counter++)
            {
                RequestInfo requestInfo = new RequestInfo()
                {
                    DatabaseName = "dbId " + (counter % numberOfGroups), // To repeat similar elements
                    ContainerName = "containerId",
                    Uri = "rntbd://host/partition/replica",
                    StatusCode = 429,
                    SubStatusCode = 1002,
                    Resource = ResourceType.Document.ToResourceTypeString(),
                    Operation = OperationType.Create.ToOperationTypeString(),
                    Metrics = new List<MetricInfo>()
                    {
                        new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit)
                        {
                            Percentiles = new Dictionary<double, double>()
                            {
                                { ClientTelemetryOptions.Percentile50, 10 },
                                { ClientTelemetryOptions.Percentile90, 20 },
                                { ClientTelemetryOptions.Percentile95, 30 },
                                { ClientTelemetryOptions.Percentile99, Random.Shared.Next(1, 100) },
                                { ClientTelemetryOptions.Percentile999, 50 }
                            },
                            Count = Random.Shared.Next(1, 100)
                        }
                    }
                };
                requestInfoList.Add(requestInfo);
            }

            List<RequestInfo> sampleDataByLatency = DataSampler.OrderAndSample(requestInfoList, DataLatencyComparer.Instance);
            Assert.AreEqual(10, sampleDataByLatency.Count);

            List<RequestInfo> sampleDataBySampleCount = DataSampler.OrderAndSample(requestInfoList, DataSampleCountComparer.Instance);
            Assert.AreEqual(10, sampleDataBySampleCount.Count);
        }

    }
}