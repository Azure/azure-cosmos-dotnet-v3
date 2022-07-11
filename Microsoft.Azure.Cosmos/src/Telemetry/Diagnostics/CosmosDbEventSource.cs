//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.Text;
    using global::Azure.Core.Diagnostics;

    [EventSource(Name = EventSourceName)]
    internal class CosmosDbEventSource : AzureEventSource
    {
        private const string EventSourceName = "Azure.Cosmos";

        private const int RequestDiagnosticEvent = 1;

        private CosmosDbEventSource() 
            : base(EventSourceName) 
        { 
        }

        public CosmosDbEventSource(string eventSourceName)
            : base(eventSourceName)
        {
        }

        public static CosmosDbEventSource Singleton { get; } = new CosmosDbEventSource();

        [Event(RequestDiagnosticEvent, Level = EventLevel.Informational, Message = "{1}")]
        public void RecordRequestDiagnostics(string diagnostics)
        {
            this.WriteEvent(RequestDiagnosticEvent, diagnostics);
        }
    }
}
