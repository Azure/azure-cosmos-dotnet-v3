//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///  Contains the cosmos diagnostic information for the current request to Azure Cosmos DB service.
    /// </summary>
    public abstract class CosmosDiagnostics
    {
        /// <summary>
        /// This represent the end to end elapsed time of the request.
        /// If the request is still in progress it will return the current
        /// elapsed time since the start of the request.
        /// </summary>
        /// <returns>The clients end to end elapsed time of the request.</returns>
        public virtual TimeSpan GetClientElapsedTime()
        {
            // Default implementation avoids breaking change for users upgrading.
            throw new NotImplementedException($"CosmosDiagnostics.GetElapsedTime");
        }

        /// <summary>
        /// Gets the string field <see cref="CosmosDiagnostics"/> instance in the Azure CosmosDB database service.
        /// </summary>
        /// <returns>The string field <see cref="CosmosDiagnostics"/> instance in the Azure CosmosDB database service.</returns>
        public abstract override string ToString();

        /// <summary>
        /// Gets the list of all regions that were contacted for a request
        /// </summary>
        /// <returns>The list of tuples containing the Region name and the URI</returns>
        public abstract IReadOnlyList<(string regionName, Uri uri)> GetContactedRegions();
    }
}
