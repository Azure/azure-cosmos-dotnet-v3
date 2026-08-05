//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    /// <summary>
    /// Builds the configured <see cref="IMetricsSink"/> for per-window result rows.
    /// </summary>
    internal static class MetricsSinkFactory
    {
        /// <summary>
        /// Creates the metrics sink selected by <c>--metrics-sink</c>, or null when none is
        /// configured. A misconfigured sink logs a warning and falls back to null so the benchmark
        /// run is never blocked by telemetry setup.
        /// </summary>
        public static IMetricsSink Create(BenchmarkConfig config)
        {
            switch (config.MetricsSinkType)
            {
                case MetricsSinkType.Console:
                    return new ConsoleMetricsSink();

                case MetricsSinkType.Adx:
                    if (string.IsNullOrWhiteSpace(config.AdxMetricsUri))
                    {
                        Utility.TeeTraceInformation("metrics-sink=adx requires --adx-metrics-uri; metrics sink disabled.");
                        return null;
                    }

                    try
                    {
                        return new AzureDataExplorerMetricsSink(config);
                    }
                    catch (System.Exception ex)
                    {
                        Utility.TeeTraceInformation("Failed to initialize Azure Data Explorer metrics sink; metrics sink disabled: " + ex);
                        return null;
                    }

                case MetricsSinkType.None:
                default:
                    return null;
            }
        }
    }
}
