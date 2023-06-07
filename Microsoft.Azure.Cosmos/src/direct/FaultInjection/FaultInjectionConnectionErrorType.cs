//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.FaultInjection
{
    /// <summary>
    /// Connection Error Type for Fault Injection
    /// </summary>
    public enum FaultInjectionConnectionErrorType
    {
        /// <summary>
        /// Emulates a connection close
        /// </summary>
        CONNECTION_CLOSE,

        /// <summary>
        /// Emulates a connection reset
        /// </summary>
        CONNECTION_RESET
    }
}
