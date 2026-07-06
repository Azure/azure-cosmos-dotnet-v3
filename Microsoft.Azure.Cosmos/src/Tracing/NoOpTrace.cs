// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;

    internal sealed class NoOpTrace : ITrace
    {
        // NoOpTraceSummary must be initialized before Singleton: the parameterless
        // constructor chains to NoOpTrace(NoOpTraceSummary), and static field
        // initializers run in textual order. If Singleton were declared first,
        // NoOpTraceSummary would still be null when the singleton is built, leaving
        // Singleton.Summary permanently null and NREing every caller that reads it
        // (for example TransportHandler.ProcessMessageAsync's Summary.UpdateRegionContacted).
        public static readonly TraceSummary NoOpTraceSummary = new TraceSummary();
        public static readonly NoOpTrace Singleton = new NoOpTrace();

        private static readonly IReadOnlyList<ITrace> NoOpChildren = new List<ITrace>();

        private static readonly IReadOnlyDictionary<string, object> NoOpData = new Dictionary<string, object>();

        private readonly TraceSummary summary;

        private NoOpTrace()
            : this(NoOpTraceSummary)
        {
        }

        // Used when a real trace suppresses an over-limit child (see Trace.MaxChildCount):
        // the returned no-op trace shares the operation's TraceSummary so imperatively
        // updated aggregates (failed request count, hedging detection state, regions
        // contacted) are still recorded even though the suppressed subtree is not retained.
        internal NoOpTrace(TraceSummary summary)
        {
            this.summary = summary ?? NoOpTraceSummary;
        }

        public string Name => "NoOp";

        public Guid Id => default;

        public DateTime StartTime => default;

        public TimeSpan Duration => default;

        public TraceLevel Level => default;

        public TraceSummary Summary => this.summary;

        public TraceComponent Component => default;

        public ITrace Parent => null;

        public IReadOnlyList<ITrace> Children => NoOpChildren;

        public IReadOnlyDictionary<string, object> Data => NoOpData;

        public bool IsBeingWalked => true; // this needs to return true to allow materialization of NoOpTrace

        public void Dispose()
        {
            // NoOp
        }

        public ITrace StartChild(
            string name)
        {
            return this.StartChild(
                name,
                component: this.Component,
                level: TraceLevel.Info);
        }

        public ITrace StartChild(
            string name,
            TraceComponent component,
            TraceLevel level)
        {
            return this;
        }

        public void AddDatum(string key, TraceDatum traceDatum)
        {
            // NoOp
        }

        public void AddDatum(string key, object value)
        {
            // NoOp
        }

        public void AddChild(ITrace trace)
        {
            // NoOp
        }

        public void AddOrUpdateDatum(string key, object value)
        {
            // NoOp
        }

        public void UpdateRegionContacted(TraceDatum traceDatum)
        {
            // NoOp
        }

        bool ITrace.TryGetDatum(string key, out object datum)
        {
            datum = null;
            return false;
        }
    }
}
