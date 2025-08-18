// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;

    internal sealed class Trace : ITrace
    {
        private static readonly IReadOnlyDictionary<string, object> EmptyDictionary = new Dictionary<string, object>();
        private volatile List<ITrace> children;
        private volatile Dictionary<string, object> data;
        private ValueStopwatch stopwatch;
        private volatile bool isBeingWalked;

        private Trace(
            string name,
            TraceLevel level,
            TraceComponent component,
            Trace parent,
            TraceSummary summary)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Id = Guid.NewGuid();
            this.StartTime = DateTime.UtcNow;
            this.stopwatch = ValueStopwatch.StartNew();
            this.Level = level;
            this.Component = component;
            this.Parent = parent;
            this.children = new List<ITrace>();
            this.data = null;
            this.Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        }

        public string Name { get; }

        public Guid Id { get; }

        public DateTime StartTime { get; }

        public TimeSpan Duration => this.stopwatch.Elapsed;

        public TraceLevel Level { get; }

        public TraceComponent Component { get; }

        public TraceSummary Summary { get; }

        public ITrace Parent { get; }

        // NOTE: no lock necessary here only because every reference to any this.children instance is immutable when isBeingWalked == true
        // and isBeingWalked is guaranteed to be set to true before this Property is called
        public IReadOnlyList<ITrace> Children => this.children;

        // NOTE: no lock necessary here only because every reference to any this.data instance is immutable when isBeingWalked == true
        // and isBeingWalked is guaranteed to be set to true before this Property is called
        public IReadOnlyDictionary<string, object> Data => this.data ?? Trace.EmptyDictionary;

        public bool IsBeingWalked => this.isBeingWalked;

        public void Dispose()
        {
            this.stopwatch.Stop();
        }

        public ITrace StartChild(
            string name)
        {
            return this.StartChild(
                name,
                level: TraceLevel.Verbose,
                component: this.Component);
        }

        public ITrace StartChild(
            string name,
            TraceComponent component,
            TraceLevel level)
        {
            if (this.Parent != null && !this.stopwatch.IsRunning)
            {
                return this.Parent.StartChild(name, component, level);
            }

            Trace child = new Trace(
                name: name,
                level: level,
                component: component,
                parent: this,
                summary: this.Summary);

            this.AddChild(child);

            return child;
        }

        public void AddChild(ITrace child)
        {
            lock (this.Name)
            {
                if (!this.isBeingWalked)
                {
                    this.children.Add(child);

                    return;
                }

                List<ITrace> writableSnapshot = new List<ITrace>(this.children.Count + 1);
                writableSnapshot.AddRange(this.children);
                writableSnapshot.Add(child);
                this.children = writableSnapshot;
            }
        }

        public static Trace GetRootTrace(string name)
        {
            return Trace.GetRootTrace(
                name,
                component: TraceComponent.Unknown,
                level: TraceLevel.Verbose);
        }

        public static Trace GetRootTrace(
            string name,
            TraceComponent component,
            TraceLevel level)
        {
            return new Trace(
                name: name,
                level: level,
                component: component,
                parent: null,
                summary: new TraceSummary());
        }

        public void AddDatum(string key, TraceDatum traceDatum)
        {
            lock (this.Name)
            {
                this.data ??= new Dictionary<string, object>();

                if (!this.isBeingWalked)
                {
                    // If materialization has not started yet no cloning is needed
                    this.data.Add(key, traceDatum);
                    return; 
                }

                this.data = new Dictionary<string, object>(this.data)
                {
                    { key, traceDatum }
                };
            }
        }

        public void AddDatum(string key, object value)
        {
            lock (this.Name)
            {
                this.data ??= new Dictionary<string, object>();

                if (!this.isBeingWalked)
                {
                    // If materialization has not started yet no cloning is needed
                    this.data.Add(key, value);
                    return;
                }

                this.data = new Dictionary<string, object>(this.data)
                {
                    { key, value }
                };
            }
        }

        public void AddOrUpdateDatum(string key, object value)
        {
            lock (this.Name)
            {
                if (!this.isBeingWalked)
                {
                    // If materialization has not started yet no cloning is needed
                    this.data[key] = value;
                    return; // Ignore modifications while being walked
                }

                this.data = new Dictionary<string, object>(this.data)
                {
                    [key] = value
                };
            }
        }

        internal void SetWalkingStateRecursively()
        {
            lock (this.Name)
            {
                if (this.isBeingWalked)
                {
                    return; // Already set, return early
                }

                foreach (ITrace child in this.children)
                {
                    if (child is Trace concreteChild)
                    {
                        concreteChild.SetWalkingStateRecursively();
                    }
                }
            }

            // Set the walking state for this trace after processing children
            this.isBeingWalked = true;
        }
    }
}
