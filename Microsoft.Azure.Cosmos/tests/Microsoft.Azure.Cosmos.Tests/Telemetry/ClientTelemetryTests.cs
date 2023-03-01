//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Telemetry
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using HdrHistogram;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.Telemetry;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using System.Text;
    using System.IO;
    using Microsoft.Azure.Cosmos.Telemetry.Resolver;
    using System.Net.Http;
    using Moq;
    using System.Threading.Tasks;
    using System.Threading;
    using System.Net;
    using System.Collections.Concurrent;

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
        public void CheckJsonSerializerWithContractResolver()
        {
            string data = File.ReadAllText("Telemetry/ClientTelemetryPayload.json", Encoding.UTF8);

            ClientTelemetryProperties clientTelemetryProperties = JsonConvert.DeserializeObject<ClientTelemetryProperties>(data);
            
            JsonSerializerSettings settings = ClientTelemetryOptions.JsonSerializerSettings;
            foreach (string property in ClientTelemetryOptions.PropertiesContainMetrics)
            {
                settings.ContractResolver = new IncludePropertyContractResolver(property);
                string json = JsonConvert.SerializeObject(clientTelemetryProperties, settings);

                ClientTelemetryProperties newClientTelemetryProperties = JsonConvert.DeserializeObject<ClientTelemetryProperties>(json);

                if (property == "OperationInfo")
                {
                    Assert.IsNull(newClientTelemetryProperties.CacheRefreshInfo);
                    Assert.IsTrue(newClientTelemetryProperties.OperationInfo.Count > 0);
                }
                else if (property == "CacheRefreshInfo")
                {
                    Assert.IsNull(newClientTelemetryProperties.OperationInfo);
                    Assert.IsTrue(newClientTelemetryProperties.CacheRefreshInfo.Count > 0);
                }
                else
                {
                    Assert.Fail("Invalid property name");
                }
            }
        }

        [TestMethod]
        public async Task CheckIfPayloadIsDividedCorrectlyAsync()
        {
            Environment.SetEnvironmentVariable(ClientTelemetryOptions.EnvPropsClientTelemetryEndpoint, "http://dummy.telemetry.endpoint/");
            ClientTelemetryOptions.PayloadSizeThreshold = 1024 * 5; //5 Kb
            ClientTelemetryOptions.PropertiesWithPageSize = new Dictionary<string, int>
            {
                { "OperationInfo", 50 },
                { "CacheRefreshInfo", 10 }
            };

            string data = File.ReadAllText("Telemetry/ClientTelemetryPayload-large.json", Encoding.UTF8);
            ClientTelemetryProperties clientTelemetryProperties = JsonConvert.DeserializeObject<ClientTelemetryProperties>(data);
 
            int operationInfoSize = clientTelemetryProperties.OperationInfo.Count; //120
            int cacheRefreshInfoSize = clientTelemetryProperties.CacheRefreshInfo.Count; //4

            int totalPayloadSizeInBytes = data.Length; //90224

            Console.WriteLine("Acceptable Length ==> " + ClientTelemetryOptions.PayloadSizeThreshold);
            
            Console.WriteLine("Original Payload Length ==> " + totalPayloadSizeInBytes);
            Console.WriteLine("     Non Metrics Length Length ==> 2130");

            Mock<IHttpHandler> mockHttpHandler = new Mock<IHttpHandler>();
            _ = mockHttpHandler.Setup(x => x.SendAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()))
                .Callback<HttpRequestMessage, CancellationToken>(
                (request, cancellationToken) =>
                {
                    string payloadJson = request.Content.ReadAsStringAsync().Result;

                    Console.WriteLine(payloadJson);
                    Console.WriteLine("Size => " + payloadJson.Length);
                    
                    ClientTelemetryProperties propertiesToSend = JsonConvert.DeserializeObject<ClientTelemetryProperties>(payloadJson);

                    Console.WriteLine("SystemInfo Count => " + propertiesToSend.SystemInfo.Count ?? "null");
                    Console.WriteLine("OperationInfo Count => " + propertiesToSend.OperationInfo?.Count ?? "null");
                    Console.WriteLine("CacheRefreshInfo Count => " + propertiesToSend.CacheRefreshInfo?.Count ?? "null");
                    
                    Console.WriteLine();
                })
                .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            
            ClientTelemetryProcessor processor = new ClientTelemetryProcessor(
                MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object))),
                Mock.Of<AuthorizationTokenProvider>());

            ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> operationInfoSnapshot 
                = new ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)>();

            for (int i = 0; i < 101; i++)
            {
                OperationInfo opeInfo = new OperationInfo(Regions.WestUS,
                                                        0,
                                                        Documents.ConsistencyLevel.Session.ToString(),
                                                        "databaseName" + i,
                                                        "containerName",
                                                        Documents.OperationType.Read,
                                                        Documents.ResourceType.Document,
                                                        200,
                                                        0);

                LongConcurrentHistogram latency = new LongConcurrentHistogram(ClientTelemetryOptions.RequestLatencyMin,
                                                            ClientTelemetryOptions.RequestLatencyMax,
                                                            ClientTelemetryOptions.RequestLatencyPrecision);
                latency.RecordValue(10l);

                LongConcurrentHistogram requestcharge = new LongConcurrentHistogram(ClientTelemetryOptions.RequestChargeMin,
                                                            ClientTelemetryOptions.RequestChargeMax,
                                                            ClientTelemetryOptions.RequestChargePrecision);
                requestcharge.RecordValue(11l);

                operationInfoSnapshot.TryAdd(opeInfo, (latency, requestcharge));
            }

            ConcurrentDictionary<CacheRefreshInfo, LongConcurrentHistogram> cacheRefreshInfoSnapshot 
                = new ConcurrentDictionary<CacheRefreshInfo, LongConcurrentHistogram>();
            for (int i = 0; i < 105; i++)
            {
                CacheRefreshInfo opeInfo = new CacheRefreshInfo(Regions.WestUS,
                                                        10,
                                                        Documents.ConsistencyLevel.Session.ToString(),
                                                        "databaseName" + i,
                                                        "containerName",
                                                        Documents.OperationType.Read,
                                                        Documents.ResourceType.Document,
                                                        200,
                                                        1002,
                                                        "dummycache") ;

                LongConcurrentHistogram latency = new LongConcurrentHistogram(ClientTelemetryOptions.RequestLatencyMin,
                                                            ClientTelemetryOptions.RequestLatencyMax,
                                                            ClientTelemetryOptions.RequestLatencyPrecision);
                latency.RecordValue(10l);

                cacheRefreshInfoSnapshot.TryAdd(opeInfo, latency);
            }

            await processor.GenerateOptimalSizeOfPayloadAndSendAsync(
                clientTelemetryProperties,
                operationInfoSnapshot,
                cacheRefreshInfoSnapshot, 
                null,
                new CancellationToken());
        }
        
        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void CheckMisconfiguredTelemetry_should_fail()
        {
            Environment.SetEnvironmentVariable(ClientTelemetryOptions.EnvPropsClientTelemetryEnabled, "non-boolean");
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
        }
    }
}
