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
        private static readonly DateTime globalStartTime = DateTime.UtcNow;
        private static readonly Stopwatch globalStopwatch = Stopwatch.StartNew();

        private readonly List<Trace> children;
        private TimeSpan? duration;

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
            this.StartTime = globalStartTime.AddTicks(globalStopwatch.ElapsedTicks);
            this.Level = level;
            this.Component = component;
            this.Parent = parent;
            this.children = new List<Trace>();
        }

        public string Name { get; }

        public Guid Id { get; }

        public StackFrame StackFrame { get; }

        public DateTime StartTime { get; }

        public TimeSpan Duration => this.duration.Value;

        public TraceLevel Level { get; }

        public TraceComponent Component { get; }

        public ITrace Parent { get; }

        public IReadOnlyList<ITrace> Children => this.children;

        public ITraceInfo Info { get; set; }

        public void Dispose()
        {
            this.duration = globalStartTime.AddTicks(globalStopwatch.ElapsedTicks) - this.StartTime;
        }

        public ITrace StartChild(
            string name,
            TraceLevel level = TraceLevel.Verbose,
            TraceComponent? component = null)
        {
            Trace child = new Trace(
                name: name,
                stackFrame: new StackFrame(skipFrames: 1, fNeedFileInfo: true),
                level: level,
                component: component ?? this.Component,
                parent: this);
            this.children.Add(child);
            return child;
        }

        public static Trace GetRootTrace(
            string name,
            TraceLevel level = TraceLevel.Verbose,
            TraceComponent component = TraceComponent.Unknown) => new Trace(
                name: name,
                stackFrame: new StackFrame(skipFrames: 1, fNeedFileInfo: true),
                level: level,
                component: component,
                parent: null);
    }
}
