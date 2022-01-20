// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    internal sealed class Trace : ITrace
    {
        private static readonly IReadOnlyDictionary<string, object> EmptyDictionary = new Dictionary<string, object>();
        private readonly List<ITrace> children;
        private readonly Lazy<Dictionary<string, object>> data;
        private readonly Stopwatch stopwatch;
        private readonly ISet<(string, Uri)> regionContactedInternal;

        private Trace(
            string name,
            TraceLevel level,
            TraceComponent component,
            Trace parent,
            ISet<(string, Uri)> regionContactedInternal)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Id = Guid.NewGuid();
            this.StartTime = DateTime.UtcNow;
            this.stopwatch = Stopwatch.StartNew();
            this.Level = level;
            this.Component = component;
            this.Parent = parent;
            this.children = new List<ITrace>();
            this.data = new Lazy<Dictionary<string, object>>();

            this.regionContactedInternal = regionContactedInternal;
        }

        public string Name { get; }

        public Guid Id { get; }

        public DateTime StartTime { get; }

        public TimeSpan Duration => this.stopwatch.Elapsed;

        public TraceLevel Level { get; }

        public TraceComponent Component { get; }

        public ITrace Parent { get; }

        public IReadOnlyList<ITrace> Children => this.children;

        public IReadOnlyDictionary<string, object> Data => this.data.IsValueCreated ? this.data.Value : Trace.EmptyDictionary;

        public IReadOnlyList<(string, Uri)> RegionsContacted 
        { 
            get
            {
                lock (this.regionContactedInternal)
                {
                    return this.regionContactedInternal.ToList();
                }
            }
        }

        /// <summary>
        /// Update region contacted information to this node
        /// </summary>
        /// <param name="traceDatum"></param>
        public void UpdateRegionContacted(TraceDatum traceDatum)
        {
            if (traceDatum is ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
            {
                if (clientSideRequestStatisticsTraceDatum.RegionsContacted == null || 
                            clientSideRequestStatisticsTraceDatum.RegionsContacted.Count == 0)
                {
                    return;
                }
               
                lock (this.regionContactedInternal)
                {
                    this.regionContactedInternal.UnionWith(clientSideRequestStatisticsTraceDatum.RegionsContacted);
                }
            }
        }

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
            Trace child = new Trace(
                name: name,
                level: level,
                component: component,
                parent: this,
                regionContactedInternal: this.regionContactedInternal);

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
                regionContactedInternal: new HashSet<(string, Uri)>());
        }

        public void AddDatum(string key, TraceDatum traceDatum)
        {
            this.data.Value.Add(key, traceDatum);
            this.UpdateRegionContacted(traceDatum);
        }

        public void AddDatum(string key, object value)
        {
            this.data.Value.Add(key, value);
        }

        public void AddOrUpdateDatum(string key, object value)
        {
            this.data[key] = value;
        }
    }
}
