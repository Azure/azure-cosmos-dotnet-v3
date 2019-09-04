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
        private readonly List<CosmosDiagnostics> cosmosDiagnostics = new List<CosmosDiagnostics>();

        public void AppendDiagnostics(CosmosDiagnostics diagnostics)
        {
            this.cosmosDiagnostics.Add(diagnostics);
        }

        public override string ToString()
        {
            if (this.cosmosDiagnostics.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder statistics = new StringBuilder();
            foreach (CosmosDiagnostics pointOperationStatistic in this.cosmosDiagnostics)
            {
                statistics.AppendLine(pointOperationStatistic.ToString());
            }

            return statistics.ToString();
        }
    }
}
