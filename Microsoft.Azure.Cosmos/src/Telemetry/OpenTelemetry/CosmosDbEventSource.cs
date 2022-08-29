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

    [EventSource(Name = EventSourceName)]
    internal sealed class CosmosDbEventSource : AzureEventSource
    {
        private const string EventSourceName = "Azure.Cosmos";

        public static CosmosDbEventSource Singleton { get; } = new CosmosDbEventSource();

        public CosmosDbEventSource()
            : base(EventSourceName)
        {
        }

        // we need to avoid serialization when nobody listens
        public static bool IsWarnEnabled
        {
            [NonEvent]
            get => Singleton.IsEnabled(EventLevel.Warning, (EventKeywords)(-1));
        }

        [Event(1, Level = EventLevel.Warning, Message = "{0}")]
        public void RecordRequestDiagnostics(string diagnostics)
        {
            this.WriteEvent(1, diagnostics);
        }
    }
}
