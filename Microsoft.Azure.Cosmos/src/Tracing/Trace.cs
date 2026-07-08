// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Microsoft.Azure.Documents;

    internal sealed class Trace : ITrace
    {
        /// <summary>
        /// Default upper bound on the number of child traces retained under a single
        /// trace node. Guards against pathological, effectively unbounded diagnostics
        /// tree growth (for example, a background cross-partition query prefetch loop
        /// retrying transport-generated 410s hundreds/thousands of times, which can
        /// produce a multi-hundred-megabyte diagnostics string). See
        /// https://github.com/Azure/azure-cosmos-dotnet-v3/issues/5325.
        /// </summary>
        private const int DefaultMaxChildCount = 1000;

        /// <summary>
        /// Data key used to surface, on the affected node, how many child traces were
        /// suppressed once <see cref="MaxChildCount"/> was reached. The presence of this
        /// key signals that the node's children (and therefore any Summary aggregated by
        /// walking the tree) were truncated.
        /// </summary>
        internal const string TruncatedChildTraceCountKey = "Truncated Child Trace Count";

        private static readonly IReadOnlyDictionary<string, object> EmptyDictionary = new Dictionary<string, object>();
        private static int maxChildCount = DefaultMaxChildCount;
        private readonly object lockObject;
        private volatile List<ITrace> children;
        private volatile Dictionary<string, object> data;
        private ValueStopwatch stopwatch;
        private volatile bool isBeingWalked;

        /// <summary>
        /// Number of child traces suppressed under this node once the retained-child
        /// limit was reached. Guarded by <see cref="lockObject"/>.
        /// </summary>
        private int suppressedChildCount;

        private Trace(
            string name,
            TraceLevel level,
            TraceComponent component,
            Trace parent,
            TraceSummary summary)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.lockObject = new object();
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

        /// <summary>
        /// Maximum number of child traces retained under a single node before further
        /// children are suppressed. Exposed internally so it can be tuned and tested;
        /// defaults to <see cref="DefaultMaxChildCount"/>. Must be positive.
        /// </summary>
        internal static int MaxChildCount
        {
            get => maxChildCount;
            set => maxChildCount = value > 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), value, "MaxChildCount must be positive.");
        }

        public string Name { get; }

        public Guid Id { get; }

        public DateTime StartTime { get; }

        public TimeSpan Duration => this.stopwatch.Elapsed;

        public TraceLevel Level { get; }

        public TraceComponent Component { get; }

        public TraceSummary Summary { get; }

        public ITrace Parent { get; }

        // NOTE: no lock necessary here only because this.children is volatile
        // and every reference to it is immutable when isBeingWalked == true
        // and isBeingWalked is guaranteed to be set to true before this
        // Property is called
        public IReadOnlyList<ITrace> Children
        {
            get
            {
                // Assert that walking state is set
                Debug.Assert(this.isBeingWalked, "SetWalkingStateRecursively should be set to true");

                return this.children;
            }
        }

        /// <summary>
        /// Number of child traces that were suppressed under this node once
        /// <see cref="MaxChildCount"/> was reached. Zero when the node was not truncated.
        /// </summary>
        internal int SuppressedChildCount => this.suppressedChildCount;

        // NOTE: no lock necessary here only because this.data is volatile
        // and every reference to it is immutable when isBeingWalked == true
        // and isBeingWalked is guaranteed to be set to true before this
        // Property is called
        public IReadOnlyDictionary<string, object> Data
        {
            get
            {
                // Assert that walking state is set
                Debug.Assert(this.isBeingWalked, "SetWalkingStateRecursively should be set to true");

                return this.data ?? Trace.EmptyDictionary;
            }
        }

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

            // Guardrail against unbounded diagnostics tree growth (issue #5325):
            // once this node has retained MaxChildCount children (for example a
            // background prefetch loop retrying transport-generated 410s), stop
            // building and retaining new subtrees under it. The returned NoOpTrace
            // shares this node's TraceSummary so imperatively-updated aggregates
            // (failed count, hedging, regions contacted) from the suppressed subtree
            // are still recorded. This Count read is an unsynchronized fast path that
            // avoids allocating a child when the node is already full; the suppression
            // itself still takes the lock (RecordSuppressedChild), and TryAddChild
            // re-checks and enforces the limit authoritatively under the lock.
            if (this.children.Count >= MaxChildCount)
            {
                this.RecordSuppressedChild();
                return new NoOpTrace(this.Summary);
            }

            Trace child = new Trace(
                name: name,
                level: level,
                component: component,
                parent: this,
                summary: this.Summary);

            // Enforce the limit atomically. If the node filled up concurrently between
            // the lock-free pre-check above and here, suppress rather than orphan the
            // child (returning it would hand the caller a node never added to the tree).
            if (!this.TryAddChild(child))
            {
                return new NoOpTrace(this.Summary);
            }

            return child;
        }

        public void AddChild(ITrace child)
        {
            this.TryAddChild(child);
        }

        // Adds a child under the retained-child limit. Returns false (and records the
        // suppression) when the node is already at capacity. Single choke point for
        // both StartChild and direct AddChild callers.
        private bool TryAddChild(ITrace child)
        {
            lock (this.lockObject)
            {
                // Guardrail against unbounded diagnostics tree growth (issue #5325).
                // Applies to both StartChild and direct AddChild callers (for example
                // batch grafting a pre-built subtree). A dropped subtree is not a silent
                // metric loss: the truncation is surfaced on this node (see
                // RecordSuppressedChildUnderLock) and rolls up to Summary.PartialResults,
                // so the walk-computed histogram counts are explicitly flagged as lower
                // bounds when a node is truncated.
                if (this.children.Count >= MaxChildCount)
                {
                    this.RecordSuppressedChildUnderLock();
                    return false;
                }

                if (!this.isBeingWalked)
                {
                    this.children.Add(child);

                    return true;
                }

                if (child is Trace traceChild)
                {
                    traceChild.SetWalkingStateRecursively();
                }

                List<ITrace> writableSnapshot = new List<ITrace>(this.children.Count + 1);
                writableSnapshot.AddRange(this.children);
                writableSnapshot.Add(child);
                this.children = writableSnapshot;
                return true;
            }
        }

        // Records that a child trace was suppressed because this node reached the
        // retained-child limit. Acquires the lock; used from the lock-free StartChild
        // fast path.
        private void RecordSuppressedChild()
        {
            lock (this.lockObject)
            {
                this.RecordSuppressedChildUnderLock();
            }
        }

        // Caller must hold this.lockObject.
        private void RecordSuppressedChildUnderLock()
        {
            this.suppressedChildCount++;
            int count = this.suppressedChildCount;

            // Surface the truncation on the node so consumers can see the diagnostics
            // tree was bounded. Once materialization has started, copy-on-write so
            // concurrent walkers see a consistent snapshot (mirrors AddOrUpdateDatum).
            if (this.isBeingWalked)
            {
                this.data = this.data == null
                    ? new Dictionary<string, object> { [TruncatedChildTraceCountKey] = count }
                    : new Dictionary<string, object>(this.data) { [TruncatedChildTraceCountKey] = count };
                return;
            }

            this.data ??= new Dictionary<string, object>();
            this.data[TruncatedChildTraceCountKey] = count;
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
            this.Summary.UpdateRegionContacted(traceDatum);
            this.AddDatum(key, traceDatum as Object);
        }

        public void AddDatum(string key, object value)
        {
            lock (this.lockObject)
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
            lock (this.lockObject)
            {
                this.data ??= new Dictionary<string, object>();

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
            if (this.isBeingWalked)
            {
                return; // Already set, return early
            }

            lock (this.lockObject)
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

                // Set the walking state for this trace after processing children
                this.isBeingWalked = true;
            }
        }

        bool ITrace.TryGetDatum(string key, out object datum)
        {
            lock (this.lockObject)
            {
                if (this.data == null)
                {
                    datum = null;
                    return false;
                }

                return this.data.TryGetValue(key, out datum);
            }
        }
    }
}
