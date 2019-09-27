//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    ///  Contains the cosmos diagnostic information for the current request to Azure Cosmos DB service.
    /// </summary>
    public abstract class CosmosDiagnostics
    {
        /// <summary>
        /// Gets the string field <see cref="CosmosDiagnostics"/> instance in the Azure CosmosDB database service.
        /// </summary>
        /// <returns>The string field <see cref="CosmosDiagnostics"/> instance in the Azure CosmosDB database service.</returns>
        public abstract override string ToString();
    }
}
