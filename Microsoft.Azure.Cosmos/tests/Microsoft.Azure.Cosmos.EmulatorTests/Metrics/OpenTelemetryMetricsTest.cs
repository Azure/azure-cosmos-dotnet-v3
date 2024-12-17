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
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry;

    [TestClass]
    public class OpenTelemetryMetricsTest : BaseCosmosClientHelper
    {
        private const string StabilityEnvVariableName = "OTEL_SEMCONV_STABILITY_OPT_IN";
        private const int AggregatingInterval = 500;

        private readonly ManualResetEventSlim manualResetEventSlim = new ManualResetEventSlim(false);

        private static readonly Dictionary<string, MetricType> expectedOperationMetrics = new()
        {
            { "db.client.operation.duration", MetricType.Histogram },
            { "db.client.response.row_count", MetricType.Histogram},
            { "db.client.cosmosdb.operation.request_charge", MetricType.Histogram },
            { "db.client.cosmosdb.active_instance.count", MetricType.LongSumNonMonotonic }
        };

        private static readonly Dictionary<string, MetricType> expectedNetworkMetrics = new()
        {
            { "db.client.cosmosdb.request.duration", MetricType.Histogram},
            { "db.client.cosmosdb.request.body.size", MetricType.Histogram},
            { "db.client.cosmosdb.response.body.size", MetricType.Histogram},
            { "db.client.cosmosdb.request.service_duration", MetricType.Histogram},
            { "db.client.cosmosdb.request.channel_aquisition.duration", MetricType.Histogram},
            { "db.client.cosmosdb.request.transit.duration", MetricType.Histogram},
            { "db.client.cosmosdb.request.received.duration", MetricType.Histogram}
        };

        private static readonly Dictionary<string, MetricType> expectedGatewayModeNetworkMetrics = new()
        {
            { "db.client.cosmosdb.request.duration", MetricType.Histogram},
            { "db.client.cosmosdb.request.body.size", MetricType.Histogram},
            { "db.client.cosmosdb.response.body.size", MetricType.Histogram},
        };

        private static readonly Dictionary<string, MetricType> expectedMetrics = expectedOperationMetrics
                                                                                    .Concat(expectedNetworkMetrics)
                                                                                    .ToDictionary(kv => kv.Key, kv => kv.Value);
        private static readonly Dictionary<string, MetricType> expectedGatewayModeMetrics = expectedOperationMetrics
                                                                                   .Concat(expectedGatewayModeNetworkMetrics)
                                                                                   .ToDictionary(kv => kv.Key, kv => kv.Value);

        private MeterProvider meterProvider;

        private Stopwatch timeoutTimer;

        [TestInitialize]
        public void TestInitialize()
        {
            Environment.SetEnvironmentVariable(StabilityEnvVariableName, null);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();

            this.meterProvider.Dispose();

            Environment.SetEnvironmentVariable(StabilityEnvVariableName, null);
        }

        [TestMethod]
        [DataRow(OpenTelemetryStablityModes.DatabaseDupe, ConnectionMode.Direct, DisplayName = "Direct Mode: Metrics and Dimensions when OTEL_SEMCONV_STABILITY_OPT_IN is set to 'database/dup'")]
        [DataRow(OpenTelemetryStablityModes.Database, ConnectionMode.Direct, DisplayName = "Direct Mode: Metrics and Dimensions when OTEL_SEMCONV_STABILITY_OPT_IN is set to 'database'")]
        [DataRow(OpenTelemetryStablityModes.ClassicAppInsights, ConnectionMode.Direct, DisplayName = "Direct Mode: Metrics and Dimensions when OTEL_SEMCONV_STABILITY_OPT_IN is set to 'appinsightssdk'")]
        [DataRow(null, ConnectionMode.Direct, DisplayName = "Direct Mode: Metrics and Dimensions when OTEL_SEMCONV_STABILITY_OPT_IN is not set")]
        [DataRow(OpenTelemetryStablityModes.DatabaseDupe, ConnectionMode.Gateway, DisplayName = "Gateway Mode: Metrics and Dimensions when OTEL_SEMCONV_STABILITY_OPT_IN is set to 'database/dup'")]
        [DataRow(OpenTelemetryStablityModes.Database, ConnectionMode.Gateway, DisplayName = "Gateway Mode: Metrics and Dimensions when OTEL_SEMCONV_STABILITY_OPT_IN is set to 'database'")]
        [DataRow(OpenTelemetryStablityModes.ClassicAppInsights, ConnectionMode.Gateway, DisplayName = "Gateway Mode: Metrics and Dimensions when OTEL_SEMCONV_STABILITY_OPT_IN is set to 'appinsightssdk'")]
        [DataRow(null, ConnectionMode.Gateway, DisplayName = "Gateway Mode: Metrics and Dimensions when OTEL_SEMCONV_STABILITY_OPT_IN is not set")]
        public async Task MetricsGenerationTest(string stabilityMode, ConnectionMode connectionMode)
        {
            Environment.SetEnvironmentVariable(StabilityEnvVariableName, stabilityMode);

            // Refreshing Static variables to reflect the new stability mode
            TracesStabilityFactory.RefreshStabilityMode();
            
            CosmosDbOperationMeter.DimensionPopulator = TracesStabilityFactory.GetAttributePopulator(null);
            CosmosDbNetworkMeter.DimensionPopulator = TracesStabilityFactory.GetAttributePopulator(null);

            // Initialize OpenTelemetry MeterProvider
            this.meterProvider = Sdk
                .CreateMeterProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Azure Cosmos DB Metrics"))
                .AddMeter(CosmosDbClientMetrics.OperationMetrics.MeterName, CosmosDbClientMetrics.NetworkMetrics.MeterName)
                .AddReader(new PeriodicExportingMetricReader(
                    exporter: new CustomMetricExporter(this.manualResetEventSlim),
                    exportIntervalMilliseconds: AggregatingInterval))
                .Build();

            if (connectionMode == ConnectionMode.Direct)
            {
                await base.TestInit((builder) => builder.WithClientTelemetryOptions(new CosmosClientTelemetryOptions()
                {
                    IsClientMetricsEnabled = true
                })
                .WithConnectionModeDirect());

            } else if (connectionMode == ConnectionMode.Gateway)
            {
                await base.TestInit((builder) => builder.WithClientTelemetryOptions(new CosmosClientTelemetryOptions()
                {
                    IsClientMetricsEnabled = true
                })
                .WithConnectionModeGateway());
            }
          
            this.timeoutTimer = Stopwatch.StartNew();

            // Cosmos Db operations
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

            // Waiting for a notification from the exporter, which means metrics are emitted
            while (!this.manualResetEventSlim.IsSet)
            {
                // Timing out, if exporter didn't send any signal is 3 seconds
                if (this.timeoutTimer.Elapsed == TimeSpan.FromSeconds(3))
                {
                    this.manualResetEventSlim.Set();
                    Assert.Fail("Timed Out: Metrics were not generated in 3 seconds.");
                }
            }

            // Asserting Metrics
            CollectionAssert.AreEquivalent(connectionMode == ConnectionMode.Direct? expectedMetrics : expectedGatewayModeMetrics, CustomMetricExporter.ActualMetrics, string.Join(", ", CustomMetricExporter.ActualMetrics.Select(kv => $"{kv.Key}: {kv.Value}")));

            // Asserting Dimensions
            foreach (KeyValuePair<string, List<string>> dimension in CustomMetricExporter.Dimensions)
            {
                if (dimension.Key == CosmosDbClientMetrics.OperationMetrics.Name.ActiveInstances)
                {
                    CollectionAssert.AreEquivalent(GetExpectedInstanceCountDimensions(), dimension.Value, $"Actual dimensions for {dimension.Key} are {string.Join(", ", dimension.Value.Select(kv => $"{kv}"))}");
                }
                else if (expectedOperationMetrics.ContainsKey(dimension.Key))
                {
                    CollectionAssert.AreEquivalent(GetExpectedOperationDimensions(), dimension.Value, $"Actual dimensions for {dimension.Key} are {string.Join(", ", dimension.Value.Select(kv => $"{kv}"))}");
                }
                else if (expectedNetworkMetrics.ContainsKey(dimension.Key))
                {
                    CollectionAssert.AreEquivalent(GetExpectedNetworkDimensions(), dimension.Value, $"Actual dimensions for {dimension.Key} are {string.Join(", ", dimension.Value.Select(kv => $"{kv}"))}");
                }
            }
        }

        private static List<string> GetExpectedInstanceCountDimensions()
        {
            List<string> otelBased = new()
                {
                    OpenTelemetryAttributeKeys.DbSystemName,
                    OpenTelemetryAttributeKeys.ServerAddress,
                    OpenTelemetryAttributeKeys.ServerPort
                };
            List<string> appInsightBased = new()
                {
                    AppInsightClassicAttributeKeys.ServerAddress
                };

            return GetBasedOnStabilityMode(otelBased, appInsightBased);
        }

        private static List<string> GetExpectedOperationDimensions()
        {
            List<string> otelBased = new()
                {
                    OpenTelemetryAttributeKeys.DbSystemName,
                    OpenTelemetryAttributeKeys.ContainerName,
                    OpenTelemetryAttributeKeys.DbName,
                    OpenTelemetryAttributeKeys.ServerAddress,
                    OpenTelemetryAttributeKeys.ServerPort,
                    OpenTelemetryAttributeKeys.DbOperation,
                    OpenTelemetryAttributeKeys.StatusCode,
                    OpenTelemetryAttributeKeys.SubStatusCode,
                    OpenTelemetryAttributeKeys.ConsistencyLevel,
                    OpenTelemetryAttributeKeys.Region,
                    OpenTelemetryAttributeKeys.ErrorType
                };
            List<string> appInsightBased = new()
                {
                    AppInsightClassicAttributeKeys.ContainerName,
                    AppInsightClassicAttributeKeys.DbName,
                    AppInsightClassicAttributeKeys.ServerAddress,
                    AppInsightClassicAttributeKeys.DbOperation,
                    AppInsightClassicAttributeKeys.StatusCode,
                    AppInsightClassicAttributeKeys.SubStatusCode,
                    AppInsightClassicAttributeKeys.Region
                };

            return GetBasedOnStabilityMode(otelBased, appInsightBased);
        }

        private static List<string> GetExpectedNetworkDimensions()
        {
            List<string> otelBased = new()
                {
                    OpenTelemetryAttributeKeys.DbSystemName,
                    OpenTelemetryAttributeKeys.ContainerName,
                    OpenTelemetryAttributeKeys.DbName,
                    OpenTelemetryAttributeKeys.ServerAddress,
                    OpenTelemetryAttributeKeys.ServerPort,
                    OpenTelemetryAttributeKeys.DbOperation,
                    OpenTelemetryAttributeKeys.StatusCode,
                    OpenTelemetryAttributeKeys.SubStatusCode,
                    OpenTelemetryAttributeKeys.ConsistencyLevel,
                    OpenTelemetryAttributeKeys.NetworkProtocolName,
                    OpenTelemetryAttributeKeys.ServiceEndpointHost,
                    OpenTelemetryAttributeKeys.ServiceEndPointPort,
                    OpenTelemetryAttributeKeys.ServiceEndpointRoutingId,
                    OpenTelemetryAttributeKeys.ServiceEndpointStatusCode,
                    OpenTelemetryAttributeKeys.ServiceEndpointSubStatusCode,
                    OpenTelemetryAttributeKeys.ServiceEndpointRegion,
                    OpenTelemetryAttributeKeys.ErrorType
                };
            List<string> appInsightBased = new()
                {
                    AppInsightClassicAttributeKeys.ContainerName,
                    AppInsightClassicAttributeKeys.DbName,
                    AppInsightClassicAttributeKeys.ServerAddress,
                    AppInsightClassicAttributeKeys.DbOperation,
                    AppInsightClassicAttributeKeys.StatusCode,
                    AppInsightClassicAttributeKeys.SubStatusCode
                };

            return GetBasedOnStabilityMode(otelBased, appInsightBased);
        }

        private static List<string> GetBasedOnStabilityMode(List<string> otelBased, List<string> appInsightBased)
        {
            string stabilityMode = Environment.GetEnvironmentVariable(StabilityEnvVariableName);

            return stabilityMode switch
            {
                OpenTelemetryStablityModes.Database or null => otelBased,
                OpenTelemetryStablityModes.DatabaseDupe => otelBased.Union(appInsightBased).ToList(),
                OpenTelemetryStablityModes.ClassicAppInsights => appInsightBased,
                _ => otelBased
            };
        }
    }
}
