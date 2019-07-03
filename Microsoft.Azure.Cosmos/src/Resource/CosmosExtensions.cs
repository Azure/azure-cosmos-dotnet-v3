//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Linq;

    /// <summary>
    /// This class provides extension methods used within cosmos sdk code.
    /// </summary>
    public static class CosmosExtensions
    {
        /// <summary>
        /// This method generate query text from LINQ query.
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="query">the IQueryable{T} to be converted.</param>
        /// <returns>An IDocumentQuery{T} that can evaluate the query.</returns>
        /// <example>
        /// This example shows how to generate query text from LINQ.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// IQueryable<T> queryable = container.GetItemsQueryIterator<T>(allowSynchronousQueryExecution = true)
        ///                      .Where(t => b.id.contains("test"));
        /// String sqlQueryText = queryable.ToSqlQueryText();
        /// ]]>
        /// </code>
        /// </example>
        internal static string ToSqlQueryText<T>(this IQueryable<T> query)
        {
            return ((CosmosLinqQuery<T>)query).ToSqlQueryText();
        }

        /// <summary>
        /// This extension method gets the FeedIterator from LINQ IQueryable to execute query asynchronously.
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="query">the IQueryable{T} to be converted.</param>
        /// <returns>An iterator to go through the items.</returns>
        /// <example>
        /// This example shows how to get FeedIterator from LINQ.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// IOrderedQueryable<ToDoActivity> linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>();
        /// FeedIterator<ToDoActivity> setIterator = linqQueryable.Where(item => (item.taskNum < 100)).ToFeedIterator()
        /// ]]>
        /// </code>
        /// </example>
        public static FeedIterator<T> ToFeedIterator<T>(this IQueryable<T> query)
        {
            CosmosLinqQuery<T> linqQuery = query as CosmosLinqQuery<T>;

            if (linqQuery == null)
            {
                throw new ArgumentOutOfRangeException(nameof(linqQuery), "ToFeedIterator is only supported on cosmos LINQ query operations");
            }

            return linqQuery.ToFeedIterator();
        }
    }
}
