// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System.Diagnostics.Tracing;
    using global::Azure.Core.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry.Diagnostics;

    /// <summary>
    /// This class is used to generate events with Azure.Cosmos.Operation Source Name
    /// </summary>
    [EventSource(Name = EventSourceName)]
    internal sealed class CosmosDbEventSource : AzureEventSource
    {
        private const string EventSourceName = "Azure-Cosmos-Operation";
        
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
            DistributedTracingOptions config,
            Documents.OperationType operationType,
            OpenTelemetryAttributes response)
        {
            if (DiagnosticsFilterHelper.IsTracingNeeded(
                    config: config,
                    operationType: operationType,
                    response: response) && CosmosDbEventSource.IsEnabled(EventLevel.Warning))
            {
                CosmosDbEventSource.Singleton.WriteWarningEvent(response.Diagnostics);
            }
        }

        [NonEvent]
        public static void RecordDiagnosticsForExceptions(CosmosDiagnostics diagnostics)
        {
            if (CosmosDbEventSource.IsEnabled(EventLevel.Error))
            {
                CosmosDbEventSource.Singleton.WriteErrorEvent(diagnostics);
            }
        }

        [Event(1, Level = EventLevel.Error)]
        private void WriteErrorEvent(object message)
        {
            this.WriteEvent(1, message);
        }

        [Event(2, Level = EventLevel.Warning)]
        private void WriteWarningEvent(object message)
        {
            this.WriteEvent(2, message);
        }
        
        [Event(3, Level = EventLevel.Informational)]
        private void WriteInfoEvent(object message)
        {
            this.WriteEvent(3, message);
        }
    }
}
