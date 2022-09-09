// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.Text;
    using global::Azure.Core.Diagnostics;
    using Microsoft.Azure.Cosmos.Diagnostics;

    [EventSource(Name = EventSourceName)]
    internal sealed class CosmosDbEventSource : AzureEventSource
    {
        private const string EventSourceName = OpenTelemetryAttributeKeys.DiagnosticNamespace;
        private static CosmosDbEventSource Singleton { get; } = new CosmosDbEventSource();

        private CosmosDbEventSource()
            : base(EventSourceName)
        {
        }

        // we need to avoid serialization when nobody listens
        public static bool IsErrorEnabled
        {
            [NonEvent]
            get => Singleton.IsEnabled(EventLevel.Error, (EventKeywords)(-1));
        }

        public static void RequestError(CosmosDiagnostics diagnostics)
        {
            if (CosmosDbEventSource.IsErrorEnabled)
            {
                Singleton.RequestError(diagnostics.ToString());
            }
        }

        /// <summary>
        /// We are generating this event only in specific scenarios where this information MUST be present.
        /// thats why going with Error Event Level
        /// </summary>
        /// <param name="message"></param>
        [Event(1, Level = EventLevel.Error)]
        private void RequestError(string message)
        {
            this.WriteEvent(1, message);
        }
    }
}
