//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DataSamplerTests
    {
        [TestMethod]
        [DataRow(5)]
        public void TestNetworkRequestSamplerForThreshold(int threshold)
        {
            int numberOfElementsInEachGroup = ClientTelemetryOptions.NetworkRequestsSampleSizeThrehold;
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

            List<RequestInfo> actualP99Sample = DataSampler.SampleOrderByP99(requestInfoList);
            Assert.AreEqual(numberOfGroups * numberOfElementsInEachGroup, actualP99Sample.Count);

            List<RequestInfo> actualCountSample = DataSampler.SampleOrderByCount(requestInfoList);
            Assert.AreEqual(numberOfGroups * numberOfElementsInEachGroup, actualCountSample.Count);
        }

        [TestMethod]
        public void TestNetworkRequestSamplerWithoutData()
        {
            List<RequestInfo> requestInfoList = new List<RequestInfo>();

            Assert.AreEqual(0, DataSampler.SampleOrderByP99(requestInfoList).Count);
            Assert.AreEqual(0, DataSampler.SampleOrderByCount(requestInfoList).Count);
        }
    }
}
