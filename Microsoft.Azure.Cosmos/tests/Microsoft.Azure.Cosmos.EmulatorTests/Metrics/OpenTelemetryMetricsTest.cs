//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.Metrics
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Moq.Protected;
    using OpenTelemetry.Metrics;
    using OpenTelemetry;
    using System.Diagnostics;

    [TestClass]
    public class OpenTelemetryMetricsTest : BaseCosmosClientHelper
    {
        private ContainerInternal Container = null;
        private const string PartitionKey = "/pk";

        [TestInitialize]
        public async Task TestInitialize()
        {
            await this.TestInit();

            ContainerResponse response = await this.database.CreateContainerAsync(
                        new ContainerProperties(id: "ClientCreateAndInitializeContainer", partitionKeyPath: PartitionKey),
                        throughput: 20000,
                        cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = (ContainerInlineCore)response;

            // Create items with different
            for (int i = 0; i < 500; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                item.pk = "Status" + i.ToString();
                item.id = i.ToString();
                ItemResponse<ToDoActivity> itemResponse = await this.Container.CreateItemAsync(item);
                Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            }
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task OperationLevelMetrics()
        {
            //var histogramBuckets = new double[] { 0, 5, 10, 25, 50, 75, 100, 250, 500 };
            MeterProvider meterProvider = Sdk
             .CreateMeterProviderBuilder()
             .AddMeter("*")/*
             .AddView("cosmos.client.op.RUs", new ExplicitBucketHistogramConfiguration { Boundaries = histogramBuckets })*/
             .AddConsoleExporter()
             .Build();

            int httpCallsMade = 0;
            HttpClientHandlerHelper httpClientHandlerHelper = new HttpClientHandlerHelper
            {
                RequestCallBack = (request, cancellationToken) =>
                {
                    httpCallsMade++;
                    return null;
                }
            };

            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            List<(string, string)> containers = new List<(string, string)> 
            { (this.database.Id, "ClientCreateAndInitializeContainer")};

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions
            {
                HttpClientFactory = () => new HttpClient(httpClientHandlerHelper),
                CosmosClientTelemetryOptions = new CosmosClientTelemetryOptions()
                {
                    IsClientMetricsEnabled = true
                }
            };

            CosmosClient cosmosClient = await CosmosClient.CreateAndInitializeAsync(endpoint, authKey, containers, cosmosClientOptions);
           // Assert.IsNotNull(cosmosClient);
            int httpCallsMadeAfterCreation = httpCallsMade;

            ContainerInternal container = (ContainerInternal)cosmosClient.GetContainer(this.database.Id, "ClientCreateAndInitializeContainer");

            await Task.Delay(1000);

            Stopwatch sw = Stopwatch.StartNew();
            sw.Start();
            while(true)
            {
                ItemResponse<ToDoActivity> readResponse = await container.ReadItemAsync<ToDoActivity>("1", new Cosmos.PartitionKey("Status1"));
                string diagnostics = readResponse.Diagnostics.ToString();
                if(sw.ElapsedMilliseconds > 2000)
                {
                    break;
                }
            }
            sw.Stop();
            
            await Task.Delay(1000);

            cosmosClient.Dispose();

            meterProvider.Dispose();

            await Task.Delay(1000);
        }
    }
}
