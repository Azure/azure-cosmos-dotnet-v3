namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class QueryPartitionProviderTests
    {
        private static readonly PartitionKeyDefinition PartitionKeyDefinition = new PartitionKeyDefinition()
        {
            Paths = new System.Collections.ObjectModel.Collection<string>()
            {
                "/id",
            },
            Kind = PartitionKind.Hash,
        };

        [TestMethod]
        public void TestQueryPartitionProviderUpdate()
        {
            IDictionary<string, object> smallQueryConfiguration = new Dictionary<string, object>() { { "maxSqlQueryInputLength", 5 } };
            IDictionary<string, object> largeQueryConfiguration = new Dictionary<string, object>() { { "maxSqlQueryInputLength", 524288 } };

            QueryPartitionProvider queryPartitionProvider = new QueryPartitionProvider(smallQueryConfiguration);

            string sqlQuerySpec = JsonConvert.SerializeObject(new SqlQuerySpec("SELECT * FROM c"));

            TryCatch<PartitionedQueryExecutionInfo> tryGetQueryPlan = queryPartitionProvider.TryGetPartitionedQueryExecutionInfo(
                    querySpecJsonString: sqlQuerySpec,
                    partitionKeyDefinition: PartitionKeyDefinition,
                    vectorEmbeddingPolicy: null,
                    requireFormattableOrderByQuery: true,
                    isContinuationExpected: false,
                    allowNonValueAggregateQuery: true,
                    hasLogicalPartitionKey: false,
                    allowDCount: true,
                    useSystemPrefix: false,
                    geospatialType: Cosmos.GeospatialType.Geography);

            Assert.IsTrue(tryGetQueryPlan.Failed);
            Assert.IsTrue(tryGetQueryPlan.Exception.ToString().Contains("The SQL query text exceeded the maximum limit of 5 characters"));

            queryPartitionProvider.Update(largeQueryConfiguration);

            tryGetQueryPlan = queryPartitionProvider.TryGetPartitionedQueryExecutionInfo(
                            querySpecJsonString: sqlQuerySpec,
                            partitionKeyDefinition: PartitionKeyDefinition,
                            vectorEmbeddingPolicy: null,
                            requireFormattableOrderByQuery: true,
                            isContinuationExpected: false,
                            allowNonValueAggregateQuery: true,
                            hasLogicalPartitionKey: false,
                            allowDCount: true,
                            useSystemPrefix: false,
                            geospatialType: Cosmos.GeospatialType.Geography);

            Assert.IsTrue(tryGetQueryPlan.Succeeded);
        }
    }
}
