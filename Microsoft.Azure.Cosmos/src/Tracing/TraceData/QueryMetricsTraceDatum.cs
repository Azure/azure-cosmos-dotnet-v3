// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;

    internal sealed class QueryMetricsTraceDatum : ITraceDatum
    {
        public QueryMetricsTraceDatum(QueryMetrics queryMetrics)
        {
            this.QueryMetrics = queryMetrics ?? throw new ArgumentNullException(nameof(queryMetrics));
        }

        public QueryMetrics QueryMetrics { get; }

        public void Accept(ITraceDatumVisitor traceDatumVisitor) => traceDatumVisitor.Visit(this);
    }
}
