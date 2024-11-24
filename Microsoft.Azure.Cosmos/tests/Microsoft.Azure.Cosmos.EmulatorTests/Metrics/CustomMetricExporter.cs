//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.Metrics
{
    using OpenTelemetry.Metrics;
    using OpenTelemetry;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Linq;

    public class CustomMetricExporter : BaseExporter<Metric>
    {
        private readonly ManualResetEventSlim manualResetEventSlim = null;

        internal static readonly Dictionary<string, MetricType> expectedOperationMetrics = new Dictionary<string, MetricType>()
        {
            { "db.client.operation.duration", MetricType.Histogram },
            { "db.client.response.row_count", MetricType.Histogram},
            { "db.client.cosmosdb.operation.request_charge", MetricType.Histogram },
            { "db.client.cosmosdb.active_instance.count", MetricType.LongSumNonMonotonic }
        };

        internal static readonly Dictionary<string, MetricType> expectedNetworkMetrics = new Dictionary<string, MetricType>()
        {
            { "db.client.cosmosdb.request.duration", MetricType.Histogram},
            { "db.client.cosmosdb.request.body.size", MetricType.Histogram},
            { "db.client.cosmosdb.response.body.size", MetricType.Histogram},
            { "db.server.cosmosdb.request.duration", MetricType.Histogram},
            { "db.client.cosmosdb.request.channel_aquisition.duration", MetricType.Histogram},
            { "db.client.cosmosdb.request.transit.duration", MetricType.Histogram},
            { "db.client.cosmosdb.request.received.duration", MetricType.Histogram}
        };
        
        private readonly static List<string> expectedOperationDimensions = new()
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

        private readonly static List<string> expectedNetworkDimensions = new()
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
            OpenTelemetryAttributeKeys.ServiceEndpointResourceId,
            OpenTelemetryAttributeKeys.ServiceEndpointStatusCode,
            OpenTelemetryAttributeKeys.ServiceEndpointSubStatusCode,
            OpenTelemetryAttributeKeys.ServiceEndpointRegion,
            OpenTelemetryAttributeKeys.ErrorType
        };

        private readonly static List<string> expectedDimensionsForInstanceCountMetrics = new()
        {
            OpenTelemetryAttributeKeys.DbSystemName,
            OpenTelemetryAttributeKeys.ServerAddress,
            OpenTelemetryAttributeKeys.ServerPort
        };

        public static Dictionary<string, MetricType> ActualMetrics = new Dictionary<string, MetricType>();

        public CustomMetricExporter(ManualResetEventSlim manualResetEventSlim)
        {
            this.manualResetEventSlim = manualResetEventSlim;
        }

        public override ExportResult Export(in Batch<Metric> batch)
        {
            try
            {
                foreach (Metric metric in batch)
                {
                    ActualMetrics.TryAdd(metric.Name, metric.MetricType);

                    HashSet<string> actualDimensions = new();
                    foreach (MetricPoint metricPoint in metric.GetMetricPoints())
                    {
                        foreach (KeyValuePair<string, object> dimension in metricPoint.Tags)
                        {
                            actualDimensions.Add(dimension.Key);
                        }
                    }

                    if (metric.Name == CosmosDbClientMetrics.OperationMetrics.Name.ActiveInstances)
                    {
                        CollectionAssert.AreEquivalent(expectedDimensionsForInstanceCountMetrics, actualDimensions.ToList(), $"Dimensions are not matching for {metric.Name}");
                    }
                    else if (expectedOperationMetrics.ContainsKey(metric.Name))
                    {
                        CollectionAssert.AreEquivalent(expectedOperationDimensions, actualDimensions.ToList(), $"Dimensions are not matching for {metric.Name}");
                    }
                    else if (expectedNetworkMetrics.ContainsKey(metric.Name))
                    {
                        CollectionAssert.AreEquivalent(expectedNetworkDimensions, actualDimensions.ToList(), $"Dimensions are not matching for {metric.Name}");
                    }
                }

                if (ActualMetrics.Count > 0)
                {
                    this.manualResetEventSlim.Set();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                this.manualResetEventSlim.Set();
                return ExportResult.Failure;
            }


            return ExportResult.Success;
        }
    }
}
