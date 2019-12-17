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

        [JsonProperty(PropertyName = "PartitionKeyRangeId" )]
        internal string PartitionKeyRangeId { get; }

        [JsonProperty(PropertyName = "QueryMetricText")]
        internal string QueryMetricText { get; }

        [JsonProperty(PropertyName = "IndexUtilizationText")]
        internal string IndexUtilizationText { get; }

        [JsonProperty(PropertyName = "RequestDiagnostics")]
        internal CosmosDiagnostics RequestDiagnostics { get; }

        [JsonProperty(PropertyName = "SchedulingTimeSpan")]
        internal SchedulingTimeSpan SchedulingTimeSpan { get; }
    }
}
