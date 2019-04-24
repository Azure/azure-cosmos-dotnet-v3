namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// This class provides extension methods for converting a <see cref="System.Linq.IQueryable{T}"/> object to a <see cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery{T}"/> object.
    /// </summary>
    /// <remarks>
    ///  The <see cref="Microsoft.Azure.Cosmos.DocumentClient"/> class provides implementation of standard query methods for querying resources in Azure Cosmos DB. 
    ///  These methods enable you to express traversal, filter, and projection operations over data persisted in the Azure Cosmos DB service.  They are defined as methods that 
    ///  extend IQueryable, and do not perform any querying directly.  Instead, their functionality is to create queries 
    ///  based the resource and query expression provided.  The actual query execution occurs when enumeration forces the expression tree associated with an IQueryable object to be executed.
    /// </remarks>
    /// <seealso cref="Microsoft.Azure.Cosmos.IDocumentClient"/>
    /// <seealso cref="Microsoft.Azure.Cosmos.DocumentClient"/>
    internal static class DocumentQueryable
    {
        /// <summary>
        /// Converts an IQueryable to IDocumentQuery which supports pagination and asynchronous execution in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="query">the IQueryable{T} to be converted.</param>
        /// <returns>An IDocumentQuery{T} that can evaluate the query.</returns>
        /// <example>
        /// This example shows how to run a query asynchronously using the AsDocumentQuery() interface.
        /// 
        /// <code language="c#">
        /// <![CDATA[
        /// using (var queryable = client.CreateDocumentQuery<Book>(
        ///     collectionLink,
        ///     new FeedOptions { MaxItemCount = 10 })
        ///     .Where(b => b.Title == "War and Peace")
        ///     .AsDocumentQuery())
        /// {
        ///     while (queryable.HasMoreResults) 
        ///     {
        ///         foreach(Book b in await queryable.ExecuteNextAsync<Book>())
        ///         {
        ///             // Iterate through books
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public static IDocumentQuery<T> AsDocumentQuery<T>(this IQueryable<T> query)
        {
            return (IDocumentQuery<T>) query;
        }

        /// <summary>
        /// Returns the maximum value in a generic <see cref="IQueryable{TSource}" />.
        /// </summary>
        /// <typeparam name="TSource">The type of the elements of source.</typeparam>
        /// <param name="source">A sequence of values to determine the maximum of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The maximum value in the sequence.</returns>
        public static Task<TSource> MaxAsync<TSource>(
            this IQueryable<TSource> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<TSource>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<TSource>, TSource>(Queryable.Max),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Returns the minimum value in a generic <see cref="IQueryable{TSource}" />.
        /// </summary>
        /// <typeparam name="TSource">The type of the elements of source.</typeparam>
        /// <param name="source">A sequence of values to determine the minimum of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The minimum value in the sequence.</returns>
        public static Task<TSource> MinAsync<TSource>(
            this IQueryable<TSource> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<TSource>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<TSource>, TSource>(Queryable.Min),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the average of a sequence of <see cref="Decimal" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<decimal> AverageAsync(
            this IQueryable<decimal> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<decimal>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<decimal>, decimal>(Queryable.Average),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the average of a sequence of <see cref="Nullable{Decimal}" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<decimal?> AverageAsync(
            this IQueryable<decimal?> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<decimal?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<decimal?>, decimal?>(Queryable.Average),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the average of a sequence of <see cref="Double" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<double> AverageAsync(
            this IQueryable<double> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<double>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<double>, double>(Queryable.Average),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the average of a sequence of <see cref="Nullable{Double}" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<double?> AverageAsync(
            this IQueryable<double?> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<double?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<double?>, double?>(Queryable.Average),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the average of a sequence of <see cref="Single" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<float> AverageAsync(
            this IQueryable<float> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<float>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<float>, float>(Queryable.Average),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the average of a sequence of <see cref="Nullable{Single}" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<float?> AverageAsync(
            this IQueryable<float?> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<float?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<float?>, float?>(Queryable.Average),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the average of a sequence of <see cref="Int32" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<double> AverageAsync(
            this IQueryable<int> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<double>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<int>, double>(Queryable.Average),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the average of a sequence of <see cref="Nullable{Int32}" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<double?> AverageAsync(
            this IQueryable<int?> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<double?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<int?>, double?>(Queryable.Average),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the average of a sequence of <see cref="Int64" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<double> AverageAsync(
            this IQueryable<long> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<double>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<long>, double>(Queryable.Average),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the average of a sequence of <see cref="Nullable{Int64}" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<double?> AverageAsync(
            this IQueryable<long?> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<double?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<long?>, double?>(Queryable.Average),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="Decimal" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<decimal> SumAsync(
            this IQueryable<decimal> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<decimal>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<decimal>, decimal>(Queryable.Sum),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="Nullable{Decimal}" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<decimal?> SumAsync(
            this IQueryable<decimal?> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<decimal?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<decimal?>, decimal?>(Queryable.Sum),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="Double" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<double> SumAsync(
            this IQueryable<double> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<double>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<double>, double>(Queryable.Sum),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="Nullable{Double}" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<double?> SumAsync(
            this IQueryable<double?> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<double?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<double?>, double?>(Queryable.Sum),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="Single" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<float> SumAsync(
            this IQueryable<float> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<float>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<float>, float>(Queryable.Sum),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="Nullable{Single}" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<float?> SumAsync(
            this IQueryable<float?> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<float?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<float?>, float?>(Queryable.Sum),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="Int32" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<int> SumAsync(
            this IQueryable<int> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<int>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<int>, int>(Queryable.Sum),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="Nullable{Int32}" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<int?> SumAsync(
            this IQueryable<int?> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<int?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<int?>, int?>(Queryable.Sum),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="Int64" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<long> SumAsync(
            this IQueryable<long> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<long>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<long>, long>(Queryable.Sum),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="Nullable{Int64}" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<long?> SumAsync(
            this IQueryable<long?> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<long?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<long?>, long?>(Queryable.Sum),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Returns the number of elements in a sequence.
        /// </summary>
        /// <typeparam name="TSource">The type of the elements of source.</typeparam>
        /// <param name="source">The sequence that contains the elements to be counted.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The number of elements in the input sequence.</returns>
        public static Task<int> CountAsync<TSource>(
            this IQueryable<TSource> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ((IDocumentQueryProvider)source.Provider).ExecuteAsync<int>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<TSource>, int>(Queryable.Count),
                    source.Expression),
                cancellationToken);
        }

        internal static IQueryable<TResult> AsSQL<TSource, TResult>(
            this IOrderedQueryable<TSource> source,
            SqlQuerySpec querySpec)
        {
            if (querySpec == null)
            {
                throw new ArgumentNullException("querySpec");
            }

            if (string.IsNullOrEmpty(querySpec.QueryText))
            {
                throw new ArgumentException("querySpec.QueryText");
            }
#if !(NETSTANDARD15 || NETSTANDARD16)
            return source.Provider.CreateQuery<TResult>(Expression.Call(null, ((MethodInfo)MethodBase.GetCurrentMethod()).MakeGenericMethod(
                new Type[]
                {
                    typeof(TSource),
                    typeof(TResult)
                }), new Expression[]
                {
                    source.Expression,
                    Expression.Constant(querySpec)
                }));
#else
            return source.Provider.CreateQuery<TResult>(Expression.Call(null,
                GetMethodInfoOf(() => DocumentQueryable.AsSQL(
                    default(IOrderedQueryable<TSource>),
                    default(SqlQuerySpec))),
                    new Expression[]
                    {
                        source.Expression,
                        Expression.Constant(querySpec)
                    }));
#endif
        }

        internal static IQueryable<dynamic> AsSQL<TSource>(
            this IOrderedQueryable<TSource> source,
            SqlQuerySpec querySpec)
        {
            if (querySpec == null)
            {
                throw new ArgumentNullException("querySpec");
            }

            if (string.IsNullOrEmpty(querySpec.QueryText))
            {
                throw new ArgumentException("querySpec.QueryText");
            }
#if !(NETSTANDARD15 || NETSTANDARD16)
            return source.Provider.CreateQuery<dynamic>(Expression.Call(null, ((MethodInfo)MethodBase.GetCurrentMethod()).MakeGenericMethod(
                new Type[]
                {
                    typeof(TSource)
                }), new Expression[]
                {
                    source.Expression,
                    Expression.Constant(querySpec)
                }));
#else
            return source.Provider.CreateQuery<dynamic>(Expression.Call(null,
               GetMethodInfoOf(() => DocumentQueryable.AsSQL(
                   default(IOrderedQueryable<TSource>),
                   default(SqlQuerySpec))),
                   new Expression[]
                   {
                        source.Expression,
                        Expression.Constant(querySpec)
                   }));
#endif
        }

        internal static IOrderedQueryable<Document> CreateDocumentQuery(this IDocumentQueryClient client, string collectionLink, FeedOptions feedOptions = null, object partitionKey = null)
        {
            return new DocumentQuery<Document>(client, ResourceType.Document, typeof (Document), collectionLink, feedOptions, partitionKey);
        }

        internal static IQueryable<dynamic> CreateDocumentQuery(this IDocumentQueryClient client, string collectionLink, SqlQuerySpec querySpec, FeedOptions feedOptions = null, object partitionKey = null)
        {
            return new DocumentQuery<Document>(client, ResourceType.Document, typeof (Document), collectionLink, feedOptions, partitionKey).AsSQL(querySpec);
        }

        private static MethodInfo GetMethodInfoOf<T>(Expression<Func<T>> expression)
        {
            MethodCallExpression body = (MethodCallExpression)expression.Body;
            return body.Method;
        }

        private static MethodInfo GetMethodInfoOf<T1, T2>(Func<T1, T2> func)
        {
            Debug.Assert(func != null);
            return func.GetMethodInfo();
        }
    }
}
