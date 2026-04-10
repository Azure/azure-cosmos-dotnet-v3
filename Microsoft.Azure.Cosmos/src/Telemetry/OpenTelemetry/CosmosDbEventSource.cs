// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System.Diagnostics.Tracing;
    using global::Azure.Core.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry.Diagnostics;

    /// <summary>
    /// This class is used to generate events with Azure-Cosmos-Operation-Request-Diagnostics Source Name
    /// </summary>
    [EventSource(Name = EventSourceName)]
    internal sealed class CosmosDbEventSource : AzureEventSource
    {
        internal const string EventSourceName = "Azure-Cosmos-Operation-Request-Diagnostics";

        private static CosmosDbEventSource Singleton { get; } = new CosmosDbEventSource();

        private CosmosDbEventSource()
            : base(EventSourceName)
        {
        }

        [NonEvent]
        public static bool IsEnabled(EventLevel level)
        {
            return CosmosDbEventSource.Singleton.IsEnabled(level, EventKeywords.None);
        }

        [NonEvent]
        public static void RecordDiagnosticsForRequests(
            CosmosThresholdOptions config,
            Documents.OperationType operationType,
            OpenTelemetryAttributes response)
        {
            if (response.Diagnostics == null)
            {
                return;
            }

            if (CosmosDbEventSource.IsEnabled(EventLevel.Warning))
            {
                if (!DiagnosticsFilterHelper.IsSuccessfulResponse(
                                        response.StatusCode, response.SubStatusCode))
                {
                    CosmosDbEventSource.Singleton.FailedRequest(response.Diagnostics.ToString());
                }
                else if (DiagnosticsFilterHelper.IsLatencyThresholdCrossed(
                            config: config,
                            operationType: operationType,
                            response: response) ||
                        (config.RequestChargeThreshold is not null &&
                            config.RequestChargeThreshold <= response.RequestCharge) ||
                        (config.PayloadSizeThresholdInBytes is not null &&
                            DiagnosticsFilterHelper.IsPayloadSizeThresholdCrossed(
                                config: config,
                                response: response)))
                {
                    CosmosDbEventSource.Singleton.ThresholdViolation(response.Diagnostics.ToString());
                }
            }
        }

        [NonEvent]
        public static void RecordDiagnosticsForExceptions(CosmosDiagnostics diagnostics)
        {
            if (CosmosDbEventSource.IsEnabled(EventLevel.Error))
            {
                CosmosDbEventSource.Singleton.Exception(diagnostics.ToString());
            }
        }

        [Event(1, Level = EventLevel.Error)]
        private void Exception(string message)
        {
            this.WriteEvent(1, message);
        }

        [Event(2, Level = EventLevel.Warning)]
        private void ThresholdViolation(string message)
        {
            this.WriteEvent(2, message);
        }

        [Event(3, Level = EventLevel.Error)]
        private void FailedRequest(string message)
        {
            this.WriteEvent(3, message);
        }
    }
}
