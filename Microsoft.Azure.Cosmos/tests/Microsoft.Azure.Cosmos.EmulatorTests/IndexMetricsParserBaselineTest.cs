//-----------------------------------------------------------------------
// <copyright file="LinqAttributeContractTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests
{
    using BaselineTest;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Threading.Tasks;
    using System.Xml;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Documents;
    using Antlr4.Runtime.Sharpen;
    using System.Collections.Generic;
    using OpenTelemetry.Trace;

    // End-to-end testing for IndexMetrics handling and parsing.
    [Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestClass]
    public class IndexMetricsParserBaselineTest : BaselineTests<IndexMetricsParserTestInput, IndexMetricsParserTestOutput>
    {
        private static CosmosClient cosmosClient;
        private static Cosmos.Database testDb;
        private static Container testContainer;

        [ClassInitialize]
        public async static Task Initialize(TestContext textContext)
        {
            cosmosClient = TestCommon.CreateCosmosClient(true);

            string dbName = $"{nameof(IndexMetricsParserBaselineTest)}-{Guid.NewGuid().ToString("N")}";
            testDb = await cosmosClient.CreateDatabaseAsync(dbName);
        }

        [ClassCleanup]
        public async static Task CleanUp()
        {
            if (testDb != null)
            {
                await testDb.DeleteStreamAsync();
            }

            cosmosClient.Dispose();
        }

        [TestInitialize]
        public async Task TestInitialize()
        {
            testContainer = await testDb.CreateContainerAsync(new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/Pk"));
        }

        [TestCleanup]
        public async Task TestCleanUp()
        {
            await testContainer.DeleteContainerStreamAsync();
        }

        [TestMethod]
        public void IndexUtilizationParse()
        {
            List<IndexMetricsParserTestInput> inputs = new List<IndexMetricsParserTestInput>
            {
                new IndexMetricsParserTestInput
                (
                    description: "Input full string",
                    query: "SELECT 'Input full string' AS test, c.id, c.statement\r\nFROM c\r\nWHERE STARTSWITH(c.statement, 'The quick brown fox jumps over the lazy dog', false)"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Unicode 1",
                    query: "SELECT * \r\nFROM c\r\nWHERE STARTSWITH(c['Î— Î³Ï\u0081Î®Î³Î¿Ï\u0081Î· ÎºÎ±Ï†Î­ Î±Î»ÎµÏ€Î¿Ï\u008d Ï€Î·Î´Î¬ÎµÎ¹ Ï€Î¬Î½Ï‰ Î±Ï€ÏŒ Ï„Î¿ Ï„ÎµÎ¼Ï€Î­Î»Î¹ÎºÎ¿ ÏƑÎºÏ…Î»Î¯'], 's', false)"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Unicode 2",
                    query: "SELECT VALUE STARTSWITH(r['\u0131'], 's', false) FROM root r"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Unicode 3",
                    query: "SELECT VALUE STARTSWITH(r['\u005A\u005A\u005A\u007A\u007A\u007A'], 's', true) FROM root r"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Unicode 4",
                    query: "SELECT VALUE STARTSWITH(r['\u0020\u0021\u0022!@#$%^&*()<>?:\"{}|\u00dfÃ\u0081ŒÆ12ếàưỏốởặ'], 's', true) FROM root r"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Unicode German",
                    query: "SELECT VALUE STARTSWITH(r['Der schnelle Braunfuchs springt über den faulen Hund'], 's', true) FROM root r"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Unicode Greek",
                    query: "SELECT VALUE STARTSWITH(r['Η γρήγορη καφέ αλεπού πηδάει πάνω από το τεμπέλικο σκυλί'], 's', true) FROM root r"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Unicode Arabic",
                    query: "SELECT VALUE STARTSWITH(r['الثعلب البني السريع يقفز فوق الكلب الكسول'], 's', true) FROM root r"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Unicode Russian",
                    query: "SELECT VALUE STARTSWITH(r['Быстрая коричневая лиса прыгает через ленивую собаку'], 's', true) FROM root r"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Unicode Japanese",
                    query: "SELECT VALUE STARTSWITH(r['素早く茶色のキツネが怠惰な犬を飛び越えます'], 's', true) FROM root r"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Unicode Hindi",
                    query: "SELECT VALUE STARTSWITH(r['तेज, भूरी लोमडी आलसी कुत्ते के उपर कूद गई'], 's', true) FROM root r"
                ),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void IndexUtilizationHeaderLengthTest()
        {
            const int repeatCount = 10000;
            string property1 = "r." + new string('a', repeatCount);
            string property2 = "r." + new string('b', repeatCount);
            string property3 = "r." + new string('c', repeatCount);

            const string equalityFilter = " = 0";
            const string inequalityFilter = " > 0";
            List<IndexMetricsParserTestInput> inputs = new List<IndexMetricsParserTestInput>
            {
                new IndexMetricsParserTestInput
                (
                    description: "Single property",
                    query: "SELECT * FROM root r WHERE " + property1 + equalityFilter
                ),
                new IndexMetricsParserTestInput
                (
                    description: "Only single index recommendation",
                    query: "SELECT * FROM root r WHERE " + property1 + inequalityFilter + " AND " + property2 + inequalityFilter
                ),
                new IndexMetricsParserTestInput
                (
                    description: "Only one composite index recommendation",
                    query: "SELECT * FROM root r WHERE " + property1 + equalityFilter + " AND " + property2 + inequalityFilter
                ),
                // Notice in this baseline the composite index recommendation is cut off
                new IndexMetricsParserTestInput
                (
                    description: "Multiple composite index recommendation",
                    query: "SELECT * FROM root r WHERE " + property1 + equalityFilter + " AND " + property2 + inequalityFilter + " AND " + property3 + inequalityFilter
                ),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void IndexUtilizationClientSideExistenceTest()
        {
            List<IndexMetricsParserTestInput> inputs = new List<IndexMetricsParserTestInput>
            {
                new IndexMetricsParserTestInput
                (
                    description: "Simple 1",
                    query: "SELECT * FROM c WHERE c.id = \"1\""
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Simple 2",
                    query: "SELECT * FROM c WHERE c.id = \"1\" and c.name = \"Abc\""
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Simple 3",
                    query: "SELECT * FROM c WHERE c.id = \"1\" and c.name > \"Abc\""
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Simple 4",
                    query: "SELECT * FROM c WHERE c.id > \"1\" and c.name > \"Abc\""
                ),

                // Aggregate
                new IndexMetricsParserTestInput
                (
                    description: "Aggregate 1",
                    query: "SELECT COUNT(1) FROM c WHERE c.id = \"1\""
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Aggregate 2",
                    query: "SELECT COUNT(1) FROM c WHERE c.id = \"1\" and c.name = \"Abc\""
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Aggregate 3",
                    query: "SELECT COUNT(1) FROM c WHERE c.id = \"1\" and c.name > \"Abc\""
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Aggregate 4",
                    query: "SELECT COUNT(1) FROM c WHERE c.id > \"1\" and c.name > \"Abc\""
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Aggregate Value 1",
                    query: "SELECT VALUE COUNT(1) FROM c WHERE c.id = \"1\""
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Aggregate Value 2",
                    query: "SELECT VALUE COUNT(1) FROM c WHERE c.id = \"1\" and c.name = \"Abc\""
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Aggregate Value 3",
                    query: "SELECT VALUE COUNT(1) FROM c WHERE c.id = \"1\" and c.name > \"Abc\""
                ),

                new IndexMetricsParserTestInput
                (
                    description: "Aggregate Value 4",
                    query: "SELECT VALUE COUNT(1) FROM c WHERE c.id > \"1\" and c.name > \"Abc\""
                ),

                // Order By
                new IndexMetricsParserTestInput
                (
                    description: "OrderBy 1",
                    query: "SELECT * FROM c WHERE c.id = \"1\" ORDER BY c.id ASC"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "OrderBy 2",
                    query: "SELECT * FROM c WHERE c.id = \"1\" and c.name = \"Abc\" ORDER BY c.id ASC"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "OrderBy 3",
                    query: "SELECT * FROM c WHERE c.id = \"1\" and c.name > \"Abc\" ORDER BY c.id ASC"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "OrderBy 4",
                    query: "SELECT * FROM c WHERE c.id > \"1\" and c.name > \"Abc\" ORDER BY c.id ASC"
                ),

                // Group By
                new IndexMetricsParserTestInput
                (
                    description: "GroupBy 1",
                    query: "SELECT COUNT(1) FROM c WHERE c.id = \"1\" GROUP BY c.id"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "GroupBy 2",
                    query: "SELECT COUNT(1) FROM c WHERE c.id = \"1\" and c.name = \"Abc\" GROUP BY c.id"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "GroupBy 3",
                    query: "SELECT COUNT(1) FROM c WHERE c.id = \"1\" and c.name > \"Abc\" GROUP BY c.id"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "GroupBy 4",
                    query: "SELECT COUNT(1) FROM c WHERE c.id > \"1\" and c.name > \"Abc\" GROUP BY c.id"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "GroupBy 5",
                    query: "SELECT COUNT(1) FROM c WHERE c.id = \"1\" GROUP BY c.id, c.name"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "GroupBy 6",
                    query: "SELECT COUNT(1) FROM c WHERE c.id = \"1\" and c.name = \"Abc\" GROUP BY c.id, c.name"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "GroupBy 7",
                    query: "SELECT COUNT(1) FROM c WHERE c.id = \"1\" and c.name > \"Abc\" GROUP BY c.id, c.name"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "GroupBy 8",
                    query: "SELECT COUNT(1) FROM c WHERE c.id > \"1\" and c.name > \"Abc\" GROUP BY c.id, c.name"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "GroupBy Value 1",
                    query: "SELECT VALUE COUNT(1) FROM c WHERE c.id = \"1\""
                ),

                new IndexMetricsParserTestInput
                (
                    description: "GroupBy Value 2",
                    query: "SELECT VALUE COUNT(1) FROM c WHERE c.id = \"1\" and c.name = \"Abc\""
                ),

                new IndexMetricsParserTestInput
                (
                    description: "GroupBy Value 3",
                    query: "SELECT VALUE COUNT(1) FROM c WHERE c.id = \"1\" and c.name > \"Abc\""
                ),

                new IndexMetricsParserTestInput
                (
                    description: "GroupBy Value 4",
                    query: "SELECT VALUE COUNT(1) FROM c WHERE c.id > \"1\" and c.name > \"Abc\""
                ),

                new IndexMetricsParserTestInput
                (
                    description: "GroupBy Value 5",
                    query: "SELECT VALUE COUNT(1) FROM c WHERE c.id = \"1\" GROUP BY c.id, c.name"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "GroupBy Value 6",
                    query: "SELECT VALUE COUNT(1) FROM c WHERE c.id = \"1\" and c.name = \"Abc\" GROUP BY c.id, c.name"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "GroupBy Value 7",
                    query: "SELECT VALUE COUNT(1) FROM c WHERE c.id = \"1\" and c.name > \"Abc\" GROUP BY c.id, c.name"
                ),

                new IndexMetricsParserTestInput
                (
                    description: "GroupBy Value 8",
                    query: "SELECT VALUE COUNT(1) FROM c WHERE c.id > \"1\" and c.name > \"Abc\" GROUP BY c.id, c.name"
                )
            };

            this.ExecuteTestSuite(inputs);
        }

        public override IndexMetricsParserTestOutput ExecuteTest(IndexMetricsParserTestInput input)
        {
            // Execute without ODE
            string indexMetricsNonODE = RunTest(input.Query, enableOptimisticDirectExecution: false);

            // Execute with ODE
            string indexMetricsODE = RunTest(input.Query, enableOptimisticDirectExecution: true);

            // Make sure ODE and non-ODE is consistent
            Assert.AreEqual(indexMetricsNonODE, indexMetricsODE);

            // ----------------------------
            // Test stream API
            // ----------------------------
            // Execute without ODE
            string indexMetricsNonODEStreaming = RunStreamAPITest(input.Query, enableOptimisticDirectExecution: false);

            // Execute with ODE
            string indexMetricsODEStreaming = RunStreamAPITest(input.Query, enableOptimisticDirectExecution: true);

            // Make sure ODE and non-ODE is consistent
            Assert.AreEqual(indexMetricsNonODEStreaming, indexMetricsODEStreaming);

            return new IndexMetricsParserTestOutput(indexMetricsNonODE);
        }

        private static string RunTest(string query, bool enableOptimisticDirectExecution)
        {
            QueryRequestOptions requestOptions = new QueryRequestOptions() { PopulateIndexMetrics = true, EnableOptimisticDirectExecution = enableOptimisticDirectExecution };

            using FeedIterator<CosmosElement> itemQuery = testContainer.GetItemQueryIterator<CosmosElement>(
                query,
                requestOptions: requestOptions);

            // Index Metrics is returned fully on the first page so no need to worry about result set
            int roundTripCount = 0;
            string indexMetrics = null;
            while (itemQuery.HasMoreResults)
            {
                FeedResponse<CosmosElement> page = itemQuery.ReadNextAsync().Result;
                Assert.IsTrue(page.Headers.AllKeys().Length > 1);
                if (roundTripCount > 0)
                {
                    if (page.IndexMetrics != null)
                    {
                        Assert.Fail("Expected only Index Metrics on first round trip. Current round trip %n", roundTripCount);
                    }
                }
                else
                {
                    Assert.IsNotNull(page.Headers.Get(HttpConstants.HttpHeaders.IndexUtilization), "Expected index utilization headers for query");
                    Assert.IsNotNull(page.IndexMetrics, "Expected index metrics response for query");

                    indexMetrics = page.IndexMetrics;
                }

                roundTripCount++;
            }

            return indexMetrics;
        }

        private static string RunStreamAPITest(string query, bool enableOptimisticDirectExecution)
        {
            QueryRequestOptions requestOptions = new QueryRequestOptions() { PopulateIndexMetrics = true, EnableOptimisticDirectExecution = enableOptimisticDirectExecution };

            using FeedIterator itemQuery = testContainer.GetItemQueryStreamIterator(
                queryText: query,
                requestOptions: requestOptions);

            // Index Metrics is returned fully on the first page so no need to worry about result set
            int roundTripCount = 0;
            string indexMetrics = null;
            while (itemQuery.HasMoreResults)
            {
                ResponseMessage page = itemQuery.ReadNextAsync().Result;
                Assert.IsTrue(page.Headers.AllKeys().Length > 1);
                if (roundTripCount > 0)
                {
                    if (page.IndexMetrics != null)
                    {
                        Assert.Fail("Expected only Index Metrics on first round trip. Current round trip %n", roundTripCount);
                    }
                }
                else
                {
                    Assert.IsNotNull(page.Headers.Get(HttpConstants.HttpHeaders.IndexUtilization), "Expected index utilization headers for query");
                    Assert.IsNotNull(page.IndexMetrics, "Expected index metrics response for query");

                    indexMetrics = page.IndexMetrics;
                }

                roundTripCount++;
            }

            return indexMetrics;
        }
    }


    public sealed class IndexMetricsParserTestInput : BaselineTestInput
    {
        public IndexMetricsParserTestInput(string description, string query)
            : base(description)
        {
            this.Query = query;
        }

        public string Query { get; }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement($"{nameof(this.Description)}");
            xmlWriter.WriteCData(this.Description);
            xmlWriter.WriteEndElement();

            xmlWriter.WriteStartElement($"{nameof(this.Query)}");
            xmlWriter.WriteCData(this.Query);
            xmlWriter.WriteEndElement();
        }
    }
    public sealed class IndexMetricsParserTestOutput : BaselineTestOutput
    {
        public string IndexMetric { get; }

        public string ErrorMessage { get; }

        public IndexMetricsParserTestOutput(string indexMetrics, string errorMessage = null)
        {
            this.IndexMetric = indexMetrics;
            this.ErrorMessage = errorMessage;
        }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement(nameof(this.IndexMetric));
            xmlWriter.WriteCData(this.IndexMetric);
            xmlWriter.WriteEndElement();
            if (this.ErrorMessage != null)
            {
                xmlWriter.WriteStartElement(nameof(this.ErrorMessage));
                xmlWriter.WriteCData(this.ErrorMessage);
                xmlWriter.WriteEndElement();
            }
        }
    }
}
