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
        /// OperationType for distributed read transactions. The backend introduced this value
        /// but it is not yet present in the Microsoft.Azure.Cosmos.Direct package, so we define
        /// it locally until the Direct package is updated.
        /// </summary>
        internal const OperationType CommitDistributedReadTransaction = (OperationType)81;

        /// <summary>
        /// Wire-format string for <see cref="CommitDistributedReadTransaction"/>.
        /// <see cref="OperationTypeExtensions.ToOperationTypeString"/> does not recognize this
        /// value in the current Direct package, so we provide the string ourselves.
        /// </summary>
        internal const string CommitDistributedReadTransactionString = "CommitDistributedReadTransaction";

        internal static bool IsDistributedTransactionRequest(OperationType operationType, ResourceType resourceType)
        {
            return (operationType == OperationType.CommitDistributedTransaction
                    || operationType == DistributedTransactionConstants.CommitDistributedReadTransaction)
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
