//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    internal sealed class EncryptionContainer : Container
    {
        private readonly Container container;

        public CosmosSerializer CosmosSerializer { get; }

        public Encryptor Encryptor { get; }

        public CosmosResponseFactory ResponseFactory { get; }

        /// <summary>
        /// All the operations / requests for exercising client-side encryption functionality need to be made using this EncryptionContainer instance.
        /// </summary>
        /// <param name="container">Regular cosmos container.</param>
        /// <param name="encryptor">Provider that allows encrypting and decrypting data.</param>
        public EncryptionContainer(
            Container container,
            Encryptor encryptor)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.Encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
            this.ResponseFactory = this.Database.Client.ResponseFactory;
            this.CosmosSerializer = this.Database.Client.ClientOptions.Serializer;
        }

        public override string Id => this.container.Id;

        public override Conflicts Conflicts => this.container.Conflicts;

        public override Scripts.Scripts Scripts => this.container.Scripts;

        public override Database Database => this.container.Database;

        public override async Task<ItemResponse<T>> CreateItemAsync<T>(
            T item,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (requestOptions is not EncryptionItemRequestOptions encryptionItemRequestOptions ||
                encryptionItemRequestOptions.EncryptionOptions == null)
            {
                return await this.container.CreateItemAsync<T>(
                    item,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
            }

            if (partitionKey == null)
            {
                throw new NotSupportedException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
            }

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("CreateItem"))
            {
                ResponseMessage responseMessage;

                if (item is EncryptableItem encryptableItem)
                {
                    using (Stream streamPayload = encryptableItem.ToStream(this.CosmosSerializer))
                    {
                        responseMessage = await this.CreateItemHelperAsync(
                            streamPayload,
                            partitionKey.Value,
                            requestOptions,
                            decryptResponse: false,
                            diagnosticsContext,
                            cancellationToken);
                    }

                    encryptableItem.SetDecryptableItem(
                        EncryptionProcessor.BaseSerializer.FromStream<JObject>(responseMessage.Content),
                        this.Encryptor,
                        this.CosmosSerializer);

                    return new EncryptionItemResponse<T>(
                        responseMessage,
                        item);
                }
                else
                {
                    using (Stream itemStream = this.CosmosSerializer.ToStream<T>(item))
                    {
                        responseMessage = await this.CreateItemHelperAsync(
                            itemStream,
                            partitionKey.Value,
                            requestOptions,
                            decryptResponse: true,
                            diagnosticsContext,
                            cancellationToken);
                    }

                    return this.ResponseFactory.CreateItemResponse<T>(responseMessage);
                }
            }
        }

        public override async Task<ResponseMessage> CreateItemStreamAsync(
            Stream streamPayload,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(streamPayload);
#else
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }
#endif

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("CreateItemStream"))
            {
                return await this.CreateItemHelperAsync(
                    streamPayload,
                    partitionKey,
                    requestOptions,
                    decryptResponse: true,
                    diagnosticsContext,
                    cancellationToken);
            }
        }

        private async Task<ResponseMessage> CreateItemHelperAsync(
            Stream streamPayload,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions,
            bool decryptResponse,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (requestOptions is not EncryptionItemRequestOptions encryptionItemRequestOptions ||
                encryptionItemRequestOptions.EncryptionOptions == null)
            {
                return await this.container.CreateItemStreamAsync(
                    streamPayload,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
            }

            streamPayload = await EncryptionProcessor.EncryptAsync(
                streamPayload,
                this.Encryptor,
                encryptionItemRequestOptions.EncryptionOptions,
                diagnosticsContext,
                cancellationToken);

            ResponseMessage responseMessage = await this.container.CreateItemStreamAsync(
                streamPayload,
                partitionKey,
                requestOptions,
                cancellationToken);

            if (decryptResponse)
            {
                (responseMessage.Content, _) = await EncryptionProcessor.DecryptAsync(
                    responseMessage.Content,
                    this.Encryptor,
                    diagnosticsContext,
                    cancellationToken);
            }

            return responseMessage;
        }

        public override Task<ItemResponse<T>> DeleteItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.container.DeleteItemAsync<T>(
                id,
                partitionKey,
                requestOptions,
                cancellationToken);
        }

        public override Task<ResponseMessage> DeleteItemStreamAsync(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.container.DeleteItemStreamAsync(
                id,
                partitionKey,
                requestOptions,
                cancellationToken);
        }

        public override async Task<ItemResponse<T>> ReadItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("ReadItem"))
            {
                ResponseMessage responseMessage;

                if (typeof(T) == typeof(DecryptableItem))
                {
                    responseMessage = await this.ReadItemHelperAsync(
                        id,
                        partitionKey,
                        requestOptions,
                        decryptResponse: false,
                        diagnosticsContext,
                        cancellationToken);

                    DecryptableItemCore decryptableItem = new (
                        EncryptionProcessor.BaseSerializer.FromStream<JObject>(responseMessage.Content),
                        this.Encryptor,
                        this.CosmosSerializer);

                    return new EncryptionItemResponse<T>(
                        responseMessage,
                        (T)(object)decryptableItem);
                }

                responseMessage = await this.ReadItemHelperAsync(
                    id,
                    partitionKey,
                    requestOptions,
                    decryptResponse: true,
                    diagnosticsContext,
                    cancellationToken);

                return this.ResponseFactory.CreateItemResponse<T>(responseMessage);
            }
        }

        public override async Task<ResponseMessage> ReadItemStreamAsync(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("ReadItemStream"))
            {
                return await this.ReadItemHelperAsync(
                    id,
                    partitionKey,
                    requestOptions,
                    decryptResponse: true,
                    diagnosticsContext,
                    cancellationToken);
            }
        }

        private async Task<ResponseMessage> ReadItemHelperAsync(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions,
            bool decryptResponse,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            ResponseMessage responseMessage = await this.container.ReadItemStreamAsync(
                id,
                partitionKey,
                requestOptions,
                cancellationToken);

            if (decryptResponse)
            {
                (responseMessage.Content, _) = await EncryptionProcessor.DecryptAsync(
                    responseMessage.Content,
                    this.Encryptor,
                    diagnosticsContext,
                    cancellationToken);
            }

            return responseMessage;
        }

        public override async Task<ItemResponse<T>> ReplaceItemAsync<T>(
            T item,
            string id,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(id);
            ArgumentNullException.ThrowIfNull(item);
#else
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }
#endif

            if (requestOptions is not EncryptionItemRequestOptions encryptionItemRequestOptions ||
                encryptionItemRequestOptions.EncryptionOptions == null)
            {
                return await this.container.ReplaceItemAsync(
                    item,
                    id,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
            }

            if (partitionKey == null)
            {
                throw new NotSupportedException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
            }

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("ReplaceItem"))
            {
                ResponseMessage responseMessage;

                if (item is EncryptableItem encryptableItem)
                {
                    using (Stream streamPayload = encryptableItem.ToStream(this.CosmosSerializer))
                    {
                        responseMessage = await this.ReplaceItemHelperAsync(
                            streamPayload,
                            id,
                            partitionKey.Value,
                            requestOptions,
                            decryptResponse: false,
                            diagnosticsContext,
                            cancellationToken);
                    }

                    encryptableItem.SetDecryptableItem(
                        EncryptionProcessor.BaseSerializer.FromStream<JObject>(responseMessage.Content),
                        this.Encryptor,
                        this.CosmosSerializer);

                    return new EncryptionItemResponse<T>(
                        responseMessage,
                        item);
                }
                else
                {
                    using (Stream itemStream = this.CosmosSerializer.ToStream<T>(item))
                    {
                        responseMessage = await this.ReplaceItemHelperAsync(
                            itemStream,
                            id,
                            partitionKey.Value,
                            requestOptions,
                            decryptResponse: true,
                            diagnosticsContext,
                            cancellationToken);
                    }

                    return this.ResponseFactory.CreateItemResponse<T>(responseMessage);
                }
            }
        }

        public override async Task<ResponseMessage> ReplaceItemStreamAsync(
            Stream streamPayload,
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(id);
            ArgumentNullException.ThrowIfNull(streamPayload);
#else
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }
#endif

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("ReplaceItemStream"))
            {
                return await this.ReplaceItemHelperAsync(
                    streamPayload,
                    id,
                    partitionKey,
                    requestOptions,
                    decryptResponse: true,
                    diagnosticsContext,
                    cancellationToken);
            }
        }

        private async Task<ResponseMessage> ReplaceItemHelperAsync(
            Stream streamPayload,
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions,
            bool decryptResponse,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (requestOptions is not EncryptionItemRequestOptions encryptionItemRequestOptions ||
                    encryptionItemRequestOptions.EncryptionOptions == null)
            {
                return await this.container.ReplaceItemStreamAsync(
                    streamPayload,
                    id,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
            }

            streamPayload = await EncryptionProcessor.EncryptAsync(
                streamPayload,
                this.Encryptor,
                encryptionItemRequestOptions.EncryptionOptions,
                diagnosticsContext,
                cancellationToken);

            ResponseMessage responseMessage = await this.container.ReplaceItemStreamAsync(
                streamPayload,
                id,
                partitionKey,
                requestOptions,
                cancellationToken);

            if (decryptResponse)
            {
                (responseMessage.Content, _) = await EncryptionProcessor.DecryptAsync(
                    responseMessage.Content,
                    this.Encryptor,
                    diagnosticsContext,
                    cancellationToken);
            }

            return responseMessage;
        }

        public override async Task<ItemResponse<T>> UpsertItemAsync<T>(
            T item,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (requestOptions is not EncryptionItemRequestOptions encryptionItemRequestOptions ||
                encryptionItemRequestOptions.EncryptionOptions == null)
            {
                return await this.container.UpsertItemAsync(
                    item,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
            }

            if (partitionKey == null)
            {
                throw new NotSupportedException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
            }

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("UpsertItem"))
            {
                ResponseMessage responseMessage;

                if (item is EncryptableItem encryptableItem)
                {
                    using (Stream streamPayload = encryptableItem.ToStream(this.CosmosSerializer))
                    {
                        responseMessage = await this.UpsertItemHelperAsync(
                            streamPayload,
                            partitionKey.Value,
                            requestOptions,
                            decryptResponse: false,
                            diagnosticsContext,
                            cancellationToken);
                    }

                    encryptableItem.SetDecryptableItem(
                        EncryptionProcessor.BaseSerializer.FromStream<JObject>(responseMessage.Content),
                        this.Encryptor,
                        this.CosmosSerializer);

                    return new EncryptionItemResponse<T>(
                        responseMessage,
                        item);
                }
                else
                {
                    using (Stream itemStream = this.CosmosSerializer.ToStream<T>(item))
                    {
                        responseMessage = await this.UpsertItemHelperAsync(
                            itemStream,
                            partitionKey.Value,
                            requestOptions,
                            decryptResponse: true,
                            diagnosticsContext,
                            cancellationToken);
                    }

                    return this.ResponseFactory.CreateItemResponse<T>(responseMessage);
                }
            }
        }

        public override async Task<ResponseMessage> UpsertItemStreamAsync(
            Stream streamPayload,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(streamPayload);
#else
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }
#endif

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("UpsertItemStream"))
            {
                return await this.UpsertItemHelperAsync(
                    streamPayload,
                    partitionKey,
                    requestOptions,
                    decryptResponse: true,
                    diagnosticsContext,
                    cancellationToken);
            }
        }

        private async Task<ResponseMessage> UpsertItemHelperAsync(
            Stream streamPayload,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions,
            bool decryptResponse,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (requestOptions is not EncryptionItemRequestOptions encryptionItemRequestOptions ||
                    encryptionItemRequestOptions.EncryptionOptions == null)
            {
                return await this.container.UpsertItemStreamAsync(
                    streamPayload,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
            }

            streamPayload = await EncryptionProcessor.EncryptAsync(
                streamPayload,
                this.Encryptor,
                encryptionItemRequestOptions.EncryptionOptions,
                diagnosticsContext,
                cancellationToken);

            ResponseMessage responseMessage = await this.container.UpsertItemStreamAsync(
                streamPayload,
                partitionKey,
                requestOptions,
                cancellationToken);

            if (decryptResponse)
            {
                (responseMessage.Content, _) = await EncryptionProcessor.DecryptAsync(
                    responseMessage.Content,
                    this.Encryptor,
                    diagnosticsContext,
                    cancellationToken);
            }

            return responseMessage;
        }

        public override TransactionalBatch CreateTransactionalBatch(
            PartitionKey partitionKey)
        {
            return new EncryptionTransactionalBatch(
                this.container.CreateTransactionalBatch(partitionKey),
                this.Encryptor,
                this.CosmosSerializer);
        }

        public override Task<ContainerResponse> DeleteContainerAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.container.DeleteContainerAsync(
                requestOptions,
                cancellationToken);
        }

        public override Task<ResponseMessage> DeleteContainerStreamAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.container.DeleteContainerStreamAsync(
                requestOptions,
                cancellationToken);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedEstimatorBuilder(
            string processorName,
            ChangesEstimationHandler estimationDelegate,
            TimeSpan? estimationPeriod = null)
        {
            return this.container.GetChangeFeedEstimatorBuilder(
                processorName,
                estimationDelegate,
                estimationPeriod);
        }

        public override IOrderedQueryable<T> GetItemLinqQueryable<T>(
            bool allowSynchronousQueryExecution = false,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CosmosLinqSerializerOptions linqSerializerOptions = null)
        {
            return this.container.GetItemLinqQueryable<T>(
                allowSynchronousQueryExecution,
                continuationToken,
                requestOptions,
                linqSerializerOptions);
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new EncryptionFeedIterator<T>(
                (EncryptionFeedIterator)this.GetItemQueryStreamIterator(
                    queryDefinition,
                    continuationToken,
                    requestOptions),
                this.ResponseFactory);
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new EncryptionFeedIterator<T>(
                (EncryptionFeedIterator)this.GetItemQueryStreamIterator(
                    queryText,
                    continuationToken,
                    requestOptions),
                this.ResponseFactory);
        }

        public override Task<ContainerResponse> ReadContainerAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.container.ReadContainerAsync(
                requestOptions,
                cancellationToken);
        }

        public override Task<ResponseMessage> ReadContainerStreamAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.container.ReadContainerStreamAsync(
                requestOptions,
                cancellationToken);
        }

        public override Task<int?> ReadThroughputAsync(
            CancellationToken cancellationToken = default)
        {
            return this.container.ReadThroughputAsync(cancellationToken);
        }

        public override Task<ThroughputResponse> ReadThroughputAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default)
        {
            return this.container.ReadThroughputAsync(
                requestOptions,
                cancellationToken);
        }

        public override Task<ContainerResponse> ReplaceContainerAsync(
            ContainerProperties containerProperties,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.container.ReplaceContainerAsync(
                containerProperties,
                requestOptions,
                cancellationToken);
        }

        public override Task<ResponseMessage> ReplaceContainerStreamAsync(
            ContainerProperties containerProperties,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.container.ReplaceContainerStreamAsync(
                containerProperties,
                requestOptions,
                cancellationToken);
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.container.ReplaceThroughputAsync(
                throughput,
                requestOptions,
                cancellationToken);
        }

        public override FeedIterator GetItemQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new EncryptionFeedIterator(
                this.container.GetItemQueryStreamIterator(
                    queryDefinition,
                    continuationToken,
                    requestOptions),
                this.Encryptor,
                this.CosmosSerializer);
        }

        public override FeedIterator GetItemQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new EncryptionFeedIterator(
                this.container.GetItemQueryStreamIterator(
                    queryText,
                    continuationToken,
                    requestOptions),
                this.Encryptor,
                this.CosmosSerializer);
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.container.ReplaceThroughputAsync(
                throughputProperties,
                requestOptions,
                cancellationToken);
        }

        public override Task<IReadOnlyList<FeedRange>> GetFeedRangesAsync(
            CancellationToken cancellationToken = default)
        {
            return this.container.GetFeedRangesAsync(cancellationToken);
        }

        public override FeedIterator GetItemQueryStreamIterator(
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions = null)
        {
            return new EncryptionFeedIterator(
                this.container.GetItemQueryStreamIterator(
                    feedRange,
                    queryDefinition,
                    continuationToken,
                    requestOptions),
                this.Encryptor,
                this.CosmosSerializer);
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new EncryptionFeedIterator<T>(
                (EncryptionFeedIterator)this.GetItemQueryStreamIterator(
                    feedRange,
                    queryDefinition,
                    continuationToken,
                    requestOptions),
                this.ResponseFactory);
        }

        public override ChangeFeedEstimator GetChangeFeedEstimator(
            string processorName,
            Container leaseContainer)
        {
            return this.container.GetChangeFeedEstimator(processorName, leaseContainer);
        }

        public override FeedIterator GetChangeFeedStreamIterator(
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedMode changeFeedMode,
            ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return new EncryptionFeedIterator(
                this.container.GetChangeFeedStreamIterator(
                    changeFeedStartFrom,
                    changeFeedMode,
                    changeFeedRequestOptions),
                this.Encryptor,
                this.CosmosSerializer);
        }

        public override FeedIterator<T> GetChangeFeedIterator<T>(
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedMode changeFeedMode,
            ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return new EncryptionFeedIterator<T>(
                (EncryptionFeedIterator)this.GetChangeFeedStreamIterator(
                    changeFeedStartFrom,
                    changeFeedMode,
                    changeFeedRequestOptions),
                this.ResponseFactory);
        }

        public override Task<ItemResponse<T>> PatchItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            IReadOnlyList<PatchOperation> patchOperations,
            PatchItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override Task<ResponseMessage> PatchItemStreamAsync(
            string id,
            PartitionKey partitionKey,
            IReadOnlyList<PatchOperation> patchOperations,
            PatchItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(
            string processorName,
            ChangesHandler<T> onChangesDelegate)
        {
            return this.container.GetChangeFeedProcessorBuilder(
                processorName,
                async (
                    IReadOnlyCollection<JObject> documents,
                    CancellationToken cancellationToken) =>
                {
                    List<T> decryptItems = await this.DecryptChangeFeedDocumentsAsync<T>(
                        documents,
                        cancellationToken);

                    // Call the original passed in delegate
                    await onChangesDelegate(decryptItems, cancellationToken);
                });
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(
            string processorName,
            ChangeFeedHandler<T> onChangesDelegate)
        {
            return this.container.GetChangeFeedProcessorBuilder(
                processorName,
                async (
                    ChangeFeedProcessorContext context,
                    IReadOnlyCollection<JObject> documents,
                    CancellationToken cancellationToken) =>
                {
                    List<T> decryptItems = await this.DecryptChangeFeedDocumentsAsync<T>(
                        documents,
                        cancellationToken);

                    // Call the original passed in delegate
                    await onChangesDelegate(context, decryptItems, cancellationToken);
                });
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithManualCheckpoint<T>(
            string processorName,
            ChangeFeedHandlerWithManualCheckpoint<T> onChangesDelegate)
        {
            return this.container.GetChangeFeedProcessorBuilderWithManualCheckpoint(
                processorName,
                async (
                    ChangeFeedProcessorContext context,
                    IReadOnlyCollection<JObject> documents,
                    Func<Task> tryCheckpointAsync,
                    CancellationToken cancellationToken) =>
                {
                    List<T> decryptItems = await this.DecryptChangeFeedDocumentsAsync<T>(
                        documents,
                        cancellationToken);

                    // Call the original passed in delegate
                    await onChangesDelegate(context, decryptItems, tryCheckpointAsync, cancellationToken);
                });
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder(
            string processorName,
            ChangeFeedStreamHandler onChangesDelegate)
        {
            return this.container.GetChangeFeedProcessorBuilder(
                processorName,
                async (
                    ChangeFeedProcessorContext context,
                    Stream changes,
                    CancellationToken cancellationToken) =>
                {
                    Stream decryptedChanges = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(
                        changes,
                        this.Encryptor,
                        cancellationToken);

                    // Call the original passed in delegate
                    await onChangesDelegate(context, decryptedChanges, cancellationToken);
                });
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithManualCheckpoint(
            string processorName,
            ChangeFeedStreamHandlerWithManualCheckpoint onChangesDelegate)
        {
            return this.container.GetChangeFeedProcessorBuilderWithManualCheckpoint(
                processorName,
                async (
                    ChangeFeedProcessorContext context,
                    Stream changes,
                    Func<Task> tryCheckpointAsync,
                    CancellationToken cancellationToken) =>
                {
                    Stream decryptedChanges = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(
                        changes,
                        this.Encryptor,
                        cancellationToken);

                    // Call the original passed in delegate
                    await onChangesDelegate(context, decryptedChanges, tryCheckpointAsync, cancellationToken);
                });
        }

        public override Task<ResponseMessage> ReadManyItemsStreamAsync(
            IReadOnlyList<(string id, PartitionKey partitionKey)> items,
            ReadManyRequestOptions readManyRequestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ReadManyItemsHelperAsync(
                items,
                readManyRequestOptions,
                cancellationToken);
        }

        public override async Task<FeedResponse<T>> ReadManyItemsAsync<T>(
            IReadOnlyList<(string id, PartitionKey partitionKey)> items,
            ReadManyRequestOptions readManyRequestOptions = null,
            CancellationToken cancellationToken = default)
        {
            ResponseMessage responseMessage = await this.ReadManyItemsHelperAsync(
                items,
                readManyRequestOptions,
                cancellationToken);

            return this.ResponseFactory.CreateItemFeedResponse<T>(responseMessage);
        }

#if ENCRYPTIONPREVIEW
        public override Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            FeedRange feedRange,
            CancellationToken cancellationToken = default)
        {
            return this.container.GetPartitionKeyRangesAsync(feedRange, cancellationToken);
        }

        public override Task<ResponseMessage> DeleteAllItemsByPartitionKeyStreamAsync(
               Cosmos.PartitionKey partitionKey,
               RequestOptions requestOptions = null,
               CancellationToken cancellationToken = default)
        {
            return this.container.DeleteAllItemsByPartitionKeyStreamAsync(
                partitionKey,
                requestOptions,
                cancellationToken);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes<T>(string processorName, ChangeFeedHandler<ChangeFeedItem<T>> onChangesDelegate)
        {
            return this.container.GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes(
                processorName,
                onChangesDelegate);
        }
#endif

#if SDKPROJECTREF
        public override Task<bool> IsFeedRangePartOfAsync(
            Cosmos.FeedRange x,
            Cosmos.FeedRange y,
            CancellationToken cancellationToken = default)
        {
            return this.container.IsFeedRangePartOfAsync(
                x,
                y,
                cancellationToken);
        }
#endif

        private async Task<ResponseMessage> ReadManyItemsHelperAsync(
           IReadOnlyList<(string id, PartitionKey partitionKey)> items,
           ReadManyRequestOptions readManyRequestOptions = null,
           CancellationToken cancellationToken = default)
        {
            ResponseMessage responseMessage = await this.container.ReadManyItemsStreamAsync(
                items,
                readManyRequestOptions,
                cancellationToken);

            Stream decryptedContent = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(
                responseMessage.Content,
                this.Encryptor,
                cancellationToken);

            return new DecryptedResponseMessage(responseMessage, decryptedContent);
        }

        private async Task<List<T>> DecryptChangeFeedDocumentsAsync<T>(
            IReadOnlyCollection<JObject> documents,
            CancellationToken cancellationToken)
        {
            List<T> decryptItems = new (documents.Count);
            if (typeof(T) == typeof(DecryptableItem))
            {
                foreach (JToken value in documents)
                {
                    DecryptableItemCore item = new (
                        value,
                        this.Encryptor,
                        this.CosmosSerializer);

                    decryptItems.Add((T)(object)item);
                }
            }
            else
            {
                foreach (JObject document in documents)
                {
                    CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(null);
                    using (diagnosticsContext.CreateScope("DecryptChangeFeedDocumentsAsync<"))
                    {
                        (JObject decryptedDocument, DecryptionContext _) = await EncryptionProcessor.DecryptAsync(
                            document,
                            this.Encryptor,
                            diagnosticsContext,
                            cancellationToken);

                        decryptItems.Add(decryptedDocument.ToObject<T>());
                    }
                }
            }

            return decryptItems;
        }
    }
}