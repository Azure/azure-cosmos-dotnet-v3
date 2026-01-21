// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;

    //using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents an operation on a document whichwill be executed as a part of a distributed transaction.
    /// </summary>
    internal class DistributedTransactionOperation
    {
        public DistributedTransactionOperation(
            Documents.OperationType operationType,
            int operationIndex,
            string database,
            string container,
            PartitionKey partitionKey,
            string id = null)
        {
            this.OperationType = operationType;
            this.OperationIndex = operationIndex;
            this.PartitionKey = partitionKey;
            this.Database = database;
            this.Container = container;
            this.Id = id;
        }

        public PartitionKey PartitionKey { get; internal set; }

        public string Database { get; internal set; }

        public string Container { get; internal set; }

        public Documents.OperationType OperationType { get; internal set; }

        public int OperationIndex { get; internal set; } 

        public String Id { get; internal set; }
    }

    internal class DistributedTransactionOperation<T> : DistributedTransactionOperation
    {
        public DistributedTransactionOperation(
            Documents.OperationType operationType,
            int operationIndex,
            string database,
            string container,
            PartitionKey partitionKey,
            T resource)
            : base(operationType, operationIndex, database, container, partitionKey)
        {
            this.Resource = resource;
        }

        public DistributedTransactionOperation(
            Documents.OperationType operationType,
            int operationIndex,
            string database,
            string container,
            PartitionKey partitionKey,
            string id,
            T resource)
            : base(operationType, operationIndex, database, container, partitionKey, id)
        {
            this.Resource = resource;
        }
        public T Resource { get; internal set; }
    }
}
