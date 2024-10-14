namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Azure;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Query")]
    public sealed class HybridSearchQueryTests : QueryTestsBase
    {
        private const string CollectionDataPath = "D:\\cosmosdb\\Product\\Backend\\native\\Test\\resources\\TestInput\\FullText\\text-3properties-1536dimensions-100documents.json";

        private static readonly IndexingPolicy CompositeIndexPolicy = CreateIndexingPolicy();

        [TestMethod]
        public async Task AllTests()
        {
            IReadOnlyList<string> documents = await LoadDocuments();

            await this.CreateIngestQueryDeleteAsync(
                connectionModes: ConnectionModes.Direct, // | ConnectionModes.Gateway,
                collectionTypes: CollectionTypes.MultiPartition, // | CollectionTypes.SinglePartition,
                documents: documents,
                query: RunTests,
                indexingPolicy: CompositeIndexPolicy);
        }

        private static async Task RunTests(Container container, IReadOnlyList<CosmosObject> _)
        {
            string queryText = @"
                SELECT c.title AS Title, c.text AS Text
                FROM c
                WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')
                ORDER BY RANK FullTextScore(c.title, ['John'])";

            List<TextDocument> result = await RunQueryCombinationsAsync<TextDocument>(
                container,
                queryText,
                queryRequestOptions: null,
                queryDrainingMode: QueryDrainingMode.HoldState);
            Assert.IsTrue(result.Count > 0);
        }

        private static async Task<IReadOnlyList<string>> LoadDocuments()
        {
            // read the json file
            string json = await File.ReadAllTextAsync(CollectionDataPath);
            byte[] jsonBuffer = Encoding.UTF8.GetBytes(json);
            ReadOnlyMemory<byte> readOnlyMemory = new ReadOnlyMemory<byte>(jsonBuffer);
            CosmosObject rootObject = CosmosObject.CreateFromBuffer(readOnlyMemory);
            if (!rootObject.TryGetValue(FieldNames.Items, out CosmosArray items))
            {
                throw new InvalidOperationException("Failed to find items in the json file.");
            }

            int index = 0;
            List<string> documents = new List<string>();
            foreach (CosmosElement item in items)
            {
                CosmosObject itemObject = item as CosmosObject;
                Dictionary<string, CosmosElement> itemDictionary = new(itemObject)
                {
                    { "index", CosmosNumber.Parse(index.ToString()) }
                };

                CosmosObject rewrittenItem = CosmosObject.Create(itemDictionary);
                documents.Add(rewrittenItem.ToString());
            }

            return documents;
        }

        private static IndexingPolicy CreateIndexingPolicy()
        {
            IndexingPolicy policy = new IndexingPolicy();

            policy.IncludedPaths.Add(new IncludedPath { Path = IndexingPolicy.DefaultPath });
            policy.CompositeIndexes.Add(new Collection<CompositePath>
            {
                new CompositePath { Path = $"/index" },
                new CompositePath { Path = $"/mixedTypefield" },
            });

            return policy;
        }

        private sealed class TextDocument
        {
            public string Title { get; set; }

            public string Text { get; set; }
        }

        private static class FieldNames
        {
            public const string Items = "items";
        }
    }
}