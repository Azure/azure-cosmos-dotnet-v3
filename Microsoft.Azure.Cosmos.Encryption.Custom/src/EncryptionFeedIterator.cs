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
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Newtonsoft.Json.Linq;

    internal sealed class EncryptionFeedIterator : FeedIterator
    {
#if NET8_0_OR_GREATER && ENCRYPTION_CUSTOM_PREVIEW
        private static readonly ArrayStreamSplitter StreamSplitter = new ();
        private readonly StreamManager streamManager;
#endif

        private readonly FeedIterator feedIterator;
        private readonly Encryptor encryptor;
        private readonly CosmosSerializer cosmosSerializer;
        private readonly JsonProcessor jsonProcessor;

        public static EncryptionFeedIterator CreateLegacyIterator(
            FeedIterator feedIterator,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer)
        {
            if (feedIterator == null)
            {
                throw new ArgumentNullException(nameof(feedIterator));
            }

            if (encryptor == null)
            {
                throw new ArgumentNullException(nameof(encryptor));
            }

            if (cosmosSerializer == null)
            {
                throw new ArgumentNullException(nameof(cosmosSerializer));
            }

            return new EncryptionFeedIterator(feedIterator, encryptor, cosmosSerializer);
        }

#if NET8_0_OR_GREATER && ENCRYPTION_CUSTOM_PREVIEW
        public static EncryptionFeedIterator CreateStreamIterator(
            FeedIterator feedIterator,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer,
            StreamManager streamManager)
        {
            if (feedIterator == null)
            {
                throw new ArgumentNullException(nameof(feedIterator));
            }

            if (encryptor == null)
            {
                throw new ArgumentNullException(nameof(encryptor));
            }

            if (cosmosSerializer == null)
            {
                throw new ArgumentNullException(nameof(cosmosSerializer));
            }

            if (streamManager == null)
            {
                throw new ArgumentNullException(nameof(streamManager));
            }

            return new EncryptionFeedIterator(feedIterator, encryptor, cosmosSerializer, streamManager);
        }
#endif

        private EncryptionFeedIterator(
            FeedIterator feedIterator,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer)
        {
            this.feedIterator = feedIterator;
            this.encryptor = encryptor;
            this.cosmosSerializer = cosmosSerializer;
            this.jsonProcessor = JsonProcessor.Newtonsoft;
        }

#if NET8_0_OR_GREATER && ENCRYPTION_CUSTOM_PREVIEW
        public EncryptionFeedIterator(
            FeedIterator feedIterator,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer,
            StreamManager streamManager)
        {
            this.feedIterator = feedIterator;
            this.encryptor = encryptor;
            this.cosmosSerializer = cosmosSerializer;
            this.streamManager = streamManager;
            this.jsonProcessor = JsonProcessor.Stream;
        }
#endif

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("FeedIterator.ReadNext"))
            {
                ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);

                if (responseMessage.IsSuccessStatusCode && responseMessage.Content != null)
                {
                    Stream decryptedContent = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(
                        responseMessage.Content,
                        this.encryptor,
                        this.jsonProcessor,
                        cancellationToken);

                    return new DecryptedResponseMessage(responseMessage, decryptedContent);
                }

                return responseMessage;
            }
        }

        public async Task<(ResponseMessage, List<T>)> ReadNextWithoutDecryptionAsync<T>(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("FeedIterator.ReadNextWithoutDecryption"))
            {
                ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);
                List<T> decryptableContent = null;

                if (responseMessage.IsSuccessStatusCode && responseMessage.Content != null)
                {
#if NET8_0_OR_GREATER && ENCRYPTION_CUSTOM_PREVIEW
                    if (this.jsonProcessor == JsonProcessor.Stream)
                    {
                        decryptableContent = await this.ConvertResponseToStreamDecryptableItemsAsync<T>(
                            responseMessage.Content,
                            cancellationToken);

                        return (responseMessage, decryptableContent);
                    }
#endif

                    decryptableContent = this.ConvertResponseToDecryptableItems<T>(
                        responseMessage.Content);

                    return (responseMessage, decryptableContent);
                }

                return (responseMessage, decryptableContent);
            }
        }

        private List<T> ConvertResponseToDecryptableItems<T>(
            Stream content)
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

#if NET8_0_OR_GREATER && ENCRYPTION_CUSTOM_PREVIEW
        private async Task<List<T>> ConvertResponseToStreamDecryptableItemsAsync<T>(Stream content, CancellationToken token)
        {
            List<Stream> decryptableStreams = await StreamSplitter.SplitCollectionAsync(content, this.streamManager, token);
            List<T> decryptableItems = new ();

            foreach (Stream item in decryptableStreams)
            {
                decryptableItems.Add(
                    (T)(object)new StreamDecryptableItem(
                        item,
                        this.encryptor,
                        JsonProcessor.Stream,
                        this.cosmosSerializer,
                        this.streamManager));
            }

            return decryptableItems;
        }
#endif
    }
}
