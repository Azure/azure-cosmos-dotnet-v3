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
            return response.Diagnostics.GetClientElapsedTime() > DiagnosticsFilterHelper.DefaultLatencyThreshold(operationType, config);
        }

        /// <summary>
        /// Allow only Payload size(request/response) is more the configured threshold
        /// </summary>
        /// <returns>true or false</returns>
        public static bool IsPayloadSizeThresholdCrossed(
            CosmosThresholdOptions config,
            OpenTelemetryAttributes response)
        {
            int requestContentLength = 0;
            int responseContentLength = 0;
            try
            {
                requestContentLength = Convert.ToInt32(response.RequestContentLength);
            }
            catch (Exception)
            {
                // Ignore, if this conversion fails for any reason.
            }

            try
            {
                responseContentLength = Convert.ToInt32(response.ResponseContentLength);
            }
            catch (Exception)
            {
                // Ignore, if this conversion fails for any reason.
            }

            return config.PayloadSizeThresholdInBytes <= Math.Max(requestContentLength, responseContentLength);
        }

        /// <summary>
        /// Check if response HTTP status code is returning successful
        /// </summary>
        /// <returns>true or false</returns>
        public static bool IsSuccessfulResponse(HttpStatusCode statusCode, int subStatusCode)
        {
            return statusCode.IsSuccess()
            || (statusCode == System.Net.HttpStatusCode.NotFound && subStatusCode == 0)
            || (statusCode == System.Net.HttpStatusCode.NotModified && subStatusCode == 0)
            || (statusCode == System.Net.HttpStatusCode.Conflict && subStatusCode == 0)
            || (statusCode == System.Net.HttpStatusCode.PreconditionFailed && subStatusCode == 0);
        }

        /// <summary>
        /// Get default Latency threshold value based on operation type
        /// </summary>
        /// <param name="operationType"></param>
        /// <param name="config"></param>
        internal static TimeSpan DefaultLatencyThreshold(OperationType operationType, CosmosThresholdOptions config)
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
