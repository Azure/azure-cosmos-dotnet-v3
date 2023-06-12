namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Query")]
    public sealed class BypassQueryParsingTests : QueryTestsBase
    {
        [TestMethod]
        public async Task TestBypassQueryParsingWithNonePartitionKey()
        {
            int documentCount = 400;
            QueryRequestOptions feedOptions = new QueryRequestOptions { PartitionKey = PartitionKey.None };
            string query = "SELECT VALUE r.numberField FROM r";
            IReadOnlyList<int> expected = Enumerable.Range(0, documentCount).ToList();

            async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                ContainerInternal containerCore = container as ContainerInlineCore;

                MockCosmosQueryClient cosmosQueryClientCore = new MockCosmosQueryClient(
                    containerCore.ClientContext,
                    containerCore,
                    forceQueryPlanGatewayElseServiceInterop: true);

                ContainerInternal containerWithBypassParsing = new ContainerInlineCore(
                    containerCore.ClientContext,
                    (DatabaseCore)containerCore.Database,
                    containerCore.Id,
                    cosmosQueryClientCore);

                List<CosmosElement> items = await RunQueryAsync(containerWithBypassParsing, query, feedOptions);
                int[] actual = items.Cast<CosmosNumber>().Select(x => (int)Number64.ToLong(x.Value)).ToArray();

                Assert.IsTrue(expected.SequenceEqual(actual));
            }

            IReadOnlyList<string> documents = CreateDocuments(documentCount);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.NonPartitioned | CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                documents,
                ImplementationAsync,
                "/undefinedPartitionKey");
        }

        [TestMethod]
        [TestCategory("Query")]
        public async Task TestBypassQueryParsingWithNonePartitionKeyEmptyPropertyName()
        {
            int documentCount = 400;

            QueryRequestOptions feedOptions = new QueryRequestOptions { PartitionKey = PartitionKey.None };
            string query = @"SELECT VALUE { """" : r.numberField } FROM r";
            IReadOnlyList<string> expected = Enumerable.Range(0, documentCount).Select(i => "{\"\":" + i.ToString() + "}").ToList();

            async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                ContainerInternal containerCore = container as ContainerInlineCore;

                MockCosmosQueryClient cosmosQueryClientCore = new MockCosmosQueryClient(
                    containerCore.ClientContext,
                    containerCore,
                    forceQueryPlanGatewayElseServiceInterop: true);

                ContainerInternal containerWithBypassParsing = new ContainerInlineCore(
                    containerCore.ClientContext,
                    (DatabaseCore)containerCore.Database,
                    containerCore.Id,
                    cosmosQueryClientCore);

                List<CosmosElement> items = await RunQueryAsync(containerWithBypassParsing, query, feedOptions);
                string[] actual = items.Select(x => x.ToString()).ToArray();

                Assert.IsTrue(expected.SequenceEqual(actual));
            }

            IReadOnlyList<string> documents = CreateDocuments(documentCount);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.NonPartitioned | CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                documents,
                ImplementationAsync,
                "/undefinedPartitionKey");
        }

        private static IReadOnlyList<string> CreateDocuments(int documentCount)
        {
            List<string> documents = new List<string>(documentCount);
            for (int i = 0; i < documentCount; ++i)
            {
                string document = $@"{{ ""numberField"": {i}, ""nullField"": null }}";
                documents.Add(document);
            }

            return documents;
        }
    }
}