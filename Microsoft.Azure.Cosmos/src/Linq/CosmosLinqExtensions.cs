//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// This class provides extension methods for cosmos LINQ code.
    /// </summary>
    public static class CosmosLinqExtensions
    {
        /// <summary>
        /// Determines if a certain property is defined or not.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Returns true if this property is defined otherwise returns false.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var isDefinedQuery = documents.Where(document => document.Name.IsDefined());
        /// ]]>
        /// </code>
        /// </example>
        public static bool IsDefined(this object obj)
        {
            throw new NotImplementedException(ClientResources.TypeCheckExtensionFunctionsNotImplemented);
        }

        /// <summary>
        /// Determines if a certain property is null or not.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Returns true if this property is null otherwise returns false.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var isNullQuery = documents.Where(document => document.Name.IsNull());
        /// ]]>
        /// </code>
        /// </example>s>
        public static bool IsNull(this object obj)
        {
            throw new NotImplementedException(ClientResources.TypeCheckExtensionFunctionsNotImplemented);
        }

        /// <summary>
        /// Determines if a certain property is of primitive JSON type.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Returns true if this property is null otherwise returns false.</returns>
        /// <remarks>
        /// Primitive JSON types (Double, String, Boolean and Null)
        /// </remarks>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var isPrimitiveQuery = documents.Where(document => document.Name.IsPrimitive());
        /// ]]>
        /// </code>
        /// </example>s>
        public static bool IsPrimitive(this object obj)
        {
            throw new NotImplementedException(ClientResources.TypeCheckExtensionFunctionsNotImplemented);
        }

        /// <summary>
        /// This method generate query definition from LINQ query.
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="query">the IQueryable{T} to be converted.</param>
        /// <param name="namedParameters">Dictionary containing parameter value and name for parameterized query</param>
        /// <returns>The queryDefinition which can be used in query execution.</returns>
        /// <example>
        /// This example shows how to generate query definition from LINQ.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// IQueryable<T> queryable = container.GetItemsQueryIterator<T>(allowSynchronousQueryExecution = true)
        ///                      .Where(t => b.id.contains("test"));
        /// QueryDefinition queryDefinition = queryable.ToQueryDefinition();
        /// ]]>
        /// </code>
        /// </example>
#if PREVIEW
        public
#else
        internal
#endif
        static QueryDefinition ToQueryDefinition<T>(this IQueryable<T> query, IDictionary<object, string> namedParameters)
        {
            if (namedParameters == null)
            {
                throw new ArgumentException("namedParameters dictionary cannot be empty for this overload, please use ToQueryDefinition<T>(IQueryable<T> query) instead", nameof(namedParameters));
            }

            if (query is CosmosLinqQuery<T> linqQuery)
            {
                return linqQuery.ToQueryDefinition(namedParameters);
            }

            throw new ArgumentException("ToQueryDefinition is only supported on Cosmos LINQ query operations", nameof(query));
        }

        /// <summary>
        /// This method generate query definition from LINQ query.
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="query">the IQueryable{T} to be converted.</param>
        /// <returns>The queryDefinition which can be used in query execution.</returns>
        /// <example>
        /// This example shows how to generate query definition from LINQ.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// IQueryable<T> queryable = container.GetItemsQueryIterator<T>(allowSynchronousQueryExecution = true)
        ///                      .Where(t => b.id.contains("test"));
        /// QueryDefinition queryDefinition = queryable.ToQueryDefinition();
        /// ]]>
        /// </code>
        /// </example>
        public static QueryDefinition ToQueryDefinition<T>(this IQueryable<T> query)
        {
            if (query is CosmosLinqQuery<T> linqQuery)
            {
                return linqQuery.ToQueryDefinition();

            }

            throw new ArgumentException("ToQueryDefinition is only supported on Cosmos LINQ query operations", nameof(query));
        }

        /// <summary>
        /// This extension method gets the FeedIterator from LINQ IQueryable to execute query asynchronously.
        /// This will create the fresh new FeedIterator when called.
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
        /// using (FeedIterator<ToDoActivity> setIterator = linqQueryable.Where(item => (item.taskNum < 100)).ToFeedIterator()
        /// ]]>
        /// </code>
        /// </example>
        public static FeedIterator<T> ToFeedIterator<T>(this IQueryable<T> query)
        {
            if (!(query is CosmosLinqQuery<T> linqQuery))
            {
                throw new ArgumentOutOfRangeException(nameof(linqQuery), "ToFeedIterator is only supported on Cosmos LINQ query operations");
            }

            return linqQuery.ToFeedIterator();
        }

        /// <summary>
        /// This extension method gets the FeedIterator from LINQ IQueryable to execute query asynchronously.
        /// This will create the fresh new FeedIterator when called.
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
        /// using (FeedIterator setIterator = linqQueryable.Where(item => (item.taskNum < 100)).ToFeedIterator()
        /// ]]>
        /// </code>
        /// </example>
        public static FeedIterator ToStreamIterator<T>(this IQueryable<T> query)
        {
            if (!(query is CosmosLinqQuery<T> linqQuery))
            {
                throw new ArgumentOutOfRangeException(nameof(linqQuery), "ToStreamFeedIterator is only supported on cosmos LINQ query operations");
            }

            return linqQuery.ToStreamIterator();
        }

        /// <summary>
        /// Returns the maximum value in a generic <see cref="IQueryable{TSource}" />.
        /// </summary>
        /// <typeparam name="TSource">The type of the elements of source.</typeparam>
        /// <param name="source">A sequence of values to determine the maximum of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The maximum value in the sequence.</returns>
        public static Task<Response<TSource>> MaxAsync<TSource>(
            this IQueryable<TSource> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Max());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<TSource>(
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
        public static Task<Response<TSource>> MinAsync<TSource>(
            this IQueryable<TSource> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Min());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<TSource>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<TSource>, TSource>(Queryable.Min),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the average of a sequence of <see cref="decimal" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<Response<decimal>> AverageAsync(
            this IQueryable<decimal> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Average());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<decimal>(
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
        public static Task<Response<decimal?>> AverageAsync(
            this IQueryable<decimal?> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Average());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<decimal?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<decimal?>, decimal?>(Queryable.Average),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the average of a sequence of <see cref="double" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<Response<double>> AverageAsync(
            this IQueryable<double> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Average());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<double>(
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
        public static Task<Response<double?>> AverageAsync(
            this IQueryable<double?> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Average());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<double?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<double?>, double?>(Queryable.Average),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the average of a sequence of <see cref="float" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<Response<float>> AverageAsync(
            this IQueryable<float> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Average());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<float>(
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
        public static Task<Response<float?>> AverageAsync(
            this IQueryable<float?> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Average());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<float?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<float?>, float?>(Queryable.Average),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the average of a sequence of <see cref="int" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<Response<double>> AverageAsync(
            this IQueryable<int> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Average());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<double>(
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
        public static Task<Response<double?>> AverageAsync(
            this IQueryable<int?> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Average());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<double?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<int?>, double?>(Queryable.Average),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the average of a sequence of <see cref="long" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<Response<double>> AverageAsync(
            this IQueryable<long> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Average());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<double>(
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
        public static Task<Response<double?>> AverageAsync(
            this IQueryable<long?> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Average());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<double?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<long?>, double?>(Queryable.Average),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="decimal" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<Response<decimal>> SumAsync(
            this IQueryable<decimal> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Sum());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<decimal>(
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
        public static Task<Response<decimal?>> SumAsync(
            this IQueryable<decimal?> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Sum());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<decimal?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<decimal?>, decimal?>(Queryable.Sum),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="double" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<Response<double>> SumAsync(
            this IQueryable<double> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Sum());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<double>(
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
        public static Task<Response<double?>> SumAsync(
            this IQueryable<double?> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Sum());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<double?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<double?>, double?>(Queryable.Sum),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="float" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<Response<float>> SumAsync(
            this IQueryable<float> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Sum());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<float>(
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
        public static Task<Response<float?>> SumAsync(
            this IQueryable<float?> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Sum());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<float?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<float?>, float?>(Queryable.Sum),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="int" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<Response<int>> SumAsync(
            this IQueryable<int> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Sum());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<int>(
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
        public static Task<Response<int?>> SumAsync(
            this IQueryable<int?> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Sum());
            }

            return ((CosmosLinqQueryProvider)source.Provider).ExecuteAggregateAsync<int?>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<int?>, int?>(Queryable.Sum),
                    source.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="long" /> values.
        /// </summary>
        /// <param name="source">A sequence of values to calculate the average of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The average value in the sequence.</returns>
        public static Task<Response<long>> SumAsync(
            this IQueryable<long> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Sum());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<long>(
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
        public static Task<Response<long?>> SumAsync(
            this IQueryable<long?> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Sum());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<long?>(
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
        public static Task<Response<int>> CountAsync<TSource>(
            this IQueryable<TSource> source,
            CancellationToken cancellationToken = default)
        {
            if (!(source.Provider is CosmosLinqQueryProvider cosmosLinqQueryProvider))
            {
                return ResponseHelperAsync(source.Count());
            }

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<int>(
                Expression.Call(
                    GetMethodInfoOf<IQueryable<TSource>, int>(Queryable.Count),
                    source.Expression),
                cancellationToken);
        }

        private static Task<Response<T>> ResponseHelperAsync<T>(T value)
        {
            return Task.FromResult<Response<T>>(
                new ItemResponse<T>(
                    System.Net.HttpStatusCode.OK,
                    new Headers(),
                    value,
                    NoOpTrace.Singleton));
        }

        private static MethodInfo GetMethodInfoOf<T1, T2>(Func<T1, T2> func)
        {
            Debug.Assert(func != null);
            return func.GetMethodInfo();
        }
    }
}
