//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Telemetry
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    /// <summary>
    /// Tests for <see cref="ClientTelemetry"/>.
    /// </summary>
    [TestClass]
    public class ClientTelemetryTests
    {
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
        [DataRow(150, 50, 200)] // When operation, cacherefresh and request info is there in payload
        [DataRow(0, 50, 0)] // When only cacherefresh info is there in payload
        [DataRow(150, 50, 0)] // When only operation and cacherefresh info is there in payload
        [DataRow(150, 0, 0)] // When only operation info is there in payload
        public async Task CheckIfPayloadIsDividedCorrectlyAsync(int expectedOperationInfoSize, int expectedCacheRefreshInfoSize, int expectedRequestInfoSize)
        {
            ClientTelemetryOptions.PayloadSizeThreshold = 1024 * 15; //15 Kb

            string data = File.ReadAllText("Telemetry/ClientTelemetryPayloadWithoutMetrics.json", Encoding.UTF8);
            ClientTelemetryProperties clientTelemetryProperties = JsonConvert.DeserializeObject<ClientTelemetryProperties>(data);

            int totalPayloadSizeInBytes = data.Length;

            int actualOperationInfoSize = 0;
            int actualCacheRefreshInfoSize = 0;
            int actualRequestInfoSize = 0;

            Mock<IHttpHandler> mockHttpHandler = new Mock<IHttpHandler>();
            _ = mockHttpHandler.Setup(x => x.SendAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()))
                .Callback<HttpRequestMessage, CancellationToken>(
                (request, cancellationToken) =>
                {
                    string payloadJson = request.Content.ReadAsStringAsync().Result;
                    Assert.IsTrue(payloadJson.Length <= ClientTelemetryOptions.PayloadSizeThreshold, "Payload Size is " + payloadJson.Length);

                    ClientTelemetryProperties propertiesToSend = JsonConvert.DeserializeObject<ClientTelemetryProperties>(payloadJson);

                    Assert.AreEqual(7, propertiesToSend.SystemInfo.Count, "System Info is not correct");

                    actualOperationInfoSize += propertiesToSend.OperationInfo?.Count ?? 0;
                    actualCacheRefreshInfoSize += propertiesToSend.CacheRefreshInfo?.Count ?? 0;
                    actualRequestInfoSize += propertiesToSend.RequestInfo?.Count ?? 0;
                })
                .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

            ClientTelemetryProcessor processor = new ClientTelemetryProcessor(
                MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object))),
                Mock.Of<AuthorizationTokenProvider>());

            ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> operationInfoSnapshot
                = new ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)>();

            int numberOfMetricsInOperationSection = 2;
            for (int i = 0; i < (expectedOperationInfoSize / numberOfMetricsInOperationSection); i++)
            {
                OperationInfo opeInfo = new OperationInfo(Regions.WestUS,
                                                        0,
                                                        Documents.ConsistencyLevel.Session.ToString(),
                                                        "databaseName" + i,
                                                        "containerName",
                                                        Documents.OperationType.Read,
                                                        Documents.ResourceType.Document,
                                                        (int)HttpStatusCode.OK,
                                                        (int)SubStatusCodes.Unknown);

                LongConcurrentHistogram latency = new LongConcurrentHistogram(ClientTelemetryOptions.RequestLatencyMin,
                                                            ClientTelemetryOptions.RequestLatencyMax,
                                                            ClientTelemetryOptions.RequestLatencyPrecision);
                latency.RecordValue(10);

                LongConcurrentHistogram requestcharge = new LongConcurrentHistogram(ClientTelemetryOptions.RequestChargeMin,
                                                            ClientTelemetryOptions.RequestChargeMax,
                                                            ClientTelemetryOptions.RequestChargePrecision);
                requestcharge.RecordValue(11);

                operationInfoSnapshot.TryAdd(opeInfo, (latency, requestcharge));
            }

            ConcurrentDictionary<CacheRefreshInfo, LongConcurrentHistogram> cacheRefreshInfoSnapshot
                = new ConcurrentDictionary<CacheRefreshInfo, LongConcurrentHistogram>();
            for (int i = 0; i < expectedCacheRefreshInfoSize; i++)
            {
                CacheRefreshInfo crInfo = new CacheRefreshInfo(Regions.WestUS,
                                                        10,
                                                        Documents.ConsistencyLevel.Session.ToString(),
                                                        "databaseName" + i,
                                                        "containerName",
                                                        Documents.OperationType.Read,
                                                        Documents.ResourceType.Document,
                                                        (int)HttpStatusCode.OK,
                                                        (int)SubStatusCodes.PartitionKeyRangeGone,
                                                        "dummycache");

                LongConcurrentHistogram latency = new LongConcurrentHistogram(ClientTelemetryOptions.RequestLatencyMin,
                                                            ClientTelemetryOptions.RequestLatencyMax,
                                                            ClientTelemetryOptions.RequestLatencyPrecision);
                latency.RecordValue(10);

                cacheRefreshInfoSnapshot.TryAdd(crInfo, latency);
            }

            List<RequestInfo> requestInfoList = new List<RequestInfo>();
            for (int i = 0; i < expectedRequestInfoSize; i++)
            {
                RequestInfo reqInfo = new RequestInfo
                {
                    Uri = "https://dummyuri.com",
                    DatabaseName = "databaseName" + i,
                    ContainerName = "containerName" + i,
                    Operation = Documents.OperationType.Read.ToString(),
                    Resource = Documents.ResourceType.Document.ToString(),
                    StatusCode = 200,
                    SubStatusCode = 0
                };

                MetricInfo metricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit);

                LongConcurrentHistogram histogram = new LongConcurrentHistogram(ClientTelemetryOptions.RequestLatencyMin,
                                                        ClientTelemetryOptions.RequestLatencyMax,
                                                        ClientTelemetryOptions.RequestLatencyPrecision);
                histogram.RecordValue(TimeSpan.FromMinutes(1).Ticks);

                metricInfo.SetAggregators(histogram, ClientTelemetryOptions.TicksToMsFactor);
                reqInfo.Metrics.Add(metricInfo);

                requestInfoList.Add(reqInfo); ;
            }

            await processor.ProcessAndSendAsync(
                clientTelemetryProperties,
                operationInfoSnapshot,
                cacheRefreshInfoSnapshot,
                requestInfoList,
                "http://dummy.telemetry.endpoint/",
                new CancellationTokenSource(ClientTelemetryOptions.ClientTelemetryProcessorTimeOut).Token);

            Assert.AreEqual(expectedOperationInfoSize, actualOperationInfoSize, "Operation Info is not correct");
            Assert.AreEqual(expectedCacheRefreshInfoSize, actualCacheRefreshInfoSize, "Cache Refresh Info is not correct");
            Assert.AreEqual(expectedRequestInfoSize, actualRequestInfoSize, "Request Info is not correct");
        }

        [TestMethod]
        public async Task ClientTelmetryProcessor_should_timeout()
        {
            try
            {
                string data = File.ReadAllText("Telemetry/ClientTelemetryPayloadWithoutMetrics.json", Encoding.UTF8);
                ClientTelemetryProperties clientTelemetryProperties = JsonConvert.DeserializeObject<ClientTelemetryProperties>(data);

                int actualOperationInfoSize = 0;
                int actualCacheRefreshInfoSize = 0;

                Mock<IHttpHandler> mockHttpHandler = new Mock<IHttpHandler>();
                _ = mockHttpHandler.Setup(x => x.SendAsync(
                    It.IsAny<HttpRequestMessage>(),
                    It.IsAny<CancellationToken>()))
                     .Callback<HttpRequestMessage, CancellationToken>(
                    (request, cancellationToken) =>
                    {
                        string payloadJson = request.Content.ReadAsStringAsync().Result;
                        Assert.IsTrue(payloadJson.Length <= ClientTelemetryOptions.PayloadSizeThreshold, "Payload Size is " + payloadJson.Length);

                        ClientTelemetryProperties propertiesToSend = JsonConvert.DeserializeObject<ClientTelemetryProperties>(payloadJson);

                        actualOperationInfoSize += propertiesToSend.OperationInfo?.Count ?? 0;
                        actualCacheRefreshInfoSize += propertiesToSend.CacheRefreshInfo?.Count ?? 0;
                    })
                    .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

                ClientTelemetryProcessor processor = new ClientTelemetryProcessor(
                    MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object))),
                    Mock.Of<AuthorizationTokenProvider>());

                ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> operationInfoSnapshot
                    = new ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)>();

                for (int i = 0; i < 20; i++)
                {
                    OperationInfo opeInfo = new OperationInfo(Regions.WestUS,
                                                            0,
                                                            Documents.ConsistencyLevel.Session.ToString(),
                                                            "databaseName" + i,
                                                            "containerName",
                                                            Documents.OperationType.Read,
                                                            Documents.ResourceType.Document,
                                                            (int)HttpStatusCode.OK,
                                                            (int)SubStatusCodes.Unknown);

                    LongConcurrentHistogram latency = new LongConcurrentHistogram(ClientTelemetryOptions.RequestLatencyMin,
                                                                ClientTelemetryOptions.RequestLatencyMax,
                                                                ClientTelemetryOptions.RequestLatencyPrecision);
                    latency.RecordValue(10);

                    LongConcurrentHistogram requestcharge = new LongConcurrentHistogram(ClientTelemetryOptions.RequestChargeMin,
                                                                ClientTelemetryOptions.RequestChargeMax,
                                                                ClientTelemetryOptions.RequestChargePrecision);
                    requestcharge.RecordValue(11);

                    operationInfoSnapshot.TryAdd(opeInfo, (latency, requestcharge));
                }

                ConcurrentDictionary<CacheRefreshInfo, LongConcurrentHistogram> cacheRefreshInfoSnapshot
                    = new ConcurrentDictionary<CacheRefreshInfo, LongConcurrentHistogram>();
                for (int i = 0; i < 10; i++)
                {
                    CacheRefreshInfo crInfo = new CacheRefreshInfo(Regions.WestUS,
                                                            10,
                                                            Documents.ConsistencyLevel.Session.ToString(),
                                                            "databaseName" + i,
                                                            "containerName",
                                                            Documents.OperationType.Read,
                                                            Documents.ResourceType.Document,
                                                            (int)HttpStatusCode.OK,
                                                            (int)SubStatusCodes.PartitionKeyRangeGone,
                                                            "dummycache");

                    LongConcurrentHistogram latency = new LongConcurrentHistogram(ClientTelemetryOptions.RequestLatencyMin,
                                                                ClientTelemetryOptions.RequestLatencyMax,
                                                                ClientTelemetryOptions.RequestLatencyPrecision);
                    latency.RecordValue(10);

                    cacheRefreshInfoSnapshot.TryAdd(crInfo, latency);
                }

                Task processorTask = Task.Run(async () =>
                {
                    CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));
                    await Task.Delay(1000, cts.Token); // Making this task wait to ensure that processir is taking more time.
                    await processor.ProcessAndSendAsync(clientTelemetryProperties,
                                                        operationInfoSnapshot,
                                                        cacheRefreshInfoSnapshot,
                                                        default,
                                                        "http://dummy.telemetry.endpoint/",
                                                        cts.Token);
                });

                await ClientTelemetry.RunProcessorTaskAsync(
                    telemetryDate: DateTime.Now.ToString(),
                    processingTask: processorTask,
                    timeout: TimeSpan.FromTicks(1));
            }
            catch (Exception ex)
            {
                Assert.Fail("Exception should not be thrown. Exception: " + ex);
            }

        }
    }
}