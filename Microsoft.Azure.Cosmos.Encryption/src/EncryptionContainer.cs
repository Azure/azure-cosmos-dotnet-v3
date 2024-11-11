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
    using Newtonsoft.Json.Linq;

    internal sealed class EncryptionContainer : Container
    {
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
            this.Container = container ?? throw new ArgumentNullException(nameof(container));
            this.EncryptionCosmosClient = encryptionCosmosClient ?? throw new ArgumentNullException(nameof(container));
            this.ResponseFactory = this.Database.Client.ResponseFactory;
            this.CosmosSerializer = this.Database.Client.ClientOptions.Serializer;
            this.encryptionSettingsByContainerName = new AsyncCache<string, EncryptionSettings>();
        }

        public CosmosSerializer CosmosSerializer { get; }

        public CosmosResponseFactory ResponseFactory { get; }

        public EncryptionCosmosClient EncryptionCosmosClient { get; }

        public override string Id => this.Container.Id;

        public override Conflicts Conflicts => this.Container.Conflicts;

        public override Scripts.Scripts Scripts => this.Container.Scripts;

        public override Database Database => this.Container.Database;

        internal Container Container { get; }

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

        public override async Task<ItemResponse<T>> DeleteItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: cancellationToken);

            id = await this.CheckIfIdIsEncryptedAndGetEncryptedIdAsync(id, encryptionSettings, cancellationToken);
            (partitionKey, _) = await this.CheckIfPkIsEncryptedAndGetEncryptedPkAsync(partitionKey, encryptionSettings, cancellationToken);

            return await this.Container.DeleteItemAsync<T>(
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
            EncryptionSettings encryptionSettings = this.GetOrUpdateEncryptionSettingsFromCacheAsync(
                obsoleteEncryptionSettings: null,
                cancellationToken: default)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            (partitionKey, _) = this.CheckIfPkIsEncryptedAndGetEncryptedPkAsync(partitionKey: partitionKey, encryptionSettings: encryptionSettings, cancellationToken: default)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            return new EncryptionTransactionalBatch(
                this.Container.CreateTransactionalBatch(partitionKey),
                this,
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
            QueryRequestOptions clonedRequestOptions = requestOptions != null ? (QueryRequestOptions)requestOptions.ShallowCopy() : new QueryRequestOptions();

            return new EncryptionFeedIterator(
                this,
                queryDefinition,
                continuationToken,
                clonedRequestOptions);
        }

        public override FeedIterator GetItemQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            QueryRequestOptions clonedRequestOptions = requestOptions != null ? (QueryRequestOptions)requestOptions.ShallowCopy() : new QueryRequestOptions();

            return new EncryptionFeedIterator(
                this,
                queryText,
                continuationToken,
                clonedRequestOptions);
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
            ChangeFeedRequestOptions clonedchangeFeedRequestOptions = changeFeedRequestOptions != null
                ? (ChangeFeedRequestOptions)changeFeedRequestOptions.ShallowCopy()
                : new ChangeFeedRequestOptions();

            return new EncryptionFeedIterator(
                this,
                changeFeedStartFrom,
                changeFeedMode,
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
            return this.Container.GetChangeFeedProcessorBuilder(
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
            return this.Container.GetChangeFeedProcessorBuilder(
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
            return this.Container.GetChangeFeedProcessorBuilderWithManualCheckpoint(
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
            return this.Container.GetChangeFeedProcessorBuilder(
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
            return this.Container.GetChangeFeedProcessorBuilderWithManualCheckpoint(
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

        public override FeedIterator GetItemQueryStreamIterator(
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions = null)
        {
            QueryRequestOptions clonedRequestOptions = requestOptions != null ? (QueryRequestOptions)requestOptions.ShallowCopy() : new QueryRequestOptions();

            return new EncryptionFeedIterator(
                this,
                feedRange,
                queryDefinition,
                continuationToken,
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

#if ENCRYPTIONPREVIEW
        public override async Task<ResponseMessage> DeleteAllItemsByPartitionKeyStreamAsync(
            Cosmos.PartitionKey partitionKey,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: cancellationToken);

            (partitionKey, _) = await this.CheckIfPkIsEncryptedAndGetEncryptedPkAsync(partitionKey, encryptionSettings, cancellationToken);

            return await this.Container.DeleteAllItemsByPartitionKeyStreamAsync(
                partitionKey,
                requestOptions,
                cancellationToken);
        }

        public override Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            FeedRange feedRange,
            CancellationToken cancellationToken = default)
        {
            return this.Container.GetPartitionKeyRangesAsync(feedRange, cancellationToken);
        }
#endif

#if SDKPROJECTREF
        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes<T>(
            string processorName,
            ChangeFeedHandler<ChangeFeedItem<T>> onChangesDelegate)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> IsFeedRangePartOfAsync(
            Cosmos.FeedRange x,
            Cosmos.FeedRange y,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
#endif

        /// <summary>
        /// This function handles the scenario where a container is deleted(say from different Client) and recreated with same Id but with different client encryption policy.
        /// The idea is to have the container Rid cached and sent out as part of RequestOptions with Container Rid set in "x-ms-cosmos-intended-collection-rid" header.
        /// So when the container being referenced here gets recreated we would end up with a stale encryption settings and container Rid and this would result in BadRequest( and a substatus 1024).
        /// This would allow us to refresh the encryption settings and Container Rid, on the premise that the container recreated could possibly be configured with a new encryption policy.
        /// </summary>
        /// <param name="responseMessage"> Response message to validate. </param>
        /// <param name="encryptionSettings"> Current cached encryption settings to refresh if required. </param>
        /// <param name="encryptionDiagnosticsContext"> Encryption specific diagnostics. </param>
        /// <param name="cancellationToken"> Cancellation token. </param>
        internal async Task ThrowIfRequestNeedsARetryPostPolicyRefreshAsync(
            ResponseMessage responseMessage,
            EncryptionSettings encryptionSettings,
            EncryptionDiagnosticsContext encryptionDiagnosticsContext,
            CancellationToken cancellationToken)
        {
            string subStatusCode = responseMessage.Headers.Get(Constants.SubStatusHeader);
            bool isPartitionKeyMismatch = string.Equals(subStatusCode, Constants.PartitionKeyMismatch);
            bool isContainerRidIncorrect = string.Equals(subStatusCode, Constants.IncorrectContainerRidSubStatus);

            // if the partition key check is done before container rid check.
            if (responseMessage.StatusCode == HttpStatusCode.BadRequest && (isContainerRidIncorrect || isPartitionKeyMismatch))
            {
                // The below code avoids unneccessary force refresh of encryption settings if wrong partition key was passed and the PartitionKeyMismatch was not
                // due to us not encrypting the partition key because of incorrect cached policy.
                if (isPartitionKeyMismatch && encryptionSettings.PartitionKeyPaths.Any())
                {
                    EncryptionSettingForProperty encryptionSettingForProperty = null;
                    foreach (string path in encryptionSettings.PartitionKeyPaths)
                    {
                        string partitionKeyPath = path.Split('/')[1];
                        encryptionSettingForProperty = encryptionSettings.GetEncryptionSettingForProperty(partitionKeyPath);

                        // break on first path encountered.
                        if (encryptionSettingForProperty != null)
                        {
                            break;
                        }
                    }

                    // if none of the paths were part of encryption policy
                    if (encryptionSettingForProperty == null)
                    {
                        return;
                    }
                }

                string currentContainerRid = encryptionSettings.ContainerRidValue;

                // either way we cannot be sure if PartitionKeyMismatch was to due us using an invalid setting or we did not encrypt it.
                // get the latest encryption settings.
                EncryptionSettings updatedEncryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(
                    obsoleteEncryptionSettings: encryptionSettings,
                    cancellationToken: cancellationToken);

                string containerRidPostSettingsUpdate = updatedEncryptionSettings.ContainerRidValue;

                // gets returned back due to PartitionKeyMismatch.(in case of batch looks like the container rid check gets done first)
                // if the container was not recreated, so policy has not changed, just return the original response.
                if (currentContainerRid == containerRidPostSettingsUpdate)
                {
                    return;
                }

                if (encryptionDiagnosticsContext == null)
                {
                    throw new ArgumentNullException(nameof(encryptionDiagnosticsContext));
                }

                encryptionDiagnosticsContext.AddEncryptionDiagnosticsToResponseMessage(responseMessage);

                throw new EncryptionCosmosException(
                    "Operation has failed due to a possible mismatch in Client Encryption Policy configured on the container. Retrying may fix the issue. Please refer to https://aka.ms/CosmosClientEncryption for more details. " + responseMessage.ErrorMessage,
                    HttpStatusCode.BadRequest,
                    int.Parse(Constants.IncorrectContainerRidSubStatus),
                    responseMessage.Headers.ActivityId,
                    responseMessage.Headers.RequestCharge,
                    responseMessage.Diagnostics);
            }
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
                EncryptionSettingForProperty encryptionSettingForProperty = encryptionSettings.GetEncryptionSettingForProperty(
                    patchOperation.Path.Split('/')[1]);

                // non-encrypted path
                if (encryptionSettingForProperty == null)
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
                    valueStreamToEncrypt: valueParam,
                    encryptionSettingForProperty: encryptionSettingForProperty,
                    shouldEscape: patchOperation.Path.Split('/')[1] == "id",
                    cancellationToken: cancellationToken);

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

        internal async Task<string> CheckIfIdIsEncryptedAndGetEncryptedIdAsync(
            string id,
            EncryptionSettings encryptionSettings,
            CancellationToken cancellationToken)
        {
            if (!encryptionSettings.PropertiesToEncrypt.Any() || string.IsNullOrEmpty(id))
            {
                return id;
            }

            EncryptionSettingForProperty encryptionSettingForProperty = encryptionSettings.GetEncryptionSettingForProperty("id");

            if (encryptionSettingForProperty == null)
            {
                return id;
            }

            Stream valueStream = this.CosmosSerializer.ToStream(id);

            Stream encryptedIdStream = await EncryptionProcessor.EncryptValueStreamAsync(
                valueStreamToEncrypt: valueStream,
                encryptionSettingForProperty: encryptionSettingForProperty,
                shouldEscape: true,
                cancellationToken: cancellationToken);
            using (StreamReader reader = new StreamReader(encryptedIdStream))
            {
                string encryptedId = await reader.ReadToEndAsync();
                return JToken.Parse(encryptedId).ToString();
            }
        }

        internal async Task<(PartitionKey, bool)> CheckIfPkIsEncryptedAndGetEncryptedPkAsync(
            PartitionKey partitionKey,
            EncryptionSettings encryptionSettings,
            CancellationToken cancellationToken)
        {
            if (!encryptionSettings.PartitionKeyPaths.Any() || !encryptionSettings.PropertiesToEncrypt.Any() || partitionKey == null || (partitionKey != null && (partitionKey == PartitionKey.None || partitionKey == PartitionKey.Null)))
            {
                return (partitionKey, false);
            }

            EncryptionSettingForProperty encryptionSettingForProperty;

            JArray jArray = JArray.Parse(partitionKey.ToString());

#if ENCRYPTIONPREVIEW
            if (encryptionSettings.PartitionKeyPaths.Count > 1)
            {
                int counter = 0;
                PartitionKeyBuilder partitionKeyBuilder = new PartitionKeyBuilder();

                if (jArray.Count() > encryptionSettings.PartitionKeyPaths.Count())
                {
                    throw new NotSupportedException($"The number of partition keys passed in the query exceeds the number of keys initialized on the container. Container Id : {this.Id}");
                }

                bool isPkEncrypted = false;

                // partitionKeyBuilder expects the paths and values to be in same order.
                for (counter = 0; counter < jArray.Count(); counter++)
                {
                    string path = encryptionSettings.PartitionKeyPaths[counter];

                    // case: partition key path is /a/b/c and the client encryption policy has /a in path.
                    // hence encrypt the partition key value with using its top level path /a since /c would have been encrypted in the document using /a's policy.
                    string partitionKeyPath = path.Split('/')[1];

                    encryptionSettingForProperty = encryptionSettings.GetEncryptionSettingForProperty(
                        partitionKeyPath);

                    if (encryptionSettingForProperty == null)
                    {
                        partitionKeyBuilder.Add(jArray[counter].ToString());
                        continue;
                    }

                    isPkEncrypted = true;
                    Stream valueStream = EncryptionProcessor.BaseSerializer.ToStream(jArray[counter]);

                    Stream encryptedPartitionKey = await EncryptionProcessor.EncryptValueStreamAsync(
                        valueStreamToEncrypt: valueStream,
                        encryptionSettingForProperty: encryptionSettingForProperty,
                        shouldEscape: partitionKeyPath == "id",
                        cancellationToken: cancellationToken);

                    string encryptedPK = null;
                    using (StreamReader reader = new StreamReader(encryptedPartitionKey))
                    {
                        encryptedPK = await reader.ReadToEndAsync();
                    }

                    JToken encryptedKey = JToken.Parse(encryptedPK);

                    partitionKeyBuilder.Add(encryptedKey.ToString());
                }

                return (partitionKeyBuilder.Build(), isPkEncrypted);
            }
            else
#endif
            {
                string partitionKeyPath = encryptionSettings.PartitionKeyPaths.Single().Split('/')[1];
                encryptionSettingForProperty = encryptionSettings.GetEncryptionSettingForProperty(
                    partitionKeyPath);

                if (encryptionSettingForProperty == null)
                {
                    return (partitionKey, false);
                }

                Stream valueStream = EncryptionProcessor.BaseSerializer.ToStream(jArray[0]);

                Stream encryptedPartitionKey = await EncryptionProcessor.EncryptValueStreamAsync(
                    valueStreamToEncrypt: valueStream,
                    encryptionSettingForProperty: encryptionSettingForProperty,
                    shouldEscape: partitionKeyPath == "id",
                    cancellationToken: cancellationToken);

                string encryptedPK = null;
                using (StreamReader reader = new StreamReader(encryptedPartitionKey))
                {
                    encryptedPK = await reader.ReadToEndAsync();
                }

                JToken encryptedKey = JToken.Parse(encryptedPK);

                return (new PartitionKey(encryptedKey.ToString()), true);
            }
        }

        /// <summary>
        /// Returns a cloned copy of the passed RequestOptions if passed else creates a new ItemRequestOptions.
        /// </summary>
        /// <param name="itemRequestOptions"> Original ItemRequestOptions.</param>
        /// <returns> ItemRequestOptions.</returns>
        private static ItemRequestOptions EncryptionContainerGetClonedItemRequestOptions(ItemRequestOptions itemRequestOptions)
        {
            ItemRequestOptions clonedRequestOptions = itemRequestOptions != null ? (ItemRequestOptions)itemRequestOptions.ShallowCopy() : new ItemRequestOptions();

            return clonedRequestOptions;
        }

        private async Task<ResponseMessage> CreateItemHelperAsync(
            Stream streamPayload,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: cancellationToken);
            if (!encryptionSettings.PropertiesToEncrypt.Any())
            {
                return await this.Container.CreateItemStreamAsync(
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

            // Clone the request options since we modify it to set AddRequestHeaders to add additional headers.
            ItemRequestOptions clonedRequestOptions = EncryptionContainerGetClonedItemRequestOptions(requestOptions);

            encryptionSettings.SetRequestHeaders(clonedRequestOptions);

            (partitionKey, _) = await this.CheckIfPkIsEncryptedAndGetEncryptedPkAsync(partitionKey, encryptionSettings, cancellationToken);
            ResponseMessage responseMessage = await this.Container.CreateItemStreamAsync(
                streamPayload,
                partitionKey,
                clonedRequestOptions,
                cancellationToken);

            await this.ThrowIfRequestNeedsARetryPostPolicyRefreshAsync(responseMessage, encryptionSettings, encryptionDiagnosticsContext, cancellationToken);

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
            CancellationToken cancellationToken)
        {
            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: cancellationToken);
            if (!encryptionSettings.PropertiesToEncrypt.Any())
            {
                return await this.Container.ReadItemStreamAsync(
                    id,
                    partitionKey,
                    requestOptions,
                    cancellationToken);
            }

            // Clone the request options since we modify it to set AddRequestHeaders to add additional headers.
            ItemRequestOptions clonedRequestOptions = EncryptionContainerGetClonedItemRequestOptions(requestOptions);

            encryptionSettings.SetRequestHeaders(clonedRequestOptions);

            (partitionKey, _) = await this.CheckIfPkIsEncryptedAndGetEncryptedPkAsync(partitionKey, encryptionSettings, cancellationToken);
            id = await this.CheckIfIdIsEncryptedAndGetEncryptedIdAsync(id, encryptionSettings, cancellationToken);

            ResponseMessage responseMessage = await this.Container.ReadItemStreamAsync(
                id,
                partitionKey,
                clonedRequestOptions,
                cancellationToken);

            EncryptionDiagnosticsContext encryptionDiagnosticsContext = new EncryptionDiagnosticsContext();

            await this.ThrowIfRequestNeedsARetryPostPolicyRefreshAsync(responseMessage, encryptionSettings, encryptionDiagnosticsContext, cancellationToken);

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
            CancellationToken cancellationToken)
        {
            if (partitionKey == null)
            {
                throw new NotSupportedException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
            }

            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: cancellationToken);
            if (!encryptionSettings.PropertiesToEncrypt.Any())
            {
                return await this.Container.ReplaceItemStreamAsync(
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

            // Clone the request options since we modify it to set AddRequestHeaders to add additional headers.
            clonedRequestOptions = EncryptionContainerGetClonedItemRequestOptions(requestOptions);

            encryptionSettings.SetRequestHeaders(clonedRequestOptions);

            id = await this.CheckIfIdIsEncryptedAndGetEncryptedIdAsync(id, encryptionSettings, cancellationToken);
            (partitionKey, _) = await this.CheckIfPkIsEncryptedAndGetEncryptedPkAsync(partitionKey, encryptionSettings, cancellationToken);
            ResponseMessage responseMessage = await this.Container.ReplaceItemStreamAsync(
                streamPayload,
                id,
                partitionKey,
                clonedRequestOptions,
                cancellationToken);

            await this.ThrowIfRequestNeedsARetryPostPolicyRefreshAsync(responseMessage, encryptionSettings, encryptionDiagnosticsContext, cancellationToken);

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
            CancellationToken cancellationToken)
        {
            if (partitionKey == null)
            {
                throw new NotSupportedException($"{nameof(partitionKey)} cannot be null for operations using {nameof(EncryptionContainer)}.");
            }

            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: cancellationToken);
            if (!encryptionSettings.PropertiesToEncrypt.Any())
            {
                return await this.Container.UpsertItemStreamAsync(
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

            // Clone the request options since we modify it to set AddRequestHeaders to add additional headers.
            clonedRequestOptions = EncryptionContainerGetClonedItemRequestOptions(requestOptions);

            encryptionSettings.SetRequestHeaders(clonedRequestOptions);
            (partitionKey, _) = await this.CheckIfPkIsEncryptedAndGetEncryptedPkAsync(partitionKey, encryptionSettings, cancellationToken);
            ResponseMessage responseMessage = await this.Container.UpsertItemStreamAsync(
                streamPayload,
                partitionKey,
                clonedRequestOptions,
                cancellationToken);

            await this.ThrowIfRequestNeedsARetryPostPolicyRefreshAsync(responseMessage, encryptionSettings, encryptionDiagnosticsContext, cancellationToken);

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

            (partitionKey, _) = await this.CheckIfPkIsEncryptedAndGetEncryptedPkAsync(partitionKey, encryptionSettings, cancellationToken);
            id = await this.CheckIfIdIsEncryptedAndGetEncryptedIdAsync(id, encryptionSettings, cancellationToken);
            ResponseMessage responseMessage = await this.Container.PatchItemStreamAsync(
                id,
                partitionKey,
                encryptedPatchOperations,
                clonedRequestOptions,
                cancellationToken);

            await this.ThrowIfRequestNeedsARetryPostPolicyRefreshAsync(responseMessage, encryptionSettings, encryptionDiagnosticsContext, cancellationToken);

            responseMessage.Content = await EncryptionProcessor.DecryptAsync(
                responseMessage.Content,
                encryptionSettings,
                encryptionDiagnosticsContext,
                cancellationToken);

            encryptionDiagnosticsContext.AddEncryptionDiagnosticsToResponseMessage(responseMessage);
            return responseMessage;
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
            CancellationToken cancellationToken = default)
        {
            EncryptionSettings encryptionSettings = await this.GetOrUpdateEncryptionSettingsFromCacheAsync(
               obsoleteEncryptionSettings: null,
               cancellationToken: cancellationToken);

            if (!encryptionSettings.PropertiesToEncrypt.Any())
            {
                return await this.Container.ReadManyItemsStreamAsync(
                    items,
                    readManyRequestOptions,
                    cancellationToken);
            }

            // Clone the request options since we modify it to set AddRequestHeaders to add additional headers.
            ReadManyRequestOptions clonedRequestOptions = readManyRequestOptions != null ? (ReadManyRequestOptions)readManyRequestOptions.ShallowCopy() : new ReadManyRequestOptions();

            encryptionSettings.SetRequestHeaders(clonedRequestOptions);

            List<(string, PartitionKey)> encryptedItemList = new List<(string, PartitionKey)>();

            for (int i = 0; i < items.Count; i++)
            {
                string id = await this.CheckIfIdIsEncryptedAndGetEncryptedIdAsync(items[i].id, encryptionSettings, cancellationToken);
                (PartitionKey partitionKey, _) = await this.CheckIfPkIsEncryptedAndGetEncryptedPkAsync(items[i].partitionKey, encryptionSettings, cancellationToken);
                encryptedItemList.Add((id, partitionKey));
            }

            ResponseMessage responseMessage = await this.Container.ReadManyItemsStreamAsync(
                encryptedItemList,
                clonedRequestOptions,
                cancellationToken);

            EncryptionDiagnosticsContext encryptionDiagnosticsContext = new EncryptionDiagnosticsContext();

            await this.ThrowIfRequestNeedsARetryPostPolicyRefreshAsync(responseMessage, encryptionSettings, encryptionDiagnosticsContext, cancellationToken);

            if (responseMessage.IsSuccessStatusCode && responseMessage.Content != null)
            {
                Stream decryptedContent = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(
                    responseMessage.Content,
                    encryptionSettings,
                    encryptionDiagnosticsContext,
                    cancellationToken);

                encryptionDiagnosticsContext.AddEncryptionDiagnosticsToResponseMessage(responseMessage);
                return new DecryptedResponseMessage(responseMessage, decryptedContent);
            }

            return responseMessage;
        }
    }
}