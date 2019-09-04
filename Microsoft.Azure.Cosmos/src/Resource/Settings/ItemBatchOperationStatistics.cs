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
        private readonly List<CosmosDiagnostics> pointOperationStatistics = new List<CosmosDiagnostics>();

        public void AppendPointOperation(CosmosDiagnostics pointOperationStatistic)
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
            foreach (CosmosDiagnostics pointOperationStatistic in this.pointOperationStatistics)
            {
                statistics.AppendLine(pointOperationStatistic.ToString());
            }

            return statistics.ToString();
        }
    }
}
