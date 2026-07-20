// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal static class DistributedTransactionCommitterUtils
    {
        /// <summary>
        /// Prepares each operation for the commit: stamps collection/database resource ids, and under
        /// Session consistency resolves and applies a per-partition session token to operations without an
        /// explicit one. Owns only the grouping and per-collection container lookup; resource-id stamping and
        /// session-token resolution are delegated to their own helpers.
        /// </summary>
        public static async Task PrepareOperationsAsync(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosClientContext clientContext,
            bool isSessionConsistency,
            CancellationToken cancellationToken)
        {
            IEnumerable<IGrouping<string, DistributedTransactionOperation>> groupedOperations = operations
                .GroupBy(op => DistributedTransactionConstants.GetCollectionFullName(op.Database, op.Container));

            // A null resolver disables auto token resolution (non-Session consistency or a custom
            // ISessionContainer); tokens can still be passed explicitly via request options.
            DistributedTransactionSessionTokenResolver sessionTokenResolver =
                await DistributedTransactionSessionTokenResolver.TryCreateAsync(clientContext, isSessionConsistency);

            foreach (IGrouping<string, DistributedTransactionOperation> group in groupedOperations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string collectionPath = group.Key;
                ContainerProperties containerProperties = await clientContext.GetCachedContainerPropertiesAsync(
                    collectionPath,
                    NoOpTrace.Singleton,
                    cancellationToken);

                DistributedTransactionCommitterUtils.ResolveCollectionRids(group, containerProperties);

                if (sessionTokenResolver != null)
                {
                    await sessionTokenResolver.ApplyTokensAsync(group, collectionPath, containerProperties);
                }
            }
        }

        /// <summary>
        /// Resolves the collection and database resource ids and stamps them on every operation in the
        /// collection. The database id is derived once per collection, not per operation.
        /// </summary>
        private static void ResolveCollectionRids(
            IEnumerable<DistributedTransactionOperation> operations,
            ContainerProperties containerProperties)
        {
            string containerResourceId = containerProperties.ResourceId;
            string databaseResourceId = ResourceId.Parse(containerResourceId).DatabaseId.ToString();

            foreach (DistributedTransactionOperation operation in operations)
            {
                operation.CollectionResourceId = containerResourceId;
                operation.DatabaseResourceId = databaseResourceId;
            }
        }
    }
}
