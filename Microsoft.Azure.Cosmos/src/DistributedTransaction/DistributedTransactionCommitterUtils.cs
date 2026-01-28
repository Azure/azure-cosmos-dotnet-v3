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
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal class DistributedTransactionCommitterUtils
    {
        public static async Task ResolveCollectionRidsAsync(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosClientContext clientContext,
            CancellationToken cancellationToken)
        {
            CollectionCache collectionCache = await clientContext.DocumentClient.GetCollectionCacheAsync(NoOpTrace.Singleton);
            IEnumerable<Task> ridResolutionTasks = operations
               .GroupBy(op => $"/dbs/{op.Database}/colls/{op.Container}")
               .Select(async group =>
               {
                   string collectionPath = group.Key;
                   try
                   {
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
                   catch (Exception ex)
                   {
                       DefaultTrace.TraceError($"Failed to resolve RID for {collectionPath}: {ex.Message}");
                       throw;
                   }
               });
            await Task.WhenAll(ridResolutionTasks);
        }

    }
}
