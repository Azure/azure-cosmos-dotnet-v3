// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.IO;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Stream APIs cannot be used to follow lazy decryption method and to retrieve the decryption information.
    /// Instead, typed APIs can be used with EncryptableItemStream as input type, which takes item input in the form of stream.
    /// </summary>
    /// <example>
    /// This example takes in a item in stream format, encrypts it and writes to Cosmos container.
    /// <code language="c#">
    /// <![CDATA[
    ///     ItemResponse<EncryptableItemStream> createResponse = await encryptionContainer.CreateItemAsync<EncryptableItemStream>(
    ///         new EncryptableItemStream(streamPayload),
    ///         new PartitionKey("streamPartitionKey"),
    ///         EncryptionItemRequestOptions);
    ///
    ///     if (!createResponse.IsSuccessStatusCode)
    ///     {
    ///         //Handle and log exception
    ///         return;
    ///     }
    ///
    ///     (T inputType, DecryptionContext _) = await item.DecryptableItem.GetItemAsync<T>();
    /// ]]>
    /// </code>
    /// </example>
    public sealed class EncryptableItemStream : EncryptableItem, IDisposable
    {
        private DecryptableItemCore decryptableItem = null;

        /// <summary>
        /// Gets input stream payload.
        /// </summary>
        public Stream StreamPayload { get; }

        /// <inheritdoc/>
        public override DecryptableItem DecryptableItem => this.decryptableItem ?? throw new InvalidOperationException("Decryptable content is not initialized.");

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptableItemStream"/> class.
        /// </summary>
        /// <param name="input">Input item stream.</param>
        public EncryptableItemStream(Stream input)
        {
            this.StreamPayload = input ?? throw new ArgumentNullException(nameof(input));
        }

        /// <inheritdoc/>
        protected internal override void SetDecryptableItem(
            JToken decryptableContent,
            Encryptor encryptor,
            Container container,
            CosmosSerializer cosmosSerializer)
        {
            if (this.decryptableItem != null)
            {
                throw new InvalidOperationException();
            }

            EncryptionContainer encryptionContainer = (EncryptionContainer)container;

            this.decryptableItem = new DecryptableItemCore(
                decryptableContent,
                encryptor,
                encryptionContainer.EncryptionProcessor,
                cosmosSerializer);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.StreamPayload.Dispose();
        }

        /// <inheritdoc/>
        protected internal override Stream ToStream(CosmosSerializer serializer)
        {
            return this.StreamPayload;
        }
    }
}
