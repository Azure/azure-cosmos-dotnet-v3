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
                .AddMeter("Azure.Cosmos.Client")
               /* .AddView(instrumentName: "cosmos.client.op.RUs", new ExplicitBucketHistogramConfiguration // Define histogram buckets
                 {
                     Boundaries = new double[] { 0, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 }
                 })*/
                //.AddView(instrumentName: "cosmos.client.op.RUs", MetricStreamConfiguration.Drop) // Dropping Particular instrument
                
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
            Container container = await database.CreateContainerIfNotExistsAsync(Guid.NewGuid().ToString(), "/pk");

            Stopwatch sw = Stopwatch.StartNew();
            sw.Start();
            while(true)
            {
                string randomId = Guid.NewGuid().ToString();
                string pk = "Status1";

                await container.CreateItemAsync<ToDoActivity>(ToDoActivity.CreateRandomToDoActivity(id: randomId, pk: pk));
                await container.ReadItemAsync<ToDoActivity>(randomId, new PartitionKey(pk));
                if(sw.ElapsedMilliseconds > TimeSpan.FromMinutes(4).TotalMilliseconds)
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
