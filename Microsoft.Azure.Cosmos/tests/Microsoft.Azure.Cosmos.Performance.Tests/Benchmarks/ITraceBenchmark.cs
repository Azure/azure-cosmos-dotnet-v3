// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    [MemoryDiagnoser]
    public class ITraceBenchmark
    {
        [Benchmark]
        public void OptimizedITrace()
        {
            this.CreateITraceTree("optimized");
        }

        [Benchmark]
        public void OldITrace()
        {
            this.CreateITraceTree("unoptimized");
        }

        private ITrace CreateITraceTree(string traceType)
        {
            ITrace root;
            using (root = this.GetRootTrace(traceType))
            {
                using (ITrace firstlevel = root.StartChild("first"))
                {
                    using (ITrace secondLevel = root.StartChild("second"))
                    {
                        using (ITrace thirdLevel = root.StartChild("third"))
                        {
                            using (ITrace fourthLevel = root.StartChild("fourth"))
                            {

                            }
                            using (ITrace fourthLevel = root.StartChild("fourth"))
                            {

                            }
                            using (ITrace fourthLevel = root.StartChild("fourth"))
                            {

                            }
                        }
                    }
                }
            }

            return root;
        }

        private ITrace GetRootTrace(string traceType)
        {
            if (traceType == "optimized")
            {
                return Trace.GetRootTrace("RootTrace");
            }
            else
            {
                return UnoptimizedITrace.GetRootTrace();
            }
        }

        private sealed class UnoptimizedITrace : ITrace
        {
            public readonly Dictionary<string, object> data;
            public readonly List<ITrace> children;

            public UnoptimizedITrace(
                string name,
                TraceLevel level,
                TraceComponent component,
                UnoptimizedITrace parent)
            {
                this.Name = name ?? throw new ArgumentNullException(nameof(name));
                this.Level = level;
                this.Component = component;
                this.Parent = parent;
                this.children = new List<ITrace>();
                this.data = new Dictionary<string, object>();
            }

            public string Name { get; }

            public Guid Id => Guid.Empty;

            public CallerInfo CallerInfo => new CallerInfo("MemberName", "FilePath", 42);

            public DateTime StartTime => DateTime.MinValue;

            public TimeSpan Duration => TimeSpan.Zero;

            public TraceLevel Level { get; }

            public TraceComponent Component { get; }

            public ITrace Parent { get; }

            public IEnumerable<ITrace> Children => this.children;

            public IReadOnlyDictionary<string, object> Data => this.data;

            public void AddDatum(string key, TraceDatum traceDatum)
            {
                this.data[key] = traceDatum;
            }

            public void AddDatum(string key, object value)
            {
                if (key.Contains("CPU"))
                {
                    // Redacted To Not Change The Baselines From Run To Run
                    return;
                }

                this.data[key] = "Redacted To Not Change The Baselines From Run To Run";
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
                UnoptimizedITrace child = new UnoptimizedITrace(name, level, component, parent: this);
                this.AddChild(child);
                return child;
            }

            public void AddChild(ITrace trace)
            {
                this.children.Add(trace);
            }

            public static UnoptimizedITrace GetRootTrace()
            {
                return new UnoptimizedITrace("Trace For Perf Testing", TraceLevel.Info, TraceComponent.Unknown, parent: null);
            }
        }
    }
}