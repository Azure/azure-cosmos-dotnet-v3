//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System.Net;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Stored an array of Stored procedure parameters.
    /// </summary>
    public struct StoredProcedureDefinition
    {
        internal dynamic[] Parameters { get; }

        /// <summary>
        /// Create a list of parameters as input for a stored procedure operation
        /// </summary>
        /// <param name="inputParams">A list of parameters to be used as input for a stored procedure execute operation</param>
        public StoredProcedureDefinition(params dynamic[] inputParams)
        {
            Parameters = inputParams;
        }
    }
}
