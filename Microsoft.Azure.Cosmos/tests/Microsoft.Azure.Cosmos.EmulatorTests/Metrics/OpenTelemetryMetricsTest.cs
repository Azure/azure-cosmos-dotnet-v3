//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.Metrics
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using OpenTelemetry.Metrics;
    using OpenTelemetry;
    using OpenTelemetry.Resources;
    using System.Threading;
    using System.Diagnostics;
    using System.Collections.Generic;
    using Microsoft.Extensions.Options;

    [TestClass]
    public class OpenTelemetryMetricsTest : BaseCosmosClientHelper
    {
        private const int AggregatingInterval = 1000;
        private readonly ManualResetEventSlim manualResetEventSlim = new ManualResetEventSlim(false);
        private static readonly Dictionary<string, MetricType> expectedMetrics = new Dictionary<string, MetricType>()
        {
            { "db.client.operation.duration", MetricType.Histogram },
            { "db.client.response.row_count", MetricType.Histogram},
            { "db.cosmosdb.operation.request_charge", MetricType.Histogram }
        };

        [TestInitialize]
        public async Task Init()
        {
            await base.TestInit();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task OperationLevelMetricsGenerationTest()
        {
            // Initialize OpenTelemetry MeterProvider
            MeterProvider meterProvider = Sdk
                .CreateMeterProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Azure Cosmos DB Operation Level Metrics"))
                .AddMeter(CosmosDbClientMetricsConstant.OperationMetrics.MeterName)
                .AddView(
                    instrumentName: CosmosDbClientMetricsConstant.OperationMetrics.Name.RequestCharge,
                    metricStreamConfiguration: new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = CosmosDbClientMetricsConstant.HistogramBuckets.RequestUnitBuckets
                    })
                .AddView(
                    instrumentName: CosmosDbClientMetricsConstant.OperationMetrics.Name.Latency,
                    metricStreamConfiguration: new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = CosmosDbClientMetricsConstant.HistogramBuckets.RequestLatencyBuckets
                    })
                .AddView(
                    instrumentName: CosmosDbClientMetricsConstant.OperationMetrics.Name.RowCount,
                    metricStreamConfiguration: new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = CosmosDbClientMetricsConstant.HistogramBuckets.RowCountBuckets
                    })
                .AddConsoleExporter()
                //.AddReader(new PeriodicExportingMetricReader(exporter: new CustomMetricExporter(this.manualResetEventSlim), exportIntervalMilliseconds: AggregatingInterval))
                .Build();

            // Intialize CosmosClient with Client Metrics enabled
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions
            {
                CosmosClientTelemetryOptions = new CosmosClientTelemetryOptions()
                {
                    IsClientMetricsEnabled = true
                }
            };
            this.SetClient(new CosmosClient(endpoint, authKey, cosmosClientOptions));

            Container container = await this.database.CreateContainerIfNotExistsAsync(Guid.NewGuid().ToString(), "/pk", throughput: 10000);
            for (int count = 0; count < 10; count++)
            {
                string randomId = Guid.NewGuid().ToString();
                string pk = "Status1";
                await container.CreateItemAsync<ToDoActivity>(ToDoActivity.CreateRandomToDoActivity(id: randomId, pk: pk));
                await container.ReadItemAsync<ToDoActivity>(randomId, new PartitionKey(pk));

                QueryRequestOptions queryRequestOptions = new QueryRequestOptions()
                {
                    MaxItemCount = Random.Shared.Next(1, 100)
                };
                FeedIterator<ToDoActivity> feedIterator = container.GetItemQueryIterator<ToDoActivity>(queryText: "SELECT * FROM c", requestOptions: queryRequestOptions);
                while (feedIterator.HasMoreResults)
                {
                    await feedIterator.ReadNextAsync();
                }
            }

            meterProvider.Dispose();

            await Task.Delay(1000);

            Console.WriteLine("Disposed............");
            //while (!this.manualResetEventSlim.IsSet)
            //{
            //    Console.WriteLine(this.manualResetEventSlim.IsSet);
            //}

            //Console.WriteLine("Asserting............");
            //Assert.AreEqual(CustomMetricExporter.ActualMetrics.Count, expectedMetrics.Count);
        }
    }
}
