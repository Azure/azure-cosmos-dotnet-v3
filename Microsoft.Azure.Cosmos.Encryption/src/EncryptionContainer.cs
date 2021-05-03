//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json.Linq;

    internal sealed class EncryptionContainer : Container
    {
        public Container Container { get; private set; }

        public CosmosSerializer CosmosSerializer { get; }

        public CosmosResponseFactory ResponseFactory { get; }

        public EncryptionProcessor EncryptionProcessor { get; }

        public EncryptionCosmosClient EncryptionCosmosClient { get; }

        private bool isEncryptionContainerCacheInitDone;

        private static readonly SemaphoreSlim CacheInitSema = new SemaphoreSlim(1, 1);

        /// <summary>
        /// All the operations / requests for exercising client-side encryption functionality need to be made using this EncryptionContainer instance.
        /// </summary>
        /// <param name="container">Regular cosmos container.</param>
        /// <param name="encryptionCosmosClient"> Cosmos Client configured with Encryption.</param>
        public EncryptionContainer(
            Container container,
            EncryptionCosmosClient encryptionCosmosClient)
        {
            this.Container = container ?? throw new ArgumentNullException(nameof(container));
            this.EncryptionCosmosClient = encryptionCosmosClient ?? throw new ArgumentNullException(nameof(container));

            this.EncryptionProcessor = new EncryptionProcessor(
                container,
                this.EncryptionCosmosClient);

            this.ResponseFactory = this.Database.Client.ResponseFactory;
            this.CosmosSerializer = this.Database.Client.ClientOptions.Serializer;
            this.isEncryptionContainerCacheInitDone = false;
        }

        public override string Id => this.Container.Id;

        public override Conflicts Conflicts => this.Container.Conflicts;

        public override Scripts.Scripts Scripts => this.Container.Scripts;

        public override Database Database => this.Container.Database;

        internal async Task InitEncryptionContainerCacheIfNotInitAsync(CancellationToken cancellationToken)
        {
            if (this.isEncryptionContainerCacheInitDone)
            {
                return;
            }

            if (await CacheInitSema.WaitAsync(-1))
            {
                if (!this.isEncryptionContainerCacheInitDone)
                {
                    try
                    {
                        await this.InitContainerCacheAsync(cancellationToken);
                        await this.EncryptionProcessor.InitEncryptionSettingsIfNotInitializedAsync();
                        this.isEncryptionContainerCacheInitDone = true;
                    }
                    finally
                    {
                        CacheInitSema.Release(1);
                    }
                }
                else
                {
                    CacheInitSema.Release(1);
                }
            }
        }

        internal async Task InitContainerCacheAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            EncryptionCosmosClient encryptionCosmosClient = this.EncryptionCosmosClient;
            ClientEncryptionPolicy clientEncryptionPolicy = await encryptionCosmosClient.GetClientEncryptionPolicyAsync(
                container: this,
                cancellationToken: cancellationToken,
                shouldForceRefresh: false);

            if (clientEncryptionPolicy != null)
            {
                foreach (string clientEncryptionKeyId in clientEncryptionPolicy.IncludedPaths.Select(p => p.ClientEncryptionKeyId).Distinct())
                {
                    await this.EncryptionCosmosClient.GetClientEncryptionKeyPropertiesAsync(
                        clientEncryptionKeyId: clientEncryptionKeyId,
                        container: this,
                        cancellationToken: cancellationToken,
                        shouldForceRefresh: false);
                }
            }
        }

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

            if (partitionKey == null)
            {
                throw new NotSupportedException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
            }

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("CreateItem"))
            {
                ResponseMessage responseMessage;

                using (Stream itemStream = this.CosmosSerializer.ToStream<T>(item))
                {
                    responseMessage = await this.CreateItemHelperAsync(
                        itemStream,
                        partitionKey.Value,
                        requestOptions,
                        diagnosticsContext,
                        cancellationToken);
                }

                return this.ResponseFactory.CreateItemResponse<T>(responseMessage);
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
                    diagnosticsContext,
                    cancellationToken);
            }
        }

        private async Task<ResponseMessage> CreateItemHelperAsync(
            Stream streamPayload,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            streamPayload = await this.EncryptionProcessor.EncryptAsync(
                streamPayload,
                diagnosticsContext,
                cancellationToken);

            ResponseMessage responseMessage = await this.Container.CreateItemStreamAsync(
                streamPayload,
                partitionKey,
                requestOptions,
                cancellationToken);

            responseMessage.Content = await this.EncryptionProcessor.DecryptAsync(
                    responseMessage.Content,
                    diagnosticsContext,
                    cancellationToken);

            return responseMessage;
        }

        public override Task<ItemResponse<T>> DeleteItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.Container.DeleteItemAsync<T>(
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
            return this.Container.DeleteItemStreamAsync(
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

                responseMessage = await this.ReadItemHelperAsync(
                    id,
                    partitionKey,
                    requestOptions,
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
                    diagnosticsContext,
                    cancellationToken);
            }
        }

        private async Task<ResponseMessage> ReadItemHelperAsync(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            ResponseMessage responseMessage = await this.Container.ReadItemStreamAsync(
                id,
                partitionKey,
                requestOptions,
                cancellationToken);

            responseMessage.Content = await this.EncryptionProcessor.DecryptAsync(
                    responseMessage.Content,
                    diagnosticsContext,
                    cancellationToken);

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

            if (partitionKey == null)
            {
                throw new NotSupportedException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
            }

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("ReplaceItem"))
            {
                ResponseMessage responseMessage;

                using (Stream itemStream = this.CosmosSerializer.ToStream<T>(item))
                {
                    responseMessage = await this.ReplaceItemHelperAsync(
                        itemStream,
                        id,
                        partitionKey.Value,
                        requestOptions,
                        diagnosticsContext,
                        cancellationToken);
                }

                return this.ResponseFactory.CreateItemResponse<T>(responseMessage);
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
                    diagnosticsContext,
                    cancellationToken);
            }
        }

        private async Task<ResponseMessage> ReplaceItemHelperAsync(
            Stream streamPayload,
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (partitionKey == null)
            {
                throw new NotSupportedException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
            }

            streamPayload = await this.EncryptionProcessor.EncryptAsync(
                streamPayload,
                diagnosticsContext,
                cancellationToken);

            ResponseMessage responseMessage = await this.Container.ReplaceItemStreamAsync(
                streamPayload,
                id,
                partitionKey,
                requestOptions,
                cancellationToken);

            responseMessage.Content = await this.EncryptionProcessor.DecryptAsync(
                    responseMessage.Content,
                    diagnosticsContext,
                    cancellationToken);

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

            if (partitionKey == null)
            {
                throw new NotSupportedException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
            }

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("UpsertItem"))
            {
                ResponseMessage responseMessage;

                using (Stream itemStream = this.CosmosSerializer.ToStream<T>(item))
                {
                    responseMessage = await this.UpsertItemHelperAsync(
                        itemStream,
                        partitionKey.Value,
                        requestOptions,
                        diagnosticsContext,
                        cancellationToken);
                }

                return this.ResponseFactory.CreateItemResponse<T>(responseMessage);
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
                    diagnosticsContext,
                    cancellationToken);
            }
        }

        private async Task<ResponseMessage> UpsertItemHelperAsync(
            Stream streamPayload,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (partitionKey == null)
            {
                throw new NotSupportedException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
            }

            streamPayload = await this.EncryptionProcessor.EncryptAsync(
                streamPayload,
                diagnosticsContext,
                cancellationToken);

            ResponseMessage responseMessage = await this.Container.UpsertItemStreamAsync(
                streamPayload,
                partitionKey,
                requestOptions,
                cancellationToken);

            responseMessage.Content = await this.EncryptionProcessor.DecryptAsync(
                    responseMessage.Content,
                    diagnosticsContext,
                    cancellationToken);

            return responseMessage;
        }

        public override TransactionalBatch CreateTransactionalBatch(
            PartitionKey partitionKey)
        {
            return new EncryptionTransactionalBatch(
                this.Container.CreateTransactionalBatch(partitionKey),
                this.EncryptionProcessor,
                this.CosmosSerializer);
        }

        public override Task<ContainerResponse> DeleteContainerAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.Container.DeleteContainerAsync(
                requestOptions,
                cancellationToken);
        }

        public override Task<ResponseMessage> DeleteContainerStreamAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.Container.DeleteContainerStreamAsync(
                requestOptions,
                cancellationToken);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedEstimatorBuilder(
            string processorName,
            ChangesEstimationHandler estimationDelegate,
            TimeSpan? estimationPeriod = null)
        {
            return this.Container.GetChangeFeedEstimatorBuilder(
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
            return this.Container.GetItemLinqQueryable<T>(
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
            return this.Container.ReadContainerAsync(
                requestOptions,
                cancellationToken);
        }

        public override Task<ResponseMessage> ReadContainerStreamAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.Container.ReadContainerStreamAsync(
                requestOptions,
                cancellationToken);
        }

        public override Task<int?> ReadThroughputAsync(
            CancellationToken cancellationToken = default)
        {
            return this.Container.ReadThroughputAsync(cancellationToken);
        }

        public override Task<ThroughputResponse> ReadThroughputAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default)
        {
            return this.Container.ReadThroughputAsync(
                requestOptions,
                cancellationToken);
        }

        public override Task<ContainerResponse> ReplaceContainerAsync(
            ContainerProperties containerProperties,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.Container.ReplaceContainerAsync(
                containerProperties,
                requestOptions,
                cancellationToken);
        }

        public override Task<ResponseMessage> ReplaceContainerStreamAsync(
            ContainerProperties containerProperties,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.Container.ReplaceContainerStreamAsync(
                containerProperties,
                requestOptions,
                cancellationToken);
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.Container.ReplaceThroughputAsync(
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
                this.Container.GetItemQueryStreamIterator(
                    queryDefinition,
                    continuationToken,
                    requestOptions),
                this.EncryptionProcessor);
        }

        public override FeedIterator GetItemQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new EncryptionFeedIterator(
                this.Container.GetItemQueryStreamIterator(
                    queryText,
                    continuationToken,
                    requestOptions),
                this.EncryptionProcessor);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(
            string processorName,
            ChangesHandler<T> onChangesDelegate)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(null);
            using (diagnosticsContext.CreateScope("GetChangeFeedProcessorBuilder"))
            {
                return this.Container.GetChangeFeedProcessorBuilder(
                    processorName,
                    async (IReadOnlyCollection<JObject> documents, CancellationToken cancellationToken) =>
                    {
                        List<T> decryptedItems = new List<T>(documents.Count);

                        foreach (JObject document in documents)
                        {
                            JObject decryptedDocument = await this.EncryptionProcessor.DecryptAsync(
                                document,
                                diagnosticsContext,
                                cancellationToken);

                            decryptedItems.Add(decryptedDocument.ToObject<T>());
                        }

                        // Call the original passed in delegate
                        await onChangesDelegate(decryptedItems, cancellationToken);
                    });
            }
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.Container.ReplaceThroughputAsync(
                throughputProperties,
                requestOptions,
                cancellationToken);
        }

        public override Task<IReadOnlyList<FeedRange>> GetFeedRangesAsync(
            CancellationToken cancellationToken = default)
        {
            return this.Container.GetFeedRangesAsync(cancellationToken);
        }

        public override Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            FeedRange feedRange,
            CancellationToken cancellationToken = default)
        {
            return this.Container.GetPartitionKeyRangesAsync(feedRange, cancellationToken);
        }

        public override FeedIterator GetItemQueryStreamIterator(
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions = null)
        {
            return new EncryptionFeedIterator(
                this.Container.GetItemQueryStreamIterator(
                    feedRange,
                    queryDefinition,
                    continuationToken,
                    requestOptions),
                this.EncryptionProcessor);
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
            return this.Container.GetChangeFeedEstimator(processorName, leaseContainer);
        }

        public override FeedIterator GetChangeFeedStreamIterator(
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedMode changeFeedMode,
            ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return new EncryptionFeedIterator(
                this.Container.GetChangeFeedStreamIterator(
                    changeFeedStartFrom,
                    changeFeedMode,
                    changeFeedRequestOptions),
                this.EncryptionProcessor);
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

        public override Task<ResponseMessage> ReadManyItemsStreamAsync(
            IReadOnlyList<(string id, PartitionKey partitionKey)> items,
            ReadManyRequestOptions readManyRequestOptions = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override Task<FeedResponse<T>> ReadManyItemsAsync<T>(
            IReadOnlyList<(string id, PartitionKey partitionKey)> items,
            ReadManyRequestOptions readManyRequestOptions = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(
            string processorName,
            ChangeFeedHandler<T> onChangesDelegate)
        {
            throw new NotImplementedException();
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithManualCheckpoint<T>(
            string processorName,
            ChangeFeedHandlerWithManualCheckpoint<T> onChangesDelegate)
        {
            throw new NotImplementedException();
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder(
            string processorName,
            ChangeFeedStreamHandler onChangesDelegate)
        {
            throw new NotImplementedException();
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithManualCheckpoint(
            string processorName,
            ChangeFeedStreamHandlerWithManualCheckpoint onChangesDelegate)
        {
            throw new NotImplementedException();
        }
    }
}