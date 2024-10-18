//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
    using Microsoft.Azure.Cosmos.Encryption.Custom.StreamProcessing;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
#else
    using System;
    using Newtonsoft.Json.Linq;
#endif

    internal sealed class EncryptionFeedIterator : FeedIterator
    {
        private readonly FeedIterator feedIterator;
        private readonly Encryptor encryptor;
        private readonly CosmosSerializer cosmosSerializer;

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        private readonly StreamManager streamManager;

        private static readonly ArrayStreamSplitter StreamSplitter = new ArrayStreamSplitter();
#endif

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
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
        }
#else
        public EncryptionFeedIterator(
            FeedIterator feedIterator,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer)
        {
            this.feedIterator = feedIterator;
            this.encryptor = encryptor;
            this.cosmosSerializer = cosmosSerializer;
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
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
                    Stream decryptedContent = this.streamManager.CreateStream();
                    await EncryptionProcessor.DeserializeAndDecryptResponseAsync(
                        responseMessage.Content,
                        decryptedContent,
                        this.encryptor,
                        this.streamManager,
                        cancellationToken);
#else
                    Stream decryptedContent = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(
                        responseMessage.Content,
                        this.encryptor,
                        cancellationToken);
#endif

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
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
                    decryptableContent = await this.ConvertResponseToDecryptableItemsAsync<T>(
                        responseMessage.Content,
                        cancellationToken);
#else
                    decryptableContent = this.ConvertResponseToDecryptableItems<T>(
                        responseMessage.Content);
#endif

                    return (responseMessage, decryptableContent);
                }

                return (responseMessage, decryptableContent);
            }
        }

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        private async Task<List<T>> ConvertResponseToDecryptableItemsAsync<T>(
            Stream content,
            CancellationToken token)
        {
            List<Stream> decryptableStreams = await StreamSplitter.SplitCollectionAsync(content, this.streamManager, token);
            List<T> decryptableItems = new List<T>();

            foreach (Stream item in decryptableStreams)
            {
                decryptableItems.Add(
                    (T)(object)new DecryptableItemStream(
                        item,
                        this.encryptor,
                        JsonProcessor.Stream,
                        this.cosmosSerializer,
                        this.streamManager));
            }

            return decryptableItems;
        }
#else
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
#endif
    }
}
