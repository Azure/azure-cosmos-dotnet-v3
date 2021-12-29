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
        private static readonly IReadOnlyDictionary<string, object> NoOpData = new Dictionary<string, object>();
        private static readonly CallerInfo NoOpCallerInfo = new CallerInfo(memberName: "NoOp", filePath: "NoOp", lineNumber: 9001);

        private NoOpTrace()
        {
        }

        internal new string Name => "NoOp";

        internal new Guid Id => default;

        internal new CallerInfo CallerInfo => NoOpCallerInfo;

        internal new DateTime StartTime => default;

        internal new TimeSpan Duration => default;

        internal new TraceLevel Level => default;

        internal new TraceComponent Component => default;

        internal new ITrace Parent => null;

        internal new IReadOnlyList<ITrace> Children => NoOpChildren;

        internal new IReadOnlyDictionary<string, object> Data => NoOpData;

        public override void Dispose()
        {
            // NoOp
        }

        internal override ITrace StartChild(
            string name,
            string memberName = "",
            string sourceFilePath = "",
            int sourceLineNumber = 0)
        {
            return this.StartChild(
                name,
                component: this.Component,
                level: TraceLevel.Info);
        }

        internal override ITrace StartChild(
            string name,
            TraceComponent component,
            TraceLevel level,
            string memberName = "",
            string sourceFilePath = "",
            int sourceLineNumber = 0)
        {
            return this;
        }

        internal override void AddDatum(string key, TraceDatum traceDatum)
        {
            // NoOp
        }

        internal override void AddDatum(string key, object value)
        {
            // NoOp
        }

        internal override void AddChild(ITrace trace)
        {
            // NoOp
        }
    }
}
