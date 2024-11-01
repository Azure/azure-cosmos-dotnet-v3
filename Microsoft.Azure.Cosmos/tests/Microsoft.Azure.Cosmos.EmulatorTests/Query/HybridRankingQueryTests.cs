namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class HybridRankingQueryTests
    {
        private CosmosClient CreateCosmosClient(bool local)
        {
        }

        [TestMethod]
        public async Task CreateCollectionIndexOnly()
        {
            string fullTextPath1 = "/fts1";
            Database databaseForVectorEmbedding = await this.CreateCosmosClient(local: false).CreateDatabaseAsync("fullTextSearchDB",
                cancellationToken: default);

            Collection<FullTextPath> fullTextPaths = new Collection<FullTextPath>()
            {
                new FullTextPath()
                {
                    Path = fullTextPath1,
                    Language = "en-US",
                }
            };

            string containerName = "fullTextContainerTestIndexOnly";
            string partitionKeyPath = "/pk";

            ContainerResponse containerResponse =
                await databaseForVectorEmbedding.DefineContainer(containerName, partitionKeyPath)
                    .WithIndexingPolicy()
                        .WithFullTextIndex()
                            .Path(fullTextPath1)
                            .Attach()
                    .Attach()
                    .CreateAsync();

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            ContainerProperties containerSettings = containerResponse.Resource;

            // Validate FullText Paths.
            Assert.IsNotNull(containerSettings.FullTextPolicy);
            Assert.IsNotNull(containerSettings.FullTextPolicy.FullTextPaths);
            Assert.AreEqual(fullTextPaths.Count, containerSettings.FullTextPolicy.FullTextPaths.Count());
            Assert.IsTrue(fullTextPaths.OrderBy(x => x.Path).SequenceEqual(containerSettings.FullTextPolicy.FullTextPaths.OrderBy(x => x.Path)));

            // Validate Full Text Indexes.
            Assert.IsNotNull(containerSettings.IndexingPolicy.FullTextIndexes);
            Assert.AreEqual(fullTextPaths.Count, containerSettings.IndexingPolicy.FullTextIndexes.Count());
            Assert.AreEqual(fullTextPath1, containerSettings.IndexingPolicy.FullTextIndexes[0].Path);
        }

        [TestMethod]
        public async Task CreatePolicyAndIdnexOnExistingContainer()
        {
            string path = "/abstract";
            CosmosClient client = this.CreateCosmosClient(local: false);
            Container container = client.GetContainer("HybridRankTesting", "arxiv-250kdocuments-index");
            ContainerResponse response = await container.ReadContainerAsync();
            ContainerProperties containerProperties = response.Resource;
            containerProperties.FullTextPolicy = new FullTextPolicy
            {
                DefaultLanguage = "en-US",
                FullTextPaths = new Collection<FullTextPath>
                    {
                        new FullTextPath
                        {
                            Path = path,
                            Language = "en-US",
                        }
                    }
            };
            containerProperties.IndexingPolicy.FullTextIndexes.Add(new FullTextIndexPath { Path = path });

            ContainerResponse containerResponse = await container.ReplaceContainerAsync(containerProperties);
            Assert.IsTrue(containerResponse.StatusCode == HttpStatusCode.OK);
        }

        [TestMethod]
        public async Task CreateCollectionBothPolicyAndIndex()
        {
            string fullTextPath1 = "/fts1";
            Database databaseForVectorEmbedding = await this.CreateCosmosClient(local: false).CreateDatabaseAsync("fullTextSearchDB",
                cancellationToken: default);

            Collection<FullTextPath> fullTextPaths = new Collection<FullTextPath>()
            {
                new FullTextPath()
                {
                    Path = fullTextPath1,
                    Language = "en-US",
                }
            };

            string containerName = "fullTextContainerTestPolicyAndIndex";
            string partitionKeyPath = "/pk";

            ContainerResponse containerResponse =
                await databaseForVectorEmbedding.DefineContainer(containerName, partitionKeyPath)
                    .WithFullTextPolicy(
                        defaultLanguage: "en-US",
                        fullTextPaths: fullTextPaths)
                    .Attach()
                    .WithIndexingPolicy()
                        .WithFullTextIndex()
                            .Path(fullTextPath1)
                            .Attach()
                    .Attach()
                    .CreateAsync();

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            ContainerProperties containerSettings = containerResponse.Resource;

            // Validate FullText Paths.
            Assert.IsNotNull(containerSettings.FullTextPolicy);
            Assert.IsNotNull(containerSettings.FullTextPolicy.FullTextPaths);
            Assert.AreEqual(fullTextPaths.Count, containerSettings.FullTextPolicy.FullTextPaths.Count());
            Assert.IsTrue(fullTextPaths.OrderBy(x => x.Path).SequenceEqual(containerSettings.FullTextPolicy.FullTextPaths.OrderBy(x => x.Path)));

            // Validate Full Text Indexes.
            Assert.IsNotNull(containerSettings.IndexingPolicy.FullTextIndexes);
            Assert.AreEqual(fullTextPaths.Count, containerSettings.IndexingPolicy.FullTextIndexes.Count());
            Assert.AreEqual(fullTextPath1, containerSettings.IndexingPolicy.FullTextIndexes[0].Path);
        }

        [TestMethod]
        public async Task CreateCollectionPolicyOnly()
        {
            string fullTextPath1 = "/fts1", fullTextPath2 = "/fts2", fullTextPath3 = "/fts3";
            Database databaseForVectorEmbedding = await this.CreateCosmosClient(local: false).CreateDatabaseAsync("fullTextSearchDBPolicyOnly",
                cancellationToken: default);

            Collection<FullTextPath> fullTextPaths = new Collection<FullTextPath>()
            {
                new FullTextPath()
                {
                    Path = fullTextPath1,
                    Language = "en-US",
                },
                new FullTextPath()
                {
                    Path = fullTextPath2,
                    Language = "en-US",
                },
                new FullTextPath()
                {
                    Path = fullTextPath3,
                    Language = "en-US",
                },
            };

            string containerName = "fullTextContainerTestPolicyOnly";
            string partitionKeyPath = "/pk";

            ContainerResponse containerResponse =
                await databaseForVectorEmbedding.DefineContainer(containerName, partitionKeyPath)
                    .WithFullTextPolicy(
                        defaultLanguage: "en-US",
                        fullTextPaths: fullTextPaths)
                    .Attach()
                    //.WithIndexingPolicy()
                    //    .WithFullTextIndex()
                    //        .Path(fullTextPath1)
                    //        .Attach()
                    //    .WithFullTextIndex()
                    //        .Path(fullTextPath2)
                    //        .Attach()
                    //    .WithFullTextIndex()
                    //        .Path(fullTextPath3)
                    //        .Attach()
                    //.Attach()
                    .CreateAsync();

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            ContainerProperties containerSettings = containerResponse.Resource;

            // Validate FullText Paths.
            //Assert.IsNotNull(containerSettings.FullTextPolicy);
            //Assert.IsNotNull(containerSettings.FullTextPolicy.FullTextPaths);
            //Assert.AreEqual(fullTextPaths.Count, containerSettings.FullTextPolicy.FullTextPaths.Count());
            //Assert.IsTrue(fullTextPaths.OrderBy(x => x.Path).SequenceEqual(containerSettings.FullTextPolicy.FullTextPaths.OrderBy(x => x.Path)));

            // Validate Full Text Indexes.
            //Assert.IsNotNull(containerSettings.IndexingPolicy.FullTextIndexes);
            //Assert.AreEqual(fullTextPaths.Count, containerSettings.IndexingPolicy.FullTextIndexes.Count());
            //Assert.AreEqual(fullTextPath1, containerSettings.IndexingPolicy.FullTextIndexes[0].Path);
            //Assert.AreEqual(fullTextPath2, containerSettings.IndexingPolicy.FullTextIndexes[1].Path);
            //Assert.AreEqual(fullTextPath3, containerSettings.IndexingPolicy.FullTextIndexes[2].Path);
        }

        [TestMethod]
        public async Task HybridRankingQuery()
        {
            CosmosClient client = this.CreateCosmosClient(local: false);
            Container container = client.GetContainer("HybridRankTesting", "arxiv-ada2-15properties-1536dimensions-100documents");
            FeedIterator<dynamic> iterator = container.GetItemQueryIterator<dynamic>(
                queryText: @"SELECT TOP 10 c.id FROM c ORDER BY RANK FullTextScore(c.text, ['quantum'])"
                );
            List<dynamic> results = new();
            while (iterator.HasMoreResults)
            {
                FeedResponse<dynamic> page = await iterator.ReadNextAsync();
                results.AddRange(page);
            }

            foreach (dynamic item in results)
            {
                Console.WriteLine(item);
            }
        }
    }
}
