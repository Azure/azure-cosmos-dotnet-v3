// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;

    internal static class DistributedTransactionConstants
    {
        // Commit guard: values used with Interlocked.CompareExchange to enforce single-use semantics.
        internal const int CommitNotStarted = 0;
        internal const int CommitStarted = 1;

        // DTX envelope sub-status codes. The full status/body matrix and the inner-vs-outer retry ownership
        // is documented at the call site in ClientRetryPolicy.ShouldRetryDtxRequest.
        internal const int DtcCoordinatorRaceConflict = 5352; // 449 sub-status: coordinator ETag race
        internal const int DtcLedgerThrottled = 3200;          // 429 sub-status: ledger backpressure
        internal const int DtcLedgerFailure = 5411;            // 500 sub-status: ledger infra failure
        internal const int DtcAccountConfigFailure = 5412;     // 500 sub-status: account-config infra failure
        internal const int DtcDispatchFailure = 5413;          // 500 sub-status: dispatch infra failure

        internal static bool IsDistributedTransactionRequest(OperationType operationType, ResourceType resourceType)
        {
            return operationType == OperationType.CommitDistributedTransaction
                && resourceType == ResourceType.DistributedTransactionBatch;
        }

        internal static string GetCollectionFullName(string database, string container)
        {
            return $"dbs/{database}/colls/{container}";
        }

        /// <summary>
        /// Validates that the <paramref name="container"/> belongs to <paramref name="expectedClient"/>
        /// and extracts the database and container identifiers.
        /// </summary>
        /// <remarks>
        /// Only the name identifiers (Database.Id and Container.Id) are used by the distributed transaction
        /// pipeline. Per-container behaviors such as custom serializers, client-side encryption policies,
        /// or decorator wrappers attached to the <see cref="Container"/> instance are not honored downstream.
        /// </remarks>
        internal static (string databaseId, string containerId) ValidateAndUnpackContainer(
            Container container,
            CosmosClient expectedClient)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }
            Database database = container.Database;
            if (database == null)
            {
                throw new ArgumentException("Container reference must expose a non-null Database.", nameof(container));
            }

            string containerId = container.Id;
            string databaseId = database.Id;

            if (string.IsNullOrWhiteSpace(containerId))
            {
                throw new ArgumentException("Container reference must have a non-empty Id.", nameof(container));
            }

            if (string.IsNullOrWhiteSpace(databaseId))
            {
                throw new ArgumentException("Container reference must have a non-empty Database.Id.", nameof(container));
            }

            CosmosClient owner = database.Client;
            if (!object.ReferenceEquals(owner, expectedClient))
            {
                throw new ArgumentException(
                    "Container must belong to the same CosmosClient instance that created this distributed transaction.",
                    nameof(container));
            }

            return (databaseId, containerId);
        }
    }
}
