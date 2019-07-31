//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    ///  Contains the cosmos diagnostic information for the current request to Azure Cosmos DB service.
    /// </summary>
    public class CosmosDiagnostic
    {
        internal PointOperationStatistics pointOperationStatistics { get; set; }
        internal QueryOperationStatistics queryOperationStatistics { get; set; }

        /// <summary>
        /// Gets the string field <see cref="Microsoft.Azure.Cosmos.CosmosDiagnostic"/> instance in the Azure DocumentDB database service.
        /// </summary>
        /// <returns>The string field <see cref="Microsoft.Azure.Cosmos.CosmosDiagnostic"/> instance in the Azure DocumentDB database service.</returns>
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
