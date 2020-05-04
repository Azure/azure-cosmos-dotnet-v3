//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using Microsoft.Azure.Cosmos;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

#if PREVIEW
    public
#else
    internal
#endif
    class EncryptionContainer : Container
    {
        public Container Container { get; }

        /// <summary>
        /// Provider that allows encrypting and decrypting data.
        /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
        /// </summary>
        public Encryptor Encryptor { get; }

        public Action<byte[], Exception> ErrorHandler { get; }

        public EncryptionContainer(
            Container container, 
            Encryptor encryptor, 
            Action<byte[], Exception> errorHandler = null)
        {
            if(container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            if (encryptor == null)
            {
                throw new ArgumentNullException(nameof(encryptor));
            }

            this.Container = container;
            this.Encryptor = encryptor;
            this.ErrorHandler = errorHandler;
        }

        public override string Id => this.Container.Id;

        public override Conflicts Conflicts => this.Container.Conflicts;

        public override Scripts.Scripts Scripts => this.Container.Scripts;

        public override Database Database => this.Container.Database;

        public override async Task<ItemResponse<T>> CreateItemAsync<T>(T item, PartitionKey? partitionKey = null, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("CreateItem"))
            {
                if (requestOptions is EncryptionItemRequestOptions encryptionItemRequest && 
                    encryptionItemRequest.EncryptionOptions != null)
                {
                    if (partitionKey == null)
                    {
                        throw new NotSupportedException($"PartitionKey cannot be null for operations using EncryptionContainer.");
                    }

                    Stream itemStream = this.Database.Client.ClientOptions.Serializer.ToStream<T>(item);
                    ResponseMessage responseMessage = await this.CreateItemStreamAsync(itemStream, partitionKey.Value, requestOptions, cancellationToken);
                    return this.Database.Client.ResponseFactory.CreateItemResponse<T>(responseMessage);
                }                
                else
                {
                    return await this.Container.CreateItemAsync<T>(item, partitionKey, requestOptions, cancellationToken);
                }
            }
        }

        public override async Task<ResponseMessage> CreateItemStreamAsync(Stream streamPayload, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("CreateItemStream"))
            {
                if (requestOptions is EncryptionItemRequestOptions encryptionItemRequest &&
                    encryptionItemRequest.EncryptionOptions != null)
                {
                    streamPayload = await EncryptionProcessor.EncryptAsync(
                        streamPayload,
                        encryptionItemRequest.EncryptionOptions,
                        this.Encryptor,
                        diagnosticsContext,
                        cancellationToken);

                    ResponseMessage responseMessage = await this.Container.CreateItemStreamAsync(streamPayload, partitionKey, requestOptions, cancellationToken);
                    responseMessage.Content = await this.DecryptResponseAsync(responseMessage.Content, diagnosticsContext, cancellationToken);
                    return responseMessage;
                }
                else
                {
                    return await this.Container.CreateItemStreamAsync(streamPayload, partitionKey, requestOptions, cancellationToken);
                }
            }
        }

        public override Task<ItemResponse<T>> DeleteItemAsync<T>(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            return this.Container.DeleteItemAsync<T>(id, partitionKey, requestOptions, cancellationToken);
        }

        public override Task<ResponseMessage> DeleteItemStreamAsync(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            return this.Container.DeleteItemStreamAsync(id, partitionKey, requestOptions, cancellationToken);
        }

        public async override Task<ItemResponse<T>> ReadItemAsync<T>(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("ReadItem"))
            {
                ResponseMessage responseMessage = await this.ReadItemStreamAsync(id, partitionKey, requestOptions, cancellationToken);
                return this.Database.Client.ResponseFactory.CreateItemResponse<T>(responseMessage);
            }
        }

        public async override Task<ResponseMessage> ReadItemStreamAsync(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("ReadItemStream"))
            {
                ResponseMessage responseMessage = await this.Container.ReadItemStreamAsync(id, partitionKey, requestOptions, cancellationToken);
                responseMessage.Content = await this.DecryptResponseAsync(responseMessage.Content, diagnosticsContext, cancellationToken);
                return responseMessage;
            }
        }

        public async override Task<ItemResponse<T>> ReplaceItemAsync<T>(T item, string id, PartitionKey? partitionKey = null, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("ReplaceItem"))
            {
                if (requestOptions is EncryptionItemRequestOptions encryptionItemRequest &&
                    encryptionItemRequest.EncryptionOptions != null)
                {
                    if (partitionKey == null)
                    {
                        throw new NotSupportedException($"PartitionKey cannot be null for operations using EncryptionContainer.");
                    }

                    Stream itemStream = this.Database.Client.ClientOptions.Serializer.ToStream<T>(item);
                    ResponseMessage responseMessage = await this.ReplaceItemStreamAsync(itemStream, id, partitionKey.Value, requestOptions, cancellationToken);
                    return this.Database.Client.ResponseFactory.CreateItemResponse<T>(responseMessage);
                }
                else
                {
                    return await this.Container.ReplaceItemAsync(item, id, partitionKey, requestOptions, cancellationToken);
                }
            }
        }

        public async override Task<ResponseMessage> ReplaceItemStreamAsync(Stream streamPayload, string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("ReplaceItemStream"))
            {
                if (requestOptions is EncryptionItemRequestOptions encryptionItemRequest &&
                    encryptionItemRequest.EncryptionOptions != null)
                {
                    if (partitionKey == null)
                    {
                        throw new NotSupportedException($"PartitionKey cannot be null for operations using EncryptionContainer.");
                    }

                    streamPayload = await EncryptionProcessor.EncryptAsync(
                        streamPayload,
                        encryptionItemRequest.EncryptionOptions,
                        this.Encryptor,
                        diagnosticsContext,
                        cancellationToken);

                    ResponseMessage responseMessage = await this.Container.ReplaceItemStreamAsync(streamPayload, id, partitionKey, requestOptions, cancellationToken);
                    responseMessage.Content = await this.DecryptResponseAsync(responseMessage.Content, diagnosticsContext, cancellationToken);
                    return responseMessage;
                }
                else
                {
                    return await this.Container.ReplaceItemStreamAsync(streamPayload, id, partitionKey, requestOptions, cancellationToken);
                }
            }
        }

        public async override Task<ItemResponse<T>> UpsertItemAsync<T>(T item, PartitionKey? partitionKey = null, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("UpsertItem"))
            {
                if (requestOptions is EncryptionItemRequestOptions encryptionItemRequest &&
                    encryptionItemRequest.EncryptionOptions != null)
                {
                    if (partitionKey == null)
                    {
                        throw new NotSupportedException($"PartitionKey cannot be null for operations using EncryptionContainer.");
                    }

                    Stream itemStream = this.Database.Client.ClientOptions.Serializer.ToStream<T>(item);
                    ResponseMessage responseMessage = await this.UpsertItemStreamAsync(itemStream, partitionKey.Value, requestOptions, cancellationToken);
                    return this.Database.Client.ResponseFactory.CreateItemResponse<T>(responseMessage);
                }
                else
                {
                    return await this.Container.UpsertItemAsync(item, partitionKey, requestOptions, cancellationToken);
                }
            }
        }

        public async override Task<ResponseMessage> UpsertItemStreamAsync(Stream streamPayload, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("UpsertItemStream"))
            {
                if (requestOptions is EncryptionItemRequestOptions encryptionItemRequest &&
                    encryptionItemRequest.EncryptionOptions != null)
                {
                    if (partitionKey == null)
                    {
                        throw new NotSupportedException($"PartitionKey cannot be null for operations using EncryptionContainer.");
                    }

                    streamPayload = await EncryptionProcessor.EncryptAsync(
                        streamPayload,
                        encryptionItemRequest.EncryptionOptions,
                        this.Encryptor,
                        diagnosticsContext,
                        cancellationToken);

                    ResponseMessage responseMessage = await this.Container.UpsertItemStreamAsync(streamPayload, partitionKey, requestOptions, cancellationToken);
                    responseMessage.Content = await this.DecryptResponseAsync(responseMessage.Content, diagnosticsContext, cancellationToken);
                    return responseMessage;
                }
                else
                {
                    return await this.Container.UpsertItemStreamAsync(streamPayload, partitionKey, requestOptions, cancellationToken);
                }
            }
        }

        public override TransactionalBatch CreateTransactionalBatch(PartitionKey partitionKey)
        {
            return this.Container.CreateTransactionalBatch(partitionKey);
        }

        public override Task<ContainerResponse> DeleteContainerAsync(ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            return this.Container.DeleteContainerAsync(requestOptions, cancellationToken);
        }

        public override Task<ResponseMessage> DeleteContainerStreamAsync(ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            return this.Container.DeleteContainerStreamAsync(requestOptions, cancellationToken);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedEstimatorBuilder(string processorName, ChangesEstimationHandler estimationDelegate, TimeSpan? estimationPeriod = null)
        {
            return this.Container.GetChangeFeedEstimatorBuilder(processorName, estimationDelegate, estimationPeriod);
        }

        public override IOrderedQueryable<T> GetItemLinqQueryable<T>(bool allowSynchronousQueryExecution = false, string continuationToken = null, QueryRequestOptions requestOptions = null)
        {
            return this.Container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution, continuationToken, requestOptions);
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(QueryDefinition queryDefinition, string continuationToken = null, QueryRequestOptions requestOptions = null)
        {
            return new EncryptionFeedIterator<T>(
                this.GetItemQueryStreamIterator(queryDefinition, continuationToken, requestOptions),
                this.Database.Client.ResponseFactory);
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(string queryText = null, string continuationToken = null, QueryRequestOptions requestOptions = null)
        {
            return new EncryptionFeedIterator<T>(
                this.GetItemQueryStreamIterator(queryText, continuationToken, requestOptions), 
                this.Database.Client.ResponseFactory);
        }

        public override Task<ContainerResponse> ReadContainerAsync(ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            return this.Container.ReadContainerAsync(requestOptions, cancellationToken);
        }

        public override Task<ResponseMessage> ReadContainerStreamAsync(ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            return this.Container.ReadContainerStreamAsync(requestOptions, cancellationToken);
        }

        public override Task<int?> ReadThroughputAsync(CancellationToken cancellationToken = default)
        {
            return this.Container.ReadThroughputAsync(cancellationToken);
        }

        public override Task<ThroughputResponse> ReadThroughputAsync(RequestOptions requestOptions, CancellationToken cancellationToken = default)
        {
            return this.Container.ReadThroughputAsync(requestOptions, cancellationToken);
        }

        public override Task<ContainerResponse> ReplaceContainerAsync(ContainerProperties containerProperties, ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            return this.Container.ReplaceContainerAsync(containerProperties, requestOptions, cancellationToken);
        }

        public override Task<ResponseMessage> ReplaceContainerStreamAsync(ContainerProperties containerProperties, ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            return this.Container.ReplaceContainerStreamAsync(containerProperties, requestOptions, cancellationToken);
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(int throughput, RequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            return this.Container.ReplaceThroughputAsync(throughput, requestOptions, cancellationToken);
        }

        public override FeedIterator GetItemQueryStreamIterator(QueryDefinition queryDefinition, string continuationToken = null, QueryRequestOptions requestOptions = null)
        {
            return new EncryptionFeedIterator(
                this.Container.GetItemQueryStreamIterator(queryDefinition, continuationToken, requestOptions), 
                this.Encryptor,
                this.ErrorHandler);
        }

        public override FeedIterator GetItemQueryStreamIterator(string queryText = null, string continuationToken = null, QueryRequestOptions requestOptions = null)
        {
            return new EncryptionFeedIterator(
                this.Container.GetItemQueryStreamIterator(queryText, continuationToken, requestOptions), 
                this.Encryptor,
                this.ErrorHandler);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(string processorName, ChangesHandler<T> onChangesDelegate)
        {
            // TODO: need client SDK to expose underlying feedIterator to make decryption work for this scenario
            return this.Container.GetChangeFeedProcessorBuilder(processorName, onChangesDelegate);
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(ThroughputProperties throughputProperties, RequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            return this.Container.ReplaceThroughputAsync(throughputProperties, requestOptions, cancellationToken);
        }

        public override Task<IReadOnlyList<FeedRange>> GetFeedRangesAsync(CancellationToken cancellationToken = default)
        {
            return this.Container.GetFeedRangesAsync(cancellationToken);
        }

        public override FeedIterator GetChangeFeedStreamIterator(string continuationToken = null, ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return new EncryptionFeedIterator(
                this.Container.GetChangeFeedStreamIterator(continuationToken, changeFeedRequestOptions),
                this.Encryptor,
                this.ErrorHandler);
        }

        public override FeedIterator GetChangeFeedStreamIterator(FeedRange feedRange, ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return new EncryptionFeedIterator(
                this.Container.GetChangeFeedStreamIterator(feedRange, changeFeedRequestOptions),
                this.Encryptor,
                this.ErrorHandler);
        }

        public override FeedIterator GetChangeFeedStreamIterator(PartitionKey partitionKey, ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return new EncryptionFeedIterator(
                this.Container.GetChangeFeedStreamIterator(partitionKey, changeFeedRequestOptions),
                this.Encryptor,
                this.ErrorHandler);
        }

        public override FeedIterator<T> GetChangeFeedIterator<T>(string continuationToken = null, ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return new EncryptionFeedIterator<T>(
                this.GetChangeFeedStreamIterator(continuationToken, changeFeedRequestOptions),
                this.Database.Client.ResponseFactory);
        }

        public override FeedIterator<T> GetChangeFeedIterator<T>(FeedRange feedRange, ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return new EncryptionFeedIterator<T>(
                this.GetChangeFeedStreamIterator(feedRange, changeFeedRequestOptions),
                this.Database.Client.ResponseFactory);
        }

        public override FeedIterator<T> GetChangeFeedIterator<T>(PartitionKey partitionKey, ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return new EncryptionFeedIterator<T>(
                this.GetChangeFeedStreamIterator(partitionKey, changeFeedRequestOptions),
                this.Database.Client.ResponseFactory);
        }

        public override Task<IEnumerable<string>> GetPartitionKeyRangesAsync(FeedRange feedRange, CancellationToken cancellationToken = default)
        {
            return this.Container.GetPartitionKeyRangesAsync(feedRange, cancellationToken);
        }

        public override FeedIterator GetItemQueryStreamIterator(FeedRange feedRange, QueryDefinition queryDefinition, string continuationToken, QueryRequestOptions requestOptions = null)
        {
            return new EncryptionFeedIterator(
                this.Container.GetItemQueryStreamIterator(feedRange, queryDefinition, continuationToken, requestOptions),
                this.Encryptor,
                this.ErrorHandler);
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(FeedRange feedRange, QueryDefinition queryDefinition, string continuationToken = null, QueryRequestOptions requestOptions = null)
        {
            return new EncryptionFeedIterator<T>(
                this.GetItemQueryStreamIterator(feedRange, queryDefinition, continuationToken, requestOptions),
                this.Database.Client.ResponseFactory);
        }

        private async Task<Stream> DecryptResponseAsync(
            Stream input,
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
            catch (Exception ex)
            {
                input.Position = 0;
                if (this.ErrorHandler != null)
                {
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        input.CopyTo(memoryStream);
                        this.ErrorHandler(memoryStream.ToArray(), ex);
                    }
                }
                return input;
            }
        }
    }
}
