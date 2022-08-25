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

    /**
     * End-to-end testing for IndexMetrics handling and parsing.
     **/
    [Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestClass]
    public class IndexMetricsParserBaselineTest : BaselineTests<IndexMetricsParserTestInput, IndexMetricsParserTestOutput>
    {
        private static CosmosClient cosmosClient;
        private static Cosmos.Database testDb;
        private static Container testContainer;

        [ClassInitialize]
        public async static Task Initialize(TestContext textContext)
        {
            string authKey = Utils.ConfigurationManager.AppSettings["MasterKey"];
            Uri uri = new Uri(Utils.ConfigurationManager.AppSettings["GatewayEndpoint"]);
            ConnectionPolicy connectionPolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Gateway,
                EnableEndpointDiscovery = true,
            };

            cosmosClient = TestCommon.CreateCosmosClient();

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
                    description: "Basic Query",
                    query: "SELECT * FROM root WHERE root.name = \"Andy\""
                ),

                new IndexMetricsParserTestInput
                (
                  description: "Unicode character",
                  query: "SELECT * FROM root WHERE root[\"namÉunicode㐀㐁㨀㨁䶴䶵\"] = \"namÉunicode㐀㐁㨀㨁䶴䶵\""
                )
            };

            this.ExecuteTestSuite(inputs);
        }

        public override IndexMetricsParserTestOutput ExecuteTest(IndexMetricsParserTestInput input)
        {
            try
            {
                QueryRequestOptions requestOptions = new QueryRequestOptions() { PopulateIndexMetrics = true };

                FeedIterator<CosmosElement> itemQuery = testContainer.GetItemQueryIterator<CosmosElement>(
                    input.Query,
                    requestOptions: requestOptions);

                // Index Metrics is returned fully on the first page so no need to worry about result set
                FeedResponse<CosmosElement> page = itemQuery.ReadNextAsync().Result;
                Assert.IsTrue(page.Headers.AllKeys().Length > 1);
                Assert.IsNotNull(page.Headers.Get(HttpConstants.HttpHeaders.IndexUtilization), "Expected index utilization headers for query");
                Assert.IsNotNull(page.IndexMetrics, "Expected index metrics response for query");

                return new IndexMetricsParserTestOutput(page.IndexMetrics);
            }
            catch (Exception e)
            {
                return new IndexMetricsParserTestOutput(String.Empty, LinqTestsCommon.BuildExceptionMessageForTest(e));
            }
           
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
