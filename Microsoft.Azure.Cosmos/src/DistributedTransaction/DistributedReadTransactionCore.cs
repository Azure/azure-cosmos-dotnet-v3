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
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            DistributedReadTransactionCore.ValidateContainerReference(database, collection);
            DistributedReadTransactionCore.ValidateItemId(id);

            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Read,
                    operationIndex: this.operations.Count,
                    database: database,
                    container: collection,
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

        private static void ValidateContainerReference(string database, string collection)
        {
            if (string.IsNullOrWhiteSpace(database))
            {
                throw new ArgumentNullException(nameof(database));
            }

            if (string.IsNullOrWhiteSpace(collection))
            {
                throw new ArgumentNullException(nameof(collection));
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
