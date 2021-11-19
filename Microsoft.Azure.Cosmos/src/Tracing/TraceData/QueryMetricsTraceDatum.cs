// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;

    internal sealed class QueryMetricsTraceDatum : TraceDatum
    {
        private readonly Lazy<QueryMetrics> LazyQueryMetrics;

        public QueryMetricsTraceDatum(Lazy<QueryMetrics> queryMetrics)
        {
            this.LazyQueryMetrics = queryMetrics ?? throw new ArgumentNullException(nameof(queryMetrics));
        }

        public QueryMetrics QueryMetrics => this.LazyQueryMetrics.Value;

        internal override void Accept(ITraceDatumVisitor traceDatumVisitor)
        {
            traceDatumVisitor.Visit(this);
        }
    }
}
