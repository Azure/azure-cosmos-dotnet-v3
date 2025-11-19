// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.IO;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Stream-only APIs cannot provide lazy decryption metadata.
    /// Instead, typed APIs can be used with StreamEncryptableItem as the input type, which takes item input in the form of a stream and keeps the lazy-decryption contract.
    /// The provided stream is disposed when the StreamEncryptableItem instance is disposed.
    /// </summary>
    /// <example>
    /// This example takes in an item in stream format, encrypts it and writes to a Cosmos container.
    /// <code language="c#">
    /// <![CDATA[
    ///     ItemResponse<StreamEncryptableItem> createResponse = await encryptionContainer.CreateItemAsync<StreamEncryptableItem>(
    ///         new StreamEncryptableItem(streamPayload),
    ///         new PartitionKey("streamPartitionKey"),
    ///         EncryptionItemRequestOptions);
    ///
    ///     if (!createResponse.IsSuccessStatusCode)
    ///     {
    ///         // Handle and log exception
    ///         return;
    ///     }
    ///
    ///     (T inputType, DecryptionContext _) = await item.DecryptableItem.GetItemAsync<T>();
    /// ]]>
    /// </code>
    /// </example>
    public sealed class StreamEncryptableItem : EncryptableItem, IDisposable
    {
        private const string DecryptableItemAlreadyInitializedMessage = "Decryptable content is already initialized.";
        private DecryptableItemCore decryptableItem = null;

        /// <summary>
        /// Gets input stream payload.
        /// </summary>
        public Stream StreamPayload { get; }

        /// <inheritdoc/>
        public override DecryptableItem DecryptableItem => this.decryptableItem ?? throw new InvalidOperationException("Decryptable content is not initialized.");

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamEncryptableItem"/> class.
        /// </summary>
        /// <param name="input">Input item stream.</param>
        public StreamEncryptableItem(Stream input)
        {
            this.StreamPayload = input ?? throw new ArgumentNullException(nameof(input));
        }

        /// <inheritdoc/>
        protected internal override void SetDecryptableItem(
            JToken decryptableContent,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer)
        {
            if (decryptableContent == null)
            {
                throw new ArgumentNullException(nameof(decryptableContent));
            }

            if (encryptor == null)
            {
                throw new ArgumentNullException(nameof(encryptor));
            }

            if (cosmosSerializer == null)
            {
                throw new ArgumentNullException(nameof(cosmosSerializer));
            }

            if (this.decryptableItem != null)
            {
                throw new InvalidOperationException(DecryptableItemAlreadyInitializedMessage);
            }

            this.decryptableItem = new DecryptableItemCore(
                decryptableContent,
                encryptor,
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
            _ = serializer; // serializer intentionally unused for stream-backed payloads
            return this.StreamPayload;
        }
    }
}
