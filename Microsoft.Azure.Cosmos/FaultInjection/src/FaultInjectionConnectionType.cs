//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    /// <summary>
    /// Connection Error Type for Fault Injection
    /// </summary>
    public enum FaultInjectionConnectionType
    {
        /// <summary>
        /// Client using Direct Mode
        /// </summary>
        Direct,

        /// <summary>
        /// Requests to Gateway
        /// </summary>
        Gateway,

        /// <summary>
        /// All connection types. Default value.
        /// </summary>
        All,
    }
}
