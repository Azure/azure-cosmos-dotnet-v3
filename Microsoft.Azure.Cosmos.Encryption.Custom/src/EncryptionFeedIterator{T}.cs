//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

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
            : this(encryptionFeedIterator, responseFactory, encryptor, cosmosSerializer, requestOptions?.GetJsonProcessor() ?? throw new ArgumentNullException(nameof(requestOptions)))
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

                decryptableContent = this.jsonProcessor switch
                {
#if NET8_0_OR_GREATER
                    JsonProcessor.Stream => await this.ConvertResponseToDecryptableItemsStreamAsync(responseMessage.Content, cancellationToken).ConfigureAwait(false),
#endif
                    JsonProcessor.Newtonsoft => this.ConvertResponseToDecryptableItemsNewtonsoft(responseMessage.Content),
                    _ => throw new NotImplementedException()
                };

                return (responseMessage, decryptableContent);
            }
        }

        private List<T> ConvertResponseToDecryptableItemsNewtonsoft(Stream content)
        {
            JObject contentJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(content);

            if (contentJObj.SelectToken(Constants.DocumentsResourcePropertyName) is not JArray documents)
            {
                throw new InvalidOperationException("Feed Response body contract was violated. Feed Response did not have an array of Documents.");
            }

            List<T> decryptableItems = new (documents.Count);

            foreach (JToken value in documents)
            {
                DecryptableItemCore item = new (
                    value,
                    this.encryptor,
                    this.cosmosSerializer);

                decryptableItems.Add((T)(object)item);
            }

            return decryptableItems;
        }

#if NET8_0_OR_GREATER
        private async Task<List<T>> ConvertResponseToDecryptableItemsStreamAsync(Stream content, CancellationToken cancellationToken)
        {
            List<T> decryptableItems = new ();

            await foreach (Stream itemStream in JsonArrayStreamSplitter.SplitIntoSubstreamsAsync(content, cancellationToken).ConfigureAwait(false))
            {
                StreamDecryptableItem item = new (
                    itemStream,
                    this.encryptor,
                    this.cosmosSerializer);

                decryptableItems.Add((T)(object)item);
            }

            return decryptableItems;
        }
#endif
    }
}
