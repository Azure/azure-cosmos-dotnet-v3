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
        private static readonly CosmosThresholdOptions defaultThresholdOptions = new CosmosThresholdOptions();

        /// <summary>
        /// Allow only when Latency is not more than 100 (non-query) /250 (query) ms
        /// </summary>
        /// <returns>true or false</returns>
        public static bool IsLatencyThresholdCrossed(
            CosmosThresholdOptions config,
            OperationType operationType,
            OpenTelemetryAttributes response)
        {
            return response.Diagnostics.GetClientElapsedTime() > DiagnosticsFilterHelper.DefaultThreshold(operationType, config);
        }

        /// <summary>
        /// Check if response HTTP status code is returning successful
        /// </summary>
        /// <returns>true or false</returns>
        public static bool IsSuccessfulResponse(HttpStatusCode statusCode, int substatusCode)
        {
            return statusCode.IsSuccess()
            || (statusCode == System.Net.HttpStatusCode.NotFound && substatusCode == 0)
            || (statusCode == System.Net.HttpStatusCode.NotModified && substatusCode == 0)
            || (statusCode == System.Net.HttpStatusCode.Conflict && substatusCode == 0)
            || (statusCode == System.Net.HttpStatusCode.PreconditionFailed && substatusCode == 0);
        }

        /// <summary>
        /// Get default threshold value based on operation type
        /// </summary>
        /// <param name="operationType"></param>
        /// <param name="config"></param>
        internal static TimeSpan DefaultThreshold(OperationType operationType, CosmosThresholdOptions config)
        {
            config ??= DiagnosticsFilterHelper.defaultThresholdOptions;
            return DiagnosticsFilterHelper.IsPointOperation(operationType) ?
                                            config.PointOperationLatencyThreshold :
                                            config.NonPointOperationLatencyThreshold;
        }

        /// <summary>
        /// Check if passed operation type is a point operation
        /// </summary>
        /// <param name="operationType"></param>
        internal static bool IsPointOperation(OperationType operationType)
        {
            return operationType == OperationType.Create ||
                   operationType == OperationType.Delete ||
                   operationType == OperationType.Replace ||
                   operationType == OperationType.Upsert ||
                   operationType == OperationType.Patch ||
                   operationType == OperationType.Read;
        }
    }
}
