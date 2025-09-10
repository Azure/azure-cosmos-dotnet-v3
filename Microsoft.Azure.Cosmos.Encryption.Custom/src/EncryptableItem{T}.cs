// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Input type that can be used to allow for lazy decryption in the write path.
    /// </summary>
    /// <typeparam name="T">Type of item.</typeparam>
    /// <example>
    /// This example takes in a item, encrypts it and writes to Cosmos container.
    /// <code language="c#">
    /// <![CDATA[
    /// public class ToDoActivity{
    ///     public string id {get; set;}
    ///     public string status {get; set;}
    /// }
    ///
    /// ToDoActivity test = new ToDoActivity()
    /// {
    ///    id = Guid.NewGuid().ToString(),
    ///    status = "InProgress"
    /// };
    ///
    /// ItemResponse<EncryptableItem<ToDoActivity>> createResponse = await encryptionContainer.CreateItemAsync<EncryptableItem<ToDoActivity>>(
    ///     new EncryptableItem<ToDoActivity>(test),
    ///     new PartitionKey(test.Status),
    ///     EncryptionItemRequestOptions);
    ///
    /// if (!createResponse.IsSuccessStatusCode)
    /// {
    ///     //Handle and log exception
    ///     return;
    /// }
    ///
    /// (ToDoActivity toDo, DecryptionContext _) = await item.DecryptableItem.GetItemAsync<ToDoActivity>();
    /// ]]>
    /// </code>
    /// </example>
    public sealed class EncryptableItem<T> : EncryptableItem
    {
        private DecryptableItemCore decryptableItem = null;

        /// <summary>
        /// Gets the input item.
        /// </summary>
        public T Item { get; }

        /// <inheritdoc/>
        public override DecryptableItem DecryptableItem => this.decryptableItem ?? throw new InvalidOperationException("Decryptable content is not initialized.");

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptableItem{T}"/> class.
        /// </summary>
        /// <param name="input">Item to be written.</param>
        public EncryptableItem(T input)
        {
            this.Item = input ?? throw new ArgumentNullException(nameof(input));
        }

        /// <inheritdoc/>
        protected internal override void SetDecryptableItem(
            JToken decryptableContent,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer)
        {
            if (this.decryptableItem != null)
            {
                throw new InvalidOperationException();
            }

            this.decryptableItem = new DecryptableItemCore(
                decryptableContent,
                encryptor,
                cosmosSerializer);
        }

        /// <inheritdoc/>
        protected internal override Stream ToStream(CosmosSerializer serializer)
        {
            return serializer.ToStream(this.Item);
        }

        /// <inheritdoc/>
        /// <remarks>This solution is not performant with Newtonsoft.Json.</remarks>
        protected internal override async Task ToStreamAsync(CosmosSerializer serializer, Stream outputStream, CancellationToken cancellationToken)
        {
            Stream temp = serializer.ToStream(this.Item);
#if NET8_0_OR_GREATER
            await temp.CopyToAsync(outputStream, cancellationToken);
#else
            await temp.CopyToAsync(outputStream, 81920, cancellationToken);
#endif
        }

#if NET8_0_OR_GREATER
        /// <inheritdoc/>
        /// <remarks>Direct stream based item is not supported with Newtonsoft.Json.</remarks>
        protected internal override void SetDecryptableStream(Stream decryptableStream, Encryptor encryptor, JsonProcessor jsonProcessor, CosmosSerializer cosmosSerializer, StreamManager streamManager)
        {
            throw new NotImplementedException("Stream based item is only allowed for EncryptionContainerStream");
        }
#endif

        /// <inheritdoc/>
        /// <remarks>Does nothing with Newtonsoft based EncryptableItem.</remarks>
        public override void Dispose()
        {
        }
    }
}
