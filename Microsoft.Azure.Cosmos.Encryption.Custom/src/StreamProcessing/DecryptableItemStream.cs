// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.StreamProcessing
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom.RecyclableMemoryStreamMirror;

    internal sealed class DecryptableItemStream : DecryptableItem
    {
        private readonly Encryptor encryptor;
        private readonly JsonProcessor jsonProcessor;
        private readonly CosmosSerializer cosmosSerializer;
        private readonly StreamManager streamManager;

        private Stream encryptedStream; // this stream should be recyclable
        private Stream decryptedStream; // this stream should be recyclable
        private DecryptionContext decryptionContext;

        private bool isDisposed;

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
            ObjectDisposedException.ThrowIf(this.isDisposed, this);

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
                this.encryptedStream = null;
            }

            T selector = default;
            switch (selector)
            {
                case Stream: // consumer doesn't need payload deserialized
                    MemoryStream ms = new ((int)this.decryptedStream.Length);
                    await this.decryptedStream.CopyToAsync(ms, cancellationToken);
                    return ((T)(object)ms, this.decryptionContext);
                default:
#if SDKPROJECTREF
                    return (await this.cosmosSerializer.FromStreamAsync<T>(this.decryptedStream, cancellationToken), this.decryptionContext);
#else
                    // this API is missing Async => should not be used
                    return (this.cosmosSerializer.FromStream<T>(this.decryptedStream), this.decryptionContext);
#endif

            }
        }

        private void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    this.encryptedStream?.Dispose();
                    this.decryptedStream?.Dispose();
                    this.encryptedStream = null;
                    this.decryptedStream = null;
                }

                this.isDisposed = true;
            }
        }

        public override void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
#endif