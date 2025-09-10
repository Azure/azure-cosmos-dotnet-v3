//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom.StreamProcessing;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;

    internal sealed class EncryptionFeedIteratorStream : FeedIterator
    {
        private readonly FeedIterator feedIterator;
        private readonly Encryptor encryptor;
        private readonly CosmosSerializer cosmosSerializer;
        private readonly StreamManager streamManager;

        private static readonly ArrayStreamSplitter StreamSplitter = new ();

        public EncryptionFeedIteratorStream(
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

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("FeedIterator.ReadNext"))
            {
                ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);

                if (responseMessage.IsSuccessStatusCode && responseMessage.Content != null)
                {
                    Stream decryptedContent = this.streamManager.CreateStream();
                    await EncryptionProcessor.DeserializeAndDecryptResponseAsync(
                        responseMessage.Content,
                        decryptedContent,
                        this.encryptor,
                        this.streamManager,
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
                    decryptableContent = await this.ConvertResponseToDecryptableItemsAsync<T>(
                        responseMessage.Content,
                        cancellationToken);

                    return (responseMessage, decryptableContent);
                }

                return (responseMessage, decryptableContent);
            }
        }

        private async Task<List<T>> ConvertResponseToDecryptableItemsAsync<T>(
            Stream content,
            CancellationToken token)
        {
            List<Stream> decryptableStreams = await StreamSplitter.SplitCollectionAsync(content, this.streamManager, token);
            List<T> decryptableItems = new ();

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
    }
}
#endif