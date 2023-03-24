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
        [DataRow(5)]
        public void TestNetworkRequestSamplerForThreshold(int threshold)
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

          /*  Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();*/
            List<RequestInfo> sampleDataByLatency = DataSampler.OrderAndSample(requestInfoList, DataSamplerOrderBy.Latency);
            /*stopWatch.Stop();

            Console.WriteLine("non linq execution time " + stopWatch.ElapsedTicks + " " + Process.GetCurrentProcess().PrivateMemorySize64);
                */
            Assert.AreEqual(numberOfGroups * numberOfElementsInEachGroup, sampleDataByLatency.Count);

          /*  stopWatch = Stopwatch.StartNew();
            stopWatch.Start();
            List<RequestInfo> sortedList = requestInfoList.GroupBy(r => new
            {
                r.DatabaseName,
                r.ContainerName,
                r.Operation,
                r.Resource,
                r.StatusCode,
                r.SubStatusCode
            })
             .SelectMany(g => g.OrderByDescending(r => r.Metrics.FirstOrDefault(m => m.MetricsName == ClientTelemetryOptions.RequestLatencyName)?.Percentiles[ClientTelemetryOptions.Percentile99])
                                .Take(ClientTelemetryOptions.NetworkRequestsSampleSizeThrehold)).ToList();
            stopWatch.Stop();
            Console.WriteLine("linq execution time " + stopWatch.ElapsedTicks + " " + Process.GetCurrentProcess().PrivateMemorySize64);
            Console.WriteLine("Count is " + sampleDataByLatency.Count);

            int c = 0;
            foreach (RequestInfo reqInfo in sampleDataByLatency)
            {
                Console.WriteLine(reqInfo?.ToString() ?? "null");
                c++;
                if (c == ClientTelemetryOptions.NetworkRequestsSampleSizeThrehold)
                {
                    Console.WriteLine();
                    c = 0;
                }
            }
            
            int c1 = 0;
            foreach (RequestInfo reqInfo in sortedList)
            {
                Console.WriteLine(reqInfo?.ToString() ?? "null");
                c1++;
                if (c1 == ClientTelemetryOptions.NetworkRequestsSampleSizeThrehold)
                {
                    Console.WriteLine();
                    c1 = 0;
                }
            }*/
            
            List<RequestInfo> sampleDataBySampleCount = DataSampler.OrderAndSample(requestInfoList, DataSamplerOrderBy.SampleCount);
            Assert.AreEqual(numberOfGroups * numberOfElementsInEachGroup, sampleDataBySampleCount.Count);
        }

        [TestMethod]
        public void TestNetworkRequestSamplerWithoutData()
        {
            List<RequestInfo> requestInfoList = new List<RequestInfo>();

            Assert.AreEqual(0, DataSampler.OrderAndSample(requestInfoList, DataSamplerOrderBy.SampleCount).Count);
            Assert.AreEqual(0, DataSampler.OrderAndSample(requestInfoList, DataSamplerOrderBy.Latency).Count);
        }
    }
}
