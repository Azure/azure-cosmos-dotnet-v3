// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a decryptable item that uses stream-based processing for decryption operations.
    /// This class provides lazy decryption capabilities and efficient memory management for large payloads.
    /// </summary>
    public sealed class StreamDecryptableItem : DecryptableItem, IAsyncDisposable
    {
        private readonly Encryptor encryptor;
        private readonly JsonProcessor jsonProcessor;
        private readonly CosmosSerializer cosmosSerializer;
        private readonly StreamManager streamManager;

        private Stream encryptedStream;
        private Stream decryptedStream;
        private DecryptionContext decryptionContext;
        private bool isDisposed;

        public StreamDecryptableItem(
            Stream encryptedStream,
            Encryptor encryptor,
            JsonProcessor processor,
            CosmosSerializer cosmosSerializer,
            StreamManager streamManager)
        {
            this.encryptedStream = encryptedStream ?? throw new ArgumentNullException(nameof(encryptedStream));
            this.encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
            this.jsonProcessor = processor;
            this.cosmosSerializer = cosmosSerializer ?? throw new ArgumentNullException(nameof(cosmosSerializer));
            this.streamManager = streamManager ?? throw new ArgumentNullException(nameof(streamManager));
        }

        public override Task<(T, DecryptionContext)> GetItemAsync<T>()
        {
            return this.GetItemAsync<T>(CancellationToken.None);
        }

        public override async Task<(T, DecryptionContext)> GetItemAsync<T>(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(this.isDisposed, this);

            await this.EnsureDecryptedAsync(cancellationToken);

            return await this.DeserializeItemAsync<T>(cancellationToken);
        }

        private async Task EnsureDecryptedAsync(CancellationToken cancellationToken)
        {
            if (this.decryptedStream != null)
            {
                return;
            }

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

        private async Task<(T, DecryptionContext)> DeserializeItemAsync<T>(CancellationToken cancellationToken)
        {
            if (typeof(T) == typeof(Stream))
            {
                MemoryStream copyStream = new MemoryStream((int)this.decryptedStream.Length);
                this.decryptedStream.Position = 0;
                await this.decryptedStream.CopyToAsync(copyStream, cancellationToken);
                copyStream.Position = 0;
                return ((T)(object)copyStream, this.decryptionContext);
            }

            this.decryptedStream.Position = 0;
#if SDKPROJECTREF
            var item = await this.cosmosSerializer.FromStreamAsync<T>(this.decryptedStream, cancellationToken);
#else
            T item = this.cosmosSerializer.FromStream<T>(this.decryptedStream);
#endif
            return (item, this.decryptionContext);
        }

        /// <summary>
        /// Performs asynchronous cleanup of resources.
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous disposal operation.</returns>
        public async ValueTask DisposeAsync()
        {
            if (this.isDisposed)
            {
                return;
            }

            if (this.encryptedStream != null)
            {
                await this.encryptedStream.DisposeAsync();
                this.encryptedStream = null;
            }

            if (this.decryptedStream != null)
            {
                await this.decryptedStream.DisposeAsync();
                this.decryptedStream = null;
            }

            this.isDisposed = true;
        }
    }
}
#endif