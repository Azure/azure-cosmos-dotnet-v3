//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;

    /// <summary>
    /// This class provides extension methods for Azure Cosmos DB for NoSQL's asynchronous feed iterator.
    /// </summary>
    public static class FeedAsyncEnumeratorExtensions
    {
        /// <summary>
        /// This extension method returns the current feed iterator for items within an Azure Cosmos DB for NoSQL container as an <see cref="IAsyncEnumerable{T}"/>.
        /// 
        /// The <see cref="IAsyncEnumerable{T}"/> enables iteration over pages of results with sets of items.
        /// </summary>
        /// <typeparam name="T">
        /// The generic type to deserialize items to.
        /// </typeparam>
        /// <param name="feedIterator">
        /// The feed iterator to be converted to an asynchronous enumerable.
        /// </param>
        /// <seealso cref="IAsyncEnumerable{T}"/>
        /// <seealso cref="FeedResponse{T}"/> 
        /// <returns>
        /// This method returns an asynchronous enumerable that enables iteration over pages of results and sets of items for each page.
        /// </returns>
        /// <example>
        /// This example shows how to:
        /// 
        /// 1. Create a <seealso cref="FeedIterator{T}"/> from an existing <seealso cref="Container"/>.
        /// 1. Use this extension method to convert the iterator into an <seealso cref="IAsyncEnumerable{T}"/>.
        /// 
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator<Item> feedIterator = container.GetItemQueryIterator<Item>(
        ///    queryText: "SELECT * FROM items"
        /// );
        /// 
        /// IAsyncEnumerable<FeedResponse<Item>> pages = query.AsAsyncEnumerable();
        /// 
        /// List<Item> items = new();
        /// double requestCharge = 0.0; 
        /// await foreach(var page in pages)
        /// {
        ///     requestCharge += page.RequestCharge;
        ///     foreach (var item in page)
        ///     {
        ///         items.Add(item);
        ///     }
        /// }
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
        public static IAsyncEnumerable<FeedResponse<T>> AsAsyncEnumerable<T>(
            this FeedIterator<T> feedIterator)
        {
            return new FeedAsyncEnumerator<T>(feedIterator);
        }
    }
}