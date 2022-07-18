namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests.QueryOracle;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    [TestCategory("Query")]
    public sealed class TryExecuteQueryTests : QueryTestsBase
    {
        [TestMethod]
        public async Task TestTryExecuteSinglePartitionWithContinuationsAsync()
        {
            int numberOfDocuments = 10;
            string partitionKey = "key";
            string numberField = "numberField";
            string nullField = "nullField";

            List<string> documents = new List<string>(numberOfDocuments);
            for (int i = 0; i < numberOfDocuments; ++i)
            {
                Document doc = new Document();
                doc.SetPropertyValue(partitionKey, "/value");
                doc.SetPropertyValue(numberField, i % 8);
                doc.SetPropertyValue(nullField, null);
                documents.Add(doc.ToString());
            }

            SinglePartitionWithContinuationsArgs args = new SinglePartitionWithContinuationsArgs()
            {
                NumberOfDocuments = numberOfDocuments,
                PartitionKey = partitionKey,
                NumberField = numberField,
                NullField = nullField,
            };

            await this.CreateIngestQueryDeleteAsync<SinglePartitionWithContinuationsArgs>(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition,
                documents,
                this.TestTryExecuteSinglePartitionWithContinuationsHelper,
                args,
                "/" + partitionKey);
        }
        private async Task TestTryExecuteSinglePartitionWithContinuationsHelper(
            Container container,
            IReadOnlyList<CosmosObject> documents,
            SinglePartitionWithContinuationsArgs args)
        {
            int documentCount = args.NumberOfDocuments;
            string partitionKey = args.PartitionKey;
            string numberField = args.NumberField;
            string nullField = args.NullField;

            QueryRequestOptions feedOptions = new QueryRequestOptions
            {
                MaxItemCount = -1,
                PartitionKey = new Cosmos.PartitionKey("/value"),
                TestSettings = new TestInjections(
                                        simulate429s: false,
                                        simulateEmptyPages: false,
                                        responseStats: new TestInjections.ResponseStats())
            };

            // All the queries below will be TryExecute queries because they have a single partition key provided

            // check if queries fail if bad continuation token is passed
            #region BadContinuations
            try
            {
                await container.GetItemQueryIterator<Document>(
                    "SELECT * FROM t",
                    continuationToken: Guid.NewGuid().ToString(),
                    requestOptions: feedOptions).ReadNextAsync();

                Assert.Fail("Expect exception");
            }
            catch (CosmosException dce)
            {
                Assert.IsTrue(dce.StatusCode == HttpStatusCode.BadRequest);
            }
            catch (AggregateException aggrEx)
            {
                Assert.Fail(aggrEx.ToString());
            }

            try
            {
                await container.GetItemQueryIterator<Document>(
                    "SELECT TOP 10 * FROM r",
                    continuationToken: "{'top':11}",
                    requestOptions: feedOptions).ReadNextAsync();

                Assert.Fail("Expect exception");
            }
            catch (CosmosException dce)
            {
                Assert.IsTrue(dce.StatusCode == HttpStatusCode.BadRequest);
            }
            #endregion

            // check if queries fail if syntax error query is passed
            #region SyntaxError
            try
            {
                await container.GetItemQueryIterator<Document>(
                    "SELECT TOP 10 * FOM r",
                    continuationToken: null,
                    requestOptions: feedOptions).ReadNextAsync();
                //MaxItemCount = 10, 
                Assert.Fail("Expect exception");
            }
            catch (CosmosException dce)
            {
                Assert.IsTrue(dce.StatusCode == HttpStatusCode.BadRequest);
            }
            #endregion

            // check if pipeline returns empty continuation token
            FeedResponse<Document> responseWithEmptyContinuationExpected = await container.GetItemQueryIterator<Document>(
                $"SELECT TOP 0 * FROM r",
                requestOptions: feedOptions).ReadNextAsync();

            Assert.AreEqual(null, responseWithEmptyContinuationExpected.ContinuationToken);

            // check if pipeline returns results for these single partition queries
            string[] queries = new[]
            {
                $"SELECT * FROM r ORDER BY r.{partitionKey}",
                $"SELECT * FROM r",
                $"SELECT TOP 10 * FROM r ORDER BY r.{numberField}",
                $"SELECT * FROM r WHERE r.{numberField} BETWEEN 0 AND {documentCount} ORDER BY r.{nullField} DESC",
            };

            foreach (string query in queries)
            { 
                List<CosmosElement> items = await RunQueryAsync(
                        container,
                        query,
                        feedOptions);

                Assert.AreEqual(documentCount, items.Count);
                Assert.IsTrue(feedOptions.TestSettings.Stats.PipelineType.HasValue);
                Assert.AreEqual(TestInjections.PipelineType.TryExecute, feedOptions.TestSettings.Stats.PipelineType.Value);
            }
        }
        private struct SinglePartitionWithContinuationsArgs
        {
            public int NumberOfDocuments;
            public string PartitionKey;
            public string NumberField;
            public string NullField;
        }
    }
}
