//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;

    internal class CosmosDiagnostic
    {
        internal PointOperationStatistics pointOperationStatistics { get; set; }
        internal QueryOperationStatistics queryOperationStatistics { get; set; }

        public override string ToString()
        {
            if (pointOperationStatistics != null)
            {
                return pointOperationStatistics.ToString();
            }
            else if (queryOperationStatistics != null)
            {
                return queryOperationStatistics.ToString();
            }

            return string.Empty;
        }
    }
}
