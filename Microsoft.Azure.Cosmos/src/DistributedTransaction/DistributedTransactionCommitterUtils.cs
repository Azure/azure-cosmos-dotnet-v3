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
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal class DistributedTransactionCommitterUtils
    {
        public static async Task ResolveCollectionRidsAsync(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosClientContext clientContext,
            CancellationToken cancellationToken)
        {
            IEnumerable<IGrouping<string, DistributedTransactionOperation>> groupedOperations = operations
                .GroupBy(op => $"/dbs/{op.Database}/colls/{op.Container}");

            foreach (IGrouping<string, DistributedTransactionOperation> group in groupedOperations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string collectionPath = group.Key;
                ContainerProperties containerProperties = await clientContext.GetCachedContainerPropertiesAsync(
                    collectionPath,
                    NoOpTrace.Singleton,
                    cancellationToken);

                string containerResourceId = containerProperties.ResourceId;
                ResourceId resourceId = ResourceId.Parse(containerResourceId);
                string databaseResourceId = resourceId.DatabaseId.ToString();

                foreach (DistributedTransactionOperation operation in group)
                {
                    operation.CollectionResourceId = containerResourceId;
                    operation.DatabaseResourceId = databaseResourceId;
                }
            }
        }
    }
}
