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
    using global::Azure.Monitor.OpenTelemetry.Exporter;
    using OpenTelemetry.Resources;

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
                .AddMeter("*")
                .AddProcessInstrumentation()
                .AddRuntimeInstrumentation()
                .AddPrometheusExporter()
                .AddAzureMonitorMetricExporter(o => o.ConnectionString = "")
                //.AddReader(new PeriodicExportingMetricReader(new CustomMetricExporter(), exportIntervalMilliseconds: 1000))
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
                if(sw.ElapsedMilliseconds > TimeSpan.FromMinutes(10).TotalMilliseconds)
                {
                    break;
                }
            }
            sw.Stop();

            meterProvider.Dispose();

            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
}
