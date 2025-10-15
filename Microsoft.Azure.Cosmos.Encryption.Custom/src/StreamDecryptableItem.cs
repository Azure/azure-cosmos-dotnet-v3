// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;

    internal sealed class StreamDecryptableItem : DecryptableItem
    {
        private readonly Encryptor encryptor;
        private readonly CosmosSerializer cosmosSerializer;
        private readonly SemaphoreSlim asyncLock = new (1, 1);

        private Stream contentStream;
        private object cachedItem;
        private DecryptionContext cachedDecryptionContext;
        private bool isDecrypted;
        private bool isDisposed;

        public StreamDecryptableItem(
            Stream decryptableContentStream,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer)
        {
            ArgumentNullException.ThrowIfNull(decryptableContentStream);
            ArgumentNullException.ThrowIfNull(encryptor);
            ArgumentNullException.ThrowIfNull(cosmosSerializer);

            this.contentStream = decryptableContentStream;
            this.encryptor = encryptor;
            this.cosmosSerializer = cosmosSerializer;
        }

        public override async Task<(T, DecryptionContext)> GetItemAsync<T>()
        {
            ObjectDisposedException.ThrowIf(this.isDisposed, this);

            await this.asyncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Return cached value if already decrypted
                if (this.isDecrypted)
                {
                    return ((T)this.cachedItem, this.cachedDecryptionContext);
                }

                try
                {
                    (Stream decryptedStream, DecryptionContext decryptionContext) = await this.DecryptContentStreamAsync().ConfigureAwait(false);

                    Stream streamToRead = decryptedStream ?? this.contentStream ?? throw new InvalidOperationException("Decryption returned no content stream.");

                    streamToRead.Position = 0;

                    T item = this.cosmosSerializer.FromStream<T>(streamToRead);

                    await this.DisposeDecryptedStreamAsync(decryptedStream).ConfigureAwait(false);

                    this.cachedItem = item;
                    this.cachedDecryptionContext = decryptionContext;
                    this.isDecrypted = true;

                    await this.DisposeContentStreamAsync().ConfigureAwait(false);

                    return (item, decryptionContext);
                }
                catch (Exception exception)
                {
                    string encryptedContent = await this.TryReadStreamAsStringAsync().ConfigureAwait(false);

                    await this.DisposeContentStreamAsync().ConfigureAwait(false);

                    throw new EncryptionException(dataEncryptionKeyId: string.Empty, encryptedContent: encryptedContent ?? string.Empty, exception);
                }
            }
            finally
            {
                this.asyncLock.Release();
            }
        }

        private async Task<(Stream Stream, DecryptionContext Context)> DecryptContentStreamAsync()
        {
            CosmosDiagnosticsContext diagnosticsContext = new ();

            return await EncryptionProcessor.DecryptAsync(
                this.contentStream,
                this.encryptor,
                JsonProcessor.Stream,
                legacyFallback: true,
                diagnosticsContext,
                cancellationToken: default).ConfigureAwait(false);
        }

        private async ValueTask DisposeDecryptedStreamAsync(Stream decryptedStream)
        {
            if (decryptedStream != null && decryptedStream != this.contentStream)
            {
                await decryptedStream.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async ValueTask DisposeContentStreamAsync()
        {
            if (this.contentStream == null)
            {
                return;
            }

            await this.contentStream.DisposeAsync().ConfigureAwait(false);
            this.contentStream = null;
        }

        private async Task<string> TryReadStreamAsStringAsync()
        {
            try
            {
                if (this.contentStream == null)
                {
                    return null;
                }

                this.contentStream.Position = 0;

                using StreamReader reader = new (this.contentStream, leaveOpen: true);
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }

        public override async ValueTask DisposeAsync()
        {
            if (this.isDisposed)
            {
                return;
            }

            await this.DisposeContentStreamAsync().ConfigureAwait(false);

            this.asyncLock.Dispose();
            this.isDisposed = true;
        }
    }
}
#endif
