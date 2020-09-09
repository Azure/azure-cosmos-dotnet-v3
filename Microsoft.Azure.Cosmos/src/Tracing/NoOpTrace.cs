// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    internal sealed class NoOpTrace : ITrace
    {
        public static readonly NoOpTrace Singleton = new NoOpTrace();

        private static readonly StackFrame NoOpStackFrame = new StackFrame();
        private static readonly IReadOnlyList<ITrace> NoOpChildren = new List<ITrace>();
        private static readonly IReadOnlyDictionary<string, object> NoOpData = new Dictionary<string, object>();

        private NoOpTrace()
        {
        }

        public string Name => "NoOp";

        public Guid Id => default;

        public StackFrame StackFrame => NoOpStackFrame;

        public DateTime StartTime => default;

        public TimeSpan Duration => default;

        public TraceLevel Level => default;

        public TraceComponent Component => default;

        public ITrace Parent => null;

        public IReadOnlyList<ITrace> Children => NoOpChildren;

        public IReadOnlyDictionary<string, object> Data => NoOpData;

        public void Dispose()
        {
            // NoOp
        }

        public ITrace StartChild(string name) => this.StartChild(name, component: this.Component, level: TraceLevel.Info);

        public ITrace StartChild(
            string name,
            TraceComponent component,
            TraceLevel level)
        {
            return this;
        }

        public void AddDatum(string key, ITraceDatum traceDatum)
        {
            // NoOp
        }

        public void AddDatum(string key, object value)
        {
            // NoOp
        }
    }
}
