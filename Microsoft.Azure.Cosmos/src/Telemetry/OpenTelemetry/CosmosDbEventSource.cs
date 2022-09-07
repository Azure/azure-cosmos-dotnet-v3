// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System.Diagnostics.Tracing;
    using global::Azure.Core.Diagnostics;

    [EventSource(Name = EventSourceName)]
    internal sealed class CosmosDbEventSource : AzureEventSource
    {
        private const string EventSourceName = OpenTelemetryAttributeKeys.DiagnosticNamespace;
        private static CosmosDbEventSource Singleton { get; } = new CosmosDbEventSource();

        private CosmosDbEventSource()
            : base(EventSourceName)
        {
        }

        public static void RecordDiagnostics(CosmosDiagnostics diagnostics)
        {
            if (Singleton.IsEnabled())
            {
                Singleton.SendRequestDiagnostics(diagnostics.ToString());
            }
        }

        /// <summary>
        /// We are generating this event only in specific scenarios where we need this information to be present.
        /// thats why going with LogAlways EventLevel
        /// </summary>
        /// <param name="message"></param>
        [Event(1, Level = EventLevel.LogAlways)]
        private void SendRequestDiagnostics(string message)
        {
            this.WriteEvent(1, message);
        }
    }
}
