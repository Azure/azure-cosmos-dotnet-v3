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
            private readonly Dictionary<string, object> data;

            private readonly List<ITrace> children;

            public TraceForest(IReadOnlyList<ITrace> children)
            {
                this.children = new List<ITrace>(children);
                this.data = new Dictionary<string, object>();
            }

            public string Name => "Trace Forest";

            public Guid Id => Guid.Empty;

            public DateTime StartTime => DateTime.MinValue;

            public TimeSpan Duration => TimeSpan.MaxValue;

            public TraceLevel Level => TraceLevel.Info;

            public TraceComponent Component => TraceComponent.Unknown;

            public ITrace Parent => null;

            public IReadOnlyList<ITrace> Children => this.children;

            public IReadOnlyDictionary<string, object> Data => this.data;

            public IReadOnlyList<(string, Uri)> RegionsContacted => new List<(string, Uri)>();

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

            public ITrace StartChild(string name)
            {
                return this.StartChild(name, TraceComponent.Unknown, TraceLevel.Info);
            }

            public ITrace StartChild(string name, TraceComponent component, TraceLevel level)
            {
                ITrace child = Trace.GetRootTrace(name, component, level);
                this.AddChild(child);
                return child;
            }

            public void AddChild(ITrace trace)
            {
                this.children.Add(trace);
            }

            public void UpdateRegionContacted(TraceDatum traceDatum)
            {
                //NoImplementation
            }

            public void AddOrUpdateDatum(string key, object value)
            {
                this.data[key] = value;
            }
        }
    }
}
