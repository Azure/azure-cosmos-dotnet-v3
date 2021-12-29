// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

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

        internal new TimeSpan Duration => this.stopwatch.Elapsed;

        internal new IReadOnlyList<ITrace> Children => this.children;

        internal new IReadOnlyDictionary<string, object> Data => this.data;

        public override void Dispose()
        {
            this.stopwatch.Stop();
        }

        internal override ITrace StartChild(
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

        internal override ITrace StartChild(
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

        internal override void AddChild(ITrace child)
        {
            lock (this.children)
            {
                this.children.Add(child);
                if (child.RegionsContacted != null)
                {
                    this.RegionsContacted = child.RegionsContacted;
                }

            }
        }

        internal static Trace GetRootTrace(string name)
        {
            return Trace.GetRootTrace(
                name,
                component: TraceComponent.Unknown,
                level: TraceLevel.Verbose);
        }

        internal static Trace GetRootTrace(
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

        internal override void AddDatum(string key, TraceDatum traceDatum)
        {
            this.data.Add(key, traceDatum);
            this.UpdateRegionContacted(traceDatum);
        }

        internal override void AddDatum(string key, object value)
        {
            this.data.Add(key, value);
        }
    }
}
