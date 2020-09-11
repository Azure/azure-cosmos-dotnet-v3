// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    internal interface ITrace : IDisposable
    {
        string Name { get; }

        Guid Id { get; }

        CallerInfo CallerInfo { get; }

        DateTime StartTime { get; }

        TimeSpan Duration { get; }

        TraceLevel Level { get; }

        TraceComponent Component { get; }

        ITrace Parent { get; }

        IReadOnlyList<ITrace> Children { get; }

        IReadOnlyDictionary<string, object> Data { get; }

        ITrace StartChild(
            string name,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0);

        ITrace StartChild(
            string name,
            TraceComponent component,
            TraceLevel level,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0);

        void AddDatum(string key, ITraceDatum traceDatum);

        void AddDatum(string key, object value);
    }
}
