//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Contains the helper methods to open connection to the backend replicas, using Rntbd context negotiation.
    /// This is a temporary interface and will eventually be removed once the OpenConnectionsAsync() method 
    /// is moved into the <see cref="IStoreModel"/>.
    /// </summary>
    internal interface IStoreModelExtension : IStoreModel
    {
        /// <summary>
        /// Establishes and Initializes the Rntbd connection to all the backend replica nodes for the given database name and container.
        /// </summary>
        /// <param name="addrsses">A string containing the name of the database.</param>
        /// <param name="cancellationToken">An Instance of the <see cref="CancellationToken"/>.</param>
        Task OpenConnectionsAsync(
            IEnumerable<Uri> addrsses,
            CancellationToken cancellationToken = default);
    }
}
