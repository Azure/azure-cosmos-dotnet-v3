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
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// This class provides extension methods for cosmos LINQ code.
    /// </summary>
    public static class CosmosLinqExtensions
    {
        /// <summary>
        /// Object representing the options for vector distance calculation. All field are optional. if a field is not specified, the default value will be used. 
        /// For more information, see https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/vectordistance.
        /// </summary>
        public sealed class VectorDistanceOptions
        {
            /// <summary>
            /// The metric used to compute distance/similarity. Valid values are "cosine", "dotproduct", "euclidean".
            /// If not specified, the default value is what is defined in the container policy
            /// </summary>
            [JsonPropertyName("distanceFunction")]
            public DistanceFunction? DistanceFunction { get; set; }

            /// <summary>
            /// The data type of the vectors. float32, int8, uint8 values. Default value is float32.
            /// </summary>
            [JsonPropertyName("dataType")]
            public VectorDataType? DataType { get; set; }

            /// <summary>
            /// An integer specifying the size of the search list when conducting a vector search on the DiskANN index. 
            /// Increasing this may improve accuracy at the expense of RU cost and latency. Min=1, Default=10, Max=100.
            /// </summary>
            [JsonPropertyName("searchListSizeMultiplier")]
            public int? SearchListSizeMultiplier { get; set; }
        }

        /// <summary>
        /// Returns the integer identifier corresponding to a specific item within a physical partition.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="obj">The root object</param>
        /// <returns>Returns the integer identifier corresponding to a specific item within a physical partition.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var documentIdQuery = documents.Where(root => root.DocumentId());
        /// ]]>
        /// </code>
        /// </example>
        public static int DocumentId(this object obj)
        {
            throw new NotImplementedException(ClientResources.TypeCheckExtensionFunctionsNotImplemented);
        }

        /// <summary>
        /// Returns a Boolean value indicating if the type of the specified expression is an array.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Returns true if the type of the specified expression is an array; otherwise, false.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var isArrayQuery = documents.Where(document => document.Names.IsArray());
        /// ]]>
        /// </code>
        /// </example>
        public static bool IsArray(this object obj)
        {
            throw new NotImplementedException(ClientResources.TypeCheckExtensionFunctionsNotImplemented);
        }

        /// <summary>
        /// Returns a Boolean value indicating if the type of the specified expression is a boolean.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Returns true if the type of the specified expression is a boolean; otherwise, false.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var isBoolQuery = documents.Where(document => document.IsRegistered.IsBool());
        /// ]]>
        /// </code>
        /// </example>
        public static bool IsBool(this object obj)
        {
            throw new NotImplementedException(ClientResources.TypeCheckExtensionFunctionsNotImplemented);
        }

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
        /// </example>
        public static bool IsNull(this object obj)
        {
            throw new NotImplementedException(ClientResources.TypeCheckExtensionFunctionsNotImplemented);
        }

        /// <summary>
        /// Returns a Boolean value indicating if the type of the specified expression is a number.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Returns true if the type of the specified expression is a number; otherwise, false.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var isNumberQuery = documents.Where(document => document.Age.IsNumber());
        /// ]]>
        /// </code>
        /// </example>
        public static bool IsNumber(this object obj)
        {
            throw new NotImplementedException(ClientResources.TypeCheckExtensionFunctionsNotImplemented);
        }

        /// <summary>
        /// Returns a Boolean value indicating if the type of the specified expression is an object.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Returns true if the type of the specified expression is an object; otherwise, false.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var isObjectQuery = documents.Where(document => document.Address.IsObject());
        /// ]]>
        /// </code>
        /// </example>
        public static bool IsObject(this object obj)
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
        /// </example>
        public static bool IsPrimitive(this object obj)
        {
            throw new NotImplementedException(ClientResources.TypeCheckExtensionFunctionsNotImplemented);
        }

        /// <summary>
        /// Returns a Boolean value indicating if the type of the specified expression is a string.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Returns true if the type of the specified expression is a string; otherwise, false.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var isStringQuery = documents.Where(document => document.Name.IsString());
        /// ]]>
        /// </code>
        /// </example>
        public static bool IsString(this object obj)
        {
            throw new NotImplementedException(ClientResources.TypeCheckExtensionFunctionsNotImplemented);
        }

        /// <summary>
        /// Returns a Boolean value indicating if the specified expression matches the supplied regex pattern.
        /// For more information, see https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/regexmatch.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="regularExpression">A string expression with a regular expression defined to use when searching.</param>
        /// <returns>Returns true if the string matches the regex expressions; otherwise, false.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var matched = documents.Where(document => document.Name.RegexMatch(<regex>));
        /// ]]>
        /// </code>
        /// </example>
        public static bool RegexMatch(this object obj, string regularExpression)
        {
            throw new NotImplementedException(ClientResources.ExtensionMethodNotImplemented);
        }

        /// <summary>
        /// Returns a Boolean value indicating if the specified expression matches the supplied regex pattern.
        /// For more information, see https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/regexmatch.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="regularExpression">A string expression with a regular expression defined to use when searching.</param>
        /// <param name="searchModifier">An optional string expression with the selected modifiers to use with the regular expression.</param>
        /// <returns>Returns true if the string matches the regex expressions; otherwise, false.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var matched = documents.Where(document => document.Name.RegexMatch(<regex>, <search_modifier>));
        /// ]]>
        /// </code>
        /// </example>
        public static bool RegexMatch(this object obj, string regularExpression, string searchModifier)
        {
            throw new NotImplementedException(ClientResources.ExtensionMethodNotImplemented);
        }

        /// <summary>
        /// Returns the similarity score between two specified vectors.
        /// For more information, see https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/vectordistance.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="vector1">The first vector.</param>
        /// <param name="vector2">The second vector.</param>
        /// <param name="isBruteForce">A boolean specifying how the computed value is used in an ORDER BY expression. If true, then brute force is used. A value of false uses any index defined on the vector property, if it exists. </param>
        /// <param name="options">An JSON formatted object literal used to specify options for the vector distance calculation. </param>
        /// <returns>Returns the similarity score between two specified vectors.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var matched = documents.Select(document => document.vector1.VectorDistance(<vector2>, true, new VectorDistanceOptions() { DistanceFunction = DistanceFunction.Cosine, DataType = VectorDataType.Float32}));
        /// ]]>
        /// </code>
        /// </example>
        public static double VectorDistance(this float[] vector1, float[] vector2, bool isBruteForce, VectorDistanceOptions options)
        {
            throw new NotImplementedException(ClientResources.ExtensionMethodNotImplemented);
        }

        /// <summary>
        /// Returns the similarity score between two specified vectors.
        /// For more information, see https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/vectordistance.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="vector1">The first vector.</param>
        /// <param name="vector2">The second vector.</param>
        /// <param name="isBruteForce">A boolean specifying how the computed value is used in an ORDER BY expression. If true, then brute force is used. A value of false uses any index defined on the vector property, if it exists. </param>
        /// <param name="options">An JSON formatted object literal used to specify options for the vector distance calculation. </param>
        /// <returns>Returns the similarity score between two specified vectors.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var matched = documents.Select(document => document.vector1.VectorDistance(<vector2>, true, new VectorDistanceOptions() { DistanceFunction = DistanceFunction.Cosine, DataType = VectorDataType.Int8}));
        /// ]]>
        /// </code>
        /// </example>
        public static double VectorDistance(this byte[] vector1, byte[] vector2, bool isBruteForce, VectorDistanceOptions options)
        {
            throw new NotImplementedException(ClientResources.ExtensionMethodNotImplemented);
        }

        /// <summary>
        /// Returns the similarity score between two specified vectors.
        /// For more information, see https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/vectordistance.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="vector1">The first vector.</param>
        /// <param name="vector2">The second vector.</param>
        /// <param name="isBruteForce">A boolean specifying how the computed value is used in an ORDER BY expression. If true, then brute force is used. A value of false uses any index defined on the vector property, if it exists. </param>
        /// <param name="options">An JSON formatted object literal used to specify options for the vector distance calculation. </param>
        /// <returns>Returns the similarity score between two specified vectors.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var matched = documents.Select(document => document.vector1.VectorDistance(<vector2>, true, new VectorDistanceOptions() { DistanceFunction = DistanceFunction.Cosine, DataType = VectorDataType.Uint8}));
        /// ]]>
        /// </code>
        /// </example>
        public static double VectorDistance(this sbyte[] vector1, sbyte[] vector2, bool isBruteForce, VectorDistanceOptions options)
        {
            throw new NotImplementedException(ClientResources.ExtensionMethodNotImplemented);
        }

        /// <summary>
        /// Returns a boolean indicating whether the keyword string expression is contained in a specified property path.
        /// For more information, see https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/fulltextcontains.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="search">The string to find.</param>
        /// <returns>Returns true if a given string is contained in the specified property of a document.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var matched = documents.Where(document => document.Name.FullTextContains(<keyword>));
        /// ]]>
        /// </code>
        /// </example>
        public static bool FullTextContains(this object obj, string search)
        {
            throw new NotImplementedException(ClientResources.ExtensionMethodNotImplemented);
        }

        /// <summary>
        /// Returns a boolean indicating whether all of the provided string expressions are contained in a specified property path.
        /// For more information, see https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/fulltextcontainsall.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="searches">The strings to find.</param>
        /// <returns>Returns true if all of the given strings are contained in the specified property of a document.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var matched = documents.Where(document => document.Name.FullTextContainsAll(<keyword1>, <keyword2>, <keyword3>, ...));
        /// ]]>
        /// </code>
        /// </example>
        public static bool FullTextContainsAll(this object obj, params string[] searches)
        {
            throw new NotImplementedException(ClientResources.ExtensionMethodNotImplemented);
        }

        /// <summary>
        /// Returns a boolean indicating whether any of the provided string expressions are contained in a specified property path.
        /// For more information, see https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/fulltextcontainsany.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="searches">The strings to find.</param>
        /// <returns>Returns true if any of the given strings are contained in the specified property of a document.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var matched = documents.Where(document => document.Name.FullTextContainsAny(<keyword1>, <keyword2>, <keyword3>, ...));
        /// ]]>
        /// </code>
        /// </example>
        public static bool FullTextContainsAny(this object obj, params string[] searches)
        {
            throw new NotImplementedException(ClientResources.ExtensionMethodNotImplemented);
        }

        /// <summary>
        /// Returns a BM25 score value that can only be used in an ORDER BY RANK function to sort results from highest relevancy to lowest relevancy.
        /// For more information, see https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/fulltextscore.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="terms">A nonempty array of string literals.</param>
        /// <returns>Returns a BM25 score value that can only be used in an ORDER BY RANK clause.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var matched = documents.OrderByRank(document => document.Name.FullTextScore([<keyword1>], [keyword2]));
        /// ]]>
        /// </code>
        /// </example>
        public static double FullTextScore<TSource>(this TSource obj, params string[] terms)
        {
            throw new NotImplementedException(ClientResources.ExtensionMethodNotImplemented); 
        }

        /// <summary>
        /// This optional ORDER BY RANK clause sorts scoring functions by their rank. It's used specifically for scoring functions like VectorDistance, FullTextScore, and RRF.
        /// For more information, see https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/order-by-rank.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="source"> A sequence of values to order.</param>
        /// <param name="scoreFunction">A scoring function.</param>
        /// <returns>Returns the sequence with the elements ordered according to the rank of the scoring functions.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var matched = documents.OrderByRank(document => document.Name.FullTextScore(<keyword>));
        /// ]]>
        /// </code>
        /// </example>
        public static IOrderedQueryable<TSource> OrderByRank<TSource, TKey>(this IQueryable<TSource> source, Expression<Func<TSource, TKey>> scoreFunction)
        {
            if (!(source is CosmosLinqQuery<TSource>))
            {
                throw new ArgumentException("OrderByRank is only supported on Cosmos LINQ query operations");
            }

            return (IOrderedQueryable<TSource>)source.Provider.CreateQuery<TSource>(
               Expression.Call(
                null,
                typeof(CosmosLinqExtensions).GetMethod("OrderByRank").MakeGenericMethod(typeof(TSource), typeof(TKey)),
                source.Expression,
                Expression.Quote(scoreFunction)));
        }

        /// <summary>
        /// This system function is used to combine two or more scores provided by other scoring functions.
        /// For more information, see https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/rrf.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="scoringFunctions">the scoring functions to combine. Valid functions are FullTextScore and VectorDistance</param>
        /// <returns>Returns the the combined scores of the scoring functions.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var matched = documents.OrderByRank(document => document.RRF(document.Name.FullTextScore(<keyword1>), document.Address.FullTextScore(<keyword2>)));
        /// ]]>
        /// </code>
        /// </example>
        public static double RRF(params double[] scoringFunctions)
        {
            // The reason for not defining "this" keyword is because this causes undesirable serialization when call Expression.ToString() on this method
            throw new NotImplementedException(ClientResources.ExtensionMethodNotImplemented); 
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
        /// using (FeedIterator setIterator = linqQueryable.Where(item => (item.taskNum < 100)).ToStreamIterator())
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

            return cosmosLinqQueryProvider.ExecuteAggregateAsync<int?>(
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
                    new CosmosTraceDiagnostics(NoOpTrace.Singleton),
                    null));
        }

        private static MethodInfo GetMethodInfoOf<T1, T2>(Func<T1, T2> func)
        {
            Debug.Assert(func != null);
            return func.GetMethodInfo();
        }
    }
}
