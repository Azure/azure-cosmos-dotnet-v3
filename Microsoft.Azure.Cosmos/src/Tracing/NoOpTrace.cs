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

        private NoOpTrace()
        {
        }

        public string Name => "NoOp";

        public Guid Id => Guid.Empty;

        public StackFrame StackFrame => NoOpStackFrame;

        public DateTime StartTime => DateTime.MinValue;

        public TimeSpan Duration => TimeSpan.Zero;

        public TraceLevel Level => TraceLevel.Verbose;

        public TraceComponent Component => TraceComponent.Unknown;

        public ITrace Parent => null;

        public IReadOnlyList<ITrace> Children => NoOpChildren;

        public ITraceInfo Info { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void Dispose()
        {
            // NoOp
        }

        public ITrace StartChild(string name, TraceLevel level = TraceLevel.Verbose, TraceComponent component = TraceComponent.Unknown)
        {
            return this;
        }
    }
}
