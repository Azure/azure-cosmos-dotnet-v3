// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Defines whether to print query in tracing attributes
    /// </summary>
    public enum ShowQueryMode
    {
        /// <summary>
        ///  Do not show query.
        /// </summary>
        NONE,

        /// <summary>
        /// Print parameterized query only.
        /// </summary>
        PARAMETERIZED_ONLY,

        /// <summary>
        /// Print both parameterized and non parameterized query.
        /// </summary>
        ALL
    }
}
