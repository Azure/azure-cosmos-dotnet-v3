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
        /// Emulates a connection close because of an unhealthy conection
        /// </summary>
        UNHEALTHY_CONNECTION_CLOSE,
    }
}
