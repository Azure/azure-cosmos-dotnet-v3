// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;

    internal class DistributedWriteTransactionCore : DistributedWriteTransaction
    {
        /// <summary>
        /// List of operations in a distributed transaction
        /// </summary>
        protected List<DistributedTransactionOperation> operations;

        internal DistributedWriteTransactionCore()
        {
            this.operations = new List<DistributedTransactionOperation>();
        }

        public override DistributedTransaction Create<T>(string database, string collection, PartitionKey partitionKey)
        {
            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Create,
                    partitionKey,
                    database,
                    collection));
            return this;
        }

        public override DistributedTransaction Delete(string database, string collection, PartitionKey partitionKey)
        {
            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Delete,
                    partitionKey,
                    database,
                    collection));
            return this;
        }

        public override DistributedTransaction Patch(string database, string collection, PartitionKey partitionKey)
        {
            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Patch,
                    partitionKey,
                    database,
                    collection));
            return this;
        }

        public override DistributedTransaction Replace<T>(string database, string collection, PartitionKey partitionKey)
        {
            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Replace,
                    partitionKey,
                    database,
                    collection));
            return this;
        }

        public override DistributedTransaction Upsert(string database, string collection, PartitionKey partitionKey)
        {
            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Upsert,
                    partitionKey,
                    database,
                    collection));
            return this;
        }

        public override void CommitTransaction()
        {
            throw new NotImplementedException();
        }

        private void ValidateTransaction()
        {
            throw new NotImplementedException();
        }
    }
}
