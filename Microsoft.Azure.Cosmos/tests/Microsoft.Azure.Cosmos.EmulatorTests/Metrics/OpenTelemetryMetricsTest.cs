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
    using System.Diagnostics;
    using OpenTelemetry.Resources;
    using global::Azure.Monitor.OpenTelemetry.Exporter;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;

    [TestClass]
    public class OpenTelemetryMetricsTest : BaseCosmosClientHelper
    {
        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task OperationLevelMetrics()
        {
            MeterProvider meterProvider = Sdk
                .CreateMeterProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyService"))
                .AddMeter("Azure.Cosmos.Client.Operation")
                .AddView(instrumentName: OpenTelemetryMetricsConstant.OperationMetrics.RUName, new ExplicitBucketHistogramConfiguration // Define histogram buckets
                {
                    Boundaries = OpenTelemetryMetricsConstant.HistogramBuckets.RequestUnitBuckets
                })
                .AddView(instrumentName: OpenTelemetryMetricsConstant.OperationMetrics.LatencyName, new ExplicitBucketHistogramConfiguration // Define histogram buckets
                {
                    Boundaries = OpenTelemetryMetricsConstant.HistogramBuckets.RequestLatencyBuckets
                })
                .AddAzureMonitorMetricExporter( o => o.ConnectionString = "")
                .AddReader(new PeriodicExportingMetricReader(new CustomMetricExporter(), exportIntervalMilliseconds: 1000))
                .Build();

            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions
            {
                CosmosClientTelemetryOptions = new CosmosClientTelemetryOptions()
                {
                    IsClientMetricsEnabled = true
                }
            };

            this.SetClient(new CosmosClient(endpoint, authKey, cosmosClientOptions));

            Database database = await this.GetClient().CreateDatabaseIfNotExistsAsync(Guid.NewGuid().ToString());
            Container container = await database.CreateContainerIfNotExistsAsync(Guid.NewGuid().ToString(), "/pk", throughput: 10000);

            Stopwatch sw = Stopwatch.StartNew();
            sw.Start();
            while(true)
            {
                string randomId = Guid.NewGuid().ToString();
                string pk = "Status1";

                await container.CreateItemAsync<ToDoActivity>(ToDoActivity.CreateRandomToDoActivity(id: randomId, pk: pk));
                await container.ReadItemAsync<ToDoActivity>(randomId, new PartitionKey(pk));

                FeedIterator<ToDoActivity> feedIterator = container.GetItemQueryIterator<ToDoActivity>("SELECT * FROM c");
                while (feedIterator.HasMoreResults)
                {
                    await feedIterator.ReadNextAsync();
                }

                if (sw.ElapsedMilliseconds > TimeSpan.FromMinutes(1.5).TotalMilliseconds)
                {
                    break;
                }
            }
            sw.Stop();

            meterProvider.Dispose();

            await Task.Delay(TimeSpan.FromSeconds(7));
        }
    }
}
