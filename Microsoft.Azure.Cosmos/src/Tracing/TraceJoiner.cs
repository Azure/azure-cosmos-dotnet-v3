// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;

    internal static class TraceJoiner
    {
        public static ITrace JoinTraces(params ITrace[] traces)
        {
            if (traces == null)
            {
                throw new ArgumentNullException(nameof(traces));
            }

            return JoinTraces(traces.ToList());
        }

        public static ITrace JoinTraces(IReadOnlyList<ITrace> traces)
        {
            if (traces == null)
            {
                throw new ArgumentNullException(nameof(traces));
            }

            TraceForest traceForest = new TraceForest(traces.ToList());
            return traceForest;
        }

        private sealed class TraceForest : ITrace
        {
            private static readonly CallerInfo EmptyInfo = new CallerInfo(string.Empty, string.Empty, 0);

            private readonly Dictionary<string, object> data;

            private readonly List<ITrace> children;

            public TraceForest(IReadOnlyList<ITrace> children)
            {
                this.children = new List<ITrace>(children);
                this.data = new Dictionary<string, object>();
            }

            public string Name => "Trace Forest";

            public Guid Id => Guid.Empty;

            public CallerInfo CallerInfo => EmptyInfo;

            public DateTime StartTime => DateTime.MinValue;

            public TimeSpan Duration => TimeSpan.MaxValue;

            public TraceLevel Level => TraceLevel.Info;

            public TraceComponent Component => TraceComponent.Unknown;

            public ITrace Parent => null;

            public IReadOnlyList<ITrace> Children => this.children;

            public IReadOnlyDictionary<string, object> Data => this.data;

            public void AddDatum(string key, TraceDatum traceDatum)
            {
                this.data[key] = traceDatum;
            }

            public void AddDatum(string key, object value)
            {
                this.data[key] = value;
            }

            public void Dispose()
            {
            }

            public ITrace StartChild(string name, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
            {
                return this.StartChild(name, TraceComponent.Unknown, TraceLevel.Info, memberName, sourceFilePath, sourceLineNumber);
            }

            public ITrace StartChild(string name, TraceComponent component, TraceLevel level, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
            {
                ITrace child = Trace.GetRootTrace(name, component, level, memberName, sourceFilePath, sourceLineNumber);
                this.children.Add(child);
                return child;
            }
        }
    }
}
