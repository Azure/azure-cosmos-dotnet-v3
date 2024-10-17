// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.StreamProcessing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json.Linq;

    public sealed class EncryptableItemStream<T> : EncryptableItem
    {
        private DecryptableItemStream decryptableItem = null;

        /// <summary>
        /// Gets the input item
        /// </summary>
        public T Item { get; }

        private readonly StreamManager streamManager;

        /// <inheritdoc/>
        public override DecryptableItem DecryptableItem => this.decryptableItem ?? throw new InvalidOperationException("Decryptable content is not initialized.");

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptableItemStream{T}"/> class.
        /// </summary>
        /// <param name="input">Item to be written.</param>
        /// <param name="streamManager">Stream manager to provide output streams.</param>
        /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
        public EncryptableItemStream(T input, StreamManager streamManager = null)
        {
            this.Item = input ?? throw new ArgumentNullException(nameof(input));
            this.streamManager = streamManager ?? new MemoryStreamManager();
        }

#pragma warning disable CS0672 // Member overrides obsolete member
        /// <inheritdoc/>
        protected internal override void SetDecryptableItem(JToken decryptableContent, Encryptor encryptor, CosmosSerializer cosmosSerializer)
#pragma warning restore CS0672 // Member overrides obsolete member
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        protected internal override void SetDecryptableStream(Stream decryptableStream, Encryptor encryptor, JsonProcessor jsonProcessor, CosmosSerializer cosmosSerializer)
        {
            ArgumentNullException.ThrowIfNull(decryptableStream);

            this.decryptableItem = new DecryptableItemStream(decryptableStream, encryptor, jsonProcessor, cosmosSerializer, this.streamManager);
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
            // TODO: CosmosSerializer is lacking suitable methods
            Stream cosmosSerializerOutput = serializer.ToStream(this.Item);
            await cosmosSerializerOutput.CopyToAsync(outputStream, cancellationToken);
        }
    }
}
#endif