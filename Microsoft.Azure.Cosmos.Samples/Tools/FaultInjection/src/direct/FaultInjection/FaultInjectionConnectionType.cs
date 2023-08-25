//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.FaultInjection
{
    /// <summary>
    /// Connection Types fault injection can be applied to
    /// </summary>
    public enum FaultInjectionConnectionType
    {
        /// <summary>
        /// Emulates a direct mode connection
        /// </summary>
        DIRECT_MODE,

        /// <summary>
        /// Emulates a gateway mode connection
        /// </summary>
        GATEWAY_MODE
    }
}
