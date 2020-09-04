// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    internal interface ITrace : IDisposable
    {
        string Name { get; }

        Guid Id { get; }

        StackFrame StackFrame { get; }

        DateTime StartTime { get; }

        TimeSpan Duration { get; }

        TraceLevel Level { get; }

        TraceComponent Component { get; }

        ITrace Parent { get; }

        IReadOnlyList<ITrace> Children { get; }

        ITraceInfo Info { get; set; }

        public ITrace StartChild(
            string name,
            TraceLevel level = TraceLevel.Verbose,
            TraceComponent component = TraceComponent.Unknown);
    }
}
