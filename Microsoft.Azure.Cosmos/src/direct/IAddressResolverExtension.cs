//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{

    using System.Threading;
    using System.Threading.Tasks;

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
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the <see cref="IOpenConnectionsHandler"/> instance to a class level readonly
        /// field for invoking the open connection request at a later point of time.
        /// </summary>
        /// <param name="openConnectionHandler">An instance of <see cref="IOpenConnectionsHandler"/></param>
        void SetOpenConnectionsHandler(
            IOpenConnectionsHandler openConnectionHandler);
    }
}
