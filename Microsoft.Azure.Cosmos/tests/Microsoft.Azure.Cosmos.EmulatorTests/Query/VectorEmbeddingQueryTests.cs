//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
    [TestCategory("Query")]
    public sealed class VectorEmbeddingQueryTests
    {
        [DataTestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task CreateContainerAndQueryAllVectorDataTypes(ConnectionMode connectionMode)
        {
            (string Property, VectorDataType DataType, JArray QueryVector)[] vectors =
            {
                ("float16Vector", VectorDataType.Float16, new JArray(-1.5, 2.5, -3.5)),
                ("float32Vector", VectorDataType.Float32, new JArray(-1.5, 2.5, -3.5)),
                ("int8Vector", VectorDataType.Int8, new JArray(-128, 0, 127)),
                ("uint8Vector", VectorDataType.Uint8, new JArray(0, 128, 255)),
            };

            Collection<Embedding> embeddings = new Collection<Embedding>(
                vectors.Select(vector => new Embedding
                {
                    Path = "/" + vector.Property,
                    DataType = vector.DataType,
                    Dimensions = 3,
                    DistanceFunction = DistanceFunction.Euclidean,
                }).ToList());

            // No vector index is needed for exact brute-force queries. This avoids ANN training
            // thresholds and tests embedding types independently of vector index support.
            ContainerProperties properties = new ContainerProperties(Guid.NewGuid().ToString(), "/category")
            {
                VectorEmbeddingPolicy = new VectorEmbeddingPolicy(embeddings),
            };

            using CosmosClient client = TestCommon.CreateCosmosClient(useGateway: connectionMode == ConnectionMode.Gateway);
            Database database = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            try
            {
                ContainerResponse created = await database.CreateContainerAsync(properties);
                Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
                AssertVectorEmbeddingPolicy(properties, created.Resource);

                Container container = created.Container;
                ContainerResponse read = await container.ReadContainerAsync();
                Assert.AreEqual(HttpStatusCode.OK, read.StatusCode);
                AssertVectorEmbeddingPolicy(properties, read.Resource);

                // Insert out of distance order. Fractional/signed floats and integer boundaries
                // ensure these are valid vectors for each declared type, not just positive floats.
                (string Id, string Category, int Offset)[] documents =
                {
                    ("far", "included", 5),
                    ("other", "excluded", 1),
                    ("exact", "included", 0),
                    ("near", "included", 2),
                };
                foreach ((string id, string category, int offset) in documents)
                {
                    JObject document = new JObject
                    {
                        ["id"] = id,
                        ["category"] = category,
                        ["float16Vector"] = new JArray(-1.5 + offset, 2.5, -3.5),
                        ["float32Vector"] = new JArray(-1.5 + offset, 2.5, -3.5),
                        ["int8Vector"] = new JArray(-128 + offset, 0, 127),
                        ["uint8Vector"] = new JArray(offset, 128, 255),
                    };
                    ItemResponse<JObject> inserted = await container.CreateItemAsync(document, new PartitionKey(category));
                    Assert.AreEqual(HttpStatusCode.Created, inserted.StatusCode);
                }

                foreach ((string property, VectorDataType dataType, JArray queryVector) in vectors)
                {
                    string distance = $"VectorDistance(c.{property}, @vector, true)";
                    QueryDefinition nearest = new QueryDefinition(
                        $"SELECT TOP 3 c.id, {distance} AS distance FROM c ORDER BY {distance}")
                        .WithParameter("@vector", queryVector);
                    await AssertQueryResultsAsync(
                        container,
                        nearest,
                        new[] { "exact", "other", "near" },
                        dataType);

                    QueryDefinition filtered = new QueryDefinition(
                        $"SELECT TOP 3 c.id, {distance} AS distance FROM c WHERE c.category = @category ORDER BY {distance}")
                        .WithParameter("@vector", queryVector)
                        .WithParameter("@category", "included");
                    await AssertQueryResultsAsync(
                        container,
                        filtered,
                        new[] { "exact", "near", "far" },
                        dataType);
                }
            }
            finally
            {
                DatabaseResponse deleted = await database.DeleteAsync();
                Assert.AreEqual(HttpStatusCode.NoContent, deleted.StatusCode);
            }
        }

        private static void AssertVectorEmbeddingPolicy(ContainerProperties expected, ContainerProperties actual)
        {
            Assert.AreEqual(expected.Id, actual.Id);
            Assert.AreEqual(expected.PartitionKeyPath, actual.PartitionKeyPath);
            Assert.IsNotNull(actual.VectorEmbeddingPolicy);
            Assert.AreEqual(expected.VectorEmbeddingPolicy.Embeddings.Count, actual.VectorEmbeddingPolicy.Embeddings.Count);
            foreach (Embedding expectedEmbedding in expected.VectorEmbeddingPolicy.Embeddings)
            {
                Embedding actualEmbedding = actual.VectorEmbeddingPolicy.Embeddings.Single(
                    embedding => embedding.Path == expectedEmbedding.Path);
                Assert.AreEqual(expectedEmbedding.DataType, actualEmbedding.DataType, expectedEmbedding.Path);
                Assert.AreEqual(expectedEmbedding.Dimensions, actualEmbedding.Dimensions, expectedEmbedding.Path);
                Assert.AreEqual(expectedEmbedding.DistanceFunction, actualEmbedding.DistanceFunction, expectedEmbedding.Path);
            }
        }

        private static async Task AssertQueryResultsAsync(
            Container container,
            QueryDefinition query,
            string[] expectedIds,
            VectorDataType dataType)
        {
            List<JObject> results = new List<JObject>();
            using FeedIterator<JObject> iterator = container.GetItemQueryIterator<JObject>(
                query,
                requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
            while (iterator.HasMoreResults)
            {
                FeedResponse<JObject> page = await iterator.ReadNextAsync();
                results.AddRange(page);
            }

            CollectionAssert.AreEqual(
                expectedIds,
                results.Select(result => result.Value<string>("id")).ToArray(),
                $"{dataType}: {query.QueryText}");

            double previousDistance = -1;
            foreach (JObject result in results)
            {
                Assert.IsNotNull(result["distance"], $"{dataType}: missing distance for {result["id"]}.");
                double distance = result.Value<double>("distance");
                Assert.IsFalse(double.IsNaN(distance) || double.IsInfinity(distance), $"{dataType}: invalid distance.");
                Assert.IsTrue(distance >= 0 && distance > previousDistance, $"{dataType}: distances must increase.");
                previousDistance = distance;
            }

            Assert.AreEqual(0, results[0].Value<double>("distance"), 0.0001, $"{dataType}: exact match distance.");
        }
    }
}
