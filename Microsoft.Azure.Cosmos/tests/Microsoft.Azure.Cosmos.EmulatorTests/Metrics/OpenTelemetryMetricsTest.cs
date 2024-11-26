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
    using System.Collections.Generic;
    using System.Linq;

    [TestClass]
    public class OpenTelemetryMetricsTest : BaseCosmosClientHelper
    {
        private const int AggregatingInterval = 500;

        private readonly ManualResetEventSlim manualResetEventSlim = new ManualResetEventSlim(false);

        private static readonly Dictionary<string, MetricType> expectedMetrics = CustomMetricExporter.expectedOperationMetrics
                                                                                    .Concat(CustomMetricExporter.expectedNetworkMetrics)
                                                                                    .ToDictionary(kv => kv.Key, kv => kv.Value);

        private MeterProvider meterProvider;
        [TestInitialize]
        public async Task Init()
        {
            // Initialize OpenTelemetry MeterProvider
            this.meterProvider = Sdk
                .CreateMeterProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Azure Cosmos DB Metrics"))
                .AddMeter(CosmosDbClientMetrics.OperationMetrics.MeterName, CosmosDbClientMetrics.NetworkMetrics.MeterName)
                .AddView(
                    instrumentName: CosmosDbClientMetrics.OperationMetrics.Name.RequestCharge,
                    metricStreamConfiguration: new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = CosmosDbClientMetrics.HistogramBuckets.RequestUnitBuckets
                    })
                .AddView(
                    instrumentName: CosmosDbClientMetrics.OperationMetrics.Name.Latency,
                    metricStreamConfiguration: new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = CosmosDbClientMetrics.HistogramBuckets.RequestLatencyBuckets
                    })
                .AddView(
                    instrumentName: CosmosDbClientMetrics.OperationMetrics.Name.RowCount,
                    metricStreamConfiguration: new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = CosmosDbClientMetrics.HistogramBuckets.RowCountBuckets
                    })
                .AddReader(new PeriodicExportingMetricReader(
                    exporter: new CustomMetricExporter(this.manualResetEventSlim), 
                    exportIntervalMilliseconds: AggregatingInterval))
                .Build();

            await base.TestInit((builder) => builder.WithClientTelemetryOptions(new CosmosClientTelemetryOptions()
            {
                IsClientMetricsEnabled = true
            }));
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();

            this.meterProvider.Dispose();
        }

        [TestMethod]
        public async Task MetricsGenerationTest()
        {
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

            while (!this.manualResetEventSlim.IsSet)
            {
            }

            CollectionAssert.AreEquivalent(expectedMetrics, CustomMetricExporter.ActualMetrics, string.Join(", ", CustomMetricExporter.ActualMetrics.Select(kv => $"{kv.Key}: {kv.Value}")));
        }
    }
}
