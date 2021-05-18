// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;

    internal sealed class Trace : ITrace
    {
        private static readonly IReadOnlyDictionary<string, object> EmptyDictionary = new Dictionary<string, object>();
        private readonly Lazy<Dictionary<string, object>> data;
        private readonly object mutex = new object();

        // singlechild to avoid List creation for trace objects with only 1 child
        private List<ITrace> children;
        private ITrace singleChild;
        private DateTime? endTime;

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
            this.Level = level;
            this.Component = component;
            this.Parent = parent;
            this.data = new Lazy<Dictionary<string, object>>(() => new Dictionary<string, object>());
        }

        public string Name { get; }

        public Guid Id { get; }

        public CallerInfo CallerInfo { get; }

        public DateTime StartTime { get; }

        public TimeSpan Duration => (this.endTime ?? DateTime.UtcNow) - this.StartTime;

        public TraceLevel Level { get; }

        public TraceComponent Component { get; }

        public ITrace Parent { get; }

        public IEnumerable<ITrace> Children  
        {
            get 
            {
                if (this.children != null)
                {
                    foreach (ITrace child in this.children)
                    {
                        yield return child;
                    }
                }
                else if (this.singleChild != null)
                {
                    yield return this.singleChild;
                }
            }
        }

        public IReadOnlyDictionary<string, object> Data => this.data.IsValueCreated ? this.data.Value : Trace.EmptyDictionary;

        public void Dispose()
        {
            this.endTime = DateTime.UtcNow;
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
            lock (this.mutex)
            {
                if (this.singleChild == null)
                {
                    this.singleChild = child;
                }
                else if (this.children == null)
                {
                    this.children = new List<ITrace> { this.singleChild, child };
                }
                else
                {
                    this.children.Add(child);
                }
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
            this.data.Value.Add(key, traceDatum);
        }

        public void AddDatum(string key, object value)
        {
            this.data.Value.Add(key, value);
        }
    }
}
