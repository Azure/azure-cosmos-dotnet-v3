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
        private readonly static List<string> expectedDimensions = new()
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
                    else
                    {
                        CollectionAssert.AreEquivalent(expectedDimensions, actualDimensions.ToList(), $"Dimensions are not matching for {metric.Name}");
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
