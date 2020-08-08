// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    /// <summary>
    /// Environment the query is going to be executed on.
    /// </summary>
    internal enum ExecutionEnvironment
    {
        /// <summary>
        /// Query is being executed on a 3rd party client.
        /// </summary>
        Client,

        /// <summary>
        /// Query is being executed on the compute gateway.
        /// </summary>
        Compute,
    }
}
