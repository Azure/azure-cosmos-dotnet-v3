//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.IO;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.Layouts;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents an operation on an item which will be executed as part of a batch request
    /// on a container.
    /// </summary>
    internal class ItemBatchOperation : IDisposable
    {
#pragma warning disable SA1401 // Fields should be private
        protected Memory<byte> body;
#pragma warning restore SA1401 // Fields should be private
        private bool isDisposed;

        public ItemBatchOperation(
            OperationType operationType,
            int operationIndex,
            PartitionKey partitionKey,
            string id = null,
            Stream resourceStream = null,
            TransactionalBatchItemRequestOptions requestOptions = null,
            CosmosDiagnosticsContext diagnosticsContext = null)
        {
            this.OperationType = operationType;
            this.OperationIndex = operationIndex;
            this.PartitionKey = partitionKey;
            this.Id = id;
            this.ResourceStream = resourceStream;
            this.RequestOptions = requestOptions;
            this.DiagnosticsContext = diagnosticsContext;
        }

        public ItemBatchOperation(
            OperationType operationType,
            int operationIndex,
            ContainerInternal containerCore,
            string id = null,
            Stream resourceStream = null,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            this.OperationType = operationType;
            this.OperationIndex = operationIndex;
            this.ContainerInternal = containerCore;
            this.Id = id;
            this.ResourceStream = resourceStream;
            this.RequestOptions = requestOptions;
            this.DiagnosticsContext = null;
        }

        public PartitionKey? PartitionKey { get; internal set; }

        public string Id { get; }

        public OperationType OperationType { get; }

        public Stream ResourceStream { get; protected set; }

        public TransactionalBatchItemRequestOptions RequestOptions { get; }

        public int OperationIndex { get; internal set; }

        internal ContainerInternal ContainerInternal { get; }

        internal CosmosDiagnosticsContext DiagnosticsContext { get; set; }

        internal string PartitionKeyJson { get; set; }

        internal Documents.PartitionKey ParsedPartitionKey { get; set; }

        internal Memory<byte> ResourceBody
        {
            get
            {
                Debug.Assert(
                    this.ResourceStream == null || !this.body.IsEmpty,
                    "ResourceBody read without materialization of ResourceStream");

                return this.body;
            }

            set
            {
                this.body = value;
            }
        }

        /// <summary>
        /// Operational context used in stream operations.
        /// </summary>
        /// <seealso cref="BatchAsyncBatcher"/>
        /// <seealso cref="BatchAsyncStreamer"/>
        /// <seealso cref="BatchAsyncContainerExecutor"/>
        internal ItemBatchOperationContext Context { get; private set; }

        /// <summary>
        /// Disposes the current <see cref="ItemBatchOperation"/>.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }

        internal static Result WriteOperation(ref RowWriter writer, TypeArgument typeArg, ItemBatchOperation operation)
        {
            bool pkWritten = false;
            Result r = writer.WriteInt32("operationType", (int)operation.OperationType);
            if (r != Result.Success)
            {
                return r;
            }

            r = writer.WriteInt32("resourceType", (int)ResourceType.Document);
            if (r != Result.Success)
            {
                return r;
            }

            if (operation.PartitionKeyJson != null)
            {
                r = writer.WriteString("partitionKey", operation.PartitionKeyJson);
                if (r != Result.Success)
                {
                    return r;
                }

                pkWritten = true;
            }

            if (operation.Id != null)
            {
                r = writer.WriteString("id", operation.Id);
                if (r != Result.Success)
                {
                    return r;
                }
            }

            if (!operation.ResourceBody.IsEmpty)
            {
                r = writer.WriteBinary("resourceBody", operation.ResourceBody.Span);
                if (r != Result.Success)
                {
                    return r;
                }
            }

            if (operation.RequestOptions != null)
            {
                TransactionalBatchItemRequestOptions options = operation.RequestOptions;
                if (options.IndexingDirective.HasValue)
                {
                    string indexingDirectiveString = IndexingDirectiveStrings.FromIndexingDirective(options.IndexingDirective.Value);
                    r = writer.WriteString("indexingDirective", indexingDirectiveString);
                    if (r != Result.Success)
                    {
                        return r;
                    }
                }

                if (ItemRequestOptions.ShouldSetNoContentHeader(
                    options.NoContentResponseOnWrite,
                    options.NoContentResponseOnRead,
                    operation.OperationType))
                {
                    r = writer.WriteBool("minimalReturnPreference", true);
                    if (r != Result.Success)
                    {
                        return r;
                    }
                }

                if (options.IfMatchEtag != null)
                {
                    r = writer.WriteString("ifMatch", options.IfMatchEtag);
                    if (r != Result.Success)
                    {
                        return r;
                    }
                }
                else if (options.IfNoneMatchEtag != null)
                {
                    r = writer.WriteString("ifNoneMatch", options.IfNoneMatchEtag);
                    if (r != Result.Success)
                    {
                        return r;
                    }
                }

                if (options.Properties != null)
                {
                    if (options.Properties.TryGetValue(WFConstants.BackendHeaders.BinaryId, out object binaryIdObj))
                    {
                        byte[] binaryId = binaryIdObj as byte[];
                        if (binaryId != null)
                        {
                            r = writer.WriteBinary("binaryId", binaryId);
                            if (r != Result.Success)
                            {
                                return r;
                            }
                        }
                    }

                    if (options.Properties.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKey, out object epkObj))
                    {
                        byte[] epk = epkObj as byte[];
                        if (epk != null)
                        {
                            r = writer.WriteBinary("effectivePartitionKey", epk);
                            if (r != Result.Success)
                            {
                                return r;
                            }
                        }
                    }

                    if (!pkWritten && options.Properties.TryGetValue(
                            HttpConstants.HttpHeaders.PartitionKey,
                            out object pkStrObj))
                    {
                        string pkString = pkStrObj as string;
                        if (pkString != null)
                        {
                            r = writer.WriteString("partitionKey", pkString);
                            if (r != Result.Success)
                            {
                                return r;
                            }
                        }
                    }

                    if (options.Properties.TryGetValue(WFConstants.BackendHeaders.TimeToLiveInSeconds, out object ttlObj))
                    {
                        string ttlStr = ttlObj as string;
                        if (ttlStr != null && int.TryParse(ttlStr, out int ttl))
                        {
                            r = writer.WriteInt32("timeToLiveInSeconds", ttl);
                            if (r != Result.Success)
                            {
                                return r;
                            }
                        }
                    }
                }
            }

            return Result.Success;
        }

        /// <summary>
        /// Computes and returns an approximation for the length of this <see cref="ItemBatchOperation"/>.
        /// when serialized.
        /// </summary>
        /// <returns>An under-estimate of the length.</returns>
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

            length += this.body.Length;

            if (this.RequestOptions != null)
            {
                if (this.RequestOptions.IfMatchEtag != null)
                {
                    length += this.RequestOptions.IfMatchEtag.Length;
                }

                if (this.RequestOptions.IfNoneMatchEtag != null)
                {
                    length += this.RequestOptions.IfNoneMatchEtag.Length;
                }

                if (this.RequestOptions.IndexingDirective.HasValue)
                {
                    length += 7; // "Default", "Include", "Exclude" are possible values
                }

                if (this.RequestOptions.Properties != null)
                {
                    if (this.RequestOptions.Properties.TryGetValue(WFConstants.BackendHeaders.BinaryId, out object binaryIdObj))
                    {
                        byte[] binaryId = binaryIdObj as byte[];
                        if (binaryId != null)
                        {
                            length += binaryId.Length;
                        }
                    }

                    if (this.RequestOptions.Properties.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKey, out object epkObj))
                    {
                        byte[] epk = epkObj as byte[];
                        if (epk != null)
                        {
                            length += epk.Length;
                        }
                    }
                }
            }

            return length;
        }

        /// <summary>
        /// Encrypts (if encryption options are set) and materializes the operation's resource into a Memory{byte} wrapping a byte array.
        /// </summary>
        /// <param name="serializerCore">Serializer to serialize user provided objects to JSON.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> for cancellation.</param>
        internal virtual async Task EncryptAndMaterializeResourceAsync(CosmosSerializerCore serializerCore, CancellationToken cancellationToken)
        {
            if (this.body.IsEmpty && this.ResourceStream != null)
            {
                Stream stream = this.ResourceStream;
                if (this.ContainerInternal != null && this.RequestOptions?.EncryptionOptions != null)
                {
                    stream = await this.ContainerInternal.ClientContext.EncryptItemAsync(
                        stream,
                        this.RequestOptions.EncryptionOptions,
                        (DatabaseInternal)this.ContainerInternal.Database,
                        this.DiagnosticsContext,
                        cancellationToken);
                }

                this.body = await BatchExecUtils.StreamToMemoryAsync(stream, cancellationToken);
            }
        }

        /// <summary>
        /// Attached a context to the current operation to track resolution.
        /// </summary>
        /// <exception cref="InvalidOperationException">If the operation already had an attached context.</exception>
        internal void AttachContext(ItemBatchOperationContext context)
        {
            if (this.Context != null)
            {
                throw new InvalidOperationException("Cannot modify the current context of an operation.");
            }

            this.Context = context;
        }

        /// <summary>
        /// Disposes the disposable members held by this class.
        /// </summary>
        /// <param name="disposing">Indicates whether to dispose managed resources or not.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !this.isDisposed)
            {
                this.isDisposed = true;
                if (this.ResourceStream != null)
                {
                    this.ResourceStream.Dispose();
                    this.ResourceStream = null;
                }
            }
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
    internal class ItemBatchOperation<T> : ItemBatchOperation
