//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Xml;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Cosmos.Services.Management.Tests.BaselineTest;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestClass]
    public sealed class QueryAdvisorBaselineTest : BaselineTests<QueryAdvisorBaselineTestInput, QueryAdvisorBaselineTestOutput>
    {
        private static CosmosClient cosmosClient;
        private static Cosmos.Database testDb;
        private static Container testContainer;

        [ClassInitialize]
        public async static Task Initialize(TestContext textContext)
        {
            cosmosClient = TestCommon.CreateCosmosClient(true);

            string dbName = $"{nameof(QueryAdvisorBaselineTest)}-{Guid.NewGuid().ToString("N")}";
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
        [Ignore] //ignore until the emulator supports query advisor
        public void QueryAdviceParse()
        {
            List<QueryAdvisorBaselineTestInput> inputs = new List<QueryAdvisorBaselineTestInput>
            {
                new QueryAdvisorBaselineTestInput
                (
                    description: "Single Advice",
                    query: " SELECT VALUE r.id \r\n FROM root r \r\n WHERE CONTAINS(r.name, \"Abc\") "
                ),

                new QueryAdvisorBaselineTestInput
                (
                    description: "Multiple Advice",
                    query: " SELECT GetCurrentTicks() \r\n FROM root r \r\n  WHERE GetCurrentTimestamp() > 10 "
                ),

                new QueryAdvisorBaselineTestInput
                (
                    description: "No Advice due to optimization",
                    query: " SELECT VALUE r.id \r\n FROM root r \r\n WHERE StringEquals(r.name, \"Abc\", false) "
                ),

                new QueryAdvisorBaselineTestInput
                (
                    description: "No Advice due to no rules matched",
                    query: " SELECT VALUE r.id \r\n FROM root r \r\n WHERE r.id = \"123\" "
                ),
            };

            this.ExecuteTestSuite(inputs);
        }

        public override QueryAdvisorBaselineTestOutput ExecuteTest(QueryAdvisorBaselineTestInput input)
        {
            // Execute without ODE
            string queryAdvicesNonODE = RunTest(input.Query, enableOptimisticDirectExecution: false);

            // Execute with ODE
            string queryAdvicesODE = RunTest(input.Query, enableOptimisticDirectExecution: true);

            // Make sure ODE and non-ODE is consistent
            Assert.AreEqual(queryAdvicesNonODE, queryAdvicesODE);

            // ----------------------------
            // Test stream API
            // ----------------------------
            // Execute without ODE
            string queryAdvicesNonODEStreaming = RunStreamAPITest(input.Query, enableOptimisticDirectExecution: false);

            // Execute with ODE
            string queryAdvicesODEStreaming = RunStreamAPITest(input.Query, enableOptimisticDirectExecution: true);

            // Make sure ODE and non-ODE is consistent
            Assert.AreEqual(queryAdvicesNonODEStreaming, queryAdvicesODEStreaming);

            return new QueryAdvisorBaselineTestOutput(queryAdvicesNonODE);
        }

        private static string RunTest(string query, bool enableOptimisticDirectExecution)
        {
            QueryRequestOptions requestOptions = new QueryRequestOptions() { PopulateQueryAdvice = true, EnableOptimisticDirectExecution = enableOptimisticDirectExecution };

            using FeedIterator<CosmosElement> itemQuery = testContainer.GetItemQueryIterator<CosmosElement>(
                query,
                requestOptions: requestOptions);

            string queryAdvice = null;
            while (itemQuery.HasMoreResults)
            {
                // query advice is the same across pages so we only need to log it once
                if (queryAdvice != null)
                {
                    break;
                }

                FeedResponse<CosmosElement> page = itemQuery.ReadNextAsync().Result;
                Assert.IsTrue(page.Headers.AllKeys().Length > 1);
                Assert.IsNotNull(page.Headers.Get(HttpConstants.HttpHeaders.QueryAdvice), "Expected query advice header for query");
                Assert.IsNotNull(page.QueryAdvice, "Expected query advice response for query");

                queryAdvice = page.QueryAdvice;
            }

            return queryAdvice;
        }

        private static string RunStreamAPITest(string query, bool enableOptimisticDirectExecution)
        {
            QueryRequestOptions requestOptions = new QueryRequestOptions() { PopulateQueryAdvice = true, EnableOptimisticDirectExecution = enableOptimisticDirectExecution };

            using FeedIterator itemQuery = testContainer.GetItemQueryStreamIterator(
                queryText: query,
                requestOptions: requestOptions);

            string queryAdvice = null;
            while (itemQuery.HasMoreResults)
            {
                // query advice is the same across pages so we only need to log it once
                if (queryAdvice != null)
                {
                    break;
                }

                ResponseMessage page = itemQuery.ReadNextAsync().Result;
                Assert.IsTrue(page.Headers.AllKeys().Length > 1);
                Assert.IsNotNull(page.Headers.Get(HttpConstants.HttpHeaders.QueryAdvice), "Expected query advice header for query");
                Assert.IsNotNull(page.QueryAdvice, "Expected query advice response for query");

                queryAdvice = page.QueryAdvice;
            }

            return queryAdvice;
        }
    }


    public sealed class QueryAdvisorBaselineTestInput : BaselineTestInput
    {
        public QueryAdvisorBaselineTestInput(string description, string query)
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
    public sealed class QueryAdvisorBaselineTestOutput : BaselineTestOutput
    {
        public string QueryAdvice { get; }

        public string ErrorMessage { get; }

        public QueryAdvisorBaselineTestOutput(string QueryAdvices, string errorMessage = null)
        {
            this.QueryAdvice = QueryAdvices;
            this.ErrorMessage = errorMessage;
        }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement(nameof(this.QueryAdvice));
            xmlWriter.WriteCData(this.QueryAdvice);
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
