namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.OptimisticDirectExecutionQuery;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class OptimisticDirectExecutionQueryBaselineTests : BaselineTests<OptimisticDirectExecutionTestInput, OptimisticDirectExecutionTestOutput>
    {
        [TestMethod]
        [Owner("akotalwar")]
        public void PositiveOptimisticDirectExecutionOutput()
        {
            List<OptimisticDirectExecutionTestInput> testVariations = new List<OptimisticDirectExecutionTestInput>
            {
                CreateInput(
                    description: @"Single Partition Key and Distinct",
                    query: "SELECT DISTINCT c.age FROM c",
                    expectedOptimisticDirectExecution: true,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: @"value"),

                CreateInput(
                    description: @"Single Partition Key and Min Aggregate",
                    query: "SELECT VALUE MIN(c.age) FROM c",
                    expectedOptimisticDirectExecution: true,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: @"value"),

                CreateInput(
                    description: @"Single Partition Key and Value Field",
                    query: "SELECT c.age FROM c",
                    expectedOptimisticDirectExecution: true,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: @"value"),

                CreateInput(
                    description: @"Single Partition Key and Value Field",
                    query: "SELECT * FROM c",
                    expectedOptimisticDirectExecution: true,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: "a",
                    continuationToken: null),

                CreateInput(
                    description: @"Single Partition Key and Ode continuation token",
                    query: "SELECT * FROM c",
                    expectedOptimisticDirectExecution: true,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: "a",
                    continuationToken: CosmosElement.Parse(
                        "{\"OptimisticDirectExecutionToken\":{\"token\":\"{\\\"resourceId\\\":\\\"AQAAAMmFOw8LAAAAAAAAAA==\\\"," +
                        "\\\"skipCount\\\":1}\", \"range\":{\"min\":\"\",\"max\":\"FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF\"}}}")),

                // Below cases are Ode because they have a collection with a single physical partition.
                // Added emulator tests (TestPassingOptimisticDirectExecutionQueries()) to verify the negation of the below cases.
                CreateInput(
                    description: @"Cosmos.PartitionKey.Null Partition Key Value",
                    query: "SELECT * FROM c",
                    expectedOptimisticDirectExecution: true,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: Cosmos.PartitionKey.Null),

                CreateInput(
                    description: @"C# Null Partition Key Value",
                    query: "SELECT * FROM c",
                    expectedOptimisticDirectExecution: true,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: null),
            };

            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("akotalwar")]
        public void NegativeOptimisticDirectExecutionOutput()
        {
            ParallelContinuationToken parallelContinuationToken = new ParallelContinuationToken(
                    token: Guid.NewGuid().ToString(),
                    range: new Documents.Routing.Range<string>("A", "B", true, false));

            OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                    parallelContinuationToken,
                    new List<OrderByItem>() { new OrderByItem(CosmosObject.Create(new Dictionary<string, CosmosElement>() { { "item", CosmosString.Create("asdf") } })) },
                    resumeValues: null,
                    rid: "43223532",
                    skipCount: 42,
                    filter: "filter");

            CosmosElement cosmosElementOrderByContinuationToken = CosmosArray.Create(
                        new List<CosmosElement>()
                        {
                        OrderByContinuationToken.ToCosmosElement(orderByContinuationToken)
                        });

            List<OptimisticDirectExecutionTestInput> testVariations = new List<OptimisticDirectExecutionTestInput>
            {
                CreateInput(
                    description: @"Single Partition Key with Parallel continuation token",
                    query: "SELECT * FROM c",
                    expectedOptimisticDirectExecution: false,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: "a",
                    continuationToken: CosmosArray.Create(new List<CosmosElement>() { ParallelContinuationToken.ToCosmosElement(parallelContinuationToken) })),

                CreateInput(
                    description: @"Single Partition Key with OrderBy continuation token",
                    query: "SELECT * FROM c ORDER BY c._ts",
                    expectedOptimisticDirectExecution: false,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: "a",
                    continuationToken: cosmosElementOrderByContinuationToken),
            };
            this.ExecuteTestSuite(testVariations);
        }

        // This test confirms that TestInjection.EnableOptimisticDirectExection is set to false from default. 
        // Check test "TestPipelineForDistributedQueryAsync" to understand why this is done
        [TestMethod]
        public void TestDefaultQueryRequestOptionsSettings()
        {
            QueryRequestOptions requestOptions = new QueryRequestOptions();
            Assert.AreEqual(true, requestOptions.EnableOptimisticDirectExecution);
        }

        // test checks that the pipeline can take a query to the backend and returns its associated document(s).
        [TestMethod]
        public async Task TestPipelineForBackendDocumentsOnSinglePartitionAsync()
        {
            int numItems = 100;
            int documentCountInSinglePartition = 0;
            OptimisticDirectExecutionTestInput input = CreateInput(
                    description: @"Single Partition Key and Value Field",
                    query: "SELECT VALUE COUNT(1) FROM c",
                    expectedOptimisticDirectExecution: true,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: "a");

            QueryRequestOptions queryRequestOptions = GetQueryRequestOptions(enableOptimisticDirectExecution: true);
            DocumentContainer inMemoryCollection = await CreateDocumentContainerAsync(numItems, multiPartition: false);
            IQueryPipelineStage queryPipelineStage = await GetOdePipelineAsync(input, inMemoryCollection, queryRequestOptions);

            while (await queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton))
            {
                Assert.AreEqual(TestInjections.PipelineType.OptimisticDirectExecution, queryRequestOptions.TestSettings.Stats.PipelineType.Value);

                TryCatch<QueryPage> tryGetPage = queryPipelineStage.Current;
                tryGetPage.ThrowIfFailed();

                documentCountInSinglePartition += Int32.Parse(tryGetPage.Result.Documents[0].ToString());

                if (tryGetPage.Result.State == null)
                {
                    break;
                }
            }

            Assert.AreEqual(100, documentCountInSinglePartition);
        }

        [TestMethod]
        public async Task TestOdeTokenWithSpecializedPipeline()
        {
            int numItems = 100;
            ParallelContinuationToken parallelContinuationToken = new ParallelContinuationToken(
                    token: Guid.NewGuid().ToString(),
                    range: new Documents.Routing.Range<string>("A", "B", true, false));

            OptimisticDirectExecutionContinuationToken optimisticDirectExecutionContinuationToken = new OptimisticDirectExecutionContinuationToken(parallelContinuationToken);
            CosmosElement cosmosElementContinuationToken = OptimisticDirectExecutionContinuationToken.ToCosmosElement(optimisticDirectExecutionContinuationToken);

            OptimisticDirectExecutionTestInput input = CreateInput(
                    description: @"Single Partition Key and Value Field",
                    query: "SELECT VALUE COUNT(1) FROM c",
                    expectedOptimisticDirectExecution: false,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: "a",
                    continuationToken: cosmosElementContinuationToken);

            DocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems, multiPartition: false);
            QueryRequestOptions queryRequestOptions = GetQueryRequestOptions(enableOptimisticDirectExecution: input.ExpectedOptimisticDirectExecution);
            (CosmosQueryExecutionContextFactory.InputParameters inputParameters, CosmosQueryContextCore cosmosQueryContextCore) = CreateInputParamsAndQueryContext(input, queryRequestOptions);

            IQueryPipelineStage queryPipelineStage = CosmosQueryExecutionContextFactory.Create(
                      documentContainer,
                      cosmosQueryContextCore,
                      inputParameters,
                      NoOpTrace.Singleton);

            string expectedErrorMessage = "Execution of this query using the supplied continuation token requires EnableOptimisticDirectExecution to be set in QueryRequestOptions. " +
                "If the error persists after that, contact system administrator.";

            while (await queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton))
            {
                if (queryPipelineStage.Current.Failed)
                {
                    Assert.IsTrue(queryPipelineStage.Current.InnerMostException.ToString().Contains(expectedErrorMessage));
                    return;
                }

                Assert.IsFalse(true);
                break;
            }
        }

        [TestMethod]
        public async Task TestQueriesWhichNeverRequireDistribution()
        {
            // requiresDist = false
            int numItems = 100;
            List<RequiresDistributionTestCase> singlePartitionContainerTestCases = new List<RequiresDistributionTestCase>()
            {
                new RequiresDistributionTestCase("SELECT * FROM r", 10, 100),
                new RequiresDistributionTestCase("SELECT VALUE r.id FROM r", 0, 10),
                new RequiresDistributionTestCase("SELECT * FROM r WHERE r.id > 5", 0,  0),
                new RequiresDistributionTestCase("SELECT r.id FROM r JOIN id IN r.id",0, 0),
                new RequiresDistributionTestCase("SELECT TOP 5 r.id FROM r ORDER BY r.id", 0, 5),
                new RequiresDistributionTestCase("SELECT TOP 5 r.id FROM r WHERE r.id > 5 ORDER BY r.id", 0, 0),
                new RequiresDistributionTestCase("SELECT * FROM r OFFSET 5 LIMIT 3", 1, 3),
                new RequiresDistributionTestCase("SELECT * FROM r WHERE r.id > 5 OFFSET 5 LIMIT 3", 0, 0)
            };

            foreach (RequiresDistributionTestCase testCase in singlePartitionContainerTestCases)
            {
                OptimisticDirectExecutionTestInput input = CreateInput(
                    description: @"Queries which will never require distribution",
                    query: testCase.Query,
                    expectedOptimisticDirectExecution: true,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: "a");

                int result = await this.GetPipelineAndDrainAsync(
                            input,
                            numItems: numItems,
                            isMultiPartition: false,
                            expectedContinuationTokenCount: testCase.ExpectedContinuationTokenCount,
                            requiresDist: false);

                Assert.AreEqual(testCase.ExpectedDocumentCount, result);
            }
        }

        [TestMethod]
        public async Task TestQueriesWhichWillAlwaysRequireDistribution()
        {
            // requiresDist = true
            int numItems = 100;
            List<RequiresDistributionTestCase> singlePartitionContainerTestCases = new List<RequiresDistributionTestCase>()
            {
                new RequiresDistributionTestCase("SELECT Sum(id) as sum_id FROM r JOIN id IN r.id", 0, 1),
                new RequiresDistributionTestCase("SELECT DISTINCT TOP 5 r.id FROM r ORDER BY r.id", 0, 5),
                new RequiresDistributionTestCase("SELECT DISTINCT r.id FROM r GROUP BY r.id", 0,  10),
                new RequiresDistributionTestCase("SELECT DISTINCT r.id, Sum(r.id) as sum_a FROM r GROUP BY r.id",0, 10),
                new RequiresDistributionTestCase("SELECT Count(1) FROM (SELECT DISTINCT r.id FROM root r)", 0, 1),
                new RequiresDistributionTestCase("SELECT DISTINCT id FROM r JOIN id in r.id", 0, 0),
                new RequiresDistributionTestCase("SELECT r.id, Count(1) AS count_a FROM r GROUP BY r.id ORDER BY r.id", 0, 10),
                new RequiresDistributionTestCase("SELECT Count(1) as count FROM root r JOIN b IN r.id", 0, 1),
                new RequiresDistributionTestCase("SELECT Avg(1) AS avg FROM root r", 0, 1),
                new RequiresDistributionTestCase("SELECT r.id, Count(1) as count FROM r WHERE r.id > 0 GROUP BY r.id", 0, 0),
                new RequiresDistributionTestCase("SELECT r.id FROM r WHERE r.id > 0 GROUP BY r.id ORDER BY r.id", 0, 0)
            };

            foreach (RequiresDistributionTestCase testCase in singlePartitionContainerTestCases)
            {
                OptimisticDirectExecutionTestInput input = CreateInput(
                    description: @"Queries which will always require distribution",
                    query: testCase.Query,
                    expectedOptimisticDirectExecution: true,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: "a");

                int result = await this.GetPipelineAndDrainAsync(
                            input,
                            numItems: numItems,
                            isMultiPartition: false,
                            expectedContinuationTokenCount: testCase.ExpectedContinuationTokenCount,
                            requiresDist: true);

                Assert.AreEqual(testCase.ExpectedDocumentCount, result);
            }
        }

        [TestMethod]
        public async Task TestQueriesWhichCanSwitchDistribution()
        {
            // requiresDist = true/false
            int numItems = 100;
            bool[] requiresDistSet = { true, false };

            List<RequiresDistributionTestCase> singlePartitionContainerTestCases = new List<RequiresDistributionTestCase>()
            {
                new RequiresDistributionTestCase("SELECT Count(r.id) AS count_a FROM r", 0, 1),
                new RequiresDistributionTestCase("SELECT DISTINCT r.id FROM r", 0, 10),
                new RequiresDistributionTestCase("SELECT r.id, Count(1) AS count_a FROM r GROUP BY r.id", 0, 10),
                new RequiresDistributionTestCase("SELECT Count(1) AS count FROM root r WHERE r.id < 2", 0, 1),
                new RequiresDistributionTestCase("SELECT r.id, Count(r.id), Avg(r.id) FROM r WHERE r.id < 2 GROUP BY r.id", 0, 0),
                new RequiresDistributionTestCase("SELECT TOP 5 Count(1) as count FROM r", 0, 1),
                new RequiresDistributionTestCase("SELECT TOP 5 Count(1) as count FROM r ORDER BY r.id", 0, 1),
                new RequiresDistributionTestCase("SELECT r.id FROM r GROUP BY r.id OFFSET 5 LIMIT 3", 0, 3),
                new RequiresDistributionTestCase("SELECT Count(1) as count FROM r", 0, 1),
                new RequiresDistributionTestCase("SELECT Sum(r.id) as sum_a FROM r", 0, 1),
                new RequiresDistributionTestCase("SELECT Min(r.a) as min_a FROM r", 0, 1),
                new RequiresDistributionTestCase("SELECT Max(r.a) as min_a FROM r", 0, 1),
                new RequiresDistributionTestCase("SELECT Avg(r.a) as min_a FROM r", 0, 1),
                new RequiresDistributionTestCase("SELECT Sum(r.a) as sum_a FROM r WHERE r.a > 0", 0, 1),
                new RequiresDistributionTestCase("SELECT Sum(r.a) as sum_a FROM r WHERE r.a > 0 OFFSET 0 LIMIT 5", 0, 1),
                new RequiresDistributionTestCase("SELECT Sum(r.a) as sum_a FROM r WHERE r.a > 0 OFFSET 5 LIMIT 5", 0, 0),
                new RequiresDistributionTestCase("SELECT Sum(r.a) as sum_a FROM r ORDER BY r.a", 0, 1),
                new RequiresDistributionTestCase("SELECT Sum(r.a) as sum_a FROM r ORDER BY r.a OFFSET 5 LIMIT 5", 0, 0),
                new RequiresDistributionTestCase("SELECT Sum(r.a) as sum_a FROM r WHERE r.a > 0 ORDER BY r.a OFFSET 5 LIMIT 5", 0, 0),
                new RequiresDistributionTestCase("SELECT DISTINCT VALUE r.id FROM r", 0, 10),
                new RequiresDistributionTestCase("SELECT DISTINCT TOP 5 r.a FROM r", 0, 1),
                new RequiresDistributionTestCase("SELECT s.id FROM (SELECT DISTINCT r.id FROM root r) as s", 0, 10),
                new RequiresDistributionTestCase("SELECT DISTINCT r.a FROM r OFFSET 3 LIMIT 5", 0, 0),
                new RequiresDistributionTestCase("SELECT Count(r.id) AS count_a FROM r", 0, 1),
                new RequiresDistributionTestCase("SELECT r.id, Count(1) AS count_a FROM r GROUP BY r.id", 0, 10),
                new RequiresDistributionTestCase("SELECT Count(1) AS count FROM root r WHERE r.id < 2", 0, 1),
                new RequiresDistributionTestCase("SELECT TOP 5 Count(1) as count FROM r", 0, 1),
                new RequiresDistributionTestCase("SELECT Count(1) AS count FROM root r WHERE r.id < 2", 0, 1),
            };

            foreach (RequiresDistributionTestCase testCase in singlePartitionContainerTestCases)
            {
                foreach (bool requiresDist in requiresDistSet)
                {
                    OptimisticDirectExecutionTestInput input = CreateInput(
                        description: @"Queries which can require distribution in certain cases",
                        query: testCase.Query,
                        expectedOptimisticDirectExecution: true,
                        partitionKeyPath: @"/pk",
                        partitionKeyValue: "a");

                    int result = await this.GetPipelineAndDrainAsync(
                                input,
                                numItems: numItems,
                                isMultiPartition: false,
                                expectedContinuationTokenCount: testCase.ExpectedContinuationTokenCount,
                                requiresDist: requiresDist);

                    Assert.AreEqual(testCase.ExpectedDocumentCount, result);
                }
            }
        }

        // test checks that the pipeline can take a query to the backend and returns its associated document(s) + continuation token.
        [TestMethod]
        public async Task TestPipelineForContinuationTokenOnSinglePartitionAsync()
        {
            int numItems = 100;
            OptimisticDirectExecutionTestInput input = CreateInput(
                description: @"Single Partition Key and Value Field",
                    query: "SELECT * FROM c",
                expectedOptimisticDirectExecution: true,
                partitionKeyPath: @"/pk",
                partitionKeyValue: "a");

            int result = await this.GetPipelineAndDrainAsync(
                            input,
                            numItems: numItems,
                            isMultiPartition: false,
                            expectedContinuationTokenCount: 10);

            Assert.AreEqual(numItems, result);
        }

        // test checks that the Ode code path ensures that a query is valid before sending it to the backend
        // these queries with previous ODE implementation would have succeeded. However, with the new query validity check, they should all throw an exception
        [TestMethod]
        public async Task TestQueryValidityCheckWithODEAsync()
        {
            const string UnsupportedSelectStarInGroupBy = "'SELECT *' is not allowed with GROUP BY";
            const string UnsupportedCompositeAggregate = "Compositions of aggregates and other expressions are not allowed.";
            const string UnsupportedNestedAggregateExpression = "Cannot perform an aggregate function on an expression containing an aggregate or a subquery.";
            const string UnsupportedSelectLisWithAggregateOrGroupByExpression = "invalid in the select list because it is not contained in either an aggregate function or the GROUP BY clause";

            List<(string Query, string ExpectedMessage)> testVariations = new List<(string Query, string ExpectedMessage)>
            {
                ("SELECT   COUNT     (1)   + 5 FROM c", UnsupportedCompositeAggregate),
                ("SELECT MIN(c.price)   + 10 FROM c", UnsupportedCompositeAggregate),
                ("SELECT      MAX(c.price)       - 4 FROM c", UnsupportedCompositeAggregate),
                ("SELECT SUM    (c.price) + 20     FROM c",UnsupportedCompositeAggregate),
                ("SELECT AVG(c.price) * 50 FROM      c", UnsupportedCompositeAggregate),
                ("SELECT * from c GROUP BY c.name", UnsupportedSelectStarInGroupBy),
                ("SELECT SUM(c.sales) AS totalSales, AVG(SUM(c.salesAmount)) AS averageTotalSales\n\n\nFROM c", UnsupportedNestedAggregateExpression),
                ("SELECT c.category, c.price, COUNT(c) FROM c GROUP BY c.category\r\n", UnsupportedSelectLisWithAggregateOrGroupByExpression)
            };

            List<(string, string)> testVariationsWithCaseSensitivity = new List<(string, string)>();
            foreach ((string Query, string ExpectedMessage) testCase in testVariations)
            {
                testVariationsWithCaseSensitivity.Add((testCase.Query, testCase.ExpectedMessage));
                testVariationsWithCaseSensitivity.Add((testCase.Query.ToLower(), testCase.ExpectedMessage));
                testVariationsWithCaseSensitivity.Add((testCase.Query.ToUpper(), testCase.ExpectedMessage));
            }

            foreach ((string Query, string ExpectedMessage) testCase in testVariationsWithCaseSensitivity)
            {
                OptimisticDirectExecutionTestInput input = CreateInput(
                    description: @"Unsupported queries in CosmosDB that were previously supported by Ode pipeline and returning wrong results",
                    query: testCase.Query,
                    expectedOptimisticDirectExecution: true,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: "a");

                try
                {
                    int result = await this.GetPipelineAndDrainAsync(
                                    input,
                                    numItems: 100,
                                    isMultiPartition: false,
                                    expectedContinuationTokenCount: 0,
                                    requiresDist: true);
                    Assert.Fail("Invalid query being executed did not result in an exception");
                }
                catch (Exception ex)
                {
                    Assert.IsTrue(ex.InnerException.InnerException.Message.Contains(testCase.ExpectedMessage));
                    continue;
                }
            }
        }

        // test to check if pipeline handles a 410 exception properly and returns all the documents.
        [TestMethod]
        public async Task TestPipelineForGoneExceptionOnSingleAndMultiplePartitionAsync()
        {
            Assert.IsTrue(await ExecuteGoneExceptionOnODEPipeline(isMultiPartition: false));

            Assert.IsTrue(await ExecuteGoneExceptionOnODEPipeline(isMultiPartition: true));
        }

        // test to check if failing fallback pipeline is handled properly
        [TestMethod]
        public async Task TestHandlingOfFailedFallbackPipelineOnSingleAndMultiplePartitionAsync()
        {
            Assert.IsTrue(await TestHandlingOfFailedFallbackPipeline(isMultiPartition: false));

            Assert.IsTrue(await TestHandlingOfFailedFallbackPipeline(isMultiPartition: true));
        }

        // The reason we have the below test is to show the missing capabilities of the OptimisticDirectExecution pipeline.
        // Currently this pipeline cannot handle distributed queries as it does not have the logic to sum up the values it gets from the backend in partial results.
        // This functionality is available for other pipelines such as the ParallelCrossPartitionQueryPipelineStage.
        [TestMethod]
        public async Task TestPipelineForDistributedQueryAsync()
        {
            int numItems = 100;
            OptimisticDirectExecutionTestInput input = CreateInput(
                    description: @"Single Partition Key and Value Field",
                    query: "SELECT AVG(c) FROM c",
                    expectedOptimisticDirectExecution: false,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: "a");

            int result = await this.GetPipelineAndDrainAsync(
                            input,
                            numItems: numItems,
                            isMultiPartition: false,
                            expectedContinuationTokenCount: 0);

            //TODO: Add validation for actual value of average
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public async Task TestClientDisableOdeLogic()
        {
            // GetPipelineAndDrainAsyc() contains asserts to confirm that the Ode pipeline only gets picked if clientDisableOptimisticDirectExecution flag is false
            int numItems = 100;
            OptimisticDirectExecutionTestInput input = CreateInput(
                    description: @"Single Partition Key and Value Field",
                    query: "SELECT * FROM c",
                    expectedOptimisticDirectExecution: true,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: "a");

            // Test with ClientDisableOde = true
            int result = await this.GetPipelineAndDrainAsync(
                            input,
                            numItems: numItems,
                            isMultiPartition: false,
                            expectedContinuationTokenCount: 10,
                            requiresDist: false,
                            clientDisableOde: true);

            Assert.AreEqual(numItems, result);

            // Test with ClientDisableOde = false
            result = await this.GetPipelineAndDrainAsync(
                            input,
                            numItems: numItems,
                            isMultiPartition: false,
                            expectedContinuationTokenCount: 10,
                            requiresDist: false,
                            clientDisableOde: false);

            Assert.AreEqual(numItems, result);
        }

        [TestMethod]
        public async Task TestOdeFlagsWithContinuationToken()
        {
            ParallelContinuationToken parallelContinuationToken = new ParallelContinuationToken(
                    token: Guid.NewGuid().ToString(),
                    range: new Range<string>("A", "B", true, false));

            OptimisticDirectExecutionContinuationToken optimisticDirectExecutionContinuationToken = new OptimisticDirectExecutionContinuationToken(parallelContinuationToken);
            CosmosElement cosmosElementContinuationToken = OptimisticDirectExecutionContinuationToken.ToCosmosElement(optimisticDirectExecutionContinuationToken);

            OptimisticDirectExecutionTestInput input = CreateInput(
                    description: @"Single Partition Key and Ode continuation token",
                    query: "SELECT * FROM c",
                    expectedOptimisticDirectExecution: true,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: "a",
                    continuationToken: cosmosElementContinuationToken);

            // All of these cases should throw the same exception message.
            await this.ValidateErrorMessageWithModifiedOdeFlags(input, enableOde: true, clientDisableOde: true);
            await this.ValidateErrorMessageWithModifiedOdeFlags(input, enableOde: false, clientDisableOde: true);
            await this.ValidateErrorMessageWithModifiedOdeFlags(input, enableOde: false, clientDisableOde: false);
        }

        private async Task ValidateErrorMessageWithModifiedOdeFlags(OptimisticDirectExecutionTestInput input, bool enableOde, bool clientDisableOde)
        {
            string expectedErrorMessage = "Execution of this query using the supplied continuation token requires EnableOptimisticDirectExecution to be set in QueryRequestOptions. " +
                "If the error persists after that, contact system administrator.";
            try
            {
                int result = await this.GetPipelineAndDrainAsync(
                                    input,
                                    numItems: 100,
                                    isMultiPartition: false,
                                    expectedContinuationTokenCount: 10,
                                    requiresDist: false,
                                    enableOde,
                                    clientDisableOde);

                Assert.Fail("A MalformedContinuationTokenException was expected in this scenario");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.InnerException.Message.Contains(expectedErrorMessage));
            }
        }

        [TestMethod]
        public async Task TestTextDistributionPlanParsingFromStream()
        {
            string textPath = "../../../Query/DistributionPlans/Text";
            string[] filePaths = Directory.GetFiles(textPath);
            
            foreach (string filePath in filePaths)
            {
                string testResponse = File.ReadAllText(filePath);
                JObject jsonObject = JObject.Parse(testResponse);

                string expectedBackendPlan = jsonObject["_distributionPlan"]["backendDistributionPlan"].ToString();
                expectedBackendPlan = RemoveWhitespace(expectedBackendPlan);

                string expectedClientPlan = jsonObject["_distributionPlan"]["clientDistributionPlan"].ToString();
                expectedClientPlan = RemoveWhitespace(expectedClientPlan);

                MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(testResponse));
                CosmosQueryClientCore.ParseRestStream(
                    memoryStream,
                    Documents.ResourceType.Document,
                    out CosmosArray documents,
                    out CosmosObject distributionPlan);

                if (distributionPlan.TryGetValue("backendDistributionPlan", out CosmosElement backendDistributionPlan) &&
                    distributionPlan.TryGetValue("clientDistributionPlan", out CosmosElement clientDistributionPlan))
                {
                    Assert.AreEqual(expectedBackendPlan, RemoveWhitespace(backendDistributionPlan.ToString()));
                    Assert.AreEqual(expectedClientPlan, RemoveWhitespace(clientDistributionPlan.ToString()));
                }
                else
                {
                    Assert.Fail();
                }
            }
        }

        [TestMethod]
        public async Task TestBinaryDistributionPlanParsingFromStream()
        {
            string expectedBackendPlan = "{\"query\":\"\\nSELECT Count(r.a) AS count_a\\nFROM r\",\"obfuscatedQuery\":\"{\\\"query\\\":\\\"SELECT Count(r.a) AS p1\\\\nFROM r\\\",\\\"parameters\\\":[]}\",\"shape\":\"{\\\"Select\\\":{\\\"Type\\\":\\\"List\\\",\\\"AggCount\\\":1},\\\"From\\\":{\\\"Expr\\\":\\\"Aliased\\\"}}\",\"signature\":-4885972563975185329,\"shapeSignature\":-6171928203673877984,\"queryIL\":{\"Expression\":{\"Kind\":\"Aggregate\",\"Type\":{\"Kind\":\"Enum\",\"ItemType\":{\"Kind\":\"Base\",\"BaseTypeKind\":\"Number\",\"ExcludesUndefined\":true}},\"Aggregate\":{\"Kind\":\"Builtin\",\"Signature\":{\"ItemType\":{\"Kind\":\"Base\",\"BaseTypeKind\":\"Variant\",\"ExcludesUndefined\":false},\"ResultType\":{\"Kind\":\"Base\",\"BaseTypeKind\":\"Number\",\"ExcludesUndefined\":true}},\"OperatorKind\":\"Count\"},\"SourceExpression\":{\"Kind\":\"Select\",\"Type\":{\"Kind\":\"Enum\",\"ItemType\":{\"Kind\":\"Base\",\"BaseTypeKind\":\"Variant\",\"ExcludesUndefined\":false}},\"Delegate\":{\"Kind\":\"ScalarExpression\",\"Type\":{\"Kind\":\"Base\",\"BaseTypeKind\":\"Variant\",\"ExcludesUndefined\":false},\"DeclaredVariable\":{\"Name\":\"v0\",\"UniqueId\":0,\"Type\":{\"Kind\":\"Base\",\"BaseTypeKind\":\"Variant\",\"ExcludesUndefined\":true}},\"Expression\":{\"Kind\":\"PropertyRef\",\"Type\":{\"Kind\":\"Base\",\"BaseTypeKind\":\"Variant\",\"ExcludesUndefined\":false},\"Expression\":{\"Kind\":\"VariableRef\",\"Type\":{\"Kind\":\"Base\",\"BaseTypeKind\":\"Variant\",\"ExcludesUndefined\":true},\"Variable\":{\"Name\":\"v0\",\"UniqueId\":0,\"Type\":{\"Kind\":\"Base\",\"BaseTypeKind\":\"Variant\",\"ExcludesUndefined\":true}}},\"PropertyName\":\"a\"}},\"SourceExpression\":{\"Kind\":\"Input\",\"Type\":{\"Kind\":\"Enum\",\"ItemType\":{\"Kind\":\"Base\",\"BaseTypeKind\":\"Variant\",\"ExcludesUndefined\":true}},\"Name\":\"r\"}}}},\"noSpatial\":true,\"language\":\"QueryIL\"}";
            string expectedClientPlan = "{\"clientQL\":{\"Kind\":\"Select\",\"DeclaredVariable\":{\"Name\":\"v0\",\"UniqueId\":2},\"Expression\":{\"Kind\":\"ObjectCreate\",\"ObjectKind\":\"Object\",\"Properties\":[{\"Name\":\"count_a\",\"Expression\":{\"Kind\":\"VariableRef\",\"Variable\":{\"Name\":\"v0\",\"UniqueId\":2}}}]},\"SourceExpression\":{\"Kind\":\"Aggregate\",\"Aggregate\":{\"Kind\":\"Builtin\",\"OperatorKind\":\"Sum\"},\"SourceExpression\":{\"Kind\":\"Input\",\"Name\":\"root\"}}}}";

            string textPath = "../../../Query/DistributionPlans/Binary";
            string[] filePaths = Directory.GetFiles(textPath);
            string testResponse = File.ReadAllText(filePaths[0]);

            MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(testResponse));
            CosmosQueryClientCore.ParseRestStream(
                memoryStream,
                Documents.ResourceType.Document,
                out CosmosArray documents,
                out CosmosObject distributionPlan);

            if (distributionPlan.TryGetValue("backendDistributionPlan", out CosmosElement backendDistributionPlan) &&
                distributionPlan.TryGetValue("clientDistributionPlan", out CosmosElement clientDistributionPlan))
            {
                Assert.IsTrue(backendDistributionPlan.ToString().Equals(expectedBackendPlan));
                Assert.IsTrue(clientDistributionPlan.ToString().Equals(expectedClientPlan));
            }
            else
            {
                Assert.Fail();
            }
        }

        // Creates a gone exception after the first MoveNexyAsync() call. This allows for the pipeline to return some documents before failing
        private static async Task<bool> ExecuteGoneExceptionOnODEPipeline(bool isMultiPartition)
        {
            int numItems = 100;
            List<CosmosElement> documents = new List<CosmosElement>();
            QueryRequestOptions queryRequestOptions = GetQueryRequestOptions(enableOptimisticDirectExecution: true);
            (MergeTestUtil mergeTest, IQueryPipelineStage queryPipelineStage) = await CreateFallbackPipelineTestInfrastructure(numItems, isFailedFallbackPipelineTest: false, isMultiPartition, queryRequestOptions);

            while (await queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton))
            {
                if (mergeTest.MoveNextCounter == 1)
                {
                    Assert.AreEqual(TestInjections.PipelineType.OptimisticDirectExecution, queryRequestOptions.TestSettings.Stats.PipelineType.Value);
                }
                else
                {
                    Assert.AreNotEqual(TestInjections.PipelineType.OptimisticDirectExecution, queryRequestOptions.TestSettings.Stats.PipelineType.Value);
                }

                TryCatch<QueryPage> tryGetPage = queryPipelineStage.Current;

                if (tryGetPage.Failed)
                {
                    // failure should never come till here. Should be handled before
                    Assert.Fail("Unexpected error. Gone Exception should not reach till here");
                }

                documents.AddRange(tryGetPage.Result.Documents);
            }

            Assert.AreEqual(numItems, documents.Count);
            return true;
        }

        private static async Task<bool> TestHandlingOfFailedFallbackPipeline(bool isMultiPartition)
        {
            int numItems = 100;
            List<CosmosElement> documents = new List<CosmosElement>();
            QueryRequestOptions queryRequestOptions = GetQueryRequestOptions(enableOptimisticDirectExecution: true);
            (MergeTestUtil mergeTest, IQueryPipelineStage queryPipelineStage) = await CreateFallbackPipelineTestInfrastructure(numItems, isFailedFallbackPipelineTest: true, isMultiPartition, queryRequestOptions);

            while (await queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton))
            {
                TryCatch<QueryPage> tryGetPage = queryPipelineStage.Current;
                if (tryGetPage.Failed)
                {
                    if (mergeTest.MoveNextCounter == 3)
                    {
                        Assert.IsTrue(tryGetPage.InnerMostException.Message.Equals("Injected failure"));
                        Assert.AreNotEqual(numItems, documents.Count);
                        return true;
                    }
                    else
                    {
                        Assert.Fail("Fallback pipeline failure not handled correctly");
                        return false;
                    }
                }

                documents.AddRange(tryGetPage.Result.Documents);
            }

            return false;
        }

        private static string RemoveWhitespace(string jsonString)
        {
            return jsonString.Replace(" ", string.Empty);
        }

        private static async Task<(MergeTestUtil, IQueryPipelineStage)> CreateFallbackPipelineTestInfrastructure(int numItems, bool isFailedFallbackPipelineTest, bool isMultiPartition, QueryRequestOptions queryRequestOptions)
        {
            List<CosmosElement> documents = new List<CosmosElement>();
            MergeTestUtil mergeTest = new MergeTestUtil(isFailedFallbackPipelineTest);

            OptimisticDirectExecutionTestInput input = CreateInput(
                    description: @"Single Partition Key and Value Field",
                    query: "SELECT * FROM c",
                    expectedOptimisticDirectExecution: true,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: "a");

            DocumentContainer inMemoryCollection = await CreateDocumentContainerAsync(
                    numItems,
                    multiPartition: isMultiPartition,
                    failureConfigs: new FlakyDocumentContainer.FailureConfigs(
                        inject429s: false,
                        injectEmptyPages: false,
                        shouldReturnFailure: mergeTest.ShouldReturnFailure));

            IQueryPipelineStage queryPipelineStage = await GetOdePipelineAsync(input, inMemoryCollection, queryRequestOptions);

            return (mergeTest, queryPipelineStage);
        }

        private async Task<int> GetPipelineAndDrainAsync(OptimisticDirectExecutionTestInput input, int numItems, bool isMultiPartition, int expectedContinuationTokenCount, bool requiresDist = false, bool enableOptimisticDirectExecution = true, bool clientDisableOde = false)
        {
            int continuationTokenCount = 0;
            List<CosmosElement> documents = new List<CosmosElement>();
            QueryRequestOptions queryRequestOptions = GetQueryRequestOptions(enableOptimisticDirectExecution);
            DocumentContainer inMemoryCollection = await CreateDocumentContainerAsync(numItems, multiPartition: isMultiPartition, requiresDist: requiresDist);
            IQueryPipelineStage queryPipelineStage = await GetOdePipelineAsync(input, inMemoryCollection, queryRequestOptions, clientDisableOde);

            while (await queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton))
            {
                TryCatch<QueryPage> tryGetPage = queryPipelineStage.Current;
                tryGetPage.ThrowIfFailed();

                if (clientDisableOde || !enableOptimisticDirectExecution)
                {
                    Assert.AreNotEqual(TestInjections.PipelineType.OptimisticDirectExecution, queryRequestOptions.TestSettings.Stats.PipelineType.Value);
                }

                if (!clientDisableOde && enableOptimisticDirectExecution && !requiresDist)
                {
                    Assert.AreEqual(TestInjections.PipelineType.OptimisticDirectExecution, queryRequestOptions.TestSettings.Stats.PipelineType.Value);
                }

                documents.AddRange(tryGetPage.Result.Documents);

                if (tryGetPage.Result.State == null)
                {
                    break;
                }
                else
                {
                    input = CreateInput(
                        description: input.Description,
                        query: input.Query,
                        expectedOptimisticDirectExecution: input.ExpectedOptimisticDirectExecution,
                        partitionKeyPath: @"/pk",
                        partitionKeyValue: input.PartitionKeyValue,
                        continuationToken: tryGetPage.Result.State.Value);

                    queryPipelineStage = await GetOdePipelineAsync(input, inMemoryCollection, queryRequestOptions);
                }

                continuationTokenCount++;
            }

            Assert.AreEqual(expectedContinuationTokenCount, continuationTokenCount);
            return documents.Count;
        }

        internal static Tuple<PartitionedQueryExecutionInfo, QueryPartitionProvider> GetPartitionedQueryExecutionInfoAndPartitionProvider(string querySpecJsonString, PartitionKeyDefinition pkDefinition, bool clientDisableOde = false)
        {
            QueryPartitionProvider queryPartitionProvider = CreateCustomQueryPartitionProvider("clientDisableOptimisticDirectExecution", clientDisableOde.ToString().ToLower());
            TryCatch<PartitionedQueryExecutionInfo> tryGetQueryPlan = queryPartitionProvider.TryGetPartitionedQueryExecutionInfo(
                querySpecJsonString: querySpecJsonString,
                partitionKeyDefinition: pkDefinition,
                requireFormattableOrderByQuery: true,
                isContinuationExpected: true,
                allowNonValueAggregateQuery: true,
                hasLogicalPartitionKey: false,
                allowDCount: true,
                useSystemPrefix: false,
                geospatialType: Cosmos.GeospatialType.Geography);
            
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo = tryGetQueryPlan.Succeeded ? tryGetQueryPlan.Result : throw tryGetQueryPlan.Exception;
            return Tuple.Create(partitionedQueryExecutionInfo, queryPartitionProvider);
        }

        private static async Task<IQueryPipelineStage> GetOdePipelineAsync(OptimisticDirectExecutionTestInput input, DocumentContainer documentContainer, QueryRequestOptions queryRequestOptions, bool clientDisableOde = false)
        {
            (CosmosQueryExecutionContextFactory.InputParameters inputParameters, CosmosQueryContextCore cosmosQueryContextCore) = CreateInputParamsAndQueryContext(input, queryRequestOptions, clientDisableOde);
            IQueryPipelineStage queryPipelineStage = CosmosQueryExecutionContextFactory.Create(
                      documentContainer,
                      cosmosQueryContextCore,
                      inputParameters,
                      NoOpTrace.Singleton);

            Assert.IsNotNull(queryPipelineStage);
            return queryPipelineStage;
        }

        private static async Task<DocumentContainer> CreateDocumentContainerAsync(
            int numItems,
            bool multiPartition,
            bool requiresDist = false,
            FlakyDocumentContainer.FailureConfigs failureConfigs = null)
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
            {
                Paths = new System.Collections.ObjectModel.Collection<string>()
                {
                    "/pk"
                },
                Kind = PartitionKind.Hash,
                Version = PartitionKeyDefinitionVersion.V2,
            };

            MockDocumentContainer mockContainer = new MockDocumentContainer(partitionKeyDefinition, requiresDist);
            IMonadicDocumentContainer monadicDocumentContainer = mockContainer;

            if (failureConfigs != null)
            {
                monadicDocumentContainer = new FlakyDocumentContainer(monadicDocumentContainer, failureConfigs);
            }

            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            // a value of 2 would lead to 4 partitions (2 * 2). 4 partitions are used because they're easy to manage + demonstrates multi partition use case
            int exponentPartitionKeyRanges = 2;

            IReadOnlyList<FeedRangeInternal> ranges;

            for (int i = 0; i < exponentPartitionKeyRanges; i++)
            {
                ranges = await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);

                if (multiPartition)
                {
                    foreach (FeedRangeInternal range in ranges)
                    {
                        await documentContainer.SplitAsync(range, cancellationToken: default);
                    }
                }

                await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
            }

            ranges = await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);

            int rangeCount = multiPartition ? 4 : 1;

            Assert.AreEqual(rangeCount, ranges.Count);

            for (int i = 0; i < numItems; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : \"a\" }}");
                TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(item, cancellationToken: default);
                Assert.IsTrue(monadicCreateRecord.Succeeded);
            }

            return documentContainer;
        }

        private static QueryPartitionProvider CreateCustomQueryPartitionProvider(string key, string value)
        {
            Dictionary<string, object> queryEngineConfiguration = new Dictionary<string, object>()
            {
                {"maxSqlQueryInputLength", 262144},
                {"maxJoinsPerSqlQuery", 5},
                {"maxLogicalAndPerSqlQuery", 2000},
                {"maxLogicalOrPerSqlQuery", 2000},
                {"maxUdfRefPerSqlQuery", 10},
                {"maxInExpressionItemsCount", 16000},
                {"queryMaxGroupByTableCellCount", 500000 },
                {"queryMaxInMemorySortDocumentCount", 500},
                {"maxQueryRequestTimeoutFraction", 0.90},
                {"sqlAllowNonFiniteNumbers", false},
                {"sqlAllowAggregateFunctions", true},
                {"sqlAllowSubQuery", true},
                {"sqlAllowScalarSubQuery", true},
                {"allowNewKeywords", true},
                {"sqlAllowLike", true},
                {"sqlAllowGroupByClause", true},
                {"maxSpatialQueryCells", 12},
                {"spatialMaxGeometryPointCount", 256},
                {"sqlDisableQueryILOptimization", false},
                {"sqlDisableFilterPlanOptimization", false},
                {"clientDisableOptimisticDirectExecution", false}
            };

            queryEngineConfiguration[key] = bool.TryParse(value, out bool boolValue) ? boolValue : value;

            return new QueryPartitionProvider(queryEngineConfiguration);
        }

        private static OptimisticDirectExecutionTestInput CreateInput(
            string description,
            string query,
            bool expectedOptimisticDirectExecution,
            string partitionKeyPath,
            string partitionKeyValue,
            CosmosElement continuationToken = null)
        {
            PartitionKeyBuilder pkBuilder = new PartitionKeyBuilder();
            pkBuilder.Add(partitionKeyValue);

            return CreateInput(description, query, expectedOptimisticDirectExecution, partitionKeyPath, pkBuilder.Build(), continuationToken);
        }

        private static OptimisticDirectExecutionTestInput CreateInput(
            string description,
            string query,
            bool expectedOptimisticDirectExecution,
            string partitionKeyPath,
            Cosmos.PartitionKey partitionKeyValue,
            CosmosElement continuationToken = null)
        {
            return new OptimisticDirectExecutionTestInput(description, query, new SqlQuerySpec(query), expectedOptimisticDirectExecution, partitionKeyPath, partitionKeyValue, continuationToken);
        }

        public override OptimisticDirectExecutionTestOutput ExecuteTest(OptimisticDirectExecutionTestInput input)
        {
            // gets DocumentContainer
            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(input.PartitionKeyDefinition);
            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);
            QueryRequestOptions queryRequestOptions = GetQueryRequestOptions(enableOptimisticDirectExecution: true);
            (CosmosQueryExecutionContextFactory.InputParameters inputParameters, CosmosQueryContextCore cosmosQueryContextCore) = CreateInputParamsAndQueryContext(input, queryRequestOptions);
            IQueryPipelineStage queryPipelineStage = CosmosQueryExecutionContextFactory.Create(
                      documentContainer,
                      cosmosQueryContextCore,
                      inputParameters,
                      NoOpTrace.Singleton);

            bool result = queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton).AsTask().GetAwaiter().GetResult();

            if (input.ExpectedOptimisticDirectExecution)
            {
                Assert.AreEqual(TestInjections.PipelineType.OptimisticDirectExecution, queryRequestOptions.TestSettings.Stats.PipelineType.Value);
            }
            else
            {
                Assert.AreNotEqual(TestInjections.PipelineType.OptimisticDirectExecution, queryRequestOptions.TestSettings.Stats.PipelineType.Value);
            }

            Assert.IsNotNull(queryPipelineStage);
            Assert.IsTrue(result);

            return new OptimisticDirectExecutionTestOutput(input.ExpectedOptimisticDirectExecution);
        }

        private static Tuple<CosmosQueryExecutionContextFactory.InputParameters, CosmosQueryContextCore> CreateInputParamsAndQueryContext(OptimisticDirectExecutionTestInput input, QueryRequestOptions queryRequestOptions, bool clientDisableOde = false)
        {
            CosmosSerializerCore serializerCore = new();
            using StreamReader streamReader = new(serializerCore.ToStreamSqlQuerySpec(new SqlQuerySpec(input.Query), Documents.ResourceType.Document));
            string sqlQuerySpecJsonString = streamReader.ReadToEnd();

            (PartitionedQueryExecutionInfo partitionedQueryExecutionInfo, QueryPartitionProvider queryPartitionProvider) = GetPartitionedQueryExecutionInfoAndPartitionProvider(sqlQuerySpecJsonString, input.PartitionKeyDefinition, clientDisableOde);
            CosmosQueryExecutionContextFactory.InputParameters inputParameters = new CosmosQueryExecutionContextFactory.InputParameters(
                sqlQuerySpec: new SqlQuerySpec(input.Query),
                initialUserContinuationToken: input.ContinuationToken,
                initialFeedRange: null,
                maxConcurrency: queryRequestOptions.MaxConcurrency,
                maxItemCount: queryRequestOptions.MaxItemCount,
                maxBufferedItemCount: queryRequestOptions.MaxBufferedItemCount,
                partitionKey: input.PartitionKeyValue,
                properties: new Dictionary<string, object>() { { "x-ms-query-partitionkey-definition", input.PartitionKeyDefinition } },
                partitionedQueryExecutionInfo: null,
                executionEnvironment: null,
                returnResultsInDeterministicOrder: null,
                enableOptimisticDirectExecution: queryRequestOptions.EnableOptimisticDirectExecution,
                testInjections: queryRequestOptions.TestSettings);

            string databaseId = "db1234";
            string resourceLink = $"dbs/{databaseId}/colls";
            CosmosQueryContextCore cosmosQueryContextCore = new CosmosQueryContextCore(
                client: new TestCosmosQueryClient(queryPartitionProvider),
                resourceTypeEnum: Documents.ResourceType.Document,
                operationType: Documents.OperationType.Query,
                resourceType: typeof(QueryResponseCore),
                resourceLink: resourceLink,
                isContinuationExpected: true,
                allowNonValueAggregateQuery: true,
                useSystemPrefix: false,
                correlatedActivityId: Guid.NewGuid());

            return Tuple.Create(inputParameters, cosmosQueryContextCore);
        }

        private static QueryRequestOptions GetQueryRequestOptions(bool enableOptimisticDirectExecution)
        {
            return new QueryRequestOptions
            {
                MaxConcurrency = 0,
                MaxItemCount = 10,
                EnableOptimisticDirectExecution = enableOptimisticDirectExecution,
                TestSettings = new TestInjections(simulate429s: true, simulateEmptyPages: false, new TestInjections.ResponseStats()),
                Properties = new Dictionary<string, object>()
            {
                { HttpConstants.HttpHeaders.EnumerationDirection, ""},
            }
            };
        }

        internal readonly struct RequiresDistributionTestCase
        {
            public string Query { get; }
            public int ExpectedContinuationTokenCount { get; }
            public int ExpectedDocumentCount { get; }

            public RequiresDistributionTestCase(
                string query,
                int expectedContinuationTokenCount,
                int expectedDocumentCount)
            {
                this.Query = query;
                this.ExpectedContinuationTokenCount = expectedContinuationTokenCount;
                this.ExpectedDocumentCount = expectedDocumentCount;
            }
        }

        private sealed class MockDocumentContainer : InMemoryContainer
        {
            private readonly bool requiresDistribution;

            public MockDocumentContainer(
                PartitionKeyDefinition partitionKeyDefinition,
                bool requiresDistribution)
                : base(partitionKeyDefinition)
            {
                this.requiresDistribution = requiresDistribution;
            }

            public override async Task<TryCatch<QueryPage>> MonadicQueryAsync(
                SqlQuerySpec sqlQuerySpec,
                FeedRangeState<QueryState> feedRangeState,
                QueryPaginationOptions queryPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                Task<TryCatch<QueryPage>> queryPage = base.MonadicQueryAsync(
                sqlQuerySpec,
                feedRangeState,
                queryPaginationOptions,
                trace,
                cancellationToken);

                ImmutableDictionary<string, string>.Builder additionalHeaders = ImmutableDictionary.CreateBuilder<string, string>();
                additionalHeaders.Add("x-ms-documentdb-partitionkeyrangeid", "0");
                additionalHeaders.Add("x-ms-test-header", "true");
                additionalHeaders.Add("x-ms-cosmos-query-requiresdistribution", this.requiresDistribution.ToString());

                return await Task.FromResult(
                    TryCatch<QueryPage>.FromResult(
                        new QueryPage(
                            queryPage.Result.Result.Documents,
                            requestCharge: 42,
                            activityId: Guid.NewGuid().ToString(),
                            responseLengthInBytes: 1337,
                            cosmosQueryExecutionInfo: default,
                            distributionPlanSpec: default,
                            disallowContinuationTokenMessage: default,
                            additionalHeaders: additionalHeaders.ToImmutable(),
                            state: queryPage.Result.Result.State)));
            }
        }

        private class MergeTestUtil
        {
            public int MoveNextCounter { get; private set; }

            public bool GoneExceptionCreated { get; private set; }

            public bool TooManyRequestsFailureCreated { get; private set; }

            public bool IsFailedFallbackPipelineTest { get; }

            public MergeTestUtil(bool isFailedFallbackPipelineTest)
            {
                this.IsFailedFallbackPipelineTest = isFailedFallbackPipelineTest;
            }

            public async Task<Exception> ShouldReturnFailure()
            {
                this.MoveNextCounter++;
                if (this.MoveNextCounter == 2 && !this.GoneExceptionCreated)
                {
                    this.GoneExceptionCreated = true;
                    return new CosmosException(
                        message: $"Epk Range: Partition does not exist at the given range.",
                        statusCode: System.Net.HttpStatusCode.Gone,
                        subStatusCode: (int)SubStatusCodes.PartitionKeyRangeGone,
                        activityId: "0f8fad5b-d9cb-469f-a165-70867728950e",
                        requestCharge: default);
                }

                if (this.IsFailedFallbackPipelineTest && this.GoneExceptionCreated && !this.TooManyRequestsFailureCreated)
                {
                    this.TooManyRequestsFailureCreated = true;
                    return new CosmosException(
                            message: "Injected failure",
                            statusCode: HttpStatusCode.TooManyRequests,
                            subStatusCode: 3200,
                            activityId: "111fad5b-d9cb-469f-a165-70867728950e",
                            requestCharge: 0);
                }

                return null;
            }
        }
    }

    public sealed class OptimisticDirectExecutionTestOutput : BaselineTestOutput
    {
        public OptimisticDirectExecutionTestOutput(bool executeAsOptimisticDirectExecution)
        {
            this.ExecuteAsOptimisticDirectExecution = executeAsOptimisticDirectExecution;
        }

        public bool ExecuteAsOptimisticDirectExecution { get; }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement(nameof(this.ExecuteAsOptimisticDirectExecution));
            xmlWriter.WriteValue(this.ExecuteAsOptimisticDirectExecution);
            xmlWriter.WriteEndElement();
        }
    }

    public sealed class OptimisticDirectExecutionTestInput : BaselineTestInput
    {
        internal PartitionKeyDefinition PartitionKeyDefinition { get; set; }
        internal SqlQuerySpec SqlQuerySpec { get; set; }
        internal Cosmos.PartitionKey PartitionKeyValue { get; set; }
        internal bool ExpectedOptimisticDirectExecution { get; set; }
        internal PartitionKeyRangeIdentity PartitionKeyRangeId { get; set; }
        internal string Query { get; set; }
        internal CosmosElement ContinuationToken { get; set; }

        internal OptimisticDirectExecutionTestInput(
            string description,
            string query,
            SqlQuerySpec sqlQuerySpec,
            bool expectedOptimisticDirectExecution,
            string partitionKeyPath,
            Cosmos.PartitionKey partitionKeyValue,
            CosmosElement continuationToken)
            : base(description)
        {
            this.PartitionKeyDefinition = new PartitionKeyDefinition()
            {
                Paths = new Collection<string>()
                {
                    partitionKeyPath
                },
                Kind = PartitionKind.Hash,
                Version = PartitionKeyDefinitionVersion.V2,
            };
            this.SqlQuerySpec = sqlQuerySpec;
            this.ExpectedOptimisticDirectExecution = expectedOptimisticDirectExecution;
            this.Query = query;
            this.PartitionKeyValue = partitionKeyValue;
            this.ContinuationToken = continuationToken;
        }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteElementString("Description", this.Description);
            xmlWriter.WriteElementString("Query", this.SqlQuerySpec.QueryText);
            xmlWriter.WriteStartElement("PartitionKeys");
            if (this.PartitionKeyDefinition != null)
            {
                foreach (string path in this.PartitionKeyDefinition.Paths)
                {
                    xmlWriter.WriteElementString("Key", path);
                }
            }

            xmlWriter.WriteEndElement();
            if (this.PartitionKeyDefinition != null)
            {
                xmlWriter.WriteElementString(
                    "PartitionKeyType",
                    this.PartitionKeyDefinition.Kind == PartitionKind.Hash ? "Hash" : (
                        this.PartitionKeyDefinition.Kind == PartitionKind.MultiHash ? "MultiHash" : "Range"));
            }

            if (this.SqlQuerySpec.ShouldSerializeParameters())
            {
                xmlWriter.WriteStartElement("QueryParameters");
                xmlWriter.WriteCData(JsonConvert.SerializeObject(
                    this.SqlQuerySpec.Parameters,
                    Newtonsoft.Json.Formatting.Indented));
                xmlWriter.WriteEndElement();
            }
        }
    }

    internal class TestCosmosQueryClient : CosmosQueryClient
    {
        private readonly QueryPartitionProvider queryPartitionProvider;

        public TestCosmosQueryClient(QueryPartitionProvider queryPartitionProvider)
        { 
            this.queryPartitionProvider = queryPartitionProvider;
        }

        public override Action<IQueryable> OnExecuteScalarQueryCallback => throw new NotImplementedException();

        public override bool BypassQueryParsing()
        {
            return false;
        }

        public override void ClearSessionTokenCache(string collectionFullName)
        {
            throw new NotImplementedException();
        }

        public override Task<TryCatch<QueryPage>> ExecuteItemQueryAsync(string resourceUri, ResourceType resourceType, OperationType operationType, Cosmos.FeedRange feedRange, QueryRequestOptions requestOptions, AdditionalRequestHeaders additionalRequestHeaders, SqlQuerySpec sqlQuerySpec, string continuationToken, int pageSize, ITrace trace, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<PartitionedQueryExecutionInfo> ExecuteQueryPlanRequestAsync(string resourceUri, ResourceType resourceType, OperationType operationType, SqlQuerySpec sqlQuerySpec, Cosmos.PartitionKey? partitionKey, string supportedQueryFeatures, Guid clientQueryCorrelationId, ITrace trace, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PartitionedQueryExecutionInfo());
        }

        public override Task ForceRefreshCollectionCacheAsync(string collectionLink, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<ContainerQueryProperties> GetCachedContainerQueryPropertiesAsync(string containerLink, Cosmos.PartitionKey? partitionKey, ITrace trace, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ContainerQueryProperties(
                 "test",
                 new List<Range<string>>
                 { 
                     new Range<string>(
                         PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                         PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                         true,
                         true)
                 },
                 new PartitionKeyDefinition(),
                 Cosmos.GeospatialType.Geometry));
        }

        public override async Task<bool> GetClientDisableOptimisticDirectExecutionAsync()
        {
            return this.queryPartitionProvider.ClientDisableOptimisticDirectExecution;
        }

        public override Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangeByFeedRangeAsync(string resourceLink, string collectionResourceId, PartitionKeyDefinition partitionKeyDefinition, FeedRangeInternal feedRangeInternal, bool forceRefresh, ITrace trace)
        {
            throw new NotImplementedException();
        }

        public override Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesAsync(string resourceLink, string collectionResourceId, IReadOnlyList<Range<string>> providedRanges, bool forceRefresh, ITrace trace)
        {
            return Task.FromResult(new List<PartitionKeyRange>{new PartitionKeyRange()
            {
                MinInclusive = PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                MaxExclusive = PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey
            }
            });
        }

        public override Task<IReadOnlyList<PartitionKeyRange>> TryGetOverlappingRangesAsync(string collectionResourceId, Range<string> range, bool forceRefresh = false)
        {
            throw new NotImplementedException();
        }

        public override async Task<TryCatch<PartitionedQueryExecutionInfo>> TryGetPartitionedQueryExecutionInfoAsync(SqlQuerySpec sqlQuerySpec, ResourceType resourceType, PartitionKeyDefinition partitionKeyDefinition, bool requireFormattableOrderByQuery, bool isContinuationExpected, bool allowNonValueAggregateQuery, bool hasLogicalPartitionKey, bool allowDCount, bool useSystemPrefix, Cosmos.GeospatialType geospatialType, CancellationToken cancellationToken)
        {
            CosmosSerializerCore serializerCore = new();
            using StreamReader streamReader = new(serializerCore.ToStreamSqlQuerySpec(sqlQuerySpec, Documents.ResourceType.Document));
            string sqlQuerySpecJsonString = streamReader.ReadToEnd();
            
            (PartitionedQueryExecutionInfo partitionedQueryExecutionInfo, QueryPartitionProvider queryPartitionProvider) = OptimisticDirectExecutionQueryBaselineTests.GetPartitionedQueryExecutionInfoAndPartitionProvider(sqlQuerySpecJsonString, partitionKeyDefinition);
            return TryCatch<PartitionedQueryExecutionInfo>.FromResult(partitionedQueryExecutionInfo);
        }
    }
}
