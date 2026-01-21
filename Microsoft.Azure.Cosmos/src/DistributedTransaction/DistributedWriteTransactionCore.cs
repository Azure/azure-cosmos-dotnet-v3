// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.ClientModel.Primitives;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal class DistributedWriteTransactionCore : DistributedWriteTransaction
    {
        protected List<DistributedTransactionOperation> operations;

        internal DistributedWriteTransactionCore()
        {
            this.operations = new List<DistributedTransactionOperation>();
        }

        public override DistributedTransaction Create<T>(string database, string collection, PartitionKey partitionKey, T resource)
        {
            this.operations.Add(
                new DistributedTransactionOperation<T>(
                    operationType: OperationType.Create,
                    operationIndex: this.operations.Count,
                    database,
                    collection,
                    partitionKey,
                    resource));
            return this;
        }

        public override DistributedTransaction Replace<T>(string database, string collection, PartitionKey partitionKey, string id, T resource)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }
            this.operations.Add(
                new DistributedTransactionOperation<T>(
                    operationType: OperationType.Replace,
                    operationIndex: this.operations.Count,
                    database,
                    collection,
                    partitionKey,
                    id,
                    resource));
            return this;
        }

        public override DistributedTransaction Delete(string database, string collection, PartitionKey partitionKey, string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }
            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Delete,
                    operationIndex: this.operations.Count,
                    database,
                    collection,
                    partitionKey,
                    id: id));
            return this;
        }

        public override DistributedTransaction Patch(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            IReadOnlyList<PatchOperation> patchOperations)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (patchOperations == null || !patchOperations.Any())
            {
                throw new ArgumentNullException(nameof(patchOperations));
            }

            PatchSpec patchSpec = new PatchSpec(patchOperations, new PatchItemRequestOptions());

            this.operations.Add(
                new DistributedTransactionOperation<PatchSpec>(
                    operationType: OperationType.Patch,
                    operationIndex: this.operations.Count,
                    database,
                    collection,
                    partitionKey,
                    resource: patchSpec));
            return this;
        }

        public override DistributedTransaction Upsert<T>(string database, string collection, PartitionKey partitionKey, T resource)
        {
            this.operations.Add(
                new DistributedTransactionOperation<T>(
                    operationType: OperationType.Upsert,
                    operationIndex: this.operations.Count,
                    database,
                    collection,
                    partitionKey,
                    resource));
            return this;
        }

        public override Task<DistributedTransactionResponse> CommitTransactionAsync()
        {
            return this.CommitTransactionAsync(this.operations);
        }

        private Task<DistributedTransactionResponse> CommitTransactionAsync(IReadOnlyList<DistributedTransactionOperation> operations)
        {
            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                collectionCache: null,
                operations: operations);

            return committer.CommitTransactionAsync();
        }
    }
}
