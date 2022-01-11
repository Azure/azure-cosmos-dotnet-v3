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
    using Microsoft.Azure.Documents;

    internal sealed class Trace : ITrace
    {
        private readonly List<ITrace> children;
        private readonly Dictionary<string, object> data;
        private readonly Stopwatch stopwatch;

        private Trace(
            string name,
            CallerInfo callerInfo,
            TraceLevel level,
            TraceComponent component,
            Trace parent)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Id = Guid.NewGuid();
            this.CallerInfo = callerInfo;
            this.StartTime = DateTime.UtcNow;
            this.stopwatch = Stopwatch.StartNew();
            this.Level = level;
            this.Component = component;
            this.Parent = parent;
            this.children = new List<ITrace>();
            this.data = new Dictionary<string, object>();
        }

        public string Name { get; }

        public Guid Id { get; }

        public CallerInfo CallerInfo { get; }

        public DateTime StartTime { get; }

        public TimeSpan Duration => this.stopwatch.Elapsed;

        public TraceLevel Level { get; }

        public TraceComponent Component { get; }

        public ITrace Parent { get; }

        public IReadOnlyList<ITrace> Children => this.children;

        public IReadOnlyDictionary<string, object> Data => this.data;

        private ISet<(string, Uri)> RegionsContactedTemporary { get; set; }

        /// <summary>
        /// Consolidated Region contacted Information of this and children nodes
        /// </summary>
        public IReadOnlyList<(string, Uri)> RegionsContacted
        {
            get => this.RegionsContactedTemporary?.ToList();
            set
            {
                if (this.Parent != null)
                {
                    this.Parent.RegionsContacted = value;
                }
                else
                {
                    // Once root is found, collect region contacted information
                    if (this.RegionsContactedTemporary == null)
                    {
                        this.RegionsContactedTemporary = new HashSet<(string, Uri)>(value);
                    }
                    else
                    {
                        this.RegionsContactedTemporary.UnionWith(value);
                    }
                }
            }
        }

        /// <summary>
        /// Update region contacted information to the parent Itrace
        /// </summary>
        /// <param name="clientSideRequestStatisticsTraceDatum"></param>
        public void UpdateRegionContacted(IClientSideRequestStatistics clientSideRequestStatisticsTraceDatum)
        {
            if (clientSideRequestStatisticsTraceDatum.RegionsContacted == null || clientSideRequestStatisticsTraceDatum.RegionsContacted.Count == 0)
            {
                return;
            }
            this.RegionsContacted = clientSideRequestStatisticsTraceDatum.RegionsContacted?.ToList();
        }

        public void Dispose()
        {
            this.stopwatch.Stop();
        }

        public ITrace StartChild(
            string name,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            return this.StartChild(
                name,
                level: TraceLevel.Verbose,
                component: this.Component,
                memberName: memberName,
                sourceFilePath: sourceFilePath,
                sourceLineNumber: sourceLineNumber);
        }

        public ITrace StartChild(
            string name,
            TraceComponent component,
            TraceLevel level,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Trace child = new Trace(
                name: name,
                callerInfo: new CallerInfo(memberName, sourceFilePath, sourceLineNumber),
                level: level,
                component: component,
                parent: this);

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
            TraceLevel level,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            return new Trace(
                name: name,
                callerInfo: new CallerInfo(memberName, sourceFilePath, sourceLineNumber),
                level: level,
                component: component,
                parent: null);
        }

        public void AddDatum(string key, TraceDatum traceDatum)
        {
            this.data.Add(key, traceDatum);
            if (traceDatum is ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
            {
                this.UpdateRegionContacted(clientSideRequestStatisticsTraceDatum);
            }
        }

        public void AddDatum(string key, object value)
        {
            this.data.Add(key, value);
        }
    }
}
