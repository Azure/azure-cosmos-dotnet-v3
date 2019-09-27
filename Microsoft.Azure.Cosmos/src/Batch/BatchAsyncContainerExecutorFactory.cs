//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Concurrent;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Factory to create and share Executor instances across the client's lifetime.
    /// </summary>
    internal static class BatchAsyncContainerExecutorFactory
    {
        private static ConcurrentDictionary<string, BatchAsyncContainerExecutor> executorsPerContainer = new ConcurrentDictionary<string, BatchAsyncContainerExecutor>();

        public static BatchAsyncContainerExecutor GetExecutorForContainer(
            ContainerCore container,
            CosmosClientContext cosmosClientContext)
        {
            if (!cosmosClientContext.ClientOptions.AllowBulkExecution)
            {
                return null;
            }

            string containerLink = container.LinkUri.ToString();
            if (BatchAsyncContainerExecutorFactory.executorsPerContainer.TryGetValue(containerLink, out BatchAsyncContainerExecutor executor))
            {
                return executor;
            }

            BatchAsyncContainerExecutor newExecutor = new BatchAsyncContainerExecutor(
                container,
                cosmosClientContext,
                Constants.MaxOperationsInDirectModeBatchRequest,
                Constants.MaxDirectModeBatchRequestBodySizeInBytes);
            if (!BatchAsyncContainerExecutorFactory.executorsPerContainer.TryAdd(containerLink, newExecutor))
            {
                newExecutor.Dispose();
            }

            return BatchAsyncContainerExecutorFactory.executorsPerContainer[containerLink];
        }

        public static void DisposeExecutor(ContainerCore container)
        {
            string containerLink = container.LinkUri.ToString();
            if (BatchAsyncContainerExecutorFactory.executorsPerContainer.TryRemove(containerLink, out BatchAsyncContainerExecutor executor))
            {
                executor.Dispose();
            }
        }
    }
}
