//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;

    /// <summary>
    /// Factory for handling telemetry trace stability modes, allowing attribute settings
    /// based on environment-specified stability mode configurations.
    /// </summary>
    internal static class TracesStabilityFactory
    {
        // Specifies the stability mode for telemetry attributes, configured via the OTEL_SEMCONV_STABILITY_OPT_IN environment variable.
        private static string otelStabilityMode = Environment.GetEnvironmentVariable("OTEL_SEMCONV_STABILITY_OPT_IN");

        public static IActivityAttributePopulator GetAttributePopulator(CosmosClientTelemetryOptions metricsOptions = null)
        {
            return otelStabilityMode switch
            {
                OpenTelemetryStablityModes.Database or null => new OpenTelemetryAttributeKeys(metricsOptions?.OperationMetricsOptions, metricsOptions?.NetworkMetricsOptions),
                OpenTelemetryStablityModes.DatabaseDupe => new DatabaseDupAttributeKeys(metricsOptions),
                OpenTelemetryStablityModes.ClassicAppInsights => new AppInsightClassicAttributeKeys(metricsOptions?.OperationMetricsOptions),
                _ => new OpenTelemetryAttributeKeys(metricsOptions?.OperationMetricsOptions, metricsOptions?.NetworkMetricsOptions)
            };
        }

        internal static void RefreshStabilityMode()
        {
            otelStabilityMode = Environment.GetEnvironmentVariable("OTEL_SEMCONV_STABILITY_OPT_IN");
        }
    }
}
