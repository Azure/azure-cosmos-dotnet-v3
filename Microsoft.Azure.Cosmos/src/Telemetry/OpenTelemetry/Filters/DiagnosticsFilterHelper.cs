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
        /// Allow only when either of below is <b>True</b><br></br>
        /// 1) Latency is not more than 100/250 (query) ms<br></br>
        /// 3) HTTP status code is not Success<br></br>
        /// </summary>
        /// <returns>true or false</returns>
        public static bool IsTracingNeeded(
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

            return response.Diagnostics.GetClientElapsedTime() > latencyThreshold || !response.StatusCode.IsSuccess();
        }
    }
}
