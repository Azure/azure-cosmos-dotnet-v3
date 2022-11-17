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
    internal interface IAddressResolverExtension : IAddressResolver
    {
        /// <summary>
        /// Invokes the gateway address cache and passes the <see cref="TransportClient"/> deligate to be invoked from the same.
        /// </summary>
        /// <param name="databaseName">A string containing the name of the database.</param>
        /// <param name="containerLinkUri">A string containing the container's link uri.</param>
        /// <param name="cancellationToken">An Instance of the <see cref="CancellationToken"/>.</param>
        Task OpenConnectionsToAllReplicasAsync(
            string databaseName,
            string containerLinkUri,
            Func<Uri, Task> openConnectionHandlerAsync,
            CancellationToken cancellationToken = default);
    }
}
