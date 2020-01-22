//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
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

        internal override void WriteJsonObject(JsonWriter jsonWriter)
        {
            jsonWriter.WriteStartObject();

            jsonWriter.WritePropertyName("PKRangeId");
            jsonWriter.WriteValue(this.PartitionKeyRangeId);

            jsonWriter.WritePropertyName("QueryMetric");
            jsonWriter.WriteValue(this.QueryMetricText);

            jsonWriter.WritePropertyName("IndexUtilization");
            jsonWriter.WriteValue(this.IndexUtilizationText);

            jsonWriter.WritePropertyName("SchedulingTimeSpan");
            this.SchedulingTimeSpan.WriteJsonObject(jsonWriter);

            jsonWriter.WritePropertyName("Context");
            jsonWriter.WriteStartArray();
            this.DiagnosticsContext.ContextList.WriteJsonObject(jsonWriter);
            jsonWriter.WriteEndArray();

            jsonWriter.WriteEndObject();
        }
    }
}
