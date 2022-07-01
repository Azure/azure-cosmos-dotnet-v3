//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class EncryptionFeedIterator : FeedIterator
    {
        private readonly object singleIteratorInitLock = new object();
        private readonly EncryptionContainer encryptionContainer;
        private readonly RequestOptions requestOptions;
        private readonly QueryDefinition queryDefinition;
        private readonly string queryText;
        private readonly string continuationToken;
        private readonly FeedRange feedRange;
        private readonly QueryType queryType;

        private bool isIteratorInitialized = false;

        public EncryptionFeedIterator(
            FeedIterator feedIterator,
            EncryptionContainer encryptionContainer,
            RequestOptions requestOptions)
        {
            this.FeedIterator = feedIterator ?? throw new ArgumentNullException(nameof(feedIterator));
            this.encryptionContainer = encryptionContainer ?? throw new ArgumentNullException(nameof(encryptionContainer));
            this.requestOptions = requestOptions ?? throw new ArgumentNullException(nameof(requestOptions));
        }

        public EncryptionFeedIterator(
            EncryptionContainer encryptionContainer,
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            this.encryptionContainer = encryptionContainer ?? throw new ArgumentNullException(nameof(encryptionContainer));
            this.queryDefinition = queryDefinition;
            this.continuationToken = continuationToken;
            this.requestOptions = requestOptions;

            this.FeedIterator = encryptionContainer.Container.GetItemQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions);

            this.queryType = QueryType.QueryDefinitionType;
        }

        public EncryptionFeedIterator(
            EncryptionContainer encryptionContainer,
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            this.encryptionContainer = encryptionContainer ?? throw new ArgumentNullException(nameof(encryptionContainer));
            this.queryText = queryText;
            this.continuationToken = continuationToken;
            this.requestOptions = requestOptions;

            this.FeedIterator = encryptionContainer.Container.GetItemQueryStreamIterator(
                queryText,
                continuationToken,
                requestOptions);

            this.queryType = QueryType.QueryTextType;
        }

        public EncryptionFeedIterator(
            EncryptionContainer encryptionContainer,
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedMode changeFeedMode,
            ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            this.encryptionContainer = encryptionContainer ?? throw new ArgumentNullException(nameof(encryptionContainer));
            this.requestOptions = changeFeedRequestOptions;
            this.FeedIterator = encryptionContainer.Container.GetChangeFeedStreamIterator(
                changeFeedStartFrom,
                changeFeedMode,
                changeFeedRequestOptions);
        }

        public EncryptionFeedIterator(
            EncryptionContainer encryptionContainer,
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions = null)
        {
            this.encryptionContainer = encryptionContainer ?? throw new ArgumentNullException(nameof(encryptionContainer));
            this.feedRange = feedRange;
            this.queryDefinition = queryDefinition;
            this.continuationToken = continuationToken;
            this.requestOptions = requestOptions;

            this.FeedIterator = encryptionContainer.Container.GetItemQueryStreamIterator(
                feedRange,
                queryDefinition,
                continuationToken,
                requestOptions);

            this.queryType = QueryType.QueryDefinitionWithFeedRangeType;
        }

        private enum QueryType
        {
            QueryWithOutEncryptedPk = 0,
            QueryTextType = 1,
            QueryDefinitionType = 2,
            QueryDefinitionWithFeedRangeType = 3,
        }

        public override bool HasMoreResults => this.FeedIterator.HasMoreResults;

        private FeedIterator FeedIterator { get; set; }

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            EncryptionSettings encryptionSettings = await this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: cancellationToken);
            await this.GetIteratorWithEncryptionHeaderAndEncryptPartitionKeyIfRequiredAsync(encryptionSettings);

            ResponseMessage responseMessage = await this.FeedIterator.ReadNextAsync(cancellationToken);

            EncryptionDiagnosticsContext encryptionDiagnosticsContext = new EncryptionDiagnosticsContext();

            // check for Bad Request and Wrong RID intended and update the cached RID and Client Encryption Policy.
            await this.encryptionContainer.ThrowIfRequestNeedsARetryPostPolicyRefreshAsync(responseMessage, encryptionSettings, encryptionDiagnosticsContext, cancellationToken);

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

        private async Task GetIteratorWithEncryptionHeaderAndEncryptPartitionKeyIfRequiredAsync(EncryptionSettings encryptionSettings)
        {
            if (!this.isIteratorInitialized)
            {
                lock (this.singleIteratorInitLock)
                {
                    if (this.isIteratorInitialized)
                    {
                        return;
                    }
                }
            }
            else
            {
                return;
            }

            encryptionSettings.SetRequestHeaders(this.requestOptions);

            if (this.queryType != QueryType.QueryWithOutEncryptedPk && this.requestOptions is QueryRequestOptions queryRequestOptions)
            {
                if (queryRequestOptions != null && queryRequestOptions.PartitionKey.HasValue)
                {
                    (queryRequestOptions.PartitionKey, bool isPkEncrypted) = await this.encryptionContainer.CheckIfPkIsEncryptedAndGetEncryptedPkAsync(
                        queryRequestOptions.PartitionKey.Value,
                        encryptionSettings,
                        cancellationToken: default);

                    if (!isPkEncrypted)
                    {
                        this.isIteratorInitialized = true;
                        return;
                    }
                }

                // we rebuild iterators which take in request options with partiton key and if partition key was encrypted.
                this.FeedIterator = this.queryType switch
                {
                    QueryType.QueryTextType => this.encryptionContainer.Container.GetItemQueryStreamIterator(
                        this.queryText,
                        this.continuationToken,
                        queryRequestOptions),
                    QueryType.QueryDefinitionType => this.encryptionContainer.Container.GetItemQueryStreamIterator(
                        this.queryDefinition,
                        this.continuationToken,
                        queryRequestOptions),
                    QueryType.QueryDefinitionWithFeedRangeType => this.encryptionContainer.Container.GetItemQueryStreamIterator(
                        this.feedRange,
                        this.queryDefinition,
                        this.continuationToken,
                        queryRequestOptions),
                    _ => this.FeedIterator,
                };
            }

            this.isIteratorInitialized = true;
        }
    }
}
