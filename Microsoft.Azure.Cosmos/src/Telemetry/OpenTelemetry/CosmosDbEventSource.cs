// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System.Diagnostics.Tracing;
    using global::Azure.Core.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry.Diagnostics;

    [EventSource(Name = EventSourceName)]
    internal sealed class CosmosDbEventSource : AzureEventSource
    {
        private const string EventSourceName = OpenTelemetryAttributeKeys.DiagnosticNamespace;
        private static CosmosDbEventSource Singleton { get; } = new CosmosDbEventSource();

        private CosmosDbEventSource()
            : base(EventSourceName)
        {
        }

        public static bool IsErrorEnabled
        {
            [NonEvent]
            get => Singleton.IsEnabled(EventLevel.Error, (EventKeywords)(-1));
        }

        public static bool IsWarnEnabled
        {
            [NonEvent]
            get => Singleton.IsEnabled(EventLevel.Warning, (EventKeywords)(-1));
        }

        public static bool IsInfoEnabled
        {
            [NonEvent]
            get => Singleton.IsEnabled(EventLevel.Informational, (EventKeywords)(-1));
        }

        public static void RecordDiagnosticsForRequests(DistributedTracingOptions config,
            OpenTelemetryAttributes response)
        {
            if (CosmosDbEventSource.IsInfoEnabled)
            {
                Singleton.WriteInfoEvent(response.Diagnostics.ToString());
            } 
            else
            {
                if (DiagnosticsFilterHelper.IsTracingNeeded(
                        config: config,
                        response: response) && CosmosDbEventSource.IsWarnEnabled)
                {
                    Singleton.WriteWarningEvent(response.Diagnostics.ToString());
                }
            }
        }

        public static void RecordDiagnosticsForExceptions(CosmosDiagnostics diagnostics)
        {
            if (CosmosDbEventSource.IsErrorEnabled)
            {
                Singleton.WriteErrorEvent(diagnostics.ToString());
            }
        }

        /// <summary>
        /// We are generating this event only in specific scenarios where this information MUST be present.
        /// thats why going with Error Event Level
        /// </summary>
        /// <param name="message"></param>
        [Event(1, Level = EventLevel.Error)]
        private void WriteErrorEvent(string message)
        {
            this.WriteEvent(1, message);
        }

        /// <summary>
        /// We are generating this event only in specific scenarios where this information MUST be present.
        /// thats why going with Error Event Level
        /// </summary>
        /// <param name="message"></param>
        [Event(1, Level = EventLevel.Warning)]
        private void WriteWarningEvent(string message)
        {
            this.WriteEvent(1, message);
        }

        /// <summary>
        /// We are generating this event only in specific scenarios where this information MUST be present.
        /// thats why going with Error Event Level
        /// </summary>
        /// <param name="message"></param>
        [Event(1, Level = EventLevel.Informational)]
        private void WriteInfoEvent(string message)
        {
            this.WriteEvent(1, message);
        }
    }
}
