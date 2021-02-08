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
    using Microsoft.Azure.Cosmos;
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

            if (!(requestOptions is EncryptionItemRequestOptions encryptionItemRequestOptions) ||
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
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

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
            if (!(requestOptions is EncryptionItemRequestOptions encryptionItemRequestOptions) ||
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

                    DecryptableItemCore decryptableItem = new DecryptableItemCore(
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
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (!(requestOptions is EncryptionItemRequestOptions encryptionItemRequestOptions) ||
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
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

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
            if (!(requestOptions is EncryptionItemRequestOptions encryptionItemRequestOptions) ||
                    encryptionItemRequestOptions.EncryptionOptions == null)
            {
                return await this.container.ReplaceItemStreamAsync(
                    streamPayload,
                    id,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
            }

            if (partitionKey == null)
            {
                throw new NotSupportedException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
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

            if (!(requestOptions is EncryptionItemRequestOptions encryptionItemRequestOptions) ||
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
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

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
            if (!(requestOptions is EncryptionItemRequestOptions encryptionItemRequestOptions) ||
                    encryptionItemRequestOptions.EncryptionOptions == null)
            {
                return await this.container.UpsertItemStreamAsync(
                    streamPayload,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
            }

            if (partitionKey == null)
            {
                throw new NotSupportedException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
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
            QueryRequestOptions requestOptions = null)
        {
            return this.container.GetItemLinqQueryable<T>(
                allowSynchronousQueryExecution,
                continuationToken,
                requestOptions);
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

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(
            string processorName,
            ChangesHandler<T> onChangesDelegate)
        {
            // TODO: need client SDK to expose underlying feedIterator to make decryption work for this scenario
            // Issue #1484
            return this.container.GetChangeFeedProcessorBuilder(
                processorName,
                onChangesDelegate);
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

        public override Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            FeedRange feedRange,
            CancellationToken cancellationToken = default)
        {
            return this.container.GetPartitionKeyRangesAsync(feedRange, cancellationToken);
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
    }
}