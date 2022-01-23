//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.DiagnosticSource
{
    /// <summary>
    /// DiagnosticSourceFilter
    /// </summary>
    public enum DiagnosticSourceFilterType
    {
        /// <summary>
        /// Allows only unhandled Exceptions i.e. OperationCanceledException, ObjectDisposedException, NullReferenceException
        /// </summary>
        Exception,

        /// <summary>
        /// Allows only Create Operations
        /// </summary>
        Create,

        /// <summary>
        /// Allows only Delete Operations
        /// </summary>
        Delete,

        /// <summary>
        /// Allows only Replace Operations
        /// </summary>
        Replace,

        /// <summary>
        /// Allows only Upsert Operations
        /// </summary>
        Upsert,

        /// <summary>
        /// Allows only Patch Operations
        /// </summary>
        Patch
    }
}
