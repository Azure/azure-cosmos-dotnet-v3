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
        public static bool IsMetadataHedgingEnabled(EventLevel level)
        {
            return CosmosDbEventSource.Singleton.IsEnabled(level, Keywords.MetadataHedging);
        }

        [NonEvent]
        public static void MetadataHedgeFired(string primaryRegion, string hedgeRegion, double elapsedMs)
        {
            if (CosmosDbEventSource.Singleton.IsEnabled(EventLevel.Informational, Keywords.MetadataHedging))
            {
                CosmosDbEventSource.Singleton.OnMetadataHedgeFired(primaryRegion ?? string.Empty, hedgeRegion ?? string.Empty, elapsedMs);
            }
        }

        [NonEvent]
        public static void MetadataHedgeWon(string hedgeRegion, double totalElapsedMs)
        {
            if (CosmosDbEventSource.Singleton.IsEnabled(EventLevel.Informational, Keywords.MetadataHedging))
            {
                CosmosDbEventSource.Singleton.OnMetadataHedgeWon(hedgeRegion ?? string.Empty, totalElapsedMs);
            }
        }

        [NonEvent]
        public static void MetadataHedgePrimaryWon(string primaryRegion, double totalElapsedMs, bool hedgeFired)
        {
            if (CosmosDbEventSource.Singleton.IsEnabled(EventLevel.Informational, Keywords.MetadataHedging))
            {
                CosmosDbEventSource.Singleton.OnMetadataHedgePrimaryWon(primaryRegion ?? string.Empty, totalElapsedMs, hedgeFired);
            }
        }

        [NonEvent]
        public static void MetadataHedgeSkipped(string skipReason, string resourceType)
        {
            if (CosmosDbEventSource.Singleton.IsEnabled(EventLevel.Informational, Keywords.MetadataHedging))
            {
                CosmosDbEventSource.Singleton.OnMetadataHedgeSkipped(skipReason ?? string.Empty, resourceType ?? string.Empty);
            }
        }

        [NonEvent]
        public static void MetadataHedgeAuthReject(string hedgeRegion, int statusCode)
        {
            if (CosmosDbEventSource.Singleton.IsEnabled(EventLevel.Warning, Keywords.MetadataHedging))
            {
                CosmosDbEventSource.Singleton.OnMetadataHedgeAuthReject(hedgeRegion ?? string.Empty, statusCode);
            }
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

        [Event(4, Level = EventLevel.Informational, Keywords = Keywords.MetadataHedging)]
        private void OnMetadataHedgeFired(string primaryRegion, string hedgeRegion, double elapsedMs)
        {
            this.WriteEvent(4, primaryRegion, hedgeRegion, elapsedMs);
        }

        [Event(5, Level = EventLevel.Informational, Keywords = Keywords.MetadataHedging)]
        private void OnMetadataHedgeWon(string hedgeRegion, double totalElapsedMs)
        {
            this.WriteEvent(5, hedgeRegion, totalElapsedMs);
        }

        [Event(6, Level = EventLevel.Informational, Keywords = Keywords.MetadataHedging)]
        private void OnMetadataHedgePrimaryWon(string primaryRegion, double totalElapsedMs, bool hedgeFired)
        {
            this.WriteEvent(6, primaryRegion, totalElapsedMs, hedgeFired);
        }

        [Event(7, Level = EventLevel.Informational, Keywords = Keywords.MetadataHedging)]
        private void OnMetadataHedgeSkipped(string skipReason, string resourceType)
        {
            this.WriteEvent(7, skipReason, resourceType);
        }

        [Event(8, Level = EventLevel.Warning, Keywords = Keywords.MetadataHedging)]
        private void OnMetadataHedgeAuthReject(string hedgeRegion, int statusCode)
        {
            this.WriteEvent(8, hedgeRegion, statusCode);
        }

        /// <summary>
        /// EventSource keywords. <see cref="MetadataHedging"/> isolates the
        /// PPAF cold-start metadata-hedging events so consumers can subscribe
        /// independently — see design §9.1.2.
        /// </summary>
        public static class Keywords
        {
            public const EventKeywords MetadataHedging = (EventKeywords)0x1;
        }
    }
}
