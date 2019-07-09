//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    /// <summary>
    /// Stored an array of Stored procedure arguments.
    /// </summary>
    public struct StoredProcedureArguments
    {
        internal dynamic[] Parameters { get; }

        /// <summary>
        /// Create a list of parameters as input for a stored procedure operation
        /// </summary>
        /// <param name="inputParams">A list of parameters to be used as input for a stored procedure execute operation</param>
        public StoredProcedureArguments(params dynamic[] inputParams)
        {
            this.Parameters = inputParams;
        }
    }
}
