// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using Documents;
    using static Antlr4.Runtime.TokenStreamRewriter;

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
            return response.Diagnostics.GetClientElapsedTime() > DiagnosticsFilterHelper.DefaultThreshold(operationType, config);
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

        /// <summary>
        /// Get default threshold value based on operation type
        /// </summary>
        /// <param name="operationType"></param>
        /// <param name="config"></param>
        internal static TimeSpan DefaultThreshold(OperationType operationType, CosmosThresholdOptions config)
        {
            config ??= new CosmosThresholdOptions();
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
