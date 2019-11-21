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
        /// Windows Server 2016 and newer: Uses the SO_REUSE_UNICASTPORT option if the operating system has automatic client port reuse enabled.
        /// Older versions of Windows, Linux, other: Uses default socket options.
        /// </summary>
        /// <remarks>
        /// see also
        /// https://docs.microsoft.com/en-us/windows/win32/winsock/sol-socket-socket-options
        /// https://docs.microsoft.com/en-us/powershell/module/nettcpip/set-nettcpsetting?view=win10-ps
        /// https://support.microsoft.com/en-us/help/3149157/reliability-and-scalability-improvements-in-tcp-ip-for-windows-8-1-and
        /// </remarks>
        ReuseUnicastPort = 0,
        /// <summary>
        /// Windows: Tracks client ports used by the Cosmos DB client and reuses them. Ports are reused at DocumentClient scope.
        /// Linux: Uses default socket options.
        /// </summary>
        PrivatePortPool = 1,
    }
}
