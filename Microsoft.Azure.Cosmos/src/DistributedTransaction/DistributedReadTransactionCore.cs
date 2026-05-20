// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal class DistributedReadTransactionCore : DistributedReadTransaction
    {
        private readonly CosmosClientContext clientContext;
        private readonly List<DistributedTransactionOperation> operations;

        internal DistributedReadTransactionCore(CosmosClientContext clientContext)
        {
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
            this.operations = new List<DistributedTransactionOperation>();
        }

        public override DistributedReadTransaction ReadItem(
            Container container,
            PartitionKey partitionKey,
            string id,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            DistributedReadTransactionCore.ValidateContainerReference(container);
            DistributedReadTransactionCore.ValidateItemId(id);

            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Read,
                    operationIndex: this.operations.Count,
                    database: container.Database.Id,
                    container: container.Id,
                    partitionKey: partitionKey,
                    id: id,
                    requestOptions: requestOptions));

            return this;
        }

        public override async Task<DistributedTransactionResponse> CommitTransactionAsync(
            CancellationToken cancellationToken = default)
        {
            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations: this.operations,
                clientContext: this.clientContext);

            return await committer.CommitTransactionAsync(cancellationToken);
        }

        private static void ValidateContainerReference(Container container)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            if (string.IsNullOrWhiteSpace(container.Id))
            {
                throw new ArgumentException(
                    "Container reference must have a non-empty Id.",
                    nameof(container));
            }

            if (container.Database == null)
            {
                throw new ArgumentException(
                    "Container reference must expose a non-null Database.",
                    nameof(container));
            }

            if (string.IsNullOrWhiteSpace(container.Database.Id))
            {
                throw new ArgumentException(
                    "Container reference must have a non-empty Database.Id.",
                    nameof(container));
            }
        }

        private static void ValidateItemId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }
        }
    }
}
