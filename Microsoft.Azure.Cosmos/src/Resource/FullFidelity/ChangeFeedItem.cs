//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Text.Json.Serialization;
    using Newtonsoft.Json;

    /// <summary>
    /// The typed response that contains the current, previous, and metadata change feed resource when <see cref="ChangeFeedMode"/> is initialized to <see cref="ChangeFeedMode.AllVersionsAndDeletes"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// public class ToDoActivity
    /// {
    ///     public string type { get; set; }
    ///     public string id { get; set; }
    ///     public string status { get; set; }
    /// }
    /// 
    /// ChangeFeedMode changeFeedMode = ChangeFeedMode.AllVersionsAndDeletes;
    /// PartitionKey partitionKey = new PartitionKey(@"learning");
    /// ChangeFeedStartFrom changeFeedStartFrom = ChangeFeedStartFrom.Now(FeedRange.FromPartitionKey(partitionKey));
    /// 
    /// using (FeedIterator<ChangeFeedItem<ToDoActivity>> feedIterator = container.GetChangeFeedIterator<ChangeFeedItemChanges<ToDoActivity>>(
    ///     changeFeedStartFrom: changeFeedStartFrom,
    ///     changeFeedMode: changeFeedMode))
    /// {
    ///     while (feedIterator.HasMoreResults)
    ///     {
    ///         FeedResponse<ChangeFeedItem<ToDoActivity>> feedResponse = await feedIterator.ReadNextAsync();
    ///         
    ///         if (feedResponse.StatusCode != HttpStatusCode.NotModified)
    ///         {
    ///             IEnumerable<ChangeFeedItem<ToDoActivity>> feedResource = feedResponse.Resource;
    ///             
    ///             foreach(ChangeFeedItem<ToDoActivity> itemChanges in feedResource)
    ///             {
    ///                 ToDoActivity currentToDoActivity = itemChanges.Current;
    ///                 ToDoActivity previousToDoActivity = itemChanges.Previous;
    ///                 ChangeFeedMetadata toDoActivityMetadata = itemChanges.Metadata;
    ///             }
    ///         }
    ///     }
    /// }
    /// ]]>
    /// </code>
    /// </example>
    /// <remarks><see cref="ChangeFeedItem{T}"/> is an optional helper class that uses Newtonsoft serialization libraries. Users are welcome to create their own custom helper class.</remarks>
#if PREVIEW
    public
#else
    internal
#endif  
        class ChangeFeedItem<T>
    {
        /// <summary>
        /// The current version of the item for all versions and deletes change feed mode.
        /// </summary>
        [JsonProperty(PropertyName = "current")]
        [JsonPropertyName("current")]
        public T Current { get; set; }

        /// <summary>
        /// The item metadata for all versions and deletes change feed mode.
        /// </summary>
        [JsonProperty(PropertyName = "metadata", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("metadata")]
        public ChangeFeedMetadata Metadata { get; set; }

        /// <summary>
        /// The previous version of the item for all versions and deletes change feed mode. The previous version on delete and replace operations is not exposed by default and requires container-level opt-in. Refer to https://aka.ms/cosmosdb-change-feed-deletes for more information.
        /// </summary>
        [JsonProperty(PropertyName = "previous", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("previous")]
        public T Previous { get; set; }
    }
}
