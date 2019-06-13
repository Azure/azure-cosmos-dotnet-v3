//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Linq;
    using Microsoft.Azure.Cosmos.Linq;

    /// <summary>
    /// This class provides extension method for converting a <see cref="System.Linq.IQueryable{T}"/> object to a  SqlQueryText.
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
        public static string ToSqlQueryText<T>(this IQueryable<T> query)
        {
            return ((CosmosLinqQuery<T>)query).ToSqlQueryText();
        }
    }
}
