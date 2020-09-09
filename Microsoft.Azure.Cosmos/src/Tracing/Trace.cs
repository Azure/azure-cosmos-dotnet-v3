// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    internal sealed class Trace : ITrace
    {
        private readonly List<Trace> children;
        private readonly Dictionary<string, object> data;
        private readonly Stopwatch stopwatch;

        private Trace(
            string name,
            StackFrame stackFrame,
            TraceLevel level,
            TraceComponent component,
            Trace parent)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Id = Guid.NewGuid();
            this.StackFrame = stackFrame ?? throw new ArgumentNullException(nameof(name));
            this.StartTime = DateTime.UtcNow;
            this.stopwatch = Stopwatch.StartNew();
            this.Level = level;
            this.Component = component;
            this.Parent = parent;
            this.children = new List<Trace>();
            this.data = new Dictionary<string, object>();
        }

        public string Name { get; }

        public Guid Id { get; }

        public StackFrame StackFrame { get; }

        public DateTime StartTime { get; }

        public TimeSpan Duration => this.stopwatch.Elapsed;

        public TraceLevel Level { get; }

        public TraceComponent Component { get; }

        public ITrace Parent { get; }

        public IReadOnlyList<ITrace> Children => this.children;

        public IReadOnlyDictionary<string, object> Data => this.data;

        public void Dispose()
        {
            this.stopwatch.Stop();
        }

        public ITrace StartChild(string name) => this.StartChild(
            name,
            level: TraceLevel.Verbose,
            component: this.Component);

        public ITrace StartChild(
            string name,
            TraceComponent component,
            TraceLevel level)
        {
            Trace child = new Trace(
                name: name,
                stackFrame: new StackFrame(skipFrames: 1, fNeedFileInfo: true),
                level: level,
                component: component,
                parent: this);
            this.children.Add(child);
            return child;
        }

        public static Trace GetRootTrace(string name) => Trace.GetRootTrace(
            name,
            component: TraceComponent.Unknown,
            level: TraceLevel.Verbose);

        public static Trace GetRootTrace(
            string name,
            TraceComponent component,
            TraceLevel level) => new Trace(
                name: name,
                stackFrame: new StackFrame(skipFrames: 1, fNeedFileInfo: true),
                level: level,
                component: component,
                parent: null);

        public void AddDatum(string key, ITraceDatum traceDatum) => this.data.Add(key, traceDatum);

        public void AddDatum(string key, object value) => this.data.Add(key, value);
    }
}
