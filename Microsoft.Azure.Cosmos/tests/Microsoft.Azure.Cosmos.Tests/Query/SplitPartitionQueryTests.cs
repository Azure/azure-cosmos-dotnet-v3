namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using System.IO;
    using Microsoft.Azure.Cosmos.Tracing;
    using Moq;
    using System.Threading;
    using System.Text;

    [TestClass]
    public class SplitPartitionQueryTests
    {
        [TestMethod]
        public async Task PrefixPartitionKeyQueryOnSplitParitionTest()
        {
            int numItems = 500;
            IDocumentContainer documentContainer = await CreateSplitDocumentContainerAsync(numItems);

            string query = "SELECT * FROM c";
            Cosmos.PartitionKey partitionKey = new PartitionKeyBuilder()
                    .Add("0")
                .Build();
            // continue test here
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
                    }
            };

            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(partitionKeyDefinition);
            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            for (int i = 0; i < 3; i++)
            {
                IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(trace: NoOpTrace.Singleton, cancellationToken: default);
                foreach (FeedRangeInternal range in ranges)
                {
                    await documentContainer.SplitAsync(range, cancellationToken: default);
                }

                await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
            }

            for (int i = 0; i < numItems; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"id\" : {i%5}, \"value1\" : {Guid.NewGuid()}, \"value2\" : {i} }}");
                while (true)
                {
                    TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(item, cancellationToken: default);
                    if (monadicCreateRecord.Succeeded)
                    {
                        break;
                    }
                }
            }

            TryCatch trySplit = await documentContainer.MonadicSplitAsync(FeedRangeEpk.FullRange, cancellationToken: default);
            if (trySplit.Failed)
            {
                throw new Exception("Split Failed");
            }
            return documentContainer;
        }
    }
}
