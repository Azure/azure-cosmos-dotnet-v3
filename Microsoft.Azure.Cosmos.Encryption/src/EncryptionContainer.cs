//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
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
        // TODO: Good to have constants available in the Cosmos SDK. Tracked via https://github.com/Azure/azure-cosmos-dotnet-v3/issues/2431
        private const string IntendedCollectionHeader = "x-ms-cosmos-intended-collection-rid";

        private const string IsClientEncryptedHeader = "x-ms-cosmos-is-client-encrypted";

        private readonly Container container;

        public CosmosSerializer CosmosSerializer { get; }

        public CosmosResponseFactory ResponseFactory { get; }

        public EncryptionCosmosClient EncryptionCosmosClient { get; }

        /// <summary>
        /// All the operations / requests for exercising client-side encryption functionality need to be made using this EncryptionContainer instance.
        /// </summary>
        /// <param name="container">Regular cosmos container.</param>
        /// <param name="encryptionCosmosClient"> Cosmos Client configured with Encryption.</param>
        public EncryptionContainer(
            Container container,
            EncryptionCosmosClient encryptionCosmosClient)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.EncryptionCosmosClient = encryptionCosmosClient ?? throw new ArgumentNullException(nameof(container));
            this.ResponseFactory = this.Database.Client.ResponseFactory;
            this.CosmosSerializer = this.Database.Client.ClientOptions.Serializer;
            this.encryptionSettingsByContainerName = new AsyncCache<string, EncryptionSettings>();
        }

        public override string Id => this.container.Id;

        public override Conflicts Conflicts => this.container.Conflicts;

        public override Scripts.Scripts Scripts => this.container.Scripts;

        public override Database Database => this.container.Database;

        private readonly AsyncCache<string, EncryptionSettings> encryptionSettingsByContainerName;

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
                ResponseMessage responseMessage = null;

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

        public override TransactionalBatch CreateTransactionalBatch(
            PartitionKey partitionKey)
        {
            return new EncryptionTransactionalBatch(
                this.container.CreateTransactionalBatch(partitionKey),
                this,
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
            QueryRequestOptions clonedRequestOptions;
            if (requestOptions != null)
            {
                clonedRequestOptions = (QueryRequestOptions)requestOptions.ShallowCopy();
            }
            else
            {
                clonedRequestOptions = new QueryRequestOptions();
            }

            return new EncryptionFeedIterator(
                this.container.GetItemQueryStreamIterator(
                    queryDefinition,
                    continuationToken,
                    clonedRequestOptions),
                this,
                clonedRequestOptions);
        }

        public override FeedIterator GetItemQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            QueryRequestOptions clonedRequestOptions;
            if (requestOptions != null)
            {
                clonedRequestOptions = (QueryRequestOptions)requestOptions.ShallowCopy();
            }
            else
            {
                clonedRequestOptions = new QueryRequestOptions();
            }

            return new EncryptionFeedIterator(
                this.container.GetItemQueryStreamIterator(
                    queryText,
                    continuationToken,
                    clonedRequestOptions),
                this,
                clonedRequestOptions);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(
            string processorName,
            ChangesHandler<T> onChangesDelegate)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(null);
            using (diagnosticsContext.CreateScope("GetChangeFeedProcessorBuilder"))
            {
                ChangeFeedProcessorBuilder changeFeedProcessorBuilder = this.container.GetChangeFeedProcessorBuilder(
                    processorName,
                    async (IReadOnlyCollection<JObject> documents, CancellationToken cancellationToken) =>
                    {
                        List<T> decryptedItems = new List<T>(documents.Count);

                        foreach (JObject document in documents)
                        {
                            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(cancellationToken);
                            JObject decryptedDocument = await EncryptionProcessor.DecryptAsync(
                                document,
                                encryptionSettings,
                                diagnosticsContext,
                                cancellationToken);

                            decryptedItems.Add(decryptedDocument.ToObject<T>());
                        }

                        // Call the original passed in delegate
                        await onChangesDelegate(decryptedItems, cancellationToken);
                    });

                return changeFeedProcessorBuilder;
            }
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
            QueryRequestOptions clonedRequestOptions;
            if (requestOptions != null)
            {
                clonedRequestOptions = (QueryRequestOptions)requestOptions.ShallowCopy();
            }
            else
            {
                clonedRequestOptions = new QueryRequestOptions();
            }

            return new EncryptionFeedIterator(
                this.container.GetItemQueryStreamIterator(
                    feedRange,
                    queryDefinition,
                    continuationToken,
                    clonedRequestOptions),
                this,
                clonedRequestOptions);
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
            ChangeFeedRequestOptions clonedchangeFeedRequestOptions;
            if (changeFeedRequestOptions != null)
            {
                clonedchangeFeedRequestOptions = (ChangeFeedRequestOptions)changeFeedRequestOptions.ShallowCopy();
            }
            else
            {
                clonedchangeFeedRequestOptions = new ChangeFeedRequestOptions();
            }

            return new EncryptionFeedIterator(
                this.container.GetChangeFeedStreamIterator(
                    changeFeedStartFrom,
                    changeFeedMode,
                    clonedchangeFeedRequestOptions),
                this,
                clonedchangeFeedRequestOptions);
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

        public async Task<EncryptionSettings> GetOrUpdateEncryptionSettingsFromCacheAsync(
            CancellationToken cancellationToken,
            EncryptionSettings obsoleteEncryptionSettings = null)
        {
            return await this.encryptionSettingsByContainerName.GetAsync(
                this.Id,
                obsoleteValue: obsoleteEncryptionSettings,
                singleValueInitFunc: async () => await EncryptionSettings.CreateAsync(this),
                cancellationToken: cancellationToken);
        }

        public void SetRequestHeaders(RequestOptions requestOptions, EncryptionSettings encryptionSettings)
        {
            requestOptions.AddRequestHeaders = (headers) =>
            {
                headers.Add(IsClientEncryptedHeader, bool.TrueString);
                headers.Add(IntendedCollectionHeader, encryptionSettings.ContainerRidValue);
            };
        }

        /// <summary>
        /// Returns a cloned copy of the passed RequestOptions if passed else creates a new ItemRequestOptions.
        /// </summary>
        /// <param name="itemRequestOptions"> Original ItemRequestOptions</param>
        /// <returns> ItemRequestOptions.</returns>
        public ItemRequestOptions GetClonedItemRequestOptions(ItemRequestOptions itemRequestOptions)
        {
            ItemRequestOptions clonedRequestOptions;

            if (itemRequestOptions != null)
            {
                clonedRequestOptions = (ItemRequestOptions)itemRequestOptions.ShallowCopy();
            }
            else
            {
                clonedRequestOptions = new ItemRequestOptions();
            }

            return clonedRequestOptions;
        }

        private async Task<ResponseMessage> CreateItemHelperAsync(
            Stream streamPayload,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(cancellationToken);
            if (!encryptionSettings.PropertiesToEncrypt.Any())
            {
                return await this.container.CreateItemStreamAsync(
                    streamPayload,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
            }

            ItemRequestOptions clonedRequestOptions = this.GetClonedItemRequestOptions(requestOptions);
            this.SetRequestHeaders(clonedRequestOptions, encryptionSettings);

            streamPayload = await EncryptionProcessor.EncryptAsync(
                    streamPayload,
                    encryptionSettings,
                    diagnosticsContext,
                    cancellationToken);

            ResponseMessage responseMessage = await this.container.CreateItemStreamAsync(
                streamPayload,
                partitionKey,
                clonedRequestOptions,
                cancellationToken);

            if (responseMessage.StatusCode == System.Net.HttpStatusCode.BadRequest && string.Equals(responseMessage.Headers.Get("x-ms-substatus"), "1024"))
            {
                streamPayload = await EncryptionProcessor.DecryptAsync(
                    streamPayload,
                    encryptionSettings,
                    diagnosticsContext,
                    cancellationToken);

                // get the latest encryption settings and re-encrypt.
                encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(
                    cancellationToken: cancellationToken,
                    obsoleteEncryptionSettings: encryptionSettings);

                this.SetRequestHeaders(clonedRequestOptions, encryptionSettings);

                streamPayload = await EncryptionProcessor.EncryptAsync(
                    streamPayload,
                    encryptionSettings,
                    diagnosticsContext,
                    cancellationToken);

                responseMessage = await this.container.CreateItemStreamAsync(
                    streamPayload,
                    partitionKey,
                    clonedRequestOptions,
                    cancellationToken);
            }

            responseMessage.Content = await EncryptionProcessor.DecryptAsync(
                    responseMessage.Content,
                    encryptionSettings,
                    diagnosticsContext,
                    cancellationToken);

            return responseMessage;
        }

        private async Task<ResponseMessage> ReadItemHelperAsync(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(cancellationToken: cancellationToken);
            if (!encryptionSettings.PropertiesToEncrypt.Any())
            {
                return await this.container.ReadItemStreamAsync(
                    id,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
            }

            ItemRequestOptions clonedRequestOptions = this.GetClonedItemRequestOptions(requestOptions);
            this.SetRequestHeaders(clonedRequestOptions, encryptionSettings);

            ResponseMessage responseMessage = await this.container.ReadItemStreamAsync(
                id,
                partitionKey,
                clonedRequestOptions,
                cancellationToken);

            if (responseMessage.StatusCode == System.Net.HttpStatusCode.BadRequest && string.Equals(responseMessage.Headers.Get("x-ms-substatus"), "1024"))
            {
                // get the latest encryption settings.
                encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(
                    cancellationToken: cancellationToken,
                    obsoleteEncryptionSettings: encryptionSettings);

                this.SetRequestHeaders(clonedRequestOptions, encryptionSettings);

                responseMessage = await this.container.ReadItemStreamAsync(
                    id,
                    partitionKey,
                    clonedRequestOptions,
                    cancellationToken);
            }

            responseMessage.Content = await EncryptionProcessor.DecryptAsync(
                responseMessage.Content,
                encryptionSettings,
                diagnosticsContext,
                cancellationToken);

            return responseMessage;
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

            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(cancellationToken);
            if (!encryptionSettings.PropertiesToEncrypt.Any())
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
                encryptionSettings,
                diagnosticsContext,
                cancellationToken);

            ItemRequestOptions clonedRequestOptions = this.GetClonedItemRequestOptions(requestOptions);
            this.SetRequestHeaders(clonedRequestOptions, encryptionSettings);

            ResponseMessage responseMessage = await this.container.ReplaceItemStreamAsync(
                streamPayload,
                id,
                partitionKey,
                clonedRequestOptions,
                cancellationToken);

            if (responseMessage.StatusCode == System.Net.HttpStatusCode.BadRequest && string.Equals(responseMessage.Headers.Get("x-ms-substatus"), "1024"))
            {
                streamPayload = await EncryptionProcessor.DecryptAsync(
                    streamPayload,
                    encryptionSettings,
                    diagnosticsContext,
                    cancellationToken);

                // get the latest encryption settings.
                encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(
                     cancellationToken: cancellationToken,
                     obsoleteEncryptionSettings: encryptionSettings);

                this.SetRequestHeaders(clonedRequestOptions, encryptionSettings);

                streamPayload = await EncryptionProcessor.EncryptAsync(
                    streamPayload,
                    encryptionSettings,
                    diagnosticsContext,
                    cancellationToken);

                responseMessage = await this.container.ReplaceItemStreamAsync(
                    streamPayload,
                    id,
                    partitionKey,
                    clonedRequestOptions,
                    cancellationToken);
            }

            responseMessage.Content = await EncryptionProcessor.DecryptAsync(
                responseMessage.Content,
                encryptionSettings,
                diagnosticsContext,
                cancellationToken);

            return responseMessage;
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

            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(cancellationToken);
            if (!encryptionSettings.PropertiesToEncrypt.Any())
            {
                return await this.container.UpsertItemStreamAsync(
                    streamPayload,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
            }

            streamPayload = await EncryptionProcessor.EncryptAsync(
                streamPayload,
                encryptionSettings,
                diagnosticsContext,
                cancellationToken);

            ItemRequestOptions clonedRequestOptions = this.GetClonedItemRequestOptions(requestOptions);
            this.SetRequestHeaders(clonedRequestOptions, encryptionSettings);

            ResponseMessage responseMessage = await this.container.UpsertItemStreamAsync(
                streamPayload,
                partitionKey,
                clonedRequestOptions,
                cancellationToken);

            if (responseMessage.StatusCode == System.Net.HttpStatusCode.BadRequest && string.Equals(responseMessage.Headers.Get("x-ms-substatus"), "1024"))
            {
                streamPayload = await EncryptionProcessor.DecryptAsync(
                    streamPayload,
                    encryptionSettings,
                    diagnosticsContext,
                    cancellationToken);

                // get the latest encryption settings.
                encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(
                    cancellationToken: cancellationToken,
                    obsoleteEncryptionSettings: encryptionSettings);

                this.SetRequestHeaders(clonedRequestOptions, encryptionSettings);

                streamPayload = await EncryptionProcessor.EncryptAsync(
                    streamPayload,
                    encryptionSettings,
                    diagnosticsContext,
                    cancellationToken);

                responseMessage = await this.container.UpsertItemStreamAsync(
                    streamPayload,
                    partitionKey,
                    clonedRequestOptions,
                    cancellationToken);
            }

            responseMessage.Content = await EncryptionProcessor.DecryptAsync(
                    responseMessage.Content,
                    encryptionSettings,
                    diagnosticsContext,
                    cancellationToken);

            return responseMessage;
        }
    }
}