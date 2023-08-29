//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    /// <summary>
    /// Connection Error Type for Fault Injection
    /// </summary>
    public enum FaultInjectionConnectionErrorType
    {
        /// <summary>
        /// Emulates a connection close because of a recieved stream close
        /// </summary>
        RECIEVED_STREAM_CLOSED,

        /// <summary>
        /// Emulates a connection close because of a failure to recieve a response
        /// </summary>
        RECIEVE_FAILED,
    }
}
