//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Port reuse policy options used by the transport stack
    /// </summary>
    public enum PortReuseMode
    {
        /// <summary>
        /// Reuse unicast port
        /// </summary>
        ReuseUnicastPort = 0,
        /// <summary>
        /// Private port pool
        /// </summary>
        PrivatePortPool = 1,
    }
}
