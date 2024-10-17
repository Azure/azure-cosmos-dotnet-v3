// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.StreamProcessing
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    internal sealed class DecryptableItemStream : DecryptableItem, IAsyncDisposable
    {
        private readonly Stream encryptedStream; // this stream should be recyclable
        private readonly Encryptor encryptor;
        private readonly JsonProcessor jsonProcessor;
        private readonly StreamManager streamManager;
        private readonly CosmosSerializer cosmosSerializer;

        private Stream decryptedStream; // this stream should be recyclable
        private DecryptionContext decryptionContext;

        public DecryptableItemStream(
            Stream encryptedStream,
            Encryptor encryptor,
            JsonProcessor processor,
            CosmosSerializer cosmosSerializer,
            StreamManager streamManager)
        {
            this.encryptedStream = encryptedStream;
            this.encryptor = encryptor;
            this.jsonProcessor = processor;
            this.cosmosSerializer = cosmosSerializer;
            this.streamManager = streamManager;
        }

        public override Task<(T, DecryptionContext)> GetItemAsync<T>()
        {
            return this.GetItemAsync<T>(CancellationToken.None);
        }

        public override async Task<(T, DecryptionContext)> GetItemAsync<T>(CancellationToken cancellationToken)
        {
            if (this.decryptedStream == null)
            {
                this.decryptedStream = this.streamManager.CreateStream();

                this.decryptionContext = await EncryptionProcessor.DecryptAsync(
                    this.encryptedStream,
                    this.decryptedStream,
                    this.encryptor,
                    new CosmosDiagnosticsContext(),
                    this.jsonProcessor,
                    cancellationToken);

                await this.encryptedStream.DisposeAsync();
            }

            // class is not generic, so we cannot reasonably cache deserialized content

            T selector = default;
            switch (selector)
            {
                case Stream: // consumer doesn't need payload deserialized
                    // should we make deep copy here? handing out 'Recyclable' memory stream
                    return ((T)(object)this.decryptedStream, this.decryptionContext);
                
                case JsonNode: // Read/Write System.Text.Json DOM
                    // we don't have anywhere to get settings from atm
                    JsonNode jsonNode = await JsonNode.ParseAsync(this.decryptedStream, cancellationToken: cancellationToken);
                    return ((T)(object)jsonNode, this.decryptionContext);
                
                case JsonDocument: // Read only System.Text.Json DOM
                    // we don't have anywhere to get settings from atm
                    JsonDocument jsonDocument = await JsonDocument.ParseAsync(this.decryptedStream, cancellationToken: cancellationToken);
                    return ((T)(object)jsonDocument, this.decryptionContext);

                case JObject: // We must call explicit Newtonsoft implementation otherwise result would be nonsense if cosmosSerializer is not Newtonsoft and we have no chance to tell
                    return ((T)(object)EncryptionProcessor.BaseSerializer.FromStream(decryptedStream))
                else: // Direct object mapping
                // this API is missing Async => should not be used
                return (this.cosmosSerializer.FromStream<T>(this.decryptedStream), this.decryptionContext);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (this.decryptedStream != null)
            {
                await this.streamManager.ReturnStreamAsync(this.decryptedStream);
                this.decryptedStream = null;
            }
        }
    }
}
#endif