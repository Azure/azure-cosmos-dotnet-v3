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
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Input type that can be used to allow for lazy decryption in the write path.
    /// </summary>
    /// <typeparam name="T">Type of item.</typeparam>
    public sealed class EncryptableItemStream<T> : EncryptableItem, IDisposable
    {
        private DecryptableItemStream decryptableItem = null;
        private bool isDisposed;

        /// <summary>
        /// Gets the input item
        /// </summary>
        public T Item { get; }

        /// <inheritdoc/>
        public override DecryptableItem DecryptableItem
        {
            get
            {
                ObjectDisposedException.ThrowIf(this.isDisposed, this);
                return this.decryptableItem ?? throw new InvalidOperationException("Decryptable content is not initialized.");
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptableItemStream{T}"/> class.
        /// </summary>
        /// <param name="input">Item to be written.</param>
        /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
        public EncryptableItemStream(T input)
        {
            this.Item = input ?? throw new ArgumentNullException(nameof(input));
        }

#pragma warning disable CS0672 // Member overrides obsolete member
        /// <inheritdoc/>
        protected internal override void SetDecryptableItem(JToken decryptableContent, Encryptor encryptor, CosmosSerializer cosmosSerializer)
#pragma warning restore CS0672 // Member overrides obsolete member
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        protected internal override void SetDecryptableStream(Stream decryptableStream, Encryptor encryptor, JsonProcessor jsonProcessor, CosmosSerializer cosmosSerializer, StreamManager streamManager)
        {
            ArgumentNullException.ThrowIfNull(decryptableStream);

            this.decryptableItem = new DecryptableItemStream(decryptableStream, encryptor, jsonProcessor, cosmosSerializer, streamManager);
        }

        /// <inheritdoc/>
#pragma warning disable CS0672 // Member overrides obsolete member
        protected internal override Stream ToStream(CosmosSerializer serializer)
#pragma warning restore CS0672 // Member overrides obsolete member
        {
            return serializer.ToStream(this.Item);
        }

        /// <inheritdoc/>
        protected internal override async Task ToStreamAsync(CosmosSerializer serializer, Stream outputStream, CancellationToken cancellationToken)
        {
#if SDKPROJECTREF
            await serializer.ToStreamAsync(this.Item, outputStream, cancellationToken);
#else
            // TODO: CosmosSerializer is lacking suitable methods
            Stream cosmosSerializerOutput = serializer.ToStream(this.Item);
            await cosmosSerializerOutput.CopyToAsync(outputStream, cancellationToken);
#endif
        }

        private void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    this.DecryptableItem?.Dispose();
                }

                this.isDisposed = true;
            }
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
#endif