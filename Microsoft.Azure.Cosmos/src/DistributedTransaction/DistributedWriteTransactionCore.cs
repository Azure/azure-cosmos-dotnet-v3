// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.ClientModel.Primitives;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Documents;

#if INTERNAL
    public 
#else
    internal
#endif
    class DistributedWriteTransactionCore : DistributedWriteTransaction
    {
        protected List<DistributedTransactionOperation> operations;

        internal DistributedWriteTransactionCore()
        {
            this.operations = new List<DistributedTransactionOperation>();
        }

        public override DistributedWriteTransaction Create<T>(string database, string collection, PartitionKey partitionKey, T resource)
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

        public override DistributedWriteTransaction Replace<T>(string database, string collection, PartitionKey partitionKey, string id, T resource)
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

        public override DistributedWriteTransaction Delete(string database, string collection, PartitionKey partitionKey, string id)
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

        public override DistributedWriteTransaction Patch(
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

        public override DistributedWriteTransaction Upsert<T>(string database, string collection, PartitionKey partitionKey, T resource)
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

        public override void CommitTransaction()
        {
            throw new NotImplementedException();
        }
    }
}
