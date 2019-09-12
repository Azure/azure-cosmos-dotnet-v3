//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// A batch operation might extend multiple requests due to retries.
    /// </summary>
    internal class ItemBatchOperationStatistics : CosmosDiagnostics
    {
        private readonly DateTime created = DateTime.UtcNow;
        private readonly List<CosmosDiagnostics> cosmosDiagnostics = new List<CosmosDiagnostics>();
        private DateTime completed;

        public void AppendDiagnostics(CosmosDiagnostics diagnostics)
        {
            this.cosmosDiagnostics.Add(diagnostics);
        }

        public void Complete()
        {
            this.completed = DateTime.UtcNow;
        }

        public override string ToString()
        {
            if (this.cosmosDiagnostics.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder statistics = new StringBuilder($"Bulk operation started at {this.created}. ");
            if (this.completed != null)
            {
                statistics.Append($"Completed at {this.completed}. ");
            }

            foreach (CosmosDiagnostics pointOperationStatistic in this.cosmosDiagnostics)
            {
                statistics.AppendLine(pointOperationStatistic.ToString());
            }

            return statistics.ToString();
        }
    }
}