#pragma warning restore SA1402 // File may only contain a single type
    {
        public ItemBatchOperation(
            OperationType operationType,
            int operationIndex,
            PartitionKey partitionKey,
            T resource,
            string id = null,
            TransactionalBatchItemRequestOptions requestOptions = null)
            : base(operationType, operationIndex, partitionKey: partitionKey, id: id, requestOptions: requestOptions)
        {
            this.Resource = resource;
        }

        public ItemBatchOperation(
            OperationType operationType,
            int operationIndex,
            T resource,
            ContainerInternal containerCore,
            string id = null,
            TransactionalBatchItemRequestOptions requestOptions = null)
            : base(operationType, operationIndex, containerCore: containerCore, id: id, requestOptions: requestOptions)
        {
            this.Resource = resource;
        }

        public T Resource { get; private set; }

        /// <summary>
        /// Materializes the operation's resource into a Memory{byte} wrapping a byte array.
        /// </summary>
        /// <param name="serializerCore">Serializer to serialize user provided objects to JSON.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> for cancellation.</param>
        internal override Task EncryptAndMaterializeResourceAsync(CosmosSerializerCore serializerCore, CancellationToken cancellationToken)
        {
            if (this.body.IsEmpty && this.Resource != null)
            {
                this.ResourceStream = serializerCore.ToStream(this.Resource);
                return base.EncryptAndMaterializeResourceAsync(serializerCore, cancellationToken);
            }

            return Task.CompletedTask;
        }
    }
}