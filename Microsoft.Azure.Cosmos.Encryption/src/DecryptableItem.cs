// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System.IO;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

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
    ///                 (ToDoActivity toDo, DecryptionInfo _) = await item.GetItemAsync<ToDoActivity>();
    ///             }
    ///             catch (Exception)
    ///             {}
    ///         }
    ///     }
    /// }
    /// ]]>
    /// </code>
    /// </example>
    public class DecryptableItem
    {
        /// <summary>
        /// The encrypted content which is yet to be decrypted.
        /// </summary>
        private JToken decryptableContent;

        private Encryptor encryptor;

        private CosmosSerializer cosmosSerializer;

        /// <summary>
        /// For customer to use for mocking
        /// </summary>
        protected DecryptableItem()
        {
        }

        internal DecryptableItem(
            JToken decryptableContent,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer)
        {
            this.decryptableContent = decryptableContent;
            this.encryptor = encryptor;
            this.cosmosSerializer = cosmosSerializer;
        }

        internal void Populate(
            JToken decryptableContent,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer)
        {
            this.decryptableContent = decryptableContent;
            this.encryptor = encryptor;
            this.cosmosSerializer = cosmosSerializer;
        }

        internal virtual Stream GetInputStreamPayload(
            CosmosSerializer cosmosSerializer)
        {
            return null;
        }

        /// <summary>
        /// Decrypts and deserializes the content.
        /// </summary>
        /// <typeparam name="T">The type of item to be returned.</typeparam>
        /// <returns>The requested item and the decryption information.</returns>
        public async Task<(T, DecryptionInfo)> GetItemAsync<T>()
        {
            (Stream decryptedStream, DecryptionInfo decryptionInfo) = await this.GetItemAsStreamAsync();
            return (this.cosmosSerializer.FromStream<T>(decryptedStream), decryptionInfo);
        }

        /// <summary>
        /// Decrypts the content and outputs stream.
        /// </summary>
        /// <returns>Decrypted stream response and the decryption information.</returns>
        public async Task<(Stream, DecryptionInfo)> GetItemAsStreamAsync()
        {
            if (!(this.decryptableContent is JObject document))
            {
                return (EncryptionProcessor.BaseSerializer.ToStream(this.decryptableContent), null);
            }

            (JObject decryptedItem, DecryptionInfo decryptionInfo) = await EncryptionProcessor.DecryptAsync(
                document,
                this.encryptor,
                new CosmosDiagnosticsContext(),
                cancellationToken: default);

            return (EncryptionProcessor.BaseSerializer.ToStream(decryptedItem), decryptionInfo);
        }
    }
}
