//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry
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

        public static IActivityAttributePopulator GetAttributePopulator()
        {
            return otelStabilityMode switch
            {
                OpenTelemetryStablityModes.Database or null => new OpenTelemetryAttributeKeys(),
                OpenTelemetryStablityModes.DatabaseDupe => new DatabaseDupAttributeKeys(),
                OpenTelemetryStablityModes.ClassicAppInsights => new AppInsightClassicAttributeKeys(),
                _ => new OpenTelemetryAttributeKeys()
            };
        }

        internal static void RefreshStabilityMode()
        {
            otelStabilityMode = Environment.GetEnvironmentVariable("OTEL_SEMCONV_STABILITY_OPT_IN");
        }
    }
}
