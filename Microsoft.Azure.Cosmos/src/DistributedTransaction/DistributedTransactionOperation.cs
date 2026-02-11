// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents an operation on a document which will be executed as a part of a distributed transaction.
    /// </summary>
    internal class DistributedTransactionOperation
    {
        protected Memory<byte> body;

        public DistributedTransactionOperation(
            OperationType operationType,
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

        public OperationType OperationType { get; internal set; }

        public int OperationIndex { get; internal set; }

        public string Id { get; internal set; }

        public string CollectionResourceId { get; internal set; }

        public string DatabaseResourceId { get; internal set; }

        internal string PartitionKeyJson { get; set; }

        internal string SessionToken { get; set; }

        internal string ETag { get; set; }

        internal Stream ResourceStream { get; set; }

        internal Memory<byte> ResourceBody
        {
            get => this.body;
            set => this.body = value;
        }

        internal virtual async Task MaterializeResourceAsync(CosmosSerializerCore serializerCore, CancellationToken cancellationToken)
        {
            if (this.body.IsEmpty && this.ResourceStream != null)
            {
                this.body = await BatchExecUtils.StreamToMemoryAsync(this.ResourceStream, cancellationToken);
            }
        }
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

        internal override Task MaterializeResourceAsync(CosmosSerializerCore serializerCore, CancellationToken cancellationToken)
        {
            if (this.body.IsEmpty && this.Resource != null)
            {
                this.ResourceStream = serializerCore.ToStream(this.Resource);
                return base.MaterializeResourceAsync(serializerCore, cancellationToken);
            }

            return Task.CompletedTask;
        }
    }
}
