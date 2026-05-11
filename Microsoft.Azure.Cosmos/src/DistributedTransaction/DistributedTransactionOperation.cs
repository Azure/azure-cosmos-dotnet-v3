// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents an operation on a document which will be executed as a part of a distributed transaction.
    /// </summary>
    internal class DistributedTransactionOperation
    {
        private static readonly byte[] IdPropertyNameUtf8 = Encoding.UTF8.GetBytes("id");

        protected Memory<byte> body;

        public DistributedTransactionOperation(
            OperationType operationType,
            int operationIndex,
            string database,
            string container,
            PartitionKey partitionKey,
            string id = null,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            this.OperationType = operationType;
            this.OperationIndex = operationIndex;
            this.PartitionKey = partitionKey;
            this.Database = database;
            this.Container = container;
            this.Id = id;
            this.RequestOptions = requestOptions;
        }

        public PartitionKey PartitionKey { get; internal set; }

        public string Database { get; internal set; }

        public string Container { get; internal set; }

        public OperationType OperationType { get; internal set; }

        public int OperationIndex { get; internal set; }

        public string Id { get; internal set; }

        public string CollectionResourceId { get; internal set; }

        public string DatabaseResourceId { get; internal set; }

        internal DistributedTransactionRequestOptions RequestOptions { get; }

        internal string PartitionKeyJson { get; set; }

        internal string SessionToken { get; set; }

        internal string ETag => this.OperationType == OperationType.Read
            ? this.RequestOptions?.IfNoneMatchEtag
            : this.RequestOptions?.IfMatchEtag;

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

            this.ReconcileIdWithBody();
        }

        /// <summary>
        /// For Create/Upsert operations the item id is always derived from the resource body.
        /// Extracts the top-level <c>id</c> string property and assigns it to <see cref="Id"/>.
        /// Throws <see cref="ArgumentException"/> if the body has no non-empty <c>id</c> string.
        /// </summary>
        private void ReconcileIdWithBody()
        {
            if (this.OperationType != Documents.OperationType.Create
                && this.OperationType != Documents.OperationType.Upsert)
            {
                return;
            }

            if (this.body.IsEmpty)
            {
                return;
            }

            string bodyId = TryReadIdFromJsonObject(this.body.Span);

            if (string.IsNullOrWhiteSpace(bodyId))
            {
                throw new ArgumentException(
                    $"The resource body for the {this.OperationType} operation at index {this.OperationIndex} does not contain a non-empty 'id' string property. Provide 'id' in the resource body.");
            }

            this.Id = bodyId;
        }

        /// <summary>
        /// Scans the top level of a JSON object for an "id" string property and returns its value.
        /// Returns null if the body is not a JSON object, has no "id" property, or the value is not a string.
        /// Allocation is limited to the returned id string itself.
        /// </summary>
        private static string TryReadIdFromJsonObject(ReadOnlySpan<byte> body)
        {
            try
            {
                Utf8JsonReader reader = new Utf8JsonReader(body);
                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                {
                    return null;
                }

                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    if (reader.ValueTextEquals(IdPropertyNameUtf8))
                    {
                        return reader.Read() && reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                    }

                    reader.Skip();
                }

                return null;
            }
            catch (JsonException)
            {
                return null;
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
            T resource,
            DistributedTransactionRequestOptions requestOptions = null)
            : base(operationType, operationIndex, database, container, partitionKey, id: null, requestOptions)
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
            T resource,
            DistributedTransactionRequestOptions requestOptions = null)
            : base(operationType, operationIndex, database, container, partitionKey, id, requestOptions)
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
