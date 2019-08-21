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
        // For now only ToString() will be used, having child concrete PointOperationStatistics
        // and QueryOperationStatistics
    }
}
