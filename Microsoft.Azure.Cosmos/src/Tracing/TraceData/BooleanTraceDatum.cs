// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    internal sealed class BooleanTraceDatum : TraceDatum
    {
        public BooleanTraceDatum(bool value)
        {
            this.Value = value;
        }

        public bool Value { get; }

        internal override void Accept(ITraceDatumVisitor traceDatumVisitor)
        {
            traceDatumVisitor.Visit(this);
        }
    }
}
