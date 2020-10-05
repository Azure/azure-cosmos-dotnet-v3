// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Input type that can be used to allow for lazy decryption & to retrieve the operation details in the write path.
    /// </summary>
    /// <typeparam name="T">Type of item.</typeparam>
    /// <example>
    /// This example takes in a item in stream format, encrypts it and writes to Cosmos container.
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
    /// (ToDoActivity toDo, DecryptionContext _) = await item.GetItemAsync<ToDoActivity>();
    /// ]]>
    /// </code>
    /// </example>
    public sealed class EncryptableItem<T> : DecryptableItem
    {
        private DecryptableItemCore decryptableItem = null;

        /// <summary>
        /// Gets the input item.
        /// </summary>
        public T Item { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptableItem{T}"/> class.
        /// </summary>
        /// <param name="input">Item to be written.</param>
        public EncryptableItem(T input)
        {
            this.Item = input ?? throw new ArgumentNullException(nameof(input));
        }

        internal void SetDecryptableItem(
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
        public override Task<(T, DecryptionContext)> GetItemAsync<T>()
        {
            if (this.decryptableItem == null)
            {
                throw new InvalidOperationException("Decryptable content is not initialized.");
            }

            return this.decryptableItem.GetItemAsync<T>();
        }
    }
}
