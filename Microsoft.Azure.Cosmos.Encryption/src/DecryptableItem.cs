// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Allows for lazy decryption, which provides user a way to handle possible exceptions encountered as part of feed / query processing.
    /// Also provides decryption operation details.
    /// </summary>
    /// <example>
    /// <code language="c#">
    /// <![CDATA[
    /// public class ToDoActivity{
    ///     public string id {get; set;}
    ///     public string status {get; set;}
    ///     public int cost {get; set;}
    /// }
    ///
    /// QueryDefinition queryDefinition = new QueryDefinition("select * from ToDos");
    /// using (FeedIterator<DecrytableItem> feedIterator = this.Container.GetItemQueryIterator<DecrytableItem>(
    ///     queryDefinition,
    ///     requestOptions: new QueryRequestOptions() { PartitionKey = new PartitionKey("Error")}))
    /// {
    ///     while (feedIterator.HasMoreResults)
    ///     {
    ///         FeedResponse<DecryptableItem> decryptableItems = await feedIterator.ReadNextAsync();
    ///         foreach(DecryptableItem item in decryptableItems){
    ///         {
    ///             try
    ///             {
    ///                 (ToDoActivity toDo, DecryptionContext _) = await item.GetItemAsync<ToDoActivity>();
    ///             }
    ///             catch (EncryptionException encryptionException)
    ///             {
    ///                 string dataEncryptionKeyId = encryptionException.DataEncryptionKeyId;
    ///                 string rawPayload = encryptionException.EncryptedContent;
    ///             }
    ///         }
    ///     }
    /// }
    /// ]]>
    /// </code>
    /// </example>
    public abstract class DecryptableItem
    {
        /// <summary>
        /// Decrypts and deserializes the content.
        /// </summary>
        /// <typeparam name="T">The type of item to be returned.</typeparam>
        /// <returns>The requested item and the decryption operation related context.</returns>
        public abstract Task<(T, DecryptionContext)> GetItemAsync<T>();
    }
}
