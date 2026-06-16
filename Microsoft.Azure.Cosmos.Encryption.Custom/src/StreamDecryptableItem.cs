// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;

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
            await this.asyncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(this.isDisposed, this);

                // Return cached value if already decrypted
                if (this.isDecrypted)
                {
                    return ((T)this.cachedItem, this.cachedDecryptionContext);
                }

                DecryptionContext decryptionContextForDiagnostics = null;
                try
                {
                    (Stream decryptedStream, DecryptionContext decryptionContext) = await this.DecryptContentStreamAsync().ConfigureAwait(false);
                    decryptionContextForDiagnostics = decryptionContext;

                    bool decryptedAliasesContent = decryptedStream != null && ReferenceEquals(decryptedStream, this.contentStream);

                    try
                    {
                        Stream streamToRead = decryptedStream ?? this.contentStream ?? throw new InvalidOperationException("Decryption returned no content stream.");

                        streamToRead.Position = 0;

                        T item = this.cosmosSerializer.FromStream<T>(streamToRead);

                        this.cachedItem = item;
                        this.cachedDecryptionContext = decryptionContext;
                        this.isDecrypted = true;

                        await this.DisposeContentStreamAsync().ConfigureAwait(false);

                        return (item, decryptionContext);
                    }
                    finally
                    {
                        if (decryptedStream != null && !decryptedAliasesContent)
                        {
                            await decryptedStream.DisposeAsync().ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception exception)
                {
                    // Best-effort DEK id for the diagnostic exception: prefer the in-flight context (decrypt
                    // succeeded, serializer threw), else parse it from the on-stream _ei, else string.Empty
                    // (EncryptionException's ctor rejects null).
                    string dataEncryptionKeyId = decryptionContextForDiagnostics?.DecryptionInfoList?.FirstOrDefault()?.DataEncryptionKeyId
                        ?? await this.TryReadDataEncryptionKeyIdAsync().ConfigureAwait(false)
                        ?? string.Empty;

                    string encryptedContent = await this.TryReadStreamAsStringAsync().ConfigureAwait(false);

                    await this.DisposeContentStreamAsync().ConfigureAwait(false);

                    // Mark disposed before throwing: the content stream is gone and decryption cannot be
                    // retried, so a second call must surface ObjectDisposedException rather than NRE on the
                    // nulled stream and mask the original failure.
                    this.isDisposed = true;

                    throw new EncryptionException(dataEncryptionKeyId: dataEncryptionKeyId, encryptedContent: encryptedContent ?? string.Empty, exception);
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

        private async Task<string> TryReadDataEncryptionKeyIdAsync()
        {
            try
            {
                if (this.contentStream == null || !this.contentStream.CanRead || !this.contentStream.CanSeek)
                {
                    return null;
                }

                EncryptionProperties properties = await EncryptionPropertiesStreamReader.ReadAsync(
                    this.contentStream,
                    PooledJsonSerializer.SerializerOptions,
                    cancellationToken: default).ConfigureAwait(false);

                return properties?.DataEncryptionKeyId;
            }
            catch
            {
                return null;
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
            await this.asyncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (this.isDisposed)
                {
                    return;
                }

                await this.DisposeContentStreamAsync().ConfigureAwait(false);

                this.isDisposed = true;
            }
            finally
            {
                this.asyncLock.Release();
            }

            GC.SuppressFinalize(this);
        }
    }
}
#endif
