// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents an operation on a document whichwill be executed as a part of a distributed transaction.
    /// </summary>
    internal class DistributedTransactionOperation : IDisposable
    {
#pragma warning disable IDE0044 // Add readonly modifier
        private bool isDisposed;
#pragma warning restore IDE0044 // Add readonly modifier

        public DistributedTransactionOperation(
            OperationType operationType,
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

        public OperationType OperationType { get; internal set; }

        /// <summary>
        /// Disposes the current DistributedTransactionOperation instance
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }
    }
}
