//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Cache to create and share Executor instances across the client's lifetime.
    /// </summary>
    internal class BatchAsyncContainerExecutorCache : IDisposable
    {
        // Large size leads to large number of index term which can lead to frequent timeouts, so limiting to 200KB. Bulk V2 also have same values.
        internal const int DefaultMaxBulkRequestBodySizeInBytes = 220201;
        private ConcurrentDictionary<string, BatchAsyncContainerExecutor> executorsPerContainer = new ConcurrentDictionary<string, BatchAsyncContainerExecutor>();

        public BatchAsyncContainerExecutor GetExecutorForContainer(
            ContainerInternal container,
            CosmosClientContext cosmosClientContext)
        {
            if (!cosmosClientContext.ClientOptions.AllowBulkExecution)
            {
                throw new InvalidOperationException("AllowBulkExecution is not currently enabled.");
            }

            string containerLink = container.LinkUri.ToString();
            if (this.executorsPerContainer.TryGetValue(containerLink, out BatchAsyncContainerExecutor executor))
            {
                return executor;
            }

            BatchAsyncContainerExecutor newExecutor = new BatchAsyncContainerExecutor(
                container,
                cosmosClientContext,
                Constants.MaxOperationsInDirectModeBatchRequest,
                DefaultMaxBulkRequestBodySizeInBytes);
            if (!this.executorsPerContainer.TryAdd(containerLink, newExecutor))
            {
                newExecutor.Dispose();
            }

            return this.executorsPerContainer[containerLink];
        }

        public void Dispose()
        {
            foreach (KeyValuePair<string, BatchAsyncContainerExecutor> cacheEntry in this.executorsPerContainer)
            {
                cacheEntry.Value.Dispose();
            }
        }
    }
}