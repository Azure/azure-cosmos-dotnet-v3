// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;

    internal sealed class NoOpTrace : ITrace
    {
        public static readonly NoOpTrace Singleton = new NoOpTrace();

        private static readonly IReadOnlyList<ITrace> NoOpChildren = new List<ITrace>();
        private static readonly IReadOnlyList<(string, Uri)> NoOpRegionsContacted = new List<(string, Uri)>();

        private static readonly IReadOnlyDictionary<string, object> NoOpData = new Dictionary<string, object>();

        private NoOpTrace()
        {
        }

        public string Name => "NoOp";

        public Guid Id => default;

        public DateTime StartTime => default;

        public TimeSpan Duration => default;

        public TraceLevel Level => default;

        public TraceSummary Summary => default;

        public TraceComponent Component => default;

        public ITrace Parent => null;

        public IReadOnlyList<ITrace> Children => NoOpChildren;

        public IReadOnlyDictionary<string, object> Data => NoOpData;

        public IReadOnlyList<(string, Uri)> RegionsContacted => NoOpRegionsContacted;

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
    }
}
