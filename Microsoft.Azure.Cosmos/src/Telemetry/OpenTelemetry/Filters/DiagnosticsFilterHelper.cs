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
            CosmosThresholdOptions config,
            OperationType operationType,
            OpenTelemetryAttributes response)
        {
            TimeSpan latencyThreshold = operationType == OperationType.Query ? config.NonPointOperationLatencyThreshold : config.PointOperationLatencyThreshold;
            return response.Diagnostics.GetClientElapsedTime() > latencyThreshold;
        }

        /// <summary>
        /// Check if response HTTP status code is returning successful
        /// </summary>
        /// <returns>true or false</returns>
        public static bool IsSuccessfulResponse(OpenTelemetryAttributes response)
        { 
            return response.StatusCode.IsSuccess() 
                        || (response.StatusCode == System.Net.HttpStatusCode.NotFound && response.SubStatusCode == 0)
                        || (response.StatusCode == System.Net.HttpStatusCode.NotModified && response.SubStatusCode == 0)
                        || (response.StatusCode == System.Net.HttpStatusCode.Conflict && response.SubStatusCode == 0)
                        || (response.StatusCode == System.Net.HttpStatusCode.PreconditionFailed && response.SubStatusCode == 0);
        }
    }
}
