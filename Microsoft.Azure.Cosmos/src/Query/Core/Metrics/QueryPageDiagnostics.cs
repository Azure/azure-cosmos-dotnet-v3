//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Text;
    using Newtonsoft.Json;

    internal sealed class QueryPageDiagnostics
    {
        public QueryPageDiagnostics(
            string partitionKeyRangeId,
            string queryMetricText,
            string indexUtilizationText,
            CosmosDiagnostics requestDiagnostics,
            SchedulingStopwatch schedulingStopwatch)
        {
            this.PartitionKeyRangeId = partitionKeyRangeId ?? throw new ArgumentNullException(nameof(partitionKeyRangeId));
            this.QueryMetricText = queryMetricText ?? string.Empty;
            this.IndexUtilizationText = indexUtilizationText ?? string.Empty;
            this.RequestDiagnostics = requestDiagnostics;
            this.SchedulingTimeSpan = schedulingStopwatch.Elapsed;
        }

        internal string PartitionKeyRangeId { get; }

        internal string QueryMetricText { get; }

        internal string IndexUtilizationText { get; }

        internal CosmosDiagnostics RequestDiagnostics { get; }

        internal SchedulingTimeSpan SchedulingTimeSpan { get; }

        public void AppendToBuilder(JsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException(nameof(jsonWriter));
            }

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("PartitionKeyRangeId");
            jsonWriter.WriteValue(this.PartitionKeyRangeId);
            jsonWriter.WritePropertyName("QueryMetricText");
            jsonWriter.WriteValue(this.QueryMetricText);
            jsonWriter.WritePropertyName("IndexUtilizationText");
            jsonWriter.WriteValue(this.IndexUtilizationText);
            jsonWriter.WritePropertyName("RequestDiagnostics");
            jsonWriter.WriteValue(this.RequestDiagnostics != null ? this.RequestDiagnostics.ToString() : string.Empty);
            jsonWriter.WritePropertyName("SchedulingTimeSpan");
            this.SchedulingTimeSpan.AppendJsonToBuilder(jsonWriter);
        }
    }
}
