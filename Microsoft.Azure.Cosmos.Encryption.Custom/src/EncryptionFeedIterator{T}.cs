//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class EncryptionFeedIterator<T> : FeedIterator<T>
    {
        private readonly EncryptionFeedIterator encryptionFeedIterator;
        private readonly CosmosResponseFactory responseFactory;
        private readonly Encryptor encryptor;
        private readonly CosmosSerializer cosmosSerializer;
        private readonly JsonProcessor jsonProcessor;

        public EncryptionFeedIterator(
            EncryptionFeedIterator encryptionFeedIterator,
            CosmosResponseFactory responseFactory,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer,
            JsonProcessor jsonProcessor)
        {
            this.encryptionFeedIterator = encryptionFeedIterator ?? throw new ArgumentNullException(nameof(encryptionFeedIterator));
            this.responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
            this.encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
            this.cosmosSerializer = cosmosSerializer ?? throw new ArgumentNullException(nameof(cosmosSerializer));
            this.jsonProcessor = jsonProcessor;
        }

        public EncryptionFeedIterator(
            EncryptionFeedIterator encryptionFeedIterator,
            CosmosResponseFactory responseFactory,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer,
            RequestOptions requestOptions)
            : this(encryptionFeedIterator, responseFactory, encryptor, cosmosSerializer, requestOptions?.GetJsonProcessor() ?? JsonProcessor.Newtonsoft)
        {
        }

        public override bool HasMoreResults => this.encryptionFeedIterator.HasMoreResults;

        public override async Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            ResponseMessage responseMessage;

            if (typeof(T) == typeof(DecryptableItem))
            {
                IReadOnlyCollection<T> resource;
                (responseMessage, resource) = await this.ReadNextWithoutDecryptionAsync(cancellationToken).ConfigureAwait(false);

                return DecryptableFeedResponse<T>.CreateResponse(
                    responseMessage,
                    resource);
            }
            else
            {
                responseMessage = await this.encryptionFeedIterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            }

            return this.responseFactory.CreateItemFeedResponse<T>(responseMessage);
        }

        private async Task<(ResponseMessage, List<T>)> ReadNextWithoutDecryptionAsync(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("FeedIterator.ReadNextWithoutDecryption"))
            {
                ResponseMessage responseMessage = await this.encryptionFeedIterator.ReadNextRawResponseAsync(cancellationToken).ConfigureAwait(false);
                List<T> decryptableContent = null;

                if (!responseMessage.IsSuccessStatusCode || responseMessage.Content == null)
                {
                    return (responseMessage, decryptableContent);
                }

                List<DecryptableItem> decryptableItems = await EncryptionProcessor.ConvertResponseToDecryptableItemsAsync(
                    responseMessage.Content,
                    this.encryptor,
                    this.cosmosSerializer,
                    this.jsonProcessor,
                    cancellationToken).ConfigureAwait(false);

                decryptableContent = new List<T>(decryptableItems.Count);

                foreach (DecryptableItem decryptableItem in decryptableItems)
                {
                    decryptableContent.Add((T)(object)decryptableItem);
                }

                return (responseMessage, decryptableContent);
            }
        }
    }
}
