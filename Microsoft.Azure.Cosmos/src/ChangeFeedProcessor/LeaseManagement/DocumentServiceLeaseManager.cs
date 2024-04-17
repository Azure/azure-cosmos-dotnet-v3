//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The DocumentServiceLeaseManager defines a way to perform operations with <see cref="DocumentServiceLease"/>.
    /// </summary>
    internal abstract class DocumentServiceLeaseManager
    {
        /// <summary>
        /// Checks whether the lease exists and creates it if it does not exist for a physical partition.
        /// </summary>
        /// <param name="partitionKeyRange">Partition for the lease.</param>
        /// <param name="continuationToken">Continuation token if it exists.</param>
        public abstract Task<DocumentServiceLease> CreateLeaseIfNotExistAsync(PartitionKeyRange partitionKeyRange, string continuationToken);

        /// <summary>
        /// Checks whether the lease exists and creates it if it does not exist for a range.
        /// </summary>
        /// <param name="feedRange">Feed range for the lease.</param>
        /// <param name="continuationToken">Continuation token if it exists.</param>
        public abstract Task<DocumentServiceLease> CreateLeaseIfNotExistAsync(FeedRangeEpk feedRange, string continuationToken);

        /// <summary>
        /// Delete the lease.
        /// </summary>
        /// <param name="lease">Lease to remove.</param>
        public abstract Task DeleteAsync(DocumentServiceLease lease);

        /// <summary>
        /// Acquire ownership of the lease.
        /// </summary>
        /// <param name="lease">Lease to acquire.</param>
        /// <returns>Updated acquired lease.</returns>
        /// <exception cref="LeaseLostException">Thrown if other host acquired concurrently</exception>
        public abstract Task<DocumentServiceLease> AcquireAsync(DocumentServiceLease lease);

        /// <summary>
        /// Release ownership of the lease.
        /// </summary>
        /// <param name="lease">Lease to acquire.</param>
        /// <exception cref="LeaseLostException">Thrown if other host acquired the lease or lease was deleted</exception>
        public abstract Task ReleaseAsync(DocumentServiceLease lease);

        /// <summary>
        /// Renew the lease. Leases are periodically renewed to prevent expiration.
        /// </summary>
        /// <param name="lease">Lease to renew.</param>
        /// <returns>Updated renewed lease.</returns>
        /// <exception cref="LeaseLostException">Thrown if other host acquired the lease or lease was deleted</exception>
        public abstract Task<DocumentServiceLease> RenewAsync(DocumentServiceLease lease);

        /// <summary>
        /// Replace properties from the specified lease.
        /// </summary>
        /// <param name="leaseToUpdatePropertiesFrom">Lease containing new properties</param>
        /// <returns>Updated lease.</returns>
        /// <exception cref="LeaseLostException">Thrown if other host acquired the lease</exception>
        public abstract Task<DocumentServiceLease> UpdatePropertiesAsync(DocumentServiceLease leaseToUpdatePropertiesFrom);

        /// <summary>
        /// If the lease container's lease document is found, this method checks for lease 
        /// document's ChangeFeedMode and if the new ChangeFeedMode is different
        /// from the current ChangeFeedMode, an exception is thrown.
        /// This is based on an issue located at <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4308"/>.
        /// </summary>
        public void ChangeFeedModeSwitchingCheck(
            IReadOnlyList<DocumentServiceLease> documentServiceLeases, 
            ChangeFeedMode changeFeedLeaseOptionsMode)
        {
            // No lease documents. Return.

            if (documentServiceLeases.Count == 0)
            {
                return;
            }

            DocumentServiceLease documentServiceLease = documentServiceLeases[0];

            this.VerifyChangeFeedProcessorMode(
                changeFeedMode: changeFeedLeaseOptionsMode,
                leaseChangeFeedMode: documentServiceLease.Mode);
        }

        /// <summary>
        /// Mode attribute exists on lease document, but it is not set. Legacy is always LatestVersion/IncrementalFeed
        /// because AllVersionsAndDeletes does not exist. There should not be any legacy lease documents that are
        /// AllVersionsAndDeletes. If the ChangeFeedProcessor's mode is not legacy, an exception should thrown.
        /// If the ChangeFeedProcessor mode is not the mode in the lease document, an exception should be thrown.
        /// </summary>
        /// <param name="changeFeedMode">The current change feed mode.</param>
        /// <param name="leaseChangeFeedMode">The change feed mode on the lease document.</param>
        private void VerifyChangeFeedProcessorMode(
            ChangeFeedMode changeFeedMode,
            string leaseChangeFeedMode)
        {
            leaseChangeFeedMode ??= HttpConstants.A_IMHeaderValues.IncrementalFeed;

            string normalizedProcessorChangeFeedMode = changeFeedMode == ChangeFeedMode.AllVersionsAndDeletes
                ? HttpConstants.A_IMHeaderValues.FullFidelityFeed
                : HttpConstants.A_IMHeaderValues.IncrementalFeed;

            if (string.Compare(leaseChangeFeedMode, normalizedProcessorChangeFeedMode, StringComparison.OrdinalIgnoreCase) != 0)
            {
                throw new ArgumentException(message: $"Switching {nameof(ChangeFeedMode)} {leaseChangeFeedMode} to {normalizedProcessorChangeFeedMode} is not allowed.");
            }
        }
    }
}
