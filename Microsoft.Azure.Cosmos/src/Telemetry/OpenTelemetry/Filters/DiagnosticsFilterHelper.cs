// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Net;
    using Documents;

    internal static class DiagnosticsFilterHelper
    {
        /// <summary>
        /// Allow only when either of below is <b>True</b><br></br>
        /// 1) Latency is not more than 100/250 (query) ms<br></br>
        /// 3) HTTP status code is not Success<br></br>
        /// </summary>
        /// <returns>true or false</returns>
        public static bool HasIssueWithOperation(
            OpenTelemetryOptions config,
            OpenTelemetryAttributes response)
        {
            TimeSpan latencyThreshold;

            if (config != null && config.LatencyThreshold.HasValue)
            {
                latencyThreshold = config.LatencyThreshold.Value;
            }
            else
            {
                latencyThreshold = response.OperationType == OperationType.Query ? OpenTelemetryOptions.DefaultQueryTimeoutThreshold : OpenTelemetryOptions.DefaultCrudLatencyThreshold;
            }

            return !(response.Diagnostics.GetClientElapsedTime() < latencyThreshold && response.StatusCode.IsSuccess());
        }
    }
}
