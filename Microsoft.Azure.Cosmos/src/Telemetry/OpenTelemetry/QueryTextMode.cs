// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Defines whether to print query text in tracing attributes.
    /// </summary>
    public enum QueryTextMode
    {
        /// <summary>
        ///  Do not show query text.
        /// </summary>
        None = 0,

        /// <summary>
        /// Only print query text that is parameterized. Parameter values won't be captured.
        /// </summary>
        ParameterizedOnly = 1,

        /// <summary>
        /// Print both parameterized and non parameterized query.
        /// </summary>
        All = 2
    }
}
