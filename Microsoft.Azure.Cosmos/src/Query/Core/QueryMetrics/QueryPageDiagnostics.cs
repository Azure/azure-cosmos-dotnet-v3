//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Text;

    internal sealed class QueryPageDiagnostics
    {
        public QueryPageDiagnostics(
            string partitionKeyRangeId,
            string queryMetricText,
            string indexUtilizationText,
            PointOperationStatistics requestDiagnostics)
        {
            this.PartitionKeyRangeId = partitionKeyRangeId ?? throw new ArgumentNullException(nameof(partitionKeyRangeId));
            this.QueryMetricText = queryMetricText ?? throw new ArgumentNullException(nameof(partitionKeyRangeId));
            this.IndexUtilizationText = indexUtilizationText;
            this.RequestDiagnostics = requestDiagnostics ?? throw new ArgumentNullException(nameof(requestDiagnostics));
        }

        internal string PartitionKeyRangeId { get; }

        internal string QueryMetricText { get; }

        internal string IndexUtilizationText { get; }

        internal PointOperationStatistics RequestDiagnostics { get; }

        public void AppendToBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"PartitionKeyRangeId\":\"");
            stringBuilder.Append(this.PartitionKeyRangeId);
            stringBuilder.Append("\",\"QueryMetricText\":\"");
            stringBuilder.Append(this.QueryMetricText);
            stringBuilder.Append("\",\"IndexUtilizationText\":\"");
            stringBuilder.Append(this.IndexUtilizationText);
            stringBuilder.Append("\",\"RequestDiagnostics\":");
            stringBuilder.Append(this.RequestDiagnostics);
            stringBuilder.Append("}");
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            this.AppendToBuilder(stringBuilder);
            return stringBuilder.ToString();
        }
    }
}
