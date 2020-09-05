// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceInfos
{
    using System;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;

    internal sealed class QueryMetricsTraceInfo : ITraceInfo
    {
        private readonly QueryMetrics queryMetrics;

        public QueryMetricsTraceInfo(QueryMetrics queryMetrics)
        {
            this.queryMetrics = queryMetrics ?? throw new ArgumentNullException(nameof(queryMetrics));
        }

        public string Serialize() => this.queryMetrics.ToString();
    }
}
