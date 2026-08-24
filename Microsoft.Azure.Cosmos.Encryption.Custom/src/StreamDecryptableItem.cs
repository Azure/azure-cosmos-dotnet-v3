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
        private PooledMemoryStream cachedDecryptedContent;
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

                if (!this.isDecrypted)
                {
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

                            this.cachedDecryptedContent = new PooledMemoryStream();
                            await streamToRead.CopyToAsync(this.cachedDecryptedContent).ConfigureAwait(false);
                            streamToRead.Position = 0;

                            T initialItem = this.cosmosSerializer.FromStream<T>(streamToRead);

                            this.cachedDecryptionContext = decryptionContext;
                            this.isDecrypted = true;

                            await this.DisposeContentStreamAsync().ConfigureAwait(false);

                            return (initialItem, decryptionContext);
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
                        EncryptionException encryptionException = await this.CreateEncryptionExceptionAsync(
                            exception,
                            decryptionContextForDiagnostics).ConfigureAwait(false);

                        await this.DisposeContentStreamAsync().ConfigureAwait(false);
                        await this.DisposeCachedDecryptedContentAsync().ConfigureAwait(false);

                        // Mark disposed before throwing: the content stream is gone and decryption cannot be
                        // retried, so a second call must surface ObjectDisposedException rather than NRE on the
                        // nulled stream and mask the original failure.
                        this.isDisposed = true;

                        throw encryptionException;
                    }
                }

                try
                {
                    using Stream cachedContentStream = this.CreateCachedContentStream();
                    T item = this.cosmosSerializer.FromStream<T>(cachedContentStream);
                    return (item, this.cachedDecryptionContext);
                }
                catch (Exception exception)
                {
                    throw await this.CreateEncryptionExceptionAsync(
                        exception,
                        this.cachedDecryptionContext).ConfigureAwait(false);
                }
            }
            finally
            {
                this.asyncLock.Release();
            }
        }

        private Stream CreateCachedContentStream()
        {
            if (this.cachedDecryptedContent == null ||
                !this.cachedDecryptedContent.TryGetBuffer(out ArraySegment<byte> buffer))
            {
                throw new InvalidOperationException("Decrypted content is unavailable.");
            }

            return new MemoryStream(buffer.Array, buffer.Offset, buffer.Count, writable: false, publiclyVisible: false);
        }

        private async Task<(Stream Stream, DecryptionContext Context)> DecryptContentStreamAsync()
        {
            CosmosDiagnosticsContext diagnosticsContext = new ();

            // The stream adapter owns its input; shield pooled response content until serialization completes.
            Stream decryptionInput = this.contentStream is PooledMemoryStream
                ? new NonDisposingStream(this.contentStream)
                : this.contentStream;

            (Stream decryptedStream, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                decryptionInput,
                this.encryptor,
                JsonProcessor.Stream,
                legacyFallback: true,
                diagnosticsContext,
                cancellationToken: default).ConfigureAwait(false);

            return (
                ReferenceEquals(decryptedStream, decryptionInput) ? this.contentStream : decryptedStream,
                decryptionContext);
        }

        private async Task<EncryptionProperties> TryReadEncryptionPropertiesAsync()
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

                return properties;
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

            Stream stream = this.contentStream;
            this.contentStream = null;
            await stream.DisposeAsync().ConfigureAwait(false);
        }

        private async ValueTask DisposeCachedDecryptedContentAsync()
        {
            if (this.cachedDecryptedContent == null)
            {
                return;
            }

            PooledMemoryStream stream = this.cachedDecryptedContent;
            this.cachedDecryptedContent = null;
            await stream.DisposeAsync().ConfigureAwait(false);
        }

        private async Task<EncryptionException> CreateEncryptionExceptionAsync(
            Exception exception,
            DecryptionContext decryptionContext)
        {
            EncryptionProperties encryptionProperties = await this.TryReadEncryptionPropertiesAsync().ConfigureAwait(false);

            // Best-effort DEK id: prefer the supplied context, then parse the original encrypted content.
            string dataEncryptionKeyId = decryptionContext?.DecryptionInfoList?.FirstOrDefault()?.DataEncryptionKeyId
                ?? encryptionProperties?.DataEncryptionKeyId
                ?? string.Empty;

            string encryptedContent = decryptionContext != null || encryptionProperties != null
                ? await TryReadStreamAsStringAsync(this.contentStream).ConfigureAwait(false) ?? string.Empty
                : string.Empty;

            return new EncryptionException(dataEncryptionKeyId, encryptedContent, exception);
        }

        private static async Task<string> TryReadStreamAsStringAsync(Stream stream)
        {
            try
            {
                if (stream is MemoryStream memoryStream && !memoryStream.CanRead)
                {
                    using MemoryStream readableCopy = new (memoryStream.ToArray(), writable: false);
                    return await TryReadStreamAsStringAsync(readableCopy).ConfigureAwait(false);
                }

                if (stream == null || !stream.CanRead || !stream.CanSeek)
                {
                    return null;
                }

                stream.Position = 0;

                using StreamReader reader = new (stream, leaveOpen: true);
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }

        private sealed class NonDisposingStream : Stream
        {
            private readonly Stream innerStream;

            public NonDisposingStream(Stream innerStream)
            {
                this.innerStream = innerStream;
            }

            public override bool CanRead => this.innerStream.CanRead;

            public override bool CanSeek => this.innerStream.CanSeek;

            public override bool CanWrite => this.innerStream.CanWrite;

            public override long Length => this.innerStream.Length;

            public override long Position
            {
                get => this.innerStream.Position;
                set => this.innerStream.Position = value;
            }

            public override void Flush()
            {
                this.innerStream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return this.innerStream.Read(buffer, offset, count);
            }

            public override ValueTask<int> ReadAsync(
                Memory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                return this.innerStream.ReadAsync(buffer, cancellationToken);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return this.innerStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                this.innerStream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                this.innerStream.Write(buffer, offset, count);
            }

            public override ValueTask DisposeAsync()
            {
                return default;
            }

            protected override void Dispose(bool disposing)
            {
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
                await this.DisposeCachedDecryptedContentAsync().ConfigureAwait(false);

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
