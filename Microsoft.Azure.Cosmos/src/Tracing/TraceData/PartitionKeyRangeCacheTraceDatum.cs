// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    internal class PartitionKeyRangeCacheTraceDatum : TraceDatum
    {
        public string PreviousContinuationToken { get; }
        public string ContinuationToken { get; }

        public PartitionKeyRangeCacheTraceDatum(string previousContinuationToken, string continuationToken)
        {
            this.PreviousContinuationToken = previousContinuationToken; 
            this.ContinuationToken = continuationToken;
        }

        internal override void Accept(ITraceDatumVisitor traceDatumVisitor)
        {
            traceDatumVisitor.Visit(this);
        }
    }
}
