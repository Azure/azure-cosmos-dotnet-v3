// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Parser;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.CosmosQueryExecutionContextFactory;

    [MemoryDiagnoser]
    public class QueryPlanBenchmark
    {
        private readonly QueryDefinition queryDefinition;
        private readonly SqlQuerySpec querySpec;
        private readonly string QueryPlan = "{\"queryInfo\":{\"distinctType\":\"None\",\"groupByExpressions\":[],\"groupByAliases\":[],\"orderBy\":[],\"orderByExpressions\":[],\"aggregates\":[],\"hasSelectValue\":0,\"rewrittenQuery\":\"\",\"groupByAliasToAggregateType\":{}},\"queryRanges\":[{\"min\":[],\"max\":\"Infinity\",\"isMinInclusive\":true,\"isMaxInclusive\":false}]}";
        private readonly QueryPartitionProvider queryPartitionProvider;
        private readonly PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
        {
            Paths = new Collection<string>()
            {
                "/pk"
            }
        };

        public QueryPlanBenchmark()
        {
            string queryConfig = "{\"maxSqlQueryInputLength\":262144,\"maxJoinsPerSqlQuery\":5,\"maxLogicalAndPerSqlQuery\":500,\"maxLogicalOrPerSqlQuery\":500,\"maxUdfRefPerSqlQuery\":10,\"maxInExpressionItemsCount\":16000,\"queryMaxInMemorySortDocumentCount\":500,\"maxQueryRequestTimeoutFraction\":0.9,\"sqlAllowNonFiniteNumbers\":false,\"sqlAllowAggregateFunctions\":true,\"sqlAllowSubQuery\":true,\"sqlAllowScalarSubQuery\":true,\"allowNewKeywords\":true,\"sqlAllowLike\":true,\"sqlAllowGroupByClause\":true,\"maxSpatialQueryCells\":12,\"spatialMaxGeometryPointCount\":256,\"sqlDisableOptimizationFlags\":0,\"sqlAllowTop\":true,\"enableSpatialIndexing\":true}";
            IDictionary<string, object> queryengineConfiguration = JsonConvert.DeserializeObject<Dictionary<string, object>>(queryConfig);
            this.queryPartitionProvider = new QueryPartitionProvider(queryengineConfiguration);
            this.queryDefinition = new QueryDefinition("select * from r");
            this.querySpec = this.queryDefinition.ToSqlQuerySpec();
           
        }

        [Benchmark]
        public void QueryPlanAntlr()
        {
            bool parsed = SqlQueryParser.TryParse(this.queryDefinition.QueryText, out SqlQuery sqlQuery);
            if (parsed)
            {
                if(sqlQuery == null)
                {
                    throw new Exception("SQL query null");
                }

                bool hasDistinct = sqlQuery.SelectClause.HasDistinct;
                bool hasGroupBy = sqlQuery.GroupByClause != default;
                bool hasAggregates = AggregateProjectionDetector.HasAggregate(sqlQuery.SelectClause.SelectSpec);
                bool createPassthroughQuery = !hasAggregates && !hasDistinct && !hasGroupBy;

                if (!createPassthroughQuery)
                {
                    throw new Exception("SQL query null");
                }
            }
        }

        [Benchmark]
        public void QueryPlanDeSerialize()
        {
            PartitionedQueryExecutionInfoInternal info = JsonConvert.DeserializeObject<PartitionedQueryExecutionInfoInternal>(
                           this.QueryPlan,
                           new JsonSerializerSettings
                           {
                               DateParseHandling = DateParseHandling.None
                           });
            if (info == null)
            {
                throw new Exception("SQL query null");
            }
        }

        [Benchmark]
        public void QueryPlanServiceInterop()
        {
            Cosmos.Query.Core.Monads.TryCatch<PartitionedQueryExecutionInfo> partitionedQueryExecutionInfo = this.queryPartitionProvider.TryGetPartitionedQueryExecutionInfo(
                this.querySpec,
                this.partitionKeyDefinition,
                true,
                true,
                true,
                true,
                true);

            if (partitionedQueryExecutionInfo.Failed)
            {
                throw new Exception("ServiceInteropParse failed");
            }
        }
    }
}