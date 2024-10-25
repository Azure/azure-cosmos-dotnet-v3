//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// This class provides extension methods for Azure Cosmos DB for NoSQL's feed iterator.
    /// </summary>
    public static class FeedIteratorExtensions
    {
        /// <summary>
        /// This extension method takes an existing <see cref="FeedIterator{T}"/> and builds an <see cref="IAsyncEnumerator{T}"/> that enables iteration over pages of results with sets of items.
        /// </summary>
        /// <typeparam name="T">The generic type to deserialize items to.</typeparam>
        /// <param name="feedIterator">
        /// The target feed iterator instance.
        /// </param>
        /// <param name="cancellationToken">
        /// (optional) The cancellation token that could be used to cancel the operation.
        /// </param>
        /// <returns>
        /// This extension method returns an asynchronous enumerator that enables iteration over pages of results and sets of items for each page.
        /// </returns>
        /// <seealso cref="FeedIterator{T}"/> 
        /// <seealso cref="IAsyncEnumerable{T}"/> 
        /// <seealso href="https://learn.microsoft.com/dotnet/csharp/iterators#iterating-with-foreach"/>
        /// <example>
        /// This example shows how to:
        /// 
        /// 1. Create a <seealso cref="FeedIterator{T}"/> from an existing <seealso cref="Container"/>. 
        /// 1. Use this extension method to convert the iterator into an <seealso cref="IAsyncEnumerator{T}"/>. 
        /// 
        /// Finally, use the `await foreach` statement to iterate over the pages and items.
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator<Item> feedIterator = container.GetItemQueryIterator<Item>(
        ///     queryText: "SELECT * FROM items"    
        /// );
        /// 
        /// IAsyncEnumerator<FeedResponse<Item>> pages = feedIterator.BuildFeedAsyncEnumerator<Item>();
        /// 
        /// List<Items> items = new();
        /// double requestCharge = 0.0; 
        /// await foreach(var page in pages)
        /// {
        ///     requestCharge += page.RequestCharge;
        ///     foreach (var item in page)
        ///     {
        ///         items.Add(item);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// 
        /// <code language="c#">
        /// <![CDATA[
        /// record Item(
        ///    string id,
        ///    string partitionKey,
        ///    string value
        /// );
        /// ]]>
        /// </code>
        /// </example>
        public static async IAsyncEnumerator<FeedResponse<T>> BuildFeedAsyncEnumerator<T>(
            this FeedIterator<T> feedIterator,
            CancellationToken cancellationToken = default)
        {
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<T> response = await feedIterator.ReadNextAsync(cancellationToken);

                yield return response;
            }
        }
    }
}