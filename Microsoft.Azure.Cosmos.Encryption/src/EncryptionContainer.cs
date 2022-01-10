//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json.Linq;

    internal sealed class EncryptionContainer : Container
    {
        private readonly Container container;

        private readonly AsyncCache<string, EncryptionSettings> encryptionSettingsByContainerName;

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptionContainer"/> class.
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

        public CosmosSerializer CosmosSerializer { get; }

        public CosmosResponseFactory ResponseFactory { get; }

        public EncryptionCosmosClient EncryptionCosmosClient { get; }

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

            if (partitionKey == null)
            {
                throw new NotSupportedException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
            }

            ResponseMessage responseMessage;
            using (Stream itemStream = this.CosmosSerializer.ToStream<T>(item))
            {
                responseMessage = await this.CreateItemHelperAsync(
                    itemStream,
                    partitionKey.Value,
                    requestOptions,
                    cancellationToken);
            }

            return this.ResponseFactory.CreateItemResponse<T>(responseMessage);
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

            return await this.CreateItemHelperAsync(
                streamPayload,
                partitionKey,
                requestOptions,
                cancellationToken);
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
            ResponseMessage responseMessage = await this.ReadItemHelperAsync(
                id,
                partitionKey,
                requestOptions,
                cancellationToken);

            return this.ResponseFactory.CreateItemResponse<T>(responseMessage);
        }

        public override async Task<ResponseMessage> ReadItemStreamAsync(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return await this.ReadItemHelperAsync(
                id,
                partitionKey,
                requestOptions,
                cancellationToken);
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

            ResponseMessage responseMessage;

            using (Stream itemStream = this.CosmosSerializer.ToStream<T>(item))
            {
                responseMessage = await this.ReplaceItemHelperAsync(
                    itemStream,
                    id,
                    partitionKey.Value,
                    requestOptions,
                    cancellationToken);
            }

            return this.ResponseFactory.CreateItemResponse<T>(responseMessage);
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

            return await this.ReplaceItemHelperAsync(
                streamPayload,
                id,
                partitionKey,
                requestOptions,
                cancellationToken);
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

            ResponseMessage responseMessage;

            using (Stream itemStream = this.CosmosSerializer.ToStream<T>(item))
            {
                responseMessage = await this.UpsertItemHelperAsync(
                    itemStream,
                    partitionKey.Value,
                    requestOptions,
                    cancellationToken);
            }

            return this.ResponseFactory.CreateItemResponse<T>(responseMessage);
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

            return await this.UpsertItemHelperAsync(
                streamPayload,
                partitionKey,
                requestOptions,
                cancellationToken);
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
            QueryRequestOptions clonedRequestOptions = requestOptions != null ? (QueryRequestOptions)requestOptions.ShallowCopy() : new QueryRequestOptions();

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
            QueryRequestOptions clonedRequestOptions = requestOptions != null ? (QueryRequestOptions)requestOptions.ShallowCopy() : new QueryRequestOptions();

            return new EncryptionFeedIterator(
                this.container.GetItemQueryStreamIterator(
                    queryText,
                    continuationToken,
                    clonedRequestOptions),
                this,
                clonedRequestOptions);
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
            QueryRequestOptions clonedRequestOptions = requestOptions != null ? (QueryRequestOptions)requestOptions.ShallowCopy() : new QueryRequestOptions();

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
            ChangeFeedRequestOptions clonedchangeFeedRequestOptions = changeFeedRequestOptions != null
                ? (ChangeFeedRequestOptions)changeFeedRequestOptions.ShallowCopy()
                : new ChangeFeedRequestOptions();

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

        public async override Task<ItemResponse<T>> PatchItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            IReadOnlyList<PatchOperation> patchOperations,
            PatchItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            ResponseMessage responseMessage = await this.PatchItemStreamAsync(
                id,
                partitionKey,
                patchOperations,
                requestOptions,
                cancellationToken);

            return this.ResponseFactory.CreateItemResponse<T>(responseMessage);
        }

        public async override Task<ResponseMessage> PatchItemStreamAsync(
            string id,
            PartitionKey partitionKey,
            IReadOnlyList<PatchOperation> patchOperations,
            PatchItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (partitionKey == null)
            {
                throw new ArgumentNullException(nameof(partitionKey));
            }

            if (patchOperations == null ||
                !patchOperations.Any())
            {
                throw new ArgumentNullException(nameof(patchOperations));
            }

            ResponseMessage responseMessage = await this.PatchItemHelperAsync(
                    id,
                    partitionKey,
                    patchOperations,
                    requestOptions,
                    cancellationToken);

            return responseMessage;
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
                    List<T> decryptedItems = await this.DecryptChangeFeedDocumentsAsync<T>(
                        documents,
                        cancellationToken);

                    // Call the original passed in delegate
                    await onChangesDelegate(decryptedItems, cancellationToken);
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
                    List<T> decryptedItems = await this.DecryptChangeFeedDocumentsAsync<T>(
                        documents,
                        cancellationToken);

                    // Call the original passed in delegate
                    await onChangesDelegate(context, decryptedItems, cancellationToken);
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
                    List<T> decryptedItems = await this.DecryptChangeFeedDocumentsAsync<T>(
                        documents,
                        cancellationToken);

                    // Call the original passed in delegate
                    await onChangesDelegate(context, decryptedItems, tryCheckpointAsync, cancellationToken);
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
                    EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(
                        obsoleteEncryptionSettings: null,
                        cancellationToken: cancellationToken);

                    Stream decryptedChanges = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(
                        changes,
                        encryptionSettings,
                        operationDiagnostics: null,
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
                    EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(
                        obsoleteEncryptionSettings: null,
                        cancellationToken: cancellationToken);

                    Stream decryptedChanges = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(
                        changes,
                        encryptionSettings,
                        operationDiagnostics: null,
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

        public async Task<EncryptionSettings> GetOrUpdateEncryptionSettingsFromCacheAsync(
            EncryptionSettings obsoleteEncryptionSettings,
            CancellationToken cancellationToken)
        {
            return await this.encryptionSettingsByContainerName.GetAsync(
                this.Id,
                obsoleteValue: obsoleteEncryptionSettings,
                singleValueInitFunc: () => EncryptionSettings.CreateAsync(this, cancellationToken),
                cancellationToken: cancellationToken);
        }

        internal async Task<List<PatchOperation>> EncryptPatchOperationsAsync(
            IReadOnlyList<PatchOperation> patchOperations,
            EncryptionSettings encryptionSettings,
            EncryptionDiagnosticsContext operationDiagnostics,
            CancellationToken cancellationToken = default)
        {
            List<PatchOperation> encryptedPatchOperations = new List<PatchOperation>(patchOperations.Count);
            operationDiagnostics.Begin(Constants.DiagnosticsEncryptOperation);
            int propertiesEncryptedCount = 0;

            foreach (PatchOperation patchOperation in patchOperations)
            {
                if (patchOperation.OperationType == PatchOperationType.Remove)
                {
                    encryptedPatchOperations.Add(patchOperation);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(patchOperation.Path) || patchOperation.Path[0] != '/')
                {
                    throw new ArgumentException($"Invalid path '{patchOperation.Path}'.");
                }

                // get the top level path's encryption setting.
                EncryptionSettingForProperty settingforProperty = encryptionSettings.GetEncryptionSettingForProperty(
                    patchOperation.Path.Split('/')[1]);

                // non-encrypted path
                if (settingforProperty == null)
                {
                    encryptedPatchOperations.Add(patchOperation);
                    continue;
                }
                else if (patchOperation.OperationType == PatchOperationType.Increment)
                {
                    throw new InvalidOperationException($"Increment patch operation is not allowed for encrypted path '{patchOperation.Path}'.");
                }

                if (!patchOperation.TrySerializeValueParameter(this.CosmosSerializer, out Stream valueParam))
                {
                    throw new ArgumentException($"Cannot serialize value parameter for operation: {patchOperation.OperationType}, path: {patchOperation.Path}.");
                }

                Stream encryptedPropertyValue = await EncryptionProcessor.EncryptValueStreamAsync(
                    valueParam,
                    settingforProperty,
                    cancellationToken);

                propertiesEncryptedCount++;

                switch (patchOperation.OperationType)
                {
                    case PatchOperationType.Add:
                        encryptedPatchOperations.Add(PatchOperation.Add(patchOperation.Path, encryptedPropertyValue));
                        break;

                    case PatchOperationType.Replace:
                        encryptedPatchOperations.Add(PatchOperation.Replace(patchOperation.Path, encryptedPropertyValue));
                        break;

                    case PatchOperationType.Set:
                        encryptedPatchOperations.Add(PatchOperation.Set(patchOperation.Path, encryptedPropertyValue));
                        break;

                    default:
                        throw new NotSupportedException(nameof(patchOperation.OperationType));
                }
            }

            operationDiagnostics?.End(propertiesEncryptedCount);
            return encryptedPatchOperations;
        }

        /// <summary>
        /// Returns a cloned copy of the passed RequestOptions if passed else creates a new ItemRequestOptions.
        /// </summary>
        /// <param name="itemRequestOptions"> Original ItemRequestOptions.</param>
        /// <returns> ItemRequestOptions.</returns>
        private static ItemRequestOptions GetClonedItemRequestOptions(ItemRequestOptions itemRequestOptions)
        {
            ItemRequestOptions clonedRequestOptions = itemRequestOptions != null ? (ItemRequestOptions)itemRequestOptions.ShallowCopy() : new ItemRequestOptions();

            return clonedRequestOptions;
        }

        private async Task<ResponseMessage> CreateItemHelperAsync(
            Stream streamPayload,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions,
            CancellationToken cancellationToken,
            bool isRetry = false)
        {
            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: cancellationToken);
            if (!encryptionSettings.PropertiesToEncrypt.Any())
            {
                return await this.container.CreateItemStreamAsync(
                    streamPayload,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
            }

            EncryptionDiagnosticsContext encryptionDiagnosticsContext = new EncryptionDiagnosticsContext();
            streamPayload = await EncryptionProcessor.EncryptAsync(
                streamPayload,
                encryptionSettings,
                encryptionDiagnosticsContext,
                cancellationToken);

            ItemRequestOptions clonedRequestOptions = requestOptions;

            // Clone(once) the request options since we modify it to set AddRequestHeaders to add additional headers.
            if (!isRetry)
            {
                clonedRequestOptions = GetClonedItemRequestOptions(requestOptions);
            }

            encryptionSettings.SetRequestHeaders(clonedRequestOptions);

            ResponseMessage responseMessage = await this.container.CreateItemStreamAsync(
                streamPayload,
                partitionKey,
                clonedRequestOptions,
                cancellationToken);

            // This handles the scenario where a container is deleted(say from different Client) and recreated with same Id but with different client encryption policy.
            // The idea is to have the container Rid cached and sent out as part of RequestOptions with Container Rid set in "x-ms-cosmos-intended-collection-rid" header.
            // So when the container being referenced here gets recreated we would end up with a stale encryption settings and container Rid and this would result in BadRequest( and a substatus 1024).
            // This would allow us to refresh the encryption settings and Container Rid, on the premise that the container recreated could possibly be configured with a new encryption policy.
            if (!isRetry &&
                responseMessage.StatusCode == HttpStatusCode.BadRequest &&
                string.Equals(responseMessage.Headers.Get(Constants.SubStatusHeader), Constants.IncorrectContainerRidSubStatus))
            {
                // Even though the streamPayload position is expected to be 0,
                // because for MemoryStream we just use the underlying buffer to send over the wire rather than using the Stream APIs
                // resetting it 0 to be on a safer side.
                streamPayload.Position = 0;

                // Now the streamPayload itself is not disposed off(and hence safe to use it in the below call) since the stream that is passed to CreateItemStreamAsync is a MemoryStream and not the original Stream
                // that the user has passed. The call to EncryptAsync reads out the stream(and processes it) and returns a MemoryStream which is eventually cloned in the
                // Cosmos SDK and then used. This stream however is to be disposed off as part of ResponseMessage when this gets returned.
                streamPayload = await this.DecryptStreamPayloadAndUpdateEncryptionSettingsAsync(
                    streamPayload,
                    encryptionSettings,
                    cancellationToken);

                // we try to recreate the item with the StreamPayload(to be encrypted) now that the encryptionSettings would have been updated with latest values if any.
                return await this.CreateItemHelperAsync(
                       streamPayload,
                       partitionKey,
                       clonedRequestOptions,
                       cancellationToken,
                       isRetry: true);
            }

            responseMessage.Content = await EncryptionProcessor.DecryptAsync(
                responseMessage.Content,
                encryptionSettings,
                encryptionDiagnosticsContext,
                cancellationToken);

            encryptionDiagnosticsContext.AddEncryptionDiagnosticsToResponseMessage(responseMessage);
            return responseMessage;
        }

        private async Task<ResponseMessage> ReadItemHelperAsync(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions,
            CancellationToken cancellationToken,
            bool isRetry = false)
        {
            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: cancellationToken);
            if (!encryptionSettings.PropertiesToEncrypt.Any())
            {
                return await this.container.ReadItemStreamAsync(
                    id,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
            }

            ItemRequestOptions clonedRequestOptions = requestOptions;

            // Clone(once) the request options since we modify it to set AddRequestHeaders to add additional headers.
            if (!isRetry)
            {
                clonedRequestOptions = GetClonedItemRequestOptions(requestOptions);
            }

            encryptionSettings.SetRequestHeaders(clonedRequestOptions);

            ResponseMessage responseMessage = await this.container.ReadItemStreamAsync(
                id,
                partitionKey,
                clonedRequestOptions,
                cancellationToken);

            if (!isRetry &&
                responseMessage.StatusCode == HttpStatusCode.BadRequest &&
                string.Equals(responseMessage.Headers.Get(Constants.SubStatusHeader), Constants.IncorrectContainerRidSubStatus))
            {
                // get the latest encryption settings.
                await this.GetOrUpdateEncryptionSettingsFromCacheAsync(
                    obsoleteEncryptionSettings: encryptionSettings,
                    cancellationToken: cancellationToken);

                return await this.ReadItemHelperAsync(
                    id,
                    partitionKey,
                    clonedRequestOptions,
                    cancellationToken,
                    isRetry: true);
            }

            EncryptionDiagnosticsContext encryptionDiagnosticsContext = new EncryptionDiagnosticsContext();
            responseMessage.Content = await EncryptionProcessor.DecryptAsync(
                responseMessage.Content,
                encryptionSettings,
                encryptionDiagnosticsContext,
                cancellationToken);

            encryptionDiagnosticsContext.AddEncryptionDiagnosticsToResponseMessage(responseMessage);
            return responseMessage;
        }

        private async Task<ResponseMessage> ReplaceItemHelperAsync(
            Stream streamPayload,
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions,
            CancellationToken cancellationToken,
            bool isRetry = false)
        {
            if (partitionKey == null)
            {
                throw new NotSupportedException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
            }

            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: cancellationToken);
            if (!encryptionSettings.PropertiesToEncrypt.Any())
            {
                return await this.container.ReplaceItemStreamAsync(
                    streamPayload,
                    id,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
            }

            EncryptionDiagnosticsContext encryptionDiagnosticsContext = new EncryptionDiagnosticsContext();
            streamPayload = await EncryptionProcessor.EncryptAsync(
                streamPayload,
                encryptionSettings,
                encryptionDiagnosticsContext,
                cancellationToken);

            ItemRequestOptions clonedRequestOptions = requestOptions;

            // Clone(once) the request options since we modify it to set AddRequestHeaders to add additional headers.
            if (!isRetry)
            {
                clonedRequestOptions = GetClonedItemRequestOptions(requestOptions);
            }

            encryptionSettings.SetRequestHeaders(clonedRequestOptions);

            ResponseMessage responseMessage = await this.container.ReplaceItemStreamAsync(
                streamPayload,
                id,
                partitionKey,
                clonedRequestOptions,
                cancellationToken);

            if (!isRetry &&
                responseMessage.StatusCode == HttpStatusCode.BadRequest &&
                string.Equals(responseMessage.Headers.Get(Constants.SubStatusHeader), Constants.IncorrectContainerRidSubStatus))
            {
                streamPayload.Position = 0;
                streamPayload = await this.DecryptStreamPayloadAndUpdateEncryptionSettingsAsync(
                    streamPayload,
                    encryptionSettings,
                    cancellationToken);

                return await this.ReplaceItemHelperAsync(
                    streamPayload,
                    id,
                    partitionKey,
                    clonedRequestOptions,
                    cancellationToken,
                    isRetry: true);
            }

            responseMessage.Content = await EncryptionProcessor.DecryptAsync(
                responseMessage.Content,
                encryptionSettings,
                encryptionDiagnosticsContext,
                cancellationToken);

            encryptionDiagnosticsContext.AddEncryptionDiagnosticsToResponseMessage(responseMessage);
            return responseMessage;
        }

        private async Task<ResponseMessage> UpsertItemHelperAsync(
            Stream streamPayload,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions,
            CancellationToken cancellationToken,
            bool isRetry = false)
        {
            if (partitionKey == null)
            {
                throw new NotSupportedException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
            }

            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: cancellationToken);
            if (!encryptionSettings.PropertiesToEncrypt.Any())
            {
                return await this.container.UpsertItemStreamAsync(
                    streamPayload,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
            }

            EncryptionDiagnosticsContext encryptionDiagnosticsContext = new EncryptionDiagnosticsContext();
            streamPayload = await EncryptionProcessor.EncryptAsync(
                streamPayload,
                encryptionSettings,
                encryptionDiagnosticsContext,
                cancellationToken);

            ItemRequestOptions clonedRequestOptions = requestOptions;

            // Clone(once) the request options since we modify it to set AddRequestHeaders to add additional headers.
            if (!isRetry)
            {
                clonedRequestOptions = GetClonedItemRequestOptions(requestOptions);
            }

            encryptionSettings.SetRequestHeaders(clonedRequestOptions);

            ResponseMessage responseMessage = await this.container.UpsertItemStreamAsync(
                streamPayload,
                partitionKey,
                clonedRequestOptions,
                cancellationToken);

            if (!isRetry &&
                responseMessage.StatusCode == HttpStatusCode.BadRequest &&
                string.Equals(responseMessage.Headers.Get(Constants.SubStatusHeader), Constants.IncorrectContainerRidSubStatus))
            {
                streamPayload.Position = 0;
                streamPayload = await this.DecryptStreamPayloadAndUpdateEncryptionSettingsAsync(
                    streamPayload,
                    encryptionSettings,
                    cancellationToken);

                return await this.UpsertItemHelperAsync(
                    streamPayload,
                    partitionKey,
                    clonedRequestOptions,
                    cancellationToken,
                    isRetry: true);
            }

            responseMessage.Content = await EncryptionProcessor.DecryptAsync(
                responseMessage.Content,
                encryptionSettings,
                encryptionDiagnosticsContext,
                cancellationToken);

            encryptionDiagnosticsContext.AddEncryptionDiagnosticsToResponseMessage(responseMessage);
            return responseMessage;
        }

        private async Task<ResponseMessage> PatchItemHelperAsync(
            string id,
            PartitionKey partitionKey,
            IReadOnlyList<PatchOperation> patchOperations,
            PatchItemRequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(
                obsoleteEncryptionSettings: null,
                cancellationToken: cancellationToken);

            PatchItemRequestOptions clonedRequestOptions;
            if (requestOptions != null)
            {
                clonedRequestOptions = (PatchItemRequestOptions)requestOptions.ShallowCopy();
            }
            else
            {
                clonedRequestOptions = new PatchItemRequestOptions();
            }

            encryptionSettings.SetRequestHeaders(clonedRequestOptions);

            EncryptionDiagnosticsContext encryptionDiagnosticsContext = new EncryptionDiagnosticsContext();
            List<PatchOperation> encryptedPatchOperations = await this.EncryptPatchOperationsAsync(
                patchOperations,
                encryptionSettings,
                encryptionDiagnosticsContext,
                cancellationToken);

            ResponseMessage responseMessage = await this.container.PatchItemStreamAsync(
                id,
                partitionKey,
                encryptedPatchOperations,
                clonedRequestOptions,
                cancellationToken);

            responseMessage.Content = await EncryptionProcessor.DecryptAsync(
                responseMessage.Content,
                encryptionSettings,
                encryptionDiagnosticsContext,
                cancellationToken);

            encryptionDiagnosticsContext.AddEncryptionDiagnosticsToResponseMessage(responseMessage);
            return responseMessage;
        }

        /// <summary>
        /// This method takes in an encrypted stream payload.
        /// The streamPayload is decrypted with the same policy which was used to encrypt and then the original plain stream payload is
        /// returned which can be used to re-encrypt after the latest encryption settings is retrieved.
        /// The method also updates the cached Encryption Settings with the latest value if any.
        /// </summary>
        /// <param name="streamPayload"> Data encrypted with wrong encryption policy. </param>
        /// <param name="encryptionSettings"> EncryptionSettings which was used to encrypt the payload. </param>
        /// <param name="cancellationToken"> Cancellation token. </param>
        /// <returns> Returns the decrypted stream payload and diagnostics content. </returns>
        private async Task<Stream> DecryptStreamPayloadAndUpdateEncryptionSettingsAsync(
           Stream streamPayload,
           EncryptionSettings encryptionSettings,
           CancellationToken cancellationToken)
        {
            streamPayload = await EncryptionProcessor.DecryptAsync(
                streamPayload,
                encryptionSettings,
                operationDiagnostics: null,
                cancellationToken);

            // get the latest encryption settings.
            await this.GetOrUpdateEncryptionSettingsFromCacheAsync(
               obsoleteEncryptionSettings: encryptionSettings,
               cancellationToken: cancellationToken);

            return streamPayload;
        }

        private async Task<List<T>> DecryptChangeFeedDocumentsAsync<T>(
            IReadOnlyCollection<JObject> documents,
            CancellationToken cancellationToken)
        {
            List<T> decryptedItems = new List<T>(documents.Count);

            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(
                obsoleteEncryptionSettings: null,
                cancellationToken: cancellationToken);

            foreach (JObject document in documents)
            {
                (JObject decryptedDocument, _) = await EncryptionProcessor.DecryptAsync(
                    document,
                    encryptionSettings,
                    cancellationToken);

                decryptedItems.Add(decryptedDocument.ToObject<T>());
            }

            return decryptedItems;
        }

        private async Task<ResponseMessage> ReadManyItemsHelperAsync(
            IReadOnlyList<(string id, PartitionKey partitionKey)> items,
            ReadManyRequestOptions readManyRequestOptions = null,
            CancellationToken cancellationToken = default,
            bool isRetry = false)
        {
            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(
               obsoleteEncryptionSettings: null,
               cancellationToken: cancellationToken);

            if (!encryptionSettings.PropertiesToEncrypt.Any())
            {
                return await this.container.ReadManyItemsStreamAsync(
                    items,
                    readManyRequestOptions,
                    cancellationToken);
            }

            ReadManyRequestOptions clonedRequestOptions = readManyRequestOptions;

            // Clone(once) the request options since we modify it to set AddRequestHeaders to add additional headers.
            if (!isRetry)
            {
                clonedRequestOptions = readManyRequestOptions != null ? (ReadManyRequestOptions)readManyRequestOptions.ShallowCopy() : new ReadManyRequestOptions();
            }

            encryptionSettings.SetRequestHeaders(clonedRequestOptions);

            ResponseMessage responseMessage = await this.container.ReadManyItemsStreamAsync(
                items,
                clonedRequestOptions,
                cancellationToken);

            if (!isRetry &&
                responseMessage.StatusCode == HttpStatusCode.BadRequest &&
                string.Equals(responseMessage.Headers.Get(Constants.SubStatusHeader), Constants.IncorrectContainerRidSubStatus))
            {
                // get the latest encryption settings.
                await this.GetOrUpdateEncryptionSettingsFromCacheAsync(
                    obsoleteEncryptionSettings: encryptionSettings,
                    cancellationToken: cancellationToken);

                return await this.ReadManyItemsHelperAsync(
                    items,
                    clonedRequestOptions,
                    cancellationToken,
                    isRetry: true);
            }

            if (responseMessage.IsSuccessStatusCode && responseMessage.Content != null)
            {
                EncryptionDiagnosticsContext decryptDiagnostics = new EncryptionDiagnosticsContext();

                Stream decryptedContent = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(
                    responseMessage.Content,
                    encryptionSettings,
                    decryptDiagnostics,
                    cancellationToken);

                decryptDiagnostics.AddEncryptionDiagnosticsToResponseMessage(responseMessage);
                return new DecryptedResponseMessage(responseMessage, decryptedContent);
            }

            return responseMessage;
        }
    }
}