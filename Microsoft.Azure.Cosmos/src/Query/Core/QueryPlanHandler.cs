//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using PartitionKeyDefinition = Documents.PartitionKeyDefinition;

    internal sealed class QueryPlanHandler
    {
        private readonly CosmosQueryClient queryClient;

        public QueryPlanHandler(CosmosQueryClient queryClient)
        {
            if (queryClient == null)
            {
                throw new ArgumentNullException($"{nameof(queryClient)}");
            }

            this.queryClient = queryClient;
        }

        public async Task<PartitionedQueryExecutionInfo> GetQueryPlanAsync(
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            QueryFeatures supportedQueryFeatures,
            bool hasLogicalPartitionKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (sqlQuerySpec == null)
            {
                throw new ArgumentNullException($"{nameof(sqlQuerySpec)}");
            }

            if (partitionKeyDefinition == null)
            {
                throw new ArgumentNullException($"{nameof(partitionKeyDefinition)}");
            }

            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo = await this.queryClient
                .GetPartitionedQueryExecutionInfoAsync(
                    sqlQuerySpec: sqlQuerySpec,
                    partitionKeyDefinition: partitionKeyDefinition,
                    requireFormattableOrderByQuery: true,
                    isContinuationExpected: false,
                    allowNonValueAggregateQuery: true,
                    hasLogicalPartitionKey: hasLogicalPartitionKey,
                    cancellationToken: cancellationToken);

            if (partitionedQueryExecutionInfo == null ||
                partitionedQueryExecutionInfo.QueryRanges == null ||
                partitionedQueryExecutionInfo.QueryInfo == null ||
                partitionedQueryExecutionInfo.QueryRanges.Any(range => range.Min == null || range.Max == null))
            {
                throw new InvalidOperationException($"{nameof(partitionedQueryExecutionInfo)} has invalid properties");
            }

            QueryInfo queryInfo = partitionedQueryExecutionInfo.QueryInfo;
            QueryPlanHandler.QueryPlanExceptionFactory.ThrowIfNotSupported(
                queryInfo,
                supportedQueryFeatures);

            return partitionedQueryExecutionInfo;
        }

        private static class QueryPlanExceptionFactory
        {
            private static readonly ArgumentException QueryContainsUnsupportedAggregates = new ArgumentException(
                QueryPlanExceptionFactory.FormatExceptionMessage(nameof(QueryFeatures.Aggregate)));

            private static readonly ArgumentException QueryContainsUnsupportedCompositeAggregate = new ArgumentException(
                QueryPlanExceptionFactory.FormatExceptionMessage(nameof(QueryFeatures.CompositeAggregate)));

            private static readonly ArgumentException QueryContainsUnsupportedMultipleAggregates = new ArgumentException(
                QueryPlanExceptionFactory.FormatExceptionMessage(nameof(QueryFeatures.MultipleAggregates)));

            private static readonly ArgumentException QueryContainsUnsupportedDistinct = new ArgumentException(
                QueryPlanExceptionFactory.FormatExceptionMessage(nameof(QueryFeatures.Distinct)));

            private static readonly ArgumentException QueryContainsUnsupportedOffsetAndLimit = new ArgumentException(
                QueryPlanExceptionFactory.FormatExceptionMessage(nameof(QueryFeatures.OffsetAndLimit)));

            private static readonly ArgumentException QueryContainsUnsupportedOrderBy = new ArgumentException(
                QueryPlanExceptionFactory.FormatExceptionMessage(nameof(QueryFeatures.OrderBy)));

            private static readonly ArgumentException QueryContainsUnsupportedMultipleOrderBy = new ArgumentException(
                QueryPlanExceptionFactory.FormatExceptionMessage(nameof(QueryFeatures.MultipleOrderBy)));

            private static readonly ArgumentException QueryContainsUnsupportedTop = new ArgumentException(
                QueryPlanExceptionFactory.FormatExceptionMessage(nameof(QueryFeatures.Top)));

            private static readonly ArgumentException QueryContainsUnsupportedNonValueAggregate = new ArgumentException(
               QueryPlanExceptionFactory.FormatExceptionMessage(nameof(QueryFeatures.NonValueAggregate)));

            public static void ThrowIfNotSupported(
                QueryInfo queryInfo,
                QueryFeatures supportedQueryFeatures)
            {
                Lazy<List<Exception>> exceptions = new Lazy<List<Exception>>(() => { return new List<Exception>(); });

                QueryPlanExceptionFactory.AddExceptionsForAggregateQueries(
                    queryInfo,
                    supportedQueryFeatures,
                    exceptions);

                QueryPlanExceptionFactory.AddExceptionsForDistinctQueries(
                    queryInfo,
                    supportedQueryFeatures,
                    exceptions);

                QueryPlanExceptionFactory.AddExceptionsForTopQueries(
                    queryInfo,
                    supportedQueryFeatures,
                    exceptions);

                QueryPlanExceptionFactory.AddExceptionsForOrderByQueries(
                    queryInfo,
                    supportedQueryFeatures,
                    exceptions);

                QueryPlanExceptionFactory.AddExceptionsForOffsetLimitQueries(
                    queryInfo,
                    supportedQueryFeatures,
                    exceptions);

                if (exceptions.IsValueCreated)
                {
                    throw new QueryPlanHandlerException(exceptions.Value);
                }
            }

            private static void AddExceptionsForAggregateQueries(
                QueryInfo queryInfo,
                QueryFeatures supportedQueryFeatures,
                Lazy<List<Exception>> exceptions)
            {
                if (queryInfo.HasAggregates)
                {
                    bool isSingleAggregate = (queryInfo.Aggregates.Length == 1)
                        || (queryInfo.GroupByAliasToAggregateType.Values.Where(aggregateOperator => aggregateOperator.HasValue).Count() == 1);
                    if (isSingleAggregate)
                    {
                        if (queryInfo.HasSelectValue)
                        {
                            if (!supportedQueryFeatures.HasFlag(QueryFeatures.Aggregate))
                            {
                                exceptions.Value.Add(QueryContainsUnsupportedAggregates);
                            }
                        }
                        else
                        {
                            if (!supportedQueryFeatures.HasFlag(QueryFeatures.NonValueAggregate))
                            {
                                exceptions.Value.Add(QueryContainsUnsupportedNonValueAggregate);
                            }
                        }
                    }
                    else
                    {
                        if (!supportedQueryFeatures.HasFlag(QueryFeatures.NonValueAggregate))
                        {
                            exceptions.Value.Add(QueryContainsUnsupportedNonValueAggregate);
                        }

                        if (!supportedQueryFeatures.HasFlag(QueryFeatures.MultipleAggregates))
                        {
                            exceptions.Value.Add(QueryContainsUnsupportedMultipleAggregates);
                        }
                    }
                }
            }

            private static void AddExceptionsForDistinctQueries(
                QueryInfo queryInfo,
                QueryFeatures supportedQueryFeatures,
                Lazy<List<Exception>> exceptions)
            {
                if (queryInfo.HasDistinct)
                {
                    if (!supportedQueryFeatures.HasFlag(QueryFeatures.Distinct))
                    {
                        exceptions.Value.Add(QueryContainsUnsupportedDistinct);
                    }
                }
            }

            private static void AddExceptionsForOffsetLimitQueries(
                QueryInfo queryInfo,
                QueryFeatures supportedQueryFeatures,
                Lazy<List<Exception>> exceptions)
            {
                if (queryInfo.HasLimit || queryInfo.HasOffset)
                {
                    if (!supportedQueryFeatures.HasFlag(QueryFeatures.OffsetAndLimit))
                    {
                        exceptions.Value.Add(QueryContainsUnsupportedOffsetAndLimit);
                    }
                }
            }

            private static void AddExceptionsForOrderByQueries(
                QueryInfo queryInfo,
                QueryFeatures supportedQueryFeatures,
                Lazy<List<Exception>> exceptions)
            {
                if (queryInfo.HasOrderBy)
                {
                    if (queryInfo.OrderByExpressions.Length == 1)
                    {
                        if (!supportedQueryFeatures.HasFlag(QueryFeatures.OrderBy))
                        {
                            exceptions.Value.Add(QueryContainsUnsupportedOrderBy);
                        }
                    }
                    else
                    {
                        if (!supportedQueryFeatures.HasFlag(QueryFeatures.MultipleOrderBy))
                        {
                            exceptions.Value.Add(QueryContainsUnsupportedMultipleOrderBy);
                        }
                    }
                }
            }

            private static void AddExceptionsForTopQueries(
                QueryInfo queryInfo,
                QueryFeatures supportedQueryFeatures,
                Lazy<List<Exception>> exceptions)
            {
                if (queryInfo.HasTop)
                {
                    if (!supportedQueryFeatures.HasFlag(QueryFeatures.Top))
                    {
                        exceptions.Value.Add(QueryContainsUnsupportedTop);
                    }
                }
            }

            private static string FormatExceptionMessage(string feature)
            {
                return $"Query contained {feature}, which the calling client does not support.";
            }
        }

        private sealed class QueryPlanHandlerException : AggregateException
        {
            private const string QueryContainsUnsupportedFeaturesExceptionMessage = "Query contains 1 or more unsupported features. Upgrade your SDK to a version that does support the requested features:";
            public QueryPlanHandlerException(IEnumerable<Exception> innerExceptions)
                : base(
                      QueryContainsUnsupportedFeaturesExceptionMessage
                          + Environment.NewLine
                          + string.Join(Environment.NewLine, innerExceptions.Select(innerException => innerException.Message)),
                      innerExceptions)
            {
            }
        }
    }
}