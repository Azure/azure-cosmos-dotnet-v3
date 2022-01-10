// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;

    internal sealed class NoOpTrace : TraceBase
    {
        public static readonly NoOpTrace Singleton = new NoOpTrace();

        private static readonly IReadOnlyList<ITrace> NoOpChildren = new List<ITrace>();
        private static readonly IReadOnlyDictionary<string, object> NoOpData = new Dictionary<string, object>();
        private static readonly CallerInfo NoOpCallerInfo = new CallerInfo(memberName: "NoOp", filePath: "NoOp", lineNumber: 9001);

        private NoOpTrace()
        {
        }

        public override string Name
        {
            get => "NoOp";
            set
            {
            }
        }

        public override Guid Id => default;

        public override CallerInfo CallerInfo => NoOpCallerInfo;

        public override DateTime StartTime => default;

        public override TimeSpan Duration => default;

        public override TraceLevel Level => default;

        public override TraceComponent Component => default;

        public override ITrace Parent => null;

        public override IReadOnlyList<ITrace> Children => NoOpChildren;

        public override IReadOnlyDictionary<string, object> Data => NoOpData;

        public override void Dispose()
        {
            // NoOp
        }

        public override ITrace StartChild(
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

        public override ITrace StartChild(
            string name,
            TraceComponent component,
            TraceLevel level,
            string memberName = "",
            string sourceFilePath = "",
            int sourceLineNumber = 0)
        {
            return this;
        }

        public override void AddDatum(string key, TraceDatum traceDatum)
        {
            // NoOp
        }

        public override void AddDatum(string key, object value)
        {
            // NoOp
        }

        public override void AddChild(ITrace trace)
        {
            // NoOp
        }
    }
}
