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
    using Microsoft.Azure.Cosmos.Telemetry;

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
            Console.WriteLine("Start => " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            MeterProvider meterProvider = Sdk
                .CreateMeterProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("OperationLevelMetrics"))
                .AddMeter("Azure.Cosmos.Client.Operation")
                .AddView(instrumentName: OpenTelemetryMetricsConstant.OperationMetrics.Name.RequestCharge, new ExplicitBucketHistogramConfiguration // Define histogram buckets
                {
                    Boundaries = OpenTelemetryMetricsConstant.HistogramBuckets.RequestUnitBuckets
                })
                .AddView(instrumentName: OpenTelemetryMetricsConstant.OperationMetrics.Name.Latency, new ExplicitBucketHistogramConfiguration // Define histogram buckets
                {
                    Boundaries = OpenTelemetryMetricsConstant.HistogramBuckets.RequestLatencyBuckets
                })
                .AddView(instrumentName: OpenTelemetryMetricsConstant.OperationMetrics.Name.RowCount, new ExplicitBucketHistogramConfiguration // Define histogram buckets
                {
                    Boundaries = OpenTelemetryMetricsConstant.HistogramBuckets.RowCountBuckets
                })
                .AddView(instrumentName: OpenTelemetryMetricsConstant.OperationMetrics.Name.ActiveInstances, new ExplicitBucketHistogramConfiguration // Define histogram buckets
                {
                    Boundaries = OpenTelemetryMetricsConstant.HistogramBuckets.ActiveInstancesBuckets
                })
                /*.AddOtlpExporter((exporterOptions, metricReaderOptions) =>
                 {
                     exporterOptions.Endpoint = new Uri("http://localhost:9090/api/v1/otlp/v1/metrics");
                     exporterOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
                     metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
                 })*/
                .AddConsoleExporter()
                //.AddAzureMonitorMetricExporter( o => o.ConnectionString = "")
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
            Container container = await database.CreateContainerIfNotExistsAsync(Guid.NewGuid().ToString(), "/pk", throughput: 10000);

            Stopwatch sw = Stopwatch.StartNew();
            sw.Start();
            int counter = 1;
            while(true)
            {
                string randomId = Guid.NewGuid().ToString();
                string pk = "Status1";

                await container.CreateItemAsync<ToDoActivity>(ToDoActivity.CreateRandomToDoActivity(id: randomId, pk: pk));
                await container.ReadItemAsync<ToDoActivity>(randomId, new PartitionKey(pk));

                QueryRequestOptions queryRequestOptions = new QueryRequestOptions()
                {
                    MaxItemCount = Random.Shared.Next(1, 100)
                };
                FeedIterator<ToDoActivity> feedIterator = container.GetItemQueryIterator<ToDoActivity>("SELECT * FROM c", requestOptions: queryRequestOptions);
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<ToDoActivity> toDoActivities = await feedIterator.ReadNextAsync();
                    Console.WriteLine($"{counter++} : Read {toDoActivities.Count} items");
                }

                if (sw.ElapsedMilliseconds > TimeSpan.FromSeconds(1).TotalMilliseconds)
                {
                    break;
                }
            }
            sw.Stop();

            meterProvider.Dispose();

            await Task.Delay(TimeSpan.FromSeconds(1));

            Console.WriteLine("End => " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }
    }
}
