//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Handler interface for opening connections to backend replica nodes.
    /// </summary>
    internal interface IOpenConnectionsHandler
    {
        /// <summary>
        /// Uses the transport client instance and attempts to open the Rntbd connection
        /// and establish Rntbd context negotiation to the backend replica nodes.
        /// </summary>
        /// <param name="addresses">An enumerable of <see cref="TransportAddressUri"/>
        /// containing the backend replica addresses.</param>
        Task TryOpenRntbdChannelsAsync(
             IEnumerable<TransportAddressUri> addresses);
    }
}
