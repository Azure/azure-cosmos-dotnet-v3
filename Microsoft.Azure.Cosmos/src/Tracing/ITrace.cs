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

        IReadOnlyDictionary<string, object> Data { get; }

        ITrace StartChild(string name);

        ITrace StartChild(
            string name,
            TraceComponent component,
            TraceLevel level);

        void AddDatum(string key, ITraceDatum traceDatum);

        void AddDatum(string key, object value);
    }
}
