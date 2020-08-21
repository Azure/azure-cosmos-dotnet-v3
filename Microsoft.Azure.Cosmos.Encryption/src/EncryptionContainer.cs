//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    internal sealed class EncryptionContainer : Container
    {
        private readonly Container container;
        private readonly CosmosSerializer cosmosSerializer;

        internal Encryptor Encryptor { get; }

        internal CosmosResponseFactory ResponseFactory { get; }

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
            this.cosmosSerializer = this.Database.Client.ClientOptions.Serializer;
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

            if (requestOptions is EncryptionItemRequestOptions encryptionItemRequestOptions &&
                encryptionItemRequestOptions.EncryptionOptions != null)
            {
                if (partitionKey == null)
                {
                    throw new NotSupportedException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
                }

                using (Stream itemStream = this.cosmosSerializer.ToStream<T>(item))
                using (ResponseMessage responseMessage = await this.CreateItemStreamAsync(
                    itemStream,
                    partitionKey.Value,
                    requestOptions,
                    cancellationToken))
                {
                    return this.ResponseFactory.CreateItemResponse<T>(responseMessage);
                }
            }
            else
            {
                return await this.container.CreateItemAsync<T>(
                    item,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
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
                if (requestOptions is EncryptionItemRequestOptions encryptionItemRequestOptions &&
                    encryptionItemRequestOptions.EncryptionOptions != null)
                {
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

                    responseMessage.Content = await this.DecryptResponseAsync(
                        responseMessage.Content,
                        encryptionItemRequestOptions.DecryptionResultHandler,
                        diagnosticsContext,
                        cancellationToken);

                    return responseMessage;
                }
                else
                {
                    return await this.container.CreateItemStreamAsync(
                        streamPayload,
                        partitionKey,
                        requestOptions,
                        cancellationToken);
                }
            }
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
            using (ResponseMessage responseMessage = await this.ReadItemStreamAsync(
                id,
                partitionKey,
                requestOptions,
                cancellationToken))
            {
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
                ResponseMessage responseMessage = await this.container.ReadItemStreamAsync(
                    id,
                    partitionKey,
                    requestOptions,
                    cancellationToken);

                Action<DecryptionResult> decryptionErroHandler = null;
                if (requestOptions is EncryptionItemRequestOptions encryptionItemRequestOptions)
                {
                    decryptionErroHandler = encryptionItemRequestOptions.DecryptionResultHandler;
                }

                responseMessage.Content = await this.DecryptResponseAsync(
                    responseMessage.Content,
                    decryptionErroHandler,
                    diagnosticsContext,
                    cancellationToken);

                return responseMessage;
            }
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

            if (requestOptions is EncryptionItemRequestOptions encryptionItemRequestOptions &&
                encryptionItemRequestOptions.EncryptionOptions != null)
            {
                if (partitionKey == null)
                {
                    throw new NotSupportedException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
                }

                using (Stream itemStream = this.cosmosSerializer.ToStream<T>(item))
                using (ResponseMessage responseMessage = await this.ReplaceItemStreamAsync(
                    itemStream,
                    id,
                    partitionKey.Value,
                    requestOptions,
                    cancellationToken))
                {
                    return this.ResponseFactory.CreateItemResponse<T>(responseMessage);
                }
            }
            else
            {
                return await this.container.ReplaceItemAsync(
                    item,
                    id,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
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
                if (requestOptions is EncryptionItemRequestOptions encryptionItemRequestOptions &&
                    encryptionItemRequestOptions.EncryptionOptions != null)
                {
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

                    responseMessage.Content = await this.DecryptResponseAsync(
                        responseMessage.Content,
                        encryptionItemRequestOptions.DecryptionResultHandler,
                        diagnosticsContext,
                        cancellationToken);

                    return responseMessage;
                }
                else
                {
                    return await this.container.ReplaceItemStreamAsync(
                        streamPayload,
                        id,
                        partitionKey,
                        requestOptions,
                        cancellationToken);
                }
            }
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

            if (requestOptions is EncryptionItemRequestOptions encryptionItemRequestOptions &&
                encryptionItemRequestOptions.EncryptionOptions != null)
            {
                if (partitionKey == null)
                {
                    throw new NotSupportedException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
                }

                using (Stream itemStream = this.cosmosSerializer.ToStream<T>(item))
                using (ResponseMessage responseMessage = await this.UpsertItemStreamAsync(
                    itemStream,
                    partitionKey.Value,
                    requestOptions,
                    cancellationToken))
                {
                    return this.ResponseFactory.CreateItemResponse<T>(responseMessage);
                }
            }
            else
            {
                return await this.container.UpsertItemAsync(
                    item,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
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
                if (requestOptions is EncryptionItemRequestOptions encryptionItemRequestOptions &&
                    encryptionItemRequestOptions.EncryptionOptions != null)
                {
                    if (partitionKey == null)
                    {
                        throw new ArgumentNullException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
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

                    responseMessage.Content = await this.DecryptResponseAsync(
                        responseMessage.Content,
                        encryptionItemRequestOptions.DecryptionResultHandler,
                        diagnosticsContext,
                        cancellationToken);

                    return responseMessage;
                }
                else
                {
                    return await this.container.UpsertItemStreamAsync(
                        streamPayload,
                        partitionKey,
                        requestOptions,
                        cancellationToken);
                }
            }
        }

        public override TransactionalBatch CreateTransactionalBatch(
            PartitionKey partitionKey)
        {
            return new EncryptionTransactionalBatch(
                this.container.CreateTransactionalBatch(partitionKey),
                this.Encryptor,
                this.cosmosSerializer);
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
            Action<DecryptionResult> decryptionResultHandler;
            if (requestOptions is EncryptionQueryRequestOptions encryptionQueryRequestOptions)
            {
                decryptionResultHandler = encryptionQueryRequestOptions.DecryptionResultHandler;
            }
            else
            {
                decryptionResultHandler = null;
            }

            return new EncryptionFeedIterator(
                this.container.GetItemQueryStreamIterator(
                    queryDefinition,
                    continuationToken,
                    requestOptions),
                this.Encryptor,
                decryptionResultHandler);
        }

        public override FeedIterator GetItemQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            Action<DecryptionResult> decryptionResultHandler;
            if (requestOptions is EncryptionQueryRequestOptions encryptionQueryRequestOptions)
            {
                decryptionResultHandler = encryptionQueryRequestOptions.DecryptionResultHandler;
            }
            else
            {
                decryptionResultHandler = null;
            }

            return new EncryptionFeedIterator(
                this.container.GetItemQueryStreamIterator(
                    queryText,
                    continuationToken,
                    requestOptions),
                this.Encryptor,
                decryptionResultHandler);
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

        public override FeedIterator GetChangeFeedStreamIterator(
            string continuationToken = null,
            ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            Action<DecryptionResult> decryptionResultHandler;
            if (changeFeedRequestOptions is EncryptionChangeFeedRequestOptions encryptionChangeFeedRequestOptions)
            {
                decryptionResultHandler = encryptionChangeFeedRequestOptions.DecryptionResultHandler;
            }
            else
            {
                decryptionResultHandler = null;
            }

            return new EncryptionFeedIterator(
                this.container.GetChangeFeedStreamIterator(
                    continuationToken,
                    changeFeedRequestOptions),
                this.Encryptor,
                decryptionResultHandler);
        }

        public override FeedIterator GetChangeFeedStreamIterator(
            FeedRange feedRange,
            ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            Action<DecryptionResult> decryptionResultHandler;
            if (changeFeedRequestOptions is EncryptionChangeFeedRequestOptions encryptionChangeFeedRequestOptions)
            {
                decryptionResultHandler = encryptionChangeFeedRequestOptions.DecryptionResultHandler;
            }
            else
            {
                decryptionResultHandler = null;
            }

            return new EncryptionFeedIterator(
                this.container.GetChangeFeedStreamIterator(
                    feedRange,
                    changeFeedRequestOptions),
                this.Encryptor,
                decryptionResultHandler);
        }

        public override FeedIterator GetChangeFeedStreamIterator(
            PartitionKey partitionKey,
            ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            Action<DecryptionResult> decryptionResultHandler;
            if (changeFeedRequestOptions is EncryptionChangeFeedRequestOptions encryptionChangeFeedRequestOptions)
            {
                decryptionResultHandler = encryptionChangeFeedRequestOptions.DecryptionResultHandler;
            }
            else
            {
                decryptionResultHandler = null;
            }

            return new EncryptionFeedIterator(
                this.container.GetChangeFeedStreamIterator(
                    partitionKey,
                    changeFeedRequestOptions),
                this.Encryptor,
                decryptionResultHandler);
        }

        public override FeedIterator<T> GetChangeFeedIterator<T>(
            string continuationToken = null,
            ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return new EncryptionFeedIterator<T>(
                (EncryptionFeedIterator)this.GetChangeFeedStreamIterator(
                    continuationToken,
                    changeFeedRequestOptions),
                this.ResponseFactory);
        }

        public override FeedIterator<T> GetChangeFeedIterator<T>(
            FeedRange feedRange,
            ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return new EncryptionFeedIterator<T>(
                (EncryptionFeedIterator)this.GetChangeFeedStreamIterator(
                    feedRange,
                    changeFeedRequestOptions),
                this.ResponseFactory);
        }

        public override FeedIterator<T> GetChangeFeedIterator<T>(
            PartitionKey partitionKey,
            ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return new EncryptionFeedIterator<T>(
                (EncryptionFeedIterator)this.GetChangeFeedStreamIterator(
                    partitionKey,
                    changeFeedRequestOptions),
                this.ResponseFactory);
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
            Action<DecryptionResult> decryptionResultHandler;
            if (requestOptions is EncryptionQueryRequestOptions encryptionQueryRequestOptions)
            {
                decryptionResultHandler = encryptionQueryRequestOptions.DecryptionResultHandler;
            }
            else
            {
                decryptionResultHandler = null;
            }

            return new EncryptionFeedIterator(
                this.container.GetItemQueryStreamIterator(
                    feedRange,
                    queryDefinition,
                    continuationToken,
                    requestOptions),
                this.Encryptor,
                decryptionResultHandler);
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

        private async Task<Stream> DecryptResponseAsync(
            Stream input,
            Action<DecryptionResult> decryptionResultHandler,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (input == null)
            {
                return input;
            }

            try
            {
                return await EncryptionProcessor.DecryptAsync(
                    input,
                    this.Encryptor,
                    diagnosticsContext,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                input.Position = 0;
                if (decryptionResultHandler == null)
                {
                    throw;
                }

                using (MemoryStream memoryStream = new MemoryStream((int)input.Length))
                {
                    await input.CopyToAsync(memoryStream);
                    bool wasBufferReturned = memoryStream.TryGetBuffer(out ArraySegment<byte> encryptedStream);
                    Debug.Assert(wasBufferReturned);

                    decryptionResultHandler(
                        DecryptionResult.CreateFailure(
                            encryptedStream,
                            exception));
                }

                input.Position = 0;
                return input;
            }
        }
    }
}