//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;

    internal sealed class QueryPageDiagnostics : CosmosDiagnosticsInternal
    {
        public QueryPageDiagnostics(
            string partitionKeyRangeId,
            string queryMetricText,
            string indexUtilizationText,
            CosmosDiagnosticsContext diagnosticsContext,
            SchedulingStopwatch schedulingStopwatch)
        {
            this.PartitionKeyRangeId = partitionKeyRangeId ?? throw new ArgumentNullException(nameof(partitionKeyRangeId));
            this.QueryMetricText = queryMetricText ?? string.Empty;
            this.IndexUtilizationText = indexUtilizationText ?? string.Empty;
            this.DiagnosticsContext = diagnosticsContext;
            this.SchedulingTimeSpan = schedulingStopwatch.Elapsed;
        }

        public string PartitionKeyRangeId { get; }

        public string QueryMetricText { get; }

        public string IndexUtilizationText { get; }

        public CosmosDiagnosticsContext DiagnosticsContext { get; }

        public SchedulingTimeSpan SchedulingTimeSpan { get; }

        public override void Accept(CosmosDiagnosticsInternalVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(CosmosDiagnosticsInternalVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }
    }
}
