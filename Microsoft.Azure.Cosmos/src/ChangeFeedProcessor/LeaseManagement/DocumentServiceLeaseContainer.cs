//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents operations to get leases from lease store.
    /// </summary>
    internal abstract class DocumentServiceLeaseContainer
    {
        /// <summary>
        /// Get all leases.
        /// </summary>
        /// <returns>List of all leases</returns>
        public abstract Task<IReadOnlyList<DocumentServiceLease>> GetAllLeasesAsync();

        /// <summary>
        /// Get all the leases owned by the current host.
        /// </summary>
        /// <returns>Enumerable of all leases owned by the current host</returns>
        public abstract Task<IEnumerable<DocumentServiceLease>> GetOwnedLeasesAsync();

        /// <summary>
        /// Exports all leases as JSON elements.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        /// <returns>A list of lease objects as JSON elements.</returns>
        public abstract Task<IReadOnlyList<JsonElement>> ExportLeasesAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Imports leases from a list of JSON elements.
        /// </summary>
        /// <param name="leases">The list of lease objects as JSON elements to import.</param>
        /// <param name="overwriteExisting">Whether to overwrite existing leases with the same ID.</param>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public abstract Task ImportLeasesAsync(
            IReadOnlyList<JsonElement> leases,
            bool overwriteExisting = false,
            CancellationToken cancellationToken = default);
    }
}
