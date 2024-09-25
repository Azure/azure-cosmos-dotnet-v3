// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Defines whether to print query in tracing attributes
    /// </summary>
    public enum QueryTextMode
    {
        /// <summary>
        ///  Do not show query.
        /// </summary>
        NONE = 0,

        /// <summary>
        /// Print parameterized query only.
        /// </summary>
        PARAMETERIZED_ONLY = 1,

        /// <summary>
        /// Print both parameterized and non parameterized query.
        /// </summary>
        ALL = 2
    }
}
