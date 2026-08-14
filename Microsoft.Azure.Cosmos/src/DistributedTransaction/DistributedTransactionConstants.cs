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

        /// <summary>
        /// Envelope sub-status paired with HTTP 200 when every operation completed with 304. The
        /// coordinator cannot send a 304 envelope because HTTP layers strip its body, discarding the
        /// per-operation results. Not defined by <see cref="SubStatusCodes"/> in the referenced Direct package.
        /// </summary>
        internal const SubStatusCodes AllOperationsNotModified = (SubStatusCodes)5425;

        /// <summary>
        /// Request header reporting whether the current dispatch of a distributed write transaction has
        /// crossed write regions while replaying the same idempotency token.
        /// </summary>
        /// <remarks>
        /// Declared here rather than on <see cref="HttpConstants.HttpHeaders"/> because that type ships in
        /// the Microsoft.Azure.Cosmos.Direct package and the name is pending coordinator-team sign-off.
        /// </remarks>
        internal const string CrossRegionRetryHeader = "x-ms-cosmos-dtx-cross-region-retry";

        internal static bool IsDistributedTransactionRequest(OperationType operationType, ResourceType resourceType)
        {
            return (operationType == OperationType.CommitDistributedTransaction
                    || operationType == OperationType.Read)
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
