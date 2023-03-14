//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using static Microsoft.Azure.Cosmos.Tests.TestListener;

    public class ProducedDiagnosticScope
    {
        public string Name { get; set; }
        public Activity Activity { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsFailed => this.Exception != null;
        public Exception Exception { get; set; }
        public List<ProducedLink> Links { get; set; } = new List<ProducedLink>();
        public List<Activity> LinkedActivities { get; set; } = new List<Activity>();

        public override string ToString()
        {
            return this.Name;
        }
    }
    
    public struct ProducedLink
    {
        public ProducedLink(string id)
        {
            this.Traceparent = id;
            this.Tracestate = null;
        }

        public ProducedLink(string traceparent, string tracestate)
        {
            this.Traceparent = traceparent;
            this.Tracestate = tracestate;
        }

        public string Traceparent { get; set; }
        public string Tracestate { get; set; }
    }
}
