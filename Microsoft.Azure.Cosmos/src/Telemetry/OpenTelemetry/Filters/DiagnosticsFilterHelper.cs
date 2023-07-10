// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using Documents;

    internal static class DiagnosticsFilterHelper
    {
        /// <summary>
        /// Allow only when Latency is not more than 100 (non-query) /250 (query) ms
        /// </summary>
        /// <returns>true or false</returns>
        public static bool IsLatencyThresholdCrossed(
            DistributedTracingOptions config,
            OperationType operationType,
            OpenTelemetryAttributes response)
        {
            TimeSpan latencyThreshold;

            if (config?.LatencyThresholdForDiagnosticEvent != null)
            {
                latencyThreshold = config.LatencyThresholdForDiagnosticEvent.Value;
            }
            else
            {
                latencyThreshold = operationType == OperationType.Query ? DistributedTracingOptions.DefaultQueryTimeoutThreshold : DistributedTracingOptions.DefaultCrudLatencyThreshold;
            }

            return response.Diagnostics.GetClientElapsedTime() > latencyThreshold;
        }

        /// <summary>
        /// Allow only when HTTP status code is not Success
        /// </summary>
        /// <returns>true or false</returns>
        public static bool IsNonSuccessResponse(
          OpenTelemetryAttributes response)
        { 
            return !response.StatusCode.IsSuccess();
        }
    }
}
