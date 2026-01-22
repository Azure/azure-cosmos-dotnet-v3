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
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal class DistributedTransactionCommitterUtils
    {
        public static void ValidateTransaction(IReadOnlyList<DistributedTransactionOperation> operations)
        {
            if (operations == null || operations.Count == 0)
            {
                throw new ArgumentException("Distributed transaction must contain at least one operation.");
            }

            List<string> validationErrors = new List<string>();

            foreach (DistributedTransactionOperation operation in operations)
            {
                int index = operation.OperationIndex;

                if (string.IsNullOrEmpty(operation.Database))
                {
                    validationErrors.Add($"Operation at index {index}: database cannot be null or empty.");
                }

                if (string.IsNullOrEmpty(operation.Container))
                {
                    validationErrors.Add($"Operation at index {index}: container cannot be null or empty.");
                }

                if (operation.PartitionKey == null)
                {
                    validationErrors.Add($"Operation at index {index}: partition key cannot be null.");
                }
            }

            if (validationErrors.Count > 0)
            {
                string errorMessage = validationErrors.Count == 1
                    ? $"Distributed transaction validation failed: {validationErrors[0]}"
                    : $"Distributed transaction validation failed with {validationErrors.Count} errors:\n{string.Join("\n", validationErrors)}";

                throw new ArgumentException(errorMessage);
            }
        }

        public static async Task ResolveCollectionRidsAsync(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosClientContext clientContext,
            CancellationToken cancellationToken)
        {
            Common.CollectionCache collectionCache = await clientContext.DocumentClient.GetCollectionCacheAsync(
                NoOpTrace.Singleton);

            IEnumerable<Task> ridResolutionTasks = operations
                .GroupBy(op => $"/dbs/{op.Database}/colls/{op.Container}")
                .Select(async group =>
                {
                    string collectionPath = group.Key;
                    try
                    {
                        DocumentServiceRequest request = DocumentServiceRequest.Create(
                            OperationType.Read,
                            ResourceType.Collection,
                            collectionPath,
                            AuthorizationTokenType.PrimaryMasterKey);

                        ContainerProperties containerProperties = await collectionCache.ResolveCollectionAsync(
                            request,
                            cancellationToken,
                            NoOpTrace.Singleton) ?? throw new InvalidOperationException($"Could not resolve collection RID for {collectionPath}");

                        foreach (DistributedTransactionOperation operation in group)
                        {
                            operation.CollectionResourceId = containerProperties.ResourceId;
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
