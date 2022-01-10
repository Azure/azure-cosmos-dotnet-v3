// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Parser;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Documents;

    [MemoryDiagnoser]
    public class ParsingBenchmark
    {
        private static readonly IDictionary<string, object> DefaultQueryengineConfiguration =
            new Dictionary<string, object>()
                {
                    {"maxSqlQueryInputLength", 30720},
                    {"maxJoinsPerSqlQuery", 5},
                    {"maxLogicalAndPerSqlQuery", 200},
                    {"maxLogicalOrPerSqlQuery", 200},
                    {"maxUdfRefPerSqlQuery", 6},
                    {"maxInExpressionItemsCount", 8000},
                    {"queryMaxInMemorySortDocumentCount", 500},
                    {"maxQueryRequestTimeoutFraction", 0.90},
                    {"sqlAllowNonFiniteNumbers", false},
                    {"sqlAllowAggregateFunctions", true},
                    {"sqlAllowSubQuery", false},
                    {"allowNewKeywords", true},
                    {"sqlAllowLike", false},
                    {"sqlAllowGroupByClause", false},
                    {"maxSpatialQueryCells", 12},
                    {"spatialMaxGeometryPointCount", 256},
                    {"sqlDisableQueryILOptimization", false},
                    {"sqlDisableFilterPlanOptimization", false}
                };

        private static readonly QueryPartitionProvider QueryPartitionProvider = new QueryPartitionProvider(DefaultQueryengineConfiguration);

        private static readonly PartitionKeyDefinition PartitionKeyDefinition = new PartitionKeyDefinition()
        {
            Paths = new System.Collections.ObjectModel.Collection<string>()
            {
                "/id",
            },
            Kind = PartitionKind.Hash,
        };

        private readonly SqlQuerySpec ShortQuery;
        private readonly SqlQuerySpec MediumQuery;
        private readonly SqlQuerySpec LongQuery;

        public ParsingBenchmark()
        {
            this.ShortQuery = new SqlQuerySpec("SELECT * FROM c");
            this.MediumQuery = new SqlQuerySpec(@"
                SELECT *
                FROM c
                WHERE c.name = 'John' AND c.age = 35
                ORDER BY c.height
                OFFSET 5 LIMIT 10");
            StringBuilder longQueryText = new StringBuilder();
            longQueryText.Append("SELECT * FROM c WHERE c.number IN (");

            for (int i = 0; i < 1024; i++)
            {
                if (i != 0)
                {
                    longQueryText.Append(",");
                }

                longQueryText.Append(i);
            }

            longQueryText.Append(")");
            this.LongQuery = new SqlQuerySpec(longQueryText.ToString());
        }

        [Benchmark]
        [ArgumentsSource(nameof(Arguments))]
        public void ParseBenchmark(QueryLength queryLength, ParserType parserType)
        {
            SqlQuerySpec sqlQuerySpec = queryLength switch
            {
                QueryLength.Short => this.ShortQuery,
                QueryLength.Medium => this.MediumQuery,
                QueryLength.Long => this.LongQuery,
                _ => throw new ArgumentOutOfRangeException(nameof(queryLength)),
            };

            Action<SqlQuerySpec> parseMethod = parserType switch
            {
                ParserType.Managed => ParseUsingMangedParser,
                ParserType.Native => ParseUsingNativeParser,
                _ => throw new ArgumentOutOfRangeException(nameof(parserType)),
            };

            for (int i = 0; i < 1000; i++)
            {
                parseMethod(sqlQuerySpec);
            }
        }

        private static void ParseUsingMangedParser(SqlQuerySpec sqlQuerySpec)
        {
            if (!SqlQueryParser.TryParse(sqlQuerySpec.QueryText, out SqlQuery sqlQuery))
            {
                throw new InvalidOperationException("FAILED TO PARSE QUERY.");
            }
        }

        private static void ParseUsingNativeParser(SqlQuerySpec sqlQuerySpec)
        {
            TryCatch<PartitionedQueryExecutionInfo> tryGetQueryPlan = QueryPartitionProvider.TryGetPartitionedQueryExecutionInfo(
                querySpec: sqlQuerySpec,
                partitionKeyDefinition: PartitionKeyDefinition,
                requireFormattableOrderByQuery: true,
                isContinuationExpected: false,
                allowNonValueAggregateQuery: true,
                hasLogicalPartitionKey: false,
                allowDCount: true);

            tryGetQueryPlan.ThrowIfFailed();
        }

        public IEnumerable<object[]> Arguments()
        {
            foreach (QueryLength queryLength in Enum.GetValues(typeof(QueryLength)))
            {
                foreach (ParserType parserType in Enum.GetValues(typeof(ParserType)))
                {
                    yield return new object[] { queryLength, parserType };
                }
            }
        }

        public enum ParserType
        {
            Managed,
            Native,
        }

        public enum QueryLength
        {
            Short,
            Medium,
            Long,
        }
    }
}
