//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// Contain the method to open connection to the backend replicas, using Rntbd context negotiation.
    /// This is a temporary interface and will eventually be removed once the OpenConnectionsAsync() method 
    /// is moved into the <see cref="IAddressResolver"/>.
    /// </summary>
    internal interface IOpenConnectionsHandler
    {
        /// <summary>
        /// blabla
        /// </summary>
        /// <param name="addresses"></param>
        /// <returns>A completed task.</returns>
        Task OpenRntbdChannelsAsync(
             IReadOnlyList<TransportAddressUri> addresses);
    }
}
