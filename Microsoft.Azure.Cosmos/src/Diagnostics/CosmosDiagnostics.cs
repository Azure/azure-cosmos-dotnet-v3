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
        /// This is a user passed in client side request id. This is used to help
        /// users correlate a request with other application layers.
        /// </summary>
        /// <remarks>
        /// This is only tracked on the client side and is never
        /// sent to the Cosmos DB service.
        /// </remarks>
        public abstract string UserClientRequestId { get; }

        /// <summary>
        /// This represent the total
        /// </summary>
        /// <returns>The elapsed time since the start of the request o</returns>
        public abstract TimeSpan GetElapsedClientLatency();

        /// <summary>
        /// Get a list of region contacted for the request.
        /// </summary>
        /// <returns>A list of regions contacted for the SDK operation</returns>
        public abstract IReadOnlyCollection<Uri> GetContactedRegions();

        /// <summary>
        /// Gets the string field <see cref="CosmosDiagnostics"/> instance in the Azure CosmosDB database service.
        /// </summary>
        /// <returns>The string field <see cref="CosmosDiagnostics"/> instance in the Azure CosmosDB database service.</returns>
        public abstract override string ToString();
    }
}
