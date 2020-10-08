// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.IO;
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
    }
}
