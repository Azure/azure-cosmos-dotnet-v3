//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// A batch operation might extend multiple requests due to retries.
    /// </summary>
    internal class ItemBatchOperationStatistics : CosmosDiagnostics
    {
        private readonly List<PointOperationStatistics> pointOperationStatistics = new List<PointOperationStatistics>();

        public void AppendPointOperation(PointOperationStatistics pointOperationStatistic)
        {
            this.pointOperationStatistics.Add(pointOperationStatistic);
        }

        public override string ToString()
        {
            if (this.pointOperationStatistics.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder statistics = new StringBuilder();
            foreach (PointOperationStatistics pointOperationStatistic in this.pointOperationStatistics)
            {
                statistics.AppendLine(pointOperationStatistic.ToString());
            }

            return statistics.ToString();
        }
    }
}
