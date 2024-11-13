namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SplitPartitionQueryTests
    {
        [TestMethod]
        public async Task PrefixPartitionKeyQueryOnSplitParitionTest()
        {
            int numItems = 500;
            IDocumentContainer documentContainer = await CreateSplitDocumentContainerAsync(numItems);

            string query = "SELECT * FROM c";
            for (int i = 0; i < 5; i++)
            {
                Cosmos.PartitionKey partitionKey = new PartitionKeyBuilder()
                    .Add(i.ToString())
                .Build();

                QueryPage queryPage = await documentContainer.QueryAsync(
                        sqlQuerySpec: new Cosmos.Query.Core.SqlQuerySpec(query),
                        feedRangeState: new FeedRangeState<QueryState>(new FeedRangePartitionKey(partitionKey), state: null),
                        queryPaginationOptions: new QueryExecutionOptions(pageSizeHint: int.MaxValue),
                        trace: NoOpTrace.Singleton,
                        cancellationToken: default);

                Assert.AreEqual(numItems / 5, queryPage.Documents.Count);
            }
        }

        private static async Task<IDocumentContainer> CreateSplitDocumentContainerAsync(int numItems)
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
            {
                Paths = new System.Collections.ObjectModel.Collection<string>()
                {
                    "/id",
                    "/value1",
                    "/value2"
                },
                Kind = PartitionKind.MultiHash,
                Version = PartitionKeyDefinitionVersion.V2,
            };

            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(partitionKeyDefinition);
            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            for (int i = 0; i < numItems; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"id\" : \"{i % 5}\", \"value1\" : \"{Guid.NewGuid()}\", \"value2\" : \"{i}\" }}");
                while (true)
                {
                    TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(item, cancellationToken: default);
                    if (monadicCreateRecord.Succeeded)
                    {
                        break;
                    }
                }
            }

            await documentContainer.SplitAsync(FeedRangeEpk.FullRange, cancellationToken: default);

            return documentContainer;
        }
    }
}