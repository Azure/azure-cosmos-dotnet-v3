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
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

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

        private sealed class TraceForest : TraceBase
        {
            private static readonly CallerInfo EmptyInfo = new CallerInfo(string.Empty, string.Empty, 0);

            private readonly Dictionary<string, object> data;

            private readonly List<ITrace> children;

            public TraceForest(IReadOnlyList<ITrace> children)
            {
                this.children = new List<ITrace>(children);
                this.data = new Dictionary<string, object>();

                HashSet<(string, Uri)> regionsList = new HashSet<(string, Uri)>();
                foreach (ITrace trace in children)
                {
                    regionsList.UnionWith(trace.RegionsContacted);
                }

                this.RegionsContacted = regionsList;
            }

            public override string Name => "Trace Forest";

            public override Guid Id => Guid.Empty;

            public override CallerInfo CallerInfo => EmptyInfo;

            public override DateTime StartTime => DateTime.MinValue;

            public override TimeSpan Duration => TimeSpan.MaxValue;

            public override TraceLevel Level => TraceLevel.Info;

            public override TraceComponent Component => TraceComponent.Unknown;

            public override ITrace Parent => null;

            public override IReadOnlyList<ITrace> Children => this.children;

            public override IReadOnlyDictionary<string, object> Data => this.data;

            public override void AddDatum(string key, TraceDatum traceDatum)
            {
                this.data[key] = traceDatum;
                this.UpdateRegionContacted(traceDatum);
            }

            public override void AddDatum(string key, object value)
            {
                this.data[key] = value;
            }

            public override void Dispose()
            {
            }

            public override ITrace StartChild(string name, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
            {
                return this.StartChild(name, TraceComponent.Unknown, TraceLevel.Info, memberName, sourceFilePath, sourceLineNumber);
            }

            public override ITrace StartChild(string name, TraceComponent component, TraceLevel level, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
            {
                ITrace child = Trace.GetRootTrace(name, component, level, memberName, sourceFilePath, sourceLineNumber);
                this.AddChild(child);
                return child;
            }

            public override void AddChild(ITrace trace)
            {
                this.children.Add(trace);
                if (trace.RegionsContacted != null)
                {
                    this.RegionsContacted = trace.RegionsContacted;
                }
            }
        }
    }
}
