//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Text;
    using Newtonsoft.Json;

    internal sealed class QueryPageDiagnostics : CosmosDiagnosticWriter
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

        internal string PartitionKeyRangeId { get; }

        internal string QueryMetricText { get; }

        internal string IndexUtilizationText { get; }

        internal CosmosDiagnosticsContext DiagnosticsContext { get; }

        internal SchedulingTimeSpan SchedulingTimeSpan { get; }

        internal override void WriteJsonObject(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"PKRangeId\":\"");
            stringBuilder.Append(this.PartitionKeyRangeId);
            stringBuilder.Append("\",\"QueryMetric\":\"");
            stringBuilder.Append(this.QueryMetricText);
            stringBuilder.Append("\",\"IndexUtilization\":\"");
            stringBuilder.Append(this.IndexUtilizationText);
            stringBuilder.Append("\",\"SchedulingTimeSpan\":");
            this.SchedulingTimeSpan.AppendJsonObjectToBuilder(stringBuilder);
            stringBuilder.Append(",\"Context\":[");
            this.DiagnosticsContext.ContextList.WriteJsonObject(stringBuilder);
            stringBuilder.Append("]}");
        }
    }
}
