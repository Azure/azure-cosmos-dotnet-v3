// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal class DistributedTransactionCommitterUtils
    {
        public static Task ResolveCollectionRidsAsync(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosClientContext clientContext,
            CancellationToken cancellationToken)
        {
            return ResolveCollectionRidsAndProcessOperationsAsync(
                operations,
                (collectionPath, _) => clientContext.GetCachedContainerPropertiesAsync(
                    collectionPath,
                    NoOpTrace.Singleton,
                    cancellationToken),
                (operation, containerResourceId, databaseResourceId) =>
                {
                    operation.CollectionResourceId = containerResourceId;
                    operation.DatabaseResourceId = databaseResourceId;
                },
                cancellationToken);
        }

        /// <summary>
        /// Refreshes stale container metadata and returns true if any submitted RID no longer matches.
        /// Operations are not mutated: the payload and idempotency token must stay intact.
        /// </summary>
        public static async Task<bool> RefreshCollectionRidsAsync(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosClientContext clientContext,
            ITrace trace,
            ISet<string> staleCollectionPaths,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool ridsChanged = false;

            try
            {
                ClientCollectionCache collectionCache = await clientContext.DocumentClient.GetCollectionCacheAsync(trace);
                await ResolveCollectionRidsAndProcessOperationsAsync(
                    operations,
                    (collectionPath, submittedCollectionRid) => collectionCache.RefreshCollectionAsync(
                        HttpConstants.Versions.CurrentVersion,
                        collectionPath,
                        submittedCollectionRid,
                        trace,
                        clientSideRequestStatistics: null,
                        cancellationToken),
                    (operation, containerResourceId, databaseResourceId) =>
                    {
                        if (operation.CollectionResourceId != containerResourceId ||
                            operation.DatabaseResourceId != databaseResourceId)
                        {
                            ridsChanged = true;
                        }
                    },
                    cancellationToken,
                    staleCollectionPaths);
            }
            catch (DocumentClientException ex)
            {
                throw CosmosExceptionFactory.Create(ex, trace);
            }

            return ridsChanged;
        }

        private static async Task ResolveCollectionRidsAndProcessOperationsAsync(
            IReadOnlyList<DistributedTransactionOperation> operations,
            Func<string, string, Task<ContainerProperties>> getContainerProperties,
            Action<DistributedTransactionOperation, string, string> processOperation,
            CancellationToken cancellationToken,
            ISet<string> priorityCollectionPaths = null)
        {
            IEnumerable<IGrouping<string, DistributedTransactionOperation>> groupedOperations = operations
                .GroupBy(op => DistributedTransactionConstants.GetCollectionFullName(op.Database, op.Container));

            if (priorityCollectionPaths != null && priorityCollectionPaths.Count > 0)
            {
                groupedOperations = groupedOperations.OrderByDescending(group => priorityCollectionPaths.Contains(group.Key));
            }

            foreach (IGrouping<string, DistributedTransactionOperation> group in groupedOperations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ContainerProperties containerProperties = await getContainerProperties(group.Key, group.First().CollectionResourceId);
                string containerResourceId = containerProperties.ResourceId;
                string databaseResourceId = ResourceId.Parse(containerResourceId).DatabaseId.ToString();
                foreach (DistributedTransactionOperation operation in group)
                {
                    processOperation(operation, containerResourceId, databaseResourceId);
                }
            }
        }
    }
}
