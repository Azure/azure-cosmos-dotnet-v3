﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;

    internal sealed class Trace : ITrace
    {
        private static readonly IReadOnlyDictionary<string, object> EmptyDictionary = new Dictionary<string, object>();
        private readonly List<ITrace> children;
        private readonly ConcurrentDictionary<string, object> data;
        private ValueStopwatch stopwatch;

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
            this.data = new ConcurrentDictionary<string, object>();
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

        public IReadOnlyList<ITrace> Children => this.children;

        public IReadOnlyDictionary<string, object> Data => this.data.Count > 0 ? this.data : Trace.EmptyDictionary;

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
            lock (this.children)
            {
                this.children.Add(child);
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

        /// <summary>
        /// Adds a datum to the this trace instance.
        /// This method is thread-safe.
        /// </summary>
        /// <param name="key">The key to associate the datum.</param>
        /// <param name="traceDatum">The datum itself.</param>
        public void AddDatum(string key, TraceDatum traceDatum)
        {
            this.data.TryAdd(key, traceDatum);
            this.Summary.UpdateRegionContacted(traceDatum);
        }

        /// <summary>
        /// Adds a datum to the this trace instance.
        /// This method is thread-safe.
        /// </summary>
        /// <param name="key">The key to associate the datum.</param>
        /// <param name="value">The datum itself.</param>
        public void AddDatum(string key, object value)
        {
            this.data.TryAdd(key, value);
        }

        /// <summary>
        /// Updates the given datum in this trace instance if exists, otherwise Add
        /// This method is thread-safe.
        /// </summary>
        /// <param name="key">The key to associate the datum.</param>
        /// <param name="value">The datum itself.</param>
        public void AddOrUpdateDatum(string key, object value)
        {
            this.data[key] = value;
        }
    }
}
