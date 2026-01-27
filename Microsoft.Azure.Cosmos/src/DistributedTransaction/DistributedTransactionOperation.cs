// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.IO;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.Layouts;
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

        internal int GetApproximateSerializedLength()
        {
            int length = 0;

            if (this.PartitionKeyJson != null)
            {
                length += this.PartitionKeyJson.Length;
            }

            if (this.Id != null)
            {
                length += this.Id.Length;
            }

            if (this.CollectionResourceId != null)
            {
                length += this.CollectionResourceId.Length;
            }

            if (this.SessionToken != null)
            {
                length += this.SessionToken.Length;
            }

            length += this.body.Length;

            return length;
        }

        internal static Result WriteOperation(ref RowWriter writer, TypeArgument typeArg, DistributedTransactionOperation operation)
        {
            Result r = writer.WriteInt32("index", operation.OperationIndex);
            if (r != Result.Success)
            {
                return r;
            }

            if (operation.CollectionResourceId != null)
            {
                r = writer.WriteString("crid", operation.CollectionResourceId);
                if (r != Result.Success)
                {
                    return r;
                }
            }

            if (operation.PartitionKeyJson != null)
            {
                r = writer.WriteString("PartitionKey", operation.PartitionKeyJson);
                if (r != Result.Success)
                {
                    return r;
                }
            }

            r = writer.WriteInt32("Operation", (int)operation.OperationType);
            if (r != Result.Success)
            {
                return r;
            }

            if (!operation.ResourceBody.IsEmpty)
            {
                r = writer.WriteBinary("resourceBody", operation.ResourceBody.Span);
                if (r != Result.Success)
                {
                    return r;
                }
            }

            if (operation.SessionToken != null)
            {
                r = writer.WriteString("x-ms-session-token", operation.SessionToken);
                if (r != Result.Success)
                {
                    return r;
                }
            }

            if (operation.ETag != null)
            {
                r = writer.WriteString("etag", operation.ETag);
                if (r != Result.Success)
                {
                    return r;
                }
            }

            r = writer.WriteInt32("resourceType", (int)ResourceType.Document);
            if (r != Result.Success)
            {
                return r;
            }

            return Result.Success;
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
