//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    internal sealed class QueryPageDiagnostics
    {
        public QueryPageDiagnostics(
            string partitionKeyRangeId,
            string queryMetricText,
            string indexUtilizationText,
            PointOperationStatistics requestDiagnostics)
        {
            this.PartitionKeyRangeId = partitionKeyRangeId;
            this.QueryMetricText = queryMetricText;
            this.IndexUtilizationText = indexUtilizationText;
            this.RequestDiagnostics = requestDiagnostics;
        }

        public string PartitionKeyRangeId { get; }

        public string QueryMetricText { get; }

        public string IndexUtilizationText { get; }

        public PointOperationStatistics RequestDiagnostics { get; }
    }
}
