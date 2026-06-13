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
            IEnumerable<IGrouping<string, DistributedTransactionOperation>> groupedOperations = operations
                .GroupBy(op => DistributedTransactionConstants.GetCollectionFullName(op.Database, op.Container));

            ISessionContainer sessionContainer = clientContext.DocumentClient?.sessionContainer;

            // The auto-resolution path requires SessionContainer.GetSessionToken(collectionLink),
            // which is not part of the ISessionContainer interface. A custom ISessionContainer
            // (e.g., from CosmosClientBuilder.WithSessionContainer) silently disables auto-
            // resolution. Detect the unsupported case ONCE per commit (not per collection-group)
            // and trace so the gap is diagnosable. Users can still pass tokens explicitly via
            // DistributedTransactionRequestOptions.SessionToken.
            SessionContainer concreteSessionContainer = sessionContainer as SessionContainer;
            if (sessionContainer != null && concreteSessionContainer == null)
            {
                DefaultTrace.TraceWarning(
                    "DistributedTransaction auto session-token resolution is disabled: " +
                    "ISessionContainer implementation is '{0}', not the built-in SessionContainer. " +
                    "Pass session tokens explicitly via DistributedTransactionRequestOptions.SessionToken " +
                    "to enforce session consistency.",
                    sessionContainer.GetType().FullName);
            }

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

                // Resolve session token from SessionContainer for operations without an
                // explicit user-provided token (mirrors GatewayStoreModel.ApplySessionTokenAsync).
                // DTX sends the compound collection-level token; the coordinator extracts
                // the per-partition token internally.
                string resolvedSessionToken = concreteSessionContainer?.GetSessionToken(collectionPath);

                foreach (DistributedTransactionOperation operation in group)
                {
                    operation.CollectionResourceId = containerResourceId;
                    operation.DatabaseResourceId = databaseResourceId;

                    if (operation.SessionToken == null
                        && !string.IsNullOrEmpty(resolvedSessionToken))
                    {
                        operation.SessionToken = resolvedSessionToken;
                    }
                }
            }
        }
    }
}
