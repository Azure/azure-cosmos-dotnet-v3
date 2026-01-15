// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    //using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents an operation on a document whichwill be executed as a part of a distributed transaction.
    /// </summary>
    internal class DistributedTransactionOperation
    {
        public DistributedTransactionOperation(
            Documents.OperationType operationType,
            PartitionKey partitionKey,
            string database,
            string container)
        {
            this.OperationType = operationType;
            this.PartitionKey = partitionKey;
            this.Database = database;
            this.Container = container;
        }

        public PartitionKey PartitionKey { get; internal set; }

        public string Database { get; internal set; }

        public string Container { get; internal set; }

        public Documents.OperationType OperationType { get; internal set; }
    }
}
