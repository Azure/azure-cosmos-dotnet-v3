// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Allows for lazy decryption, which provides user a way to handle possible exceptions encountered as part of feed / query processing.
    /// Also provides decryption details.
    /// </summary>
    /// <remarks>
    /// It is recommended to follow the same pattern for point operations as well (for consistent error / exception handling).
    /// </remarks>
    /// <example>
    /// The following example is for query processing. The feed response is cast to
    /// <see cref="IAsyncDisposable"/> and disposed in a <c>finally</c> block so that any
    /// items the caller skipped or did not enumerate release their pooled buffers (relevant
    /// when <c>JsonProcessor.Stream</c> is selected on .NET 8+).
    /// <code language="c#">
    /// <![CDATA[
    /// public class ToDoActivity{
    ///     public string id {get; set;}
    ///     public string status {get; set;}
    ///     public int cost {get; set;}
    /// }
    ///
    /// QueryDefinition queryDefinition = new QueryDefinition("select * from ToDos");
    /// using (FeedIterator<DecryptableItem> feedIterator = this.Container.GetItemQueryIterator<DecryptableItem>(
    ///     queryDefinition,
    ///     requestOptions: new QueryRequestOptions() { PartitionKey = new PartitionKey("Error")}))
    /// {
    ///     while (feedIterator.HasMoreResults)
    ///     {
    ///         FeedResponse<DecryptableItem> decryptableItems = await feedIterator.ReadNextAsync();
    ///         try
    ///         {
    ///             foreach (DecryptableItem item in decryptableItems)
    ///             {
    ///                 try
    ///                 {
    ///                     (ToDoActivity toDo, DecryptionContext _) = await item.GetItemAsync<ToDoActivity>();
    ///                 }
    ///                 catch (EncryptionException encryptionException)
    ///                 {
    ///                     string dataEncryptionKeyId = encryptionException.DataEncryptionKeyId;
    ///                     string rawPayload = encryptionException.EncryptedContent;
    ///                 }
    ///             }
    ///         }
    ///         finally
    ///         {
    ///             // Ensures pooled buffers are returned even if the foreach exits early.
    ///             if (decryptableItems is IAsyncDisposable disposableResponse)
    ///             {
    ///                 await disposableResponse.DisposeAsync();
    ///             }
    ///         }
    ///     }
    /// }
    /// ]]>
    /// </code>
    /// </example>
    /// <example>
    /// The following example is for point read operation.
    /// <code language="c#">
    /// <![CDATA[
    /// public class ToDoActivity{
    ///     public string id {get; set;}
    ///     public string status {get; set;}
    ///     public int cost {get; set;}
    /// }
    ///
    /// ItemResponse<DecryptableItem> decryptableItemResponse = await this.Container.ReadItemAsync<DecryptableItem>("id", new PartitionKey("partitionKey"));
    /// try
    /// {
    ///     (ToDoActivity toDo, DecryptionContext _) = await decryptableItemResponse.Resource.GetItemAsync<ToDoActivity>();
    /// }
    /// catch (EncryptionException encryptionException)
    /// {
    ///     string dataEncryptionKeyId = encryptionException.DataEncryptionKeyId;
    ///     string rawPayload = encryptionException.EncryptedContent;
    /// }
    /// ]]>
    /// </code>
    /// </example>
    public abstract class DecryptableItem : IAsyncDisposable
    {
        /// <summary>
        /// Decrypts and deserializes the content.
        /// </summary>
        /// <typeparam name="T">The type of item to be returned.</typeparam>
        /// <returns>The requested item and the decryption related context.</returns>
        public abstract Task<(T, DecryptionContext)> GetItemAsync<T>();

        /// <summary>
        /// Disposes any resources held by the decryptable item.
        /// Default implementation does nothing. Override in derived classes that hold disposable resources.
        /// </summary>
        /// <remarks>
        /// Stream-mode <see cref="DecryptableItem"/> implementations wrap pooled <c>ArrayPool&lt;byte&gt;</c>
        /// buffers that must be returned to prevent buffer leaks and clear any plaintext residue. Callers
        /// that obtain a <c>FeedResponse&lt;DecryptableItem&gt;</c> page and abandon iteration (early-exit,
        /// exception, or never enumerate) should dispose the page through <see cref="IAsyncDisposable"/> so
        /// that disposal cascades to every item. See the type-level remarks for the recommended pattern.
        /// </remarks>
        /// <returns>A ValueTask representing the asynchronous dispose operation.</returns>
        public virtual ValueTask DisposeAsync()
        {
            return default;
        }
    }
}
