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

        private static readonly Dictionary<string, MetricType> expectedOperationMetrics = new()
        {
            { "db.client.operation.duration", MetricType.Histogram },
            { "db.client.response.row_count", MetricType.Histogram},
            { "azure.cosmosdb.client.operation.request_charge", MetricType.Histogram },
            { "azure.cosmosdb.client.active_instance.count", MetricType.LongSumNonMonotonic }
        };

        private static readonly Dictionary<string, MetricType> expectedNetworkMetrics = new()
        {
            { "azure.cosmosdb.client.request.duration", MetricType.Histogram},
            { "azure.cosmosdb.client.request.body.size", MetricType.Histogram},
            { "azure.cosmosdb.client.response.body.size", MetricType.Histogram},
            { "azure.cosmosdb.client.request.service_duration", MetricType.Histogram},
            { "azure.cosmosdb.client.request.channel_aquisition.duration", MetricType.Histogram},
            { "azure.cosmosdb.client.request.transit.duration", MetricType.Histogram},
            { "azure.cosmosdb.client.request.received.duration", MetricType.Histogram}
        };

        private static readonly Dictionary<string, MetricType> expectedGatewayModeNetworkMetrics = new()
        {
            { "azure.cosmosdb.client.request.duration", MetricType.Histogram},
            { "azure.cosmosdb.client.request.body.size", MetricType.Histogram},
            { "azure.cosmosdb.client.response.body.size", MetricType.Histogram},
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

            CosmosDbOperationMeter.Reset();
            CosmosDbNetworkMeter.Reset();

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
            ManualResetEventSlim manualResetEventSlim = this.SetupOpenTelemetry(stabilityMode);

            if (connectionMode == ConnectionMode.Direct)
            {
                await base.TestInit((builder) => builder.WithClientTelemetryOptions(new CosmosClientTelemetryOptions()
                {
                    DisableDistributedTracing = true,
                    IsClientMetricsEnabled = true
                })
                .WithConnectionModeDirect());

            }
            else if (connectionMode == ConnectionMode.Gateway)
            {
                await base.TestInit((builder) => builder.WithClientTelemetryOptions(new CosmosClientTelemetryOptions()
                {
                    DisableDistributedTracing = true,
                    IsClientMetricsEnabled = true
                })
                .WithConnectionModeGateway());
            }

            await this.ExecuteOperation(manualResetEventSlim);

            // Asserting Metrics
            CollectionAssert.AreEquivalent(connectionMode == ConnectionMode.Direct ? expectedMetrics : expectedGatewayModeMetrics, CustomMetricExporter.ActualMetrics, string.Join(", ", CustomMetricExporter.ActualMetrics.Select(kv => $"{kv.Key}: {kv.Value}")));

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

        [TestMethod]
        [DataRow(true, true, DisplayName = "Optional and Custom Dimensions are included")]
        [DataRow(false, true, DisplayName = "Optional Dimension is not included but Custom Dimensions are included")]
        [DataRow(null, true, DisplayName = "Optional Dimension is not included with null but Custom Dimensions are included")]
        [DataRow(null, true, DisplayName = "Optional Dimension is not included with null but Custom Dimensions are included")]
        [DataRow(true, false, DisplayName = "Optional Dimension is included but Custom Dimensions are not included")]
        [DataRow(false, false, DisplayName = "Optional Dimension is not included but Custom Dimensions are included")]
        public async Task MetricsWithOptionalDimensionTest(bool? shouldIncludeOptionalDimensions, bool isCustomDimensionNull)
        {
            ManualResetEventSlim manualResetEventSlim = this.SetupOpenTelemetry(null);

            RequestOptions requestOptions = new RequestOptions()
            {
                OperationMetricsOptions = isCustomDimensionNull? null : new OperationMetricsOptions()
                {
                    CustomDimensions = new Dictionary<string, string>()
                    {
                        { "custom_dimension5", "custom_dimension5_value" },
                        { "custom_dimension6", "custom_dimension6_value" }
                    },
                    IncludeRegion = shouldIncludeOptionalDimensions
                },
                NetworkMetricsOptions = new NetworkMetricsOptions()
                {
                    CustomDimensions = isCustomDimensionNull ? null : new Dictionary<string, string>()
                    {
                        { "custom_dimension7", "custom_dimension7_value" },
                        { "custom_dimension8", "custom_dimension8_value" }
                    },
                    IncludeRoutingId = shouldIncludeOptionalDimensions
                }
            };

            await base.TestInit((builder) => builder.WithClientTelemetryOptions(new CosmosClientTelemetryOptions()
            {
                DisableDistributedTracing = true,
                IsClientMetricsEnabled = true,
                OperationMetricsOptions = new OperationMetricsOptions()
                {
                    IncludeRegion  = shouldIncludeOptionalDimensions,
                    CustomDimensions = isCustomDimensionNull ? null : new Dictionary<string, string>()
                    {
                        { "custom_dimension1", "custom_dimension1_value" },
                        { "custom_dimension2", "custom_dimension2_value" }
                    }
                },
                NetworkMetricsOptions = new NetworkMetricsOptions()
                {
                    IncludeRoutingId = shouldIncludeOptionalDimensions,
                    CustomDimensions = isCustomDimensionNull ? null : new Dictionary<string, string>()
                    {
                        { "custom_dimension3", "custom_dimension3_value" },
                        { "custom_dimension4", "custom_dimension4_value" }
                    }
                }
            })
            .WithConnectionModeDirect(), new ItemRequestOptions()
            {
                OperationMetricsOptions = requestOptions.OperationMetricsOptions,
                NetworkMetricsOptions = requestOptions.NetworkMetricsOptions
            });

            // Cosmos Db operations
            await this.ExecuteOperation(manualResetEventSlim, requestOptions);

            // Asserting Dimensions
            foreach (KeyValuePair<string, List<string>> dimension in CustomMetricExporter.Dimensions)
            {
                if (dimension.Key == CosmosDbClientMetrics.OperationMetrics.Name.ActiveInstances)
                {
                    Assert.IsFalse(dimension.Value.Contains(OpenTelemetryAttributeKeys.Region));
                    Assert.IsFalse(dimension.Value.Contains(OpenTelemetryAttributeKeys.ServiceEndpointRoutingId));
                    Assert.IsFalse(dimension.Value.Contains("custom_dimension1"), $"Dimension missing for {dimension.Key}");
                    Assert.IsFalse(dimension.Value.Contains("custom_dimension2"), $"Dimension missing for {dimension.Key}");
                    Assert.IsFalse(dimension.Value.Contains("custom_dimension3"), $"Dimension missing for {dimension.Key}");
                    Assert.IsFalse(dimension.Value.Contains("custom_dimension4"), $"Dimension missing for {dimension.Key}");
                    Assert.IsFalse(dimension.Value.Contains("custom_dimension5"), $"Dimension missing for {dimension.Key}");
                    Assert.IsFalse(dimension.Value.Contains("custom_dimension6"), $"Dimension missing for {dimension.Key}");
                    Assert.IsFalse(dimension.Value.Contains("custom_dimension7"), $"Dimension missing for {dimension.Key}");
                    Assert.IsFalse(dimension.Value.Contains("custom_dimension8"), $"Dimension missing for {dimension.Key}");
                }
                else if (expectedOperationMetrics.ContainsKey(dimension.Key))
                {
                    Assert.AreEqual(dimension.Value.Contains(OpenTelemetryAttributeKeys.Region), shouldIncludeOptionalDimensions ?? false);
                    Assert.AreEqual(dimension.Value.Contains("custom_dimension1"), !isCustomDimensionNull, $"Dimension missing for {dimension.Key}");
                    Assert.AreEqual(dimension.Value.Contains("custom_dimension2"), !isCustomDimensionNull, $"Dimension missing for {dimension.Key}");
                    Assert.AreEqual(dimension.Value.Contains("custom_dimension5"), !isCustomDimensionNull, $"Dimension missing for {dimension.Key}");
                    Assert.AreEqual(dimension.Value.Contains("custom_dimension6"), !isCustomDimensionNull, $"Dimension missing for {dimension.Key}");
                }
                else if (expectedNetworkMetrics.ContainsKey(dimension.Key))
                {
                    Assert.AreEqual(dimension.Value.Contains(OpenTelemetryAttributeKeys.ServiceEndpointRoutingId), shouldIncludeOptionalDimensions ?? false);
                    Assert.AreEqual(dimension.Value.Contains("custom_dimension3"), !isCustomDimensionNull, $"Dimension missing for {dimension.Key}");
                    Assert.AreEqual(dimension.Value.Contains("custom_dimension4"), !isCustomDimensionNull, $"Dimension missing for {dimension.Key}");
                    Assert.AreEqual(dimension.Value.Contains("custom_dimension7"), !isCustomDimensionNull, $"Dimension missing for {dimension.Key}");
                    Assert.AreEqual(dimension.Value.Contains("custom_dimension8"), !isCustomDimensionNull, $"Dimension missing for {dimension.Key}");
                }
            }
        }

        private async Task ExecuteOperation(ManualResetEventSlim manualResetEventSlim, RequestOptions requestOptions = null)
        {
            this.timeoutTimer = Stopwatch.StartNew();

            ItemRequestOptions itemRequestOptions = new ItemRequestOptions()
            {
                OperationMetricsOptions = requestOptions?.OperationMetricsOptions,
                NetworkMetricsOptions = requestOptions?.NetworkMetricsOptions
            };

            QueryRequestOptions queryRequestOptions = new QueryRequestOptions()
            {
                OperationMetricsOptions = requestOptions?.OperationMetricsOptions,
                NetworkMetricsOptions = requestOptions?.NetworkMetricsOptions
            };

            // Cosmos Db operations
            Container container = await this.database.CreateContainerIfNotExistsAsync(Guid.NewGuid().ToString(), "/pk", throughput: 10000, requestOptions: itemRequestOptions);
            for (int count = 0; count < 10; count++)
            {
                string randomId = Guid.NewGuid().ToString();
                string pk = "Status1";

                await container.CreateItemAsync<ToDoActivity>(ToDoActivity.CreateRandomToDoActivity(id: randomId, pk: pk), requestOptions: itemRequestOptions);
                await container.ReadItemAsync<ToDoActivity>(randomId, new PartitionKey(pk), requestOptions: itemRequestOptions);

                FeedIterator<ToDoActivity> feedIterator = container.GetItemQueryIterator<ToDoActivity>(queryText: "SELECT * FROM c", requestOptions: queryRequestOptions);
                while (feedIterator.HasMoreResults)
                {
                    await feedIterator.ReadNextAsync();
                }
            }

            // Waiting for a notification from the exporter, which means metrics are emitted
            while (!manualResetEventSlim.IsSet)
            {
                // Timing out, if exporter didn't send any signal is 3 seconds
                if (this.timeoutTimer.Elapsed == TimeSpan.FromSeconds(3))
                {
                    manualResetEventSlim.Set();
                    Assert.Fail("Timed Out: Metrics were not generated in 3 seconds.");
                }
            }
        }

        private ManualResetEventSlim SetupOpenTelemetry(string stabilityMode)
        {
            ManualResetEventSlim manualResetEventSlim = new ManualResetEventSlim(false);
            Environment.SetEnvironmentVariable(StabilityEnvVariableName, stabilityMode);

            // Refreshing Static variables to reflect the new stability mode
            TracesStabilityFactory.RefreshStabilityMode();
            CosmosDbOperationMeter.DimensionPopulator = TracesStabilityFactory.GetAttributePopulator();
            CosmosDbNetworkMeter.DimensionPopulator = TracesStabilityFactory.GetAttributePopulator();

            // Initialize OpenTelemetry MeterProvider
            this.meterProvider = Sdk
                .CreateMeterProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Azure Cosmos DB Metrics"))
                .AddMeter(CosmosDbClientMetrics.OperationMetrics.MeterName, CosmosDbClientMetrics.NetworkMetrics.MeterName)
                .AddReader(new PeriodicExportingMetricReader(
                    exporter: new CustomMetricExporter(manualResetEventSlim),
                    exportIntervalMilliseconds: AggregatingInterval))
                .Build();
            return manualResetEventSlim;
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
