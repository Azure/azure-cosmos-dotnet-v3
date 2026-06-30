//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    // Similar tests to CosmosContainerTests but with Fluent syntax
    [TestClass]
    public class ContainerSettingsTests : BaseCosmosClientHelper
    {
        private static long ToEpoch(DateTime dateTime)
        {
            return (long)(dateTime - new DateTime(1970, 1, 1)).TotalSeconds;
        }

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task ContainerContractTest()
        {
            DatabaseInlineCore databaseInlineCore = (DatabaseInlineCore)this.database;
            await TestCommon.CreateClientEncryptionKey("dekId", databaseInlineCore); 
            
            ClientEncryptionIncludedPath clientEncryptionIncludedPath1 = new ClientEncryptionIncludedPath()
            {
                Path = "/path",
                ClientEncryptionKeyId = "dekId",
                EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                EncryptionType = "Randomized"
            };

            Collection<ClientEncryptionIncludedPath> paths = new Collection<ClientEncryptionIncludedPath>()
            {
                clientEncryptionIncludedPath1
            };

            ContainerProperties containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/users")
            {
                IndexingPolicy = new IndexingPolicy()
                {
                    Automatic = true,
                    IndexingMode = IndexingMode.Consistent,
                    IncludedPaths = new Collection<IncludedPath>()
                    {
                        new IncludedPath()
                        {
                            Path = "/*"
                        }
                    },
                    ExcludedPaths = new Collection<ExcludedPath>()
                    {
                        new ExcludedPath()
                        {
                            Path = "/test/*"
                        }
                    },
                    CompositeIndexes = new Collection<Collection<CompositePath>>()
                    {
                        new Collection<CompositePath>()
                        {
                            new CompositePath()
                            {
                                Path = "/address/city",
                                Order = CompositePathSortOrder.Ascending
                            },
                            new CompositePath()
                            {
                                Path = "/address/zipcode",
                                Order = CompositePathSortOrder.Descending
                            }
                        }
                    },
                    SpatialIndexes = new Collection<SpatialPath>()
                    {
                        new SpatialPath()
                        {
                            Path = "/address/spatial/*",
                            SpatialTypes = new Collection<SpatialType>()
                            {
                                SpatialType.LineString
                            }
                        }
                    }
                },
                // ComputedProperties = new Collection<ComputedProperty>
                // {
                //     { new ComputedProperty{ Name = "lowerName", Query = "SELECT VALUE LOWER(c.Name) FROM c" } },
                //     { new ComputedProperty{ Name = "fullName", Query = "SELECT VALUE CONCAT(c.Name, ' ', c.LastName) FROM c" } }
                // },
                ClientEncryptionPolicy = new ClientEncryptionPolicy(paths)
            };

            CosmosJsonDotNetSerializer serializer = new CosmosJsonDotNetSerializer();
            Stream stream = serializer.ToStream(containerProperties);
            ContainerProperties deserialziedTest = serializer.FromStream<ContainerProperties>(stream);

            ContainerResponse response = await this.database.CreateContainerAsync(containerProperties);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.RequestCharge > 0);
            Assert.IsNotNull(response.Headers);
            Assert.IsNotNull(response.Headers.ActivityId);

            ContainerProperties responseProperties = response.Resource;
            Assert.IsNotNull(responseProperties.Id);
            Assert.IsNotNull(responseProperties.ResourceId);
            Assert.IsNotNull(responseProperties.ETag);
            Assert.IsTrue(responseProperties.LastModified.HasValue);

            Assert.IsTrue(responseProperties.LastModified.Value > new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), responseProperties.LastModified.Value.ToString());

            Assert.AreEqual(1, responseProperties.IndexingPolicy.IncludedPaths.Count);
            IncludedPath includedPath = responseProperties.IndexingPolicy.IncludedPaths.First();
            Assert.AreEqual("/*", includedPath.Path);

            Assert.AreEqual("/test/*", responseProperties.IndexingPolicy.ExcludedPaths.First().Path);

            Assert.AreEqual(1, responseProperties.IndexingPolicy.CompositeIndexes.Count);
            Assert.AreEqual(2, responseProperties.IndexingPolicy.CompositeIndexes.First().Count);
            CompositePath compositePath = responseProperties.IndexingPolicy.CompositeIndexes.First().First();
            Assert.AreEqual("/address/city", compositePath.Path);
            Assert.AreEqual(CompositePathSortOrder.Ascending, compositePath.Order);

            Assert.AreEqual(1, responseProperties.IndexingPolicy.SpatialIndexes.Count);
            SpatialPath spatialPath = responseProperties.IndexingPolicy.SpatialIndexes.First();
            Assert.AreEqual("/address/spatial/*", spatialPath.Path);
            Assert.AreEqual(4, spatialPath.SpatialTypes.Count); // All SpatialTypes are returned

            Assert.AreEqual(1, responseProperties.ClientEncryptionPolicy.IncludedPaths.Count());
            Assert.IsTrue(responseProperties.ClientEncryptionPolicy.PolicyFormatVersion <= 2);
            ClientEncryptionIncludedPath clientEncryptionIncludedPath = responseProperties.ClientEncryptionPolicy.IncludedPaths.First();
            Assert.IsTrue(this.VerifyClientEncryptionIncludedPath(clientEncryptionIncludedPath1, clientEncryptionIncludedPath));

            ComputedPropertyComparer.AssertAreEqual(containerProperties.ComputedProperties, responseProperties.ComputedProperties);
            ComputedPropertyComparer.AssertAreEqual(containerProperties.ComputedProperties, deserialziedTest.ComputedProperties);
        }

        [TestMethod]
        public async Task ContainerNegativeComputedPropertyTest()
        {
            string query = "SELECT VALUE LOWER(c.name) FROM c";
            var variations = new[]
            {
                new
                {
                    ComputedProperties = new Collection<ComputedProperty>
                    {
                        new ComputedProperty {Name = "lowerName", Query = @"SELECT VALUE LOWER(c.name) FROM c"},
                        new ComputedProperty {Name = "lowerName", Query = @"SELECT VALUE LOWER(c.lastName) FROM c"}
                    },
                    Error = @"""Errors"":[""Computed property name 'lowerName' cannot be used in multiple definitions.""]"
                },
                new
                {
                    ComputedProperties = new Collection<ComputedProperty>{ new ComputedProperty { Query = query } },
                    Error = @"""Errors"":[""One of the specified inputs is invalid""]"
                },
                new
                {
                    ComputedProperties = new Collection<ComputedProperty>{ new ComputedProperty { Name = "", Query = query } },
                    Error = @"""Errors"":[""Computed property 'name' is either empty or unspecified.""]"
                },
                new
                {
                    ComputedProperties = new Collection<ComputedProperty>{ new ComputedProperty { Name = "lowerName" } },
                    Error = @"""Errors"":[""One of the specified inputs is invalid""]"
                },
                new
                {
                    ComputedProperties = new Collection<ComputedProperty>{ new ComputedProperty { Name = "lowerName", Query = "" } },
                    Error = @"""Errors"":[""Computed property 'query' is either empty or unspecified.""]"
                },
                new
                {
                    ComputedProperties = new Collection<ComputedProperty>{ new ComputedProperty { Name = "id", Query = query } },
                    Error = @"""Errors"":[""The system property name 'id' cannot be used as a computed property name.""]"
                },
                new
                {
                    ComputedProperties = new Collection<ComputedProperty>{ new ComputedProperty { Name = "spatial", Query = query } },
                    Error = @"""Errors"":[""Computed property 'spatial' at index (0) has a spatial index. Remove the spatial index on this path.""]"
                },
                new
                {
                    ComputedProperties = new Collection<ComputedProperty>{ new ComputedProperty {Name = "lowerName", Query = @"SELECT LOWER(c.name) FROM c"} },
                    Error = @"""Errors"":[""Required VALUE expression missing from computed property query 'SELECT LOWER(c.name) FROM c' at index (0).""]"
                },
                new
                {
                    ComputedProperties = new Collection<ComputedProperty>{ new ComputedProperty {Name = "lowerName", Query = @"SELECT LOWER(c.name) FROM r"} },
                    Error = @"""Errors"":[""Computed property at index (0) has a malformed query: 'SELECT LOWER(c.name) FROM r' Error details: '{\""errors\"":[{\""severity\"":\""Error\"",\""code\"":2001,\""message\"":\""Identifier 'c' could not be resolved.\""}]}'""]"
                },
            };

            IndexingPolicy indexingPolicy = new IndexingPolicy
            {
                SpatialIndexes = new Collection<SpatialPath>
                {
                    new SpatialPath
                    {
                        Path = "/spatial/*",
                        SpatialTypes = new Collection<Cosmos.SpatialType>()
                        {
                            Cosmos.SpatialType.LineString,
                            Cosmos.SpatialType.MultiPolygon,
                            Cosmos.SpatialType.Point,
                            Cosmos.SpatialType.Polygon,
                        }
                    }
                }
            };

            // Create
            foreach (var variation in variations)
            {
                ContainerProperties containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/users")
                {
                    IndexingPolicy = indexingPolicy,
                    GeospatialConfig = new GeospatialConfig(GeospatialType.Geography),
                    ComputedProperties = variation.ComputedProperties
                };

                try
                {
                    ContainerResponse response = await this.database.CreateContainerAsync(containerProperties);
                    Assert.Fail($@"Computed Property '{variation.ComputedProperties.Last().Name}' Query '{variation.ComputedProperties.Last().Query}' was expected to fail with error '{variation.Error}'.");
                }
                catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.BadRequest)
                {
                    Assert.IsTrue(ce.Message.Contains(variation.Error), $"Message expected to contain:'{variation.Error}'{Environment.NewLine}Actual Message: '{ce.Message}'");
                }
            }

            // Replace
            Container containerToReplace = await this.database.CreateContainerAsync(new ContainerProperties(Guid.NewGuid().ToString(), "/users"));
            foreach (var variation in variations)
            {
                ContainerProperties containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/users")
                {
                    IndexingPolicy = indexingPolicy,
                    GeospatialConfig = new GeospatialConfig(GeospatialType.Geography),
                    ComputedProperties = variation.ComputedProperties
                };

                try
                {
                    ContainerResponse response = await containerToReplace.ReplaceContainerAsync(containerProperties);
                    Assert.Fail($@"Computed Property '{variation.ComputedProperties.Last().Name}' Query '{variation.ComputedProperties.Last().Query}' was expected to fail with error '{variation.Error}'.");
                }
                catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.BadRequest)
                {
                    Assert.IsTrue(ce.Message.Contains(variation.Error), $"Message expected to contain:'{variation.Error}'{Environment.NewLine}Actual Message: '{ce.Message}'");
                }
            }
        }

        [TestMethod]
        public async Task ContainerNegativeSpatialIndexTest()
        {
            ContainerProperties containerProperties = new ContainerProperties(Guid.NewGuid().ToString(), "/users")
            {
                IndexingPolicy = new IndexingPolicy()
                {
                    SpatialIndexes = new Collection<SpatialPath>()
                    {
                        new SpatialPath()
                        {
                            Path = "/address/spatial/*"
                        }
                    }
                }
            };

            try
            {
                ContainerResponse response = await this.database.CreateContainerAsync(containerProperties);
                Assert.Fail("Should require spatial type");
            }
            catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.BadRequest)
            {
                Assert.IsTrue(ce.Message.Contains("The spatial data types array cannot be empty. Assign at least one spatial type for the 'types' array for the path"));
            }
        }

        [TestMethod]
        public async Task ContainerMigrationTest()
        {
            string containerName = "MigrationIndexTest";
            Documents.Index index1 = new Documents.RangeIndex(Documents.DataType.String, -1);
            Documents.Index index2 = new Documents.RangeIndex(Documents.DataType.Number, -1);
            Documents.DocumentCollection documentCollection = new Microsoft.Azure.Documents.DocumentCollection()
            {
                Id = containerName,
                IndexingPolicy = new Documents.IndexingPolicy()
                {
                    IncludedPaths = new Collection<Documents.IncludedPath>()
                    {
                        new Documents.IncludedPath()
                        {
                            Path = "/*",
                            Indexes = new Collection<Documents.Index>()
                            {
                                index1,
                                index2
                            }
                        }
                    }
                }
            };

            Documents.DocumentCollection createResponse = await NonPartitionedContainerHelper.CreateNonPartitionedContainer(this.database, documentCollection);

            // Verify the collection was created with deprecated Index objects
            Assert.AreEqual(2, createResponse.IndexingPolicy.IncludedPaths.First().Indexes.Count);
            Documents.Index createIndex = createResponse.IndexingPolicy.IncludedPaths.First().Indexes.First();
            Assert.AreEqual(index1.Kind, createIndex.Kind);

            // Verify v3 can add composite indexes and update the container
            Container container = this.database.GetContainer(containerName);
            ContainerProperties containerProperties = await container.ReadContainerAsync();
            Assert.IsNotNull(containerProperties.SelfLink);
            string cPath0 = "/address/city";
            string cPath1 = "/address/state";
            containerProperties.IndexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>()
            {
                new CompositePath()
                {
                    Path= cPath0,
                    Order = CompositePathSortOrder.Descending
                },
                new CompositePath()
                {
                    Path= cPath1,
                    Order = CompositePathSortOrder.Ascending
                }
            });

            containerProperties.IndexingPolicy.SpatialIndexes.Add(
                new SpatialPath()
                {
                    Path = "/address/test/*",
                    SpatialTypes = new Collection<SpatialType>() { SpatialType.Point }
                });

            // List<ComputedProperty> computedProperties = new List<ComputedProperty>
            // {
            //     new ComputedProperty() { Name = "lowerName", Query = "SELECT VALUE LOWER(c.name) FROM c" },
            //     new ComputedProperty() { Name = "estimatedTax", Query = "SELECT VALUE c.salary * 0.2 FROM c" }
            // };
               
            // foreach (ComputedProperty computedProperty in computedProperties)
            // {
            //     containerProperties.ComputedProperties.Add(computedProperty);
            // }

            ContainerProperties propertiesAfterReplace = await container.ReplaceContainerAsync(containerProperties);
            Assert.AreEqual(0, propertiesAfterReplace.IndexingPolicy.IncludedPaths.First().Indexes.Count);
            Assert.AreEqual(1, propertiesAfterReplace.IndexingPolicy.CompositeIndexes.Count);
            Collection<CompositePath> compositePaths = propertiesAfterReplace.IndexingPolicy.CompositeIndexes.First();
            Assert.AreEqual(2, compositePaths.Count);
            CompositePath compositePath0 = compositePaths.ElementAt(0);
            CompositePath compositePath1 = compositePaths.ElementAt(1);
            Assert.IsTrue(string.Equals(cPath0, compositePath0.Path) || string.Equals(cPath1, compositePath0.Path));
            Assert.IsTrue(string.Equals(cPath0, compositePath1.Path) || string.Equals(cPath1, compositePath1.Path));

            Assert.AreEqual(1, propertiesAfterReplace.IndexingPolicy.SpatialIndexes.Count);
            Assert.AreEqual("/address/test/*", propertiesAfterReplace.IndexingPolicy.SpatialIndexes.First().Path);

            ComputedPropertyComparer.AssertAreEqual(containerProperties.ComputedProperties, propertiesAfterReplace.ComputedProperties);
        }

        [TestMethod]
        public async Task PartitionedCRUDTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse =
                await this.database.DefineContainer(containerName, partitionKeyPath)
                    .WithIndexingPolicy()
                        .WithIndexingMode(IndexingMode.None)
                        .WithAutomaticIndexing(false)
                        .Attach()
                    .CreateAsync();

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            Container container = containerResponse;
            Assert.AreEqual(IndexingMode.None, containerResponse.Resource.IndexingPolicy.IndexingMode);
            Assert.IsFalse(containerResponse.Resource.IndexingPolicy.Automatic);

            containerResponse = await container.ReadContainerAsync();
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            Assert.AreEqual(IndexingMode.None, containerResponse.Resource.IndexingPolicy.IndexingMode);
            Assert.IsFalse(containerResponse.Resource.IndexingPolicy.Automatic);

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task WithUniqueKeys()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse =
                await this.database.DefineContainer(containerName, partitionKeyPath)
                    .WithUniqueKey()
                        .Path("/attribute1")
                        .Path("/attribute2")
                        .Attach()
                    .CreateAsync();

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            Container container = containerResponse;
            Assert.AreEqual(1, containerResponse.Resource.UniqueKeyPolicy.UniqueKeys.Count);
            Assert.AreEqual(2, containerResponse.Resource.UniqueKeyPolicy.UniqueKeys[0].Paths.Count);
            Assert.AreEqual("/attribute1", containerResponse.Resource.UniqueKeyPolicy.UniqueKeys[0].Paths[0]);
            Assert.AreEqual("/attribute2", containerResponse.Resource.UniqueKeyPolicy.UniqueKeys[0].Paths[1]);

            containerResponse = await container.ReadContainerAsync();
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            Assert.AreEqual(1, containerResponse.Resource.UniqueKeyPolicy.UniqueKeys.Count);
            Assert.AreEqual(2, containerResponse.Resource.UniqueKeyPolicy.UniqueKeys[0].Paths.Count);
            Assert.AreEqual("/attribute1", containerResponse.Resource.UniqueKeyPolicy.UniqueKeys[0].Paths[0]);
            Assert.AreEqual("/attribute2", containerResponse.Resource.UniqueKeyPolicy.UniqueKeys[0].Paths[1]);

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task TestConflictResolutionPolicy()
        {
            Database databaseForConflicts = await this.GetClient().CreateDatabaseAsync("conflictResolutionContainerTest",
                cancellationToken: this.cancellationToken);

            try
            {
                string containerName = "conflictResolutionContainerTest";
                string partitionKeyPath = "/users";

                ContainerResponse containerResponse =
                    await databaseForConflicts.DefineContainer(containerName, partitionKeyPath)
                        .WithConflictResolution()
                            .WithLastWriterWinsResolution("/lww")
                            .Attach()
                        .CreateAsync();

                Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
                Assert.AreEqual(containerName, containerResponse.Resource.Id);
                Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
                ContainerProperties containerSettings = containerResponse.Resource;
                Assert.IsNotNull(containerSettings.ConflictResolutionPolicy);
                Assert.AreEqual(ConflictResolutionMode.LastWriterWins, containerSettings.ConflictResolutionPolicy.Mode);
                Assert.AreEqual("/lww", containerSettings.ConflictResolutionPolicy.ResolutionPath);
                Assert.IsTrue(string.IsNullOrEmpty(containerSettings.ConflictResolutionPolicy.ResolutionProcedure));

                // Delete container
                await containerResponse.Container.DeleteContainerAsync();

                // Re-create with custom policy
                string sprocName = "customresolsproc";
                containerResponse = await databaseForConflicts.DefineContainer(containerName, partitionKeyPath)
                        .WithConflictResolution()
                            .WithCustomStoredProcedureResolution(sprocName)
                            .Attach()
                        .CreateAsync();

                Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
                Assert.AreEqual(containerName, containerResponse.Resource.Id);
                Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
                containerSettings = containerResponse.Resource;
                Assert.IsNotNull(containerSettings.ConflictResolutionPolicy);
                Assert.AreEqual(ConflictResolutionMode.Custom, containerSettings.ConflictResolutionPolicy.Mode);
                Assert.AreEqual(UriFactory.CreateStoredProcedureUri(databaseForConflicts.Id, containerName, sprocName).ToString(), containerSettings.ConflictResolutionPolicy.ResolutionProcedure);
                Assert.IsTrue(string.IsNullOrEmpty(containerSettings.ConflictResolutionPolicy.ResolutionPath));
            }
            finally
            {
                await databaseForConflicts.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task TestChangeFeedPolicy()
        {
            Database databaseForChangeFeed = await this.GetClient().CreateDatabaseAsync("changeFeedRetentionContainerTest",
                cancellationToken: this.cancellationToken);

            try
            {
                string containerName = "changeFeedRetentionContainerTest";
                string partitionKeyPath = "/users";
                TimeSpan retention = TimeSpan.FromMinutes(10);

                ContainerResponse containerResponse =
                    await databaseForChangeFeed.DefineContainer(containerName, partitionKeyPath)
                        .WithChangeFeedPolicy(retention)
                            .Attach()
                        .CreateAsync();

                Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
                Assert.AreEqual(containerName, containerResponse.Resource.Id);
                Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
                ContainerProperties containerSettings = containerResponse.Resource;
                Assert.IsNotNull(containerSettings.ChangeFeedPolicy);
                Assert.AreEqual(retention.TotalMinutes, containerSettings.ChangeFeedPolicy.FullFidelityRetention.TotalMinutes);
            }
            finally
            {
                await databaseForChangeFeed.DeleteAsync();
            }
        }

        [TestMethod]
        [Ignore("This test will be enabled once the vector similarity changes are made available into the public emulator.")]
        public async Task TestVectorEmbeddingPolicy()
        {
            string vector1Path = "/vector1", vector2Path = "/vector2", vector3Path = "/vector3";
            Database databaseForVectorEmbedding = await this.GetClient().CreateDatabaseAsync("vectorEmbeddingContainerTest",
                cancellationToken: this.cancellationToken);

            try
            {
                Collection<Embedding> embeddings = new Collection<Embedding>()
                {
                    new Embedding()
                    {
                        Path = vector1Path,
                        DataType = VectorDataType.Int8,
                        DistanceFunction = DistanceFunction.DotProduct,
                        Dimensions = 1200,
                    },
                    new Embedding()
                    {
                        Path = vector2Path,
                        DataType = VectorDataType.Uint8,
                        DistanceFunction = DistanceFunction.Cosine,
                        Dimensions = 3,
                    },
                    new Embedding()
                    {
                        Path = vector3Path,
                        DataType = VectorDataType.Float32,
                        DistanceFunction = DistanceFunction.Euclidean,
                        Dimensions = 400,
                    },
                };

                string containerName = "vectorEmbeddingContainerTest";
                string partitionKeyPath = "/users";

                ContainerResponse containerResponse =
                    await databaseForVectorEmbedding.DefineContainer(containerName, partitionKeyPath)
                        .WithVectorEmbeddingPolicy(embeddings)
                        .Attach()
                        .WithIndexingPolicy()
                            .WithVectorIndex()
                                .Path(vector1Path, VectorIndexType.Flat)
                             .Attach()
                            .WithVectorIndex()
                                .Path(vector2Path, VectorIndexType.QuantizedFlat)
                                .WithQuantizationByteSize(3)
                                .WithVectorIndexShardKey(new string[] { "/Country" })
                             .Attach()
                            .WithVectorIndex()
                                .Path(vector3Path, VectorIndexType.DiskANN)
                                .WithQuantizationByteSize(2)
                                .WithIndexingSearchListSize(35)
                                .WithVectorIndexShardKey(new string[] { "/ZipCode" })
                             .Attach()
                        .Attach()
                        .CreateAsync();

                Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
                Assert.AreEqual(containerName, containerResponse.Resource.Id);
                Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
                ContainerProperties containerSettings = containerResponse.Resource;

                // Validate Vector Embeddings.
                Assert.IsNotNull(containerSettings.VectorEmbeddingPolicy);
                Assert.IsNotNull(containerSettings.VectorEmbeddingPolicy.Embeddings);
                Assert.AreEqual(embeddings.Count, containerSettings.VectorEmbeddingPolicy.Embeddings.Count());
                Assert.IsTrue(embeddings.OrderBy(x => x.Path).SequenceEqual(containerSettings.VectorEmbeddingPolicy.Embeddings.OrderBy(x => x.Path)));

                // Validate Vector Indexes.
                Assert.IsNotNull(containerSettings.IndexingPolicy.VectorIndexes);
                Assert.AreEqual(embeddings.Count, containerSettings.IndexingPolicy.VectorIndexes.Count());
                Assert.AreEqual(vector1Path, containerSettings.IndexingPolicy.VectorIndexes[0].Path);
                Assert.AreEqual(VectorIndexType.Flat, containerSettings.IndexingPolicy.VectorIndexes[0].Type);
                Assert.AreEqual(vector2Path, containerSettings.IndexingPolicy.VectorIndexes[1].Path);
                Assert.AreEqual(VectorIndexType.QuantizedFlat, containerSettings.IndexingPolicy.VectorIndexes[1].Type);
                Assert.AreEqual(3, containerSettings.IndexingPolicy.VectorIndexes[1].QuantizationByteSize);
                CollectionAssert.AreEqual(new string[] { "/Country" }, containerSettings.IndexingPolicy.VectorIndexes[1].VectorIndexShardKey);
                Assert.AreEqual(vector3Path, containerSettings.IndexingPolicy.VectorIndexes[2].Path);
                Assert.AreEqual(VectorIndexType.DiskANN, containerSettings.IndexingPolicy.VectorIndexes[2].Type);
                Assert.AreEqual(2, containerSettings.IndexingPolicy.VectorIndexes[2].QuantizationByteSize);
                Assert.AreEqual(35, containerSettings.IndexingPolicy.VectorIndexes[2].IndexingSearchListSize);
                CollectionAssert.AreEqual(new string[] { "/ZipCode" }, containerSettings.IndexingPolicy.VectorIndexes[2].VectorIndexShardKey);
            }
            finally
            {
                await databaseForVectorEmbedding.DeleteAsync();
            }
        }

        [TestMethod]
        [Ignore("Requires a real Cosmos DB account with vector-embedding preview enabled. Fill in the endpoint and key before running.")]
        public async Task TestVectorEmbeddingPolicyWithEmbeddingSource()
        {
            const string accountEndpoint = "";
            const string accountKey = "";

            const string databaseId = "embeddingSourceIntegrationDb";
            const string containerId = "embeddingSourceIntegrationContainer";
            const string partitionKeyPath = "/pk";
            const string embeddingPath = "/embedding";

            CosmosClient client = new CosmosClient(accountEndpoint, accountKey);
            Database database = await client.CreateDatabaseIfNotExistsAsync(databaseId);

            try
            {
                EmbeddingSource embeddingSource = new EmbeddingSource()
                {
                    SourcePaths = new Collection<string>
                    {
                        "/journal_title",
                        "/title",
                        "/toc_abstract",
                        "/abstract",
                        "/full_text",
                    },
                    DeploymentName = "text-embedding-3-small",
                    ModelName = "text-embedding-3-small",
                    Endpoint = "https://embedding-south-central.cognitiveservices.azure.com/",
                    AuthType = EmbeddingAuthType.ApiKey,
                };

                Collection<Embedding> embeddings = new Collection<Embedding>()
                {
                    new Embedding()
                    {
                        Path = embeddingPath,
                        DataType = VectorDataType.Float32,
                        DistanceFunction = DistanceFunction.Cosine,
                        Dimensions = 1536,
                        EmbeddingSource = embeddingSource,
                    },
                };

                try
                {
                    await database.GetContainer(containerId).DeleteContainerAsync();
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                }

                ContainerResponse containerResponse =
                    await database.DefineContainer(containerId, partitionKeyPath)
                        .WithVectorEmbeddingPolicy(embeddings)
                        .Attach()
                        .CreateAsync();

                Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
                Assert.AreEqual(containerId, containerResponse.Resource.Id);
                Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

                this.AssertEmbeddingSourceRoundTrip(containerResponse.Resource.VectorEmbeddingPolicy, embeddingPath, embeddingSource);

                ContainerResponse readResponse = await containerResponse.Container.ReadContainerAsync();
                Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
                Assert.AreEqual(containerId, readResponse.Resource.Id);
                Assert.AreEqual(partitionKeyPath, readResponse.Resource.PartitionKey.Paths.First());

                this.AssertEmbeddingSourceRoundTrip(readResponse.Resource.VectorEmbeddingPolicy, embeddingPath, embeddingSource);
            }
            finally
            {
                await database.DeleteAsync();
                client.Dispose();
            }
        }

        private void AssertEmbeddingSourceRoundTrip(VectorEmbeddingPolicy policy, string expectedEmbeddingPath, EmbeddingSource expected)
        {
            Assert.IsNotNull(policy);
            Assert.AreEqual(1, policy.Embeddings.Count());

            Embedding readEmbedding = policy.Embeddings.Single();
            Assert.AreEqual(expectedEmbeddingPath, readEmbedding.Path);
            Assert.AreEqual(VectorDataType.Float32, readEmbedding.DataType);
            Assert.AreEqual(DistanceFunction.Cosine, readEmbedding.DistanceFunction);
            Assert.AreEqual(1536, readEmbedding.Dimensions);

            EmbeddingSource readSource = readEmbedding.EmbeddingSource;
            Assert.IsNotNull(readSource, "EmbeddingSource should be returned by the server.");
            CollectionAssert.AreEqual(expected.SourcePaths.ToArray(), readSource.SourcePaths.ToArray());
            Assert.AreEqual(expected.DeploymentName, readSource.DeploymentName);
            Assert.AreEqual(expected.ModelName, readSource.ModelName);
            Assert.AreEqual(expected.Endpoint, readSource.Endpoint);
            Assert.AreEqual(expected.AuthType, readSource.AuthType);
        }

        [TestMethod]
        public async Task WithIndexingPolicy()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse =
                await this.database.DefineContainer(containerName, partitionKeyPath)
                    .WithIndexingPolicy()
                        .WithIncludedPaths()
                            .Path("/included1/*")
                            .Path("/included2/*")
                            .Attach()
                        .WithExcludedPaths()
                            .Path("/*")
                            .Attach()
                        .WithCompositeIndex()
                            .Path("/composite1")
                            .Path("/composite2", CompositePathSortOrder.Descending)
                            .Attach()
                        .Attach()
                    .CreateAsync();

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            Container container = containerResponse;
            Assert.AreEqual(2, containerResponse.Resource.IndexingPolicy.IncludedPaths.Count);
            Assert.AreEqual("/included1/*", containerResponse.Resource.IndexingPolicy.IncludedPaths[0].Path);
            Assert.AreEqual("/included2/*", containerResponse.Resource.IndexingPolicy.IncludedPaths[1].Path);
            Assert.AreEqual("/*", containerResponse.Resource.IndexingPolicy.ExcludedPaths[0].Path);
            Assert.AreEqual(1, containerResponse.Resource.IndexingPolicy.CompositeIndexes.Count);
            Assert.AreEqual("/composite1", containerResponse.Resource.IndexingPolicy.CompositeIndexes[0][0].Path);
            Assert.AreEqual("/composite2", containerResponse.Resource.IndexingPolicy.CompositeIndexes[0][1].Path);
            Assert.AreEqual(CompositePathSortOrder.Descending, containerResponse.Resource.IndexingPolicy.CompositeIndexes[0][1].Order);

            containerResponse = await container.ReadContainerAsync();
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            Assert.AreEqual(2, containerResponse.Resource.IndexingPolicy.IncludedPaths.Count);
            Assert.AreEqual("/included1/*", containerResponse.Resource.IndexingPolicy.IncludedPaths[0].Path);
            Assert.AreEqual("/included2/*", containerResponse.Resource.IndexingPolicy.IncludedPaths[1].Path);
            Assert.AreEqual("/*", containerResponse.Resource.IndexingPolicy.ExcludedPaths[0].Path);
            Assert.AreEqual(1, containerResponse.Resource.IndexingPolicy.CompositeIndexes.Count);
            Assert.AreEqual("/composite1", containerResponse.Resource.IndexingPolicy.CompositeIndexes[0][0].Path);
            Assert.AreEqual("/composite2", containerResponse.Resource.IndexingPolicy.CompositeIndexes[0][1].Path);
            Assert.AreEqual(CompositePathSortOrder.Descending, containerResponse.Resource.IndexingPolicy.CompositeIndexes[0][1].Order);

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task TestFullTextSearchPolicy()
        {
            string fullTextPath1 = "/fts1", fullTextPath2 = "/fts2", fullTextPath3 = "/fts3";
            Database databaseForVectorEmbedding = await this.GetClient().CreateDatabaseAsync("fullTextSearchDB",
                cancellationToken: this.cancellationToken);

            try
            {
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

                string containerName = "fullTextContainerTest";
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
                            .WithFullTextIndex()
                                .Path(fullTextPath2)
                             .Attach()
                            .WithFullTextIndex()
                                .Path(fullTextPath3)
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
                Assert.AreEqual(fullTextPath2, containerSettings.IndexingPolicy.FullTextIndexes[1].Path);
                Assert.AreEqual(fullTextPath3, containerSettings.IndexingPolicy.FullTextIndexes[2].Path);
            }
            finally
            {
                await databaseForVectorEmbedding.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task TestFullTextSearchPolicyWithDefaultLanguage()
        {
            string fullTextPath1 = "/fts1";
            Database databaseForVectorEmbedding = await this.GetClient().CreateDatabaseAsync("fullTextSearchDB",
                cancellationToken: this.cancellationToken);

            try
            {
                string containerName = "fullTextContainerTest";
                string partitionKeyPath = "/pk";

                ContainerResponse containerResponse =
                    await databaseForVectorEmbedding.DefineContainer(containerName, partitionKeyPath)
                        .WithFullTextPolicy(
                            defaultLanguage: "en-US",
                            fullTextPaths: new Collection<FullTextPath>() {  new FullTextPath()
                            {
                                Language = "en-US",
                                Path = fullTextPath1
                            }})
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
                Assert.AreEqual(1, containerSettings.FullTextPolicy.FullTextPaths.Count());

                // Validate Full Text Indexes.
                Assert.IsNotNull(containerSettings.IndexingPolicy.FullTextIndexes);
                Assert.AreEqual(1, containerSettings.IndexingPolicy.FullTextIndexes.Count());
                Assert.AreEqual(fullTextPath1, containerSettings.IndexingPolicy.FullTextIndexes[0].Path);
            }
            finally
            {
                await databaseForVectorEmbedding.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task TestFullTextSearchPolicyOptionalLanguage()
        {
            string fullTextPath1 = "/fts1";
            Database databaseForFullTextSearch = await this.GetClient().CreateDatabaseAsync("fullTextSearchDB",
                cancellationToken: this.cancellationToken);

            try
            {
                string containerName = "fullTextContainerTest";
                string partitionKeyPath = "/pk";

                ContainerResponse containerResponse =
                    await databaseForFullTextSearch.DefineContainer(containerName, partitionKeyPath)
                        .WithFullTextPolicy(
                            defaultLanguage: null,
                            fullTextPaths: new Collection<FullTextPath>() {  new FullTextPath()
                            {
                                Path = fullTextPath1
                            }})
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
                Assert.AreEqual("en-US", containerSettings.FullTextPolicy.DefaultLanguage);
                Assert.IsNotNull(containerSettings.FullTextPolicy.DefaultSpec);
                Assert.AreEqual("en-US", containerSettings.FullTextPolicy.DefaultSpec.Language);
                Assert.IsNotNull(containerSettings.FullTextPolicy.FullTextPaths);
                Assert.AreEqual(1, containerSettings.FullTextPolicy.FullTextPaths.Count());
                Assert.IsNull(containerSettings.FullTextPolicy.FullTextPaths[0].Language);

                // Validate Full Text Indexes.
                Assert.IsNotNull(containerSettings.IndexingPolicy.FullTextIndexes);
                Assert.AreEqual(1, containerSettings.IndexingPolicy.FullTextIndexes.Count());
                Assert.AreEqual(fullTextPath1, containerSettings.IndexingPolicy.FullTextIndexes[0].Path);
            }
            finally
            {
                await databaseForFullTextSearch.DeleteAsync();
            }
        }

#if PREVIEW
        [TestMethod]
        public async Task TestFullTextSearchPolicyStandardPackageWithQueryExecution()
        {
            string fullTextPath1 = "/title", fullTextPath2 = "/description";
            Database databaseForFullText = await this.GetClient().CreateDatabaseAsync("fullTextStandardDB",
                cancellationToken: this.cancellationToken);

            try
            {
                Collection<FullTextPath> fullTextPaths = new Collection<FullTextPath>()
                {
                    new FullTextPath()
                    {
                        Path = fullTextPath1,
                    },
                    new FullTextPath()
                    {
                        Path = fullTextPath2,
                        Tokenizer = "word",
                        Filters = new Collection<string> { "stop", "lowercase" },
                    },
                };

                FullTextDefaultSpec defaultSpec = new FullTextDefaultSpec
                {
                    Language = "en-US",
                    Tokenizer = "word",
                    Filters = new Collection<string> { "stop", "lowercase", "stem" },
                    StopWordListKind = "basic",
                };

                string containerName = "fullTextStandardContainer";
                string partitionKeyPath = "/pk";

                ContainerResponse containerResponse =
                    await databaseForFullText.DefineContainer(containerName, partitionKeyPath)
                        .WithFullTextPolicy(
                            package: "standard",
                            defaultSpec: defaultSpec,
                            fullTextPaths: fullTextPaths)
                        .Attach()
                        .WithIndexingPolicy()
                            .WithFullTextIndex()
                                .Path(fullTextPath1)
                             .Attach()
                            .WithFullTextIndex()
                                .Path(fullTextPath2)
                             .Attach()
                        .Attach()
                        .CreateAsync();

                Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
                ContainerProperties containerSettings = containerResponse.Resource;

                // Validate FullText Policy round-trip.
                Assert.IsNotNull(containerSettings.FullTextPolicy);
                Assert.AreEqual("standard", containerSettings.FullTextPolicy.Package);
                Assert.IsNotNull(containerSettings.FullTextPolicy.DefaultSpec);
                Assert.AreEqual("en-US", containerSettings.FullTextPolicy.DefaultSpec.Language);
                Assert.AreEqual("word", containerSettings.FullTextPolicy.DefaultSpec.Tokenizer);
                Assert.IsNotNull(containerSettings.FullTextPolicy.DefaultSpec.Filters);
                Assert.AreEqual("basic", containerSettings.FullTextPolicy.DefaultSpec.StopWordListKind);

                Assert.IsNotNull(containerSettings.FullTextPolicy.FullTextPaths);
                Assert.AreEqual(2, containerSettings.FullTextPolicy.FullTextPaths.Count);

                // Insert documents and run a FullTextContains query.
                Container container = containerResponse.Container;
                await container.CreateItemAsync(new { id = "1", pk = "1", title = "Hello world from Azure", description = "The quick brown fox jumps over the lazy dog" });
                await container.CreateItemAsync(new { id = "2", pk = "2", title = "Cosmos DB is great", description = "Azure Cosmos DB is a globally distributed database" });

                string query = "SELECT c.id FROM c WHERE FullTextContains(c.title, 'Azure') ORDER BY RANK FullTextScore(c.title, 'Azure')";
                FeedIterator<dynamic> feedIterator = container.GetItemQueryIterator<dynamic>(query);

                List<dynamic> results = new List<dynamic>();
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<dynamic> response = await feedIterator.ReadNextAsync();
                    results.AddRange(response);
                }

                Assert.IsTrue(results.Count > 0, "Expected at least one result from FullTextContains query");
            }
            finally
            {
                await databaseForFullText.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task TestFullTextSearchPolicyStandardPackageWithStopWords()
        {
            Database databaseForFullText = await this.GetClient().CreateDatabaseAsync("fullTextStopWordsDB",
                cancellationToken: this.cancellationToken);

            try
            {
                // Create container with custom stop words configuration.
                FullTextDefaultSpec defaultSpec = new FullTextDefaultSpec
                {
                    Language = "en-US",
                    Tokenizer = "word",
                    Filters = new Collection<string> { "stop", "lowercase" },
                    StopWordListKind = "basic",
                    AddStopWords = new Collection<string> { "customword", "anotherword" },
                    RemoveStopWords = new Collection<string> { "the" },
                };

                Collection<FullTextPath> fullTextPaths = new Collection<FullTextPath>()
                {
                    new FullTextPath() { Path = "/text" },
                };

                ContainerResponse containerResponse =
                    await databaseForFullText.DefineContainer("stopWordsContainer", "/pk")
                        .WithFullTextPolicy(
                            package: "standard",
                            defaultSpec: defaultSpec,
                            fullTextPaths: fullTextPaths)
                        .Attach()
                        .WithIndexingPolicy()
                            .WithFullTextIndex()
                                .Path("/text")
                             .Attach()
                        .Attach()
                        .CreateAsync();

                Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);

                // Validate addStopWords/removeStopWords round-trip.
                ContainerProperties settings = containerResponse.Resource;
                Assert.AreEqual("standard", settings.FullTextPolicy.Package);
                Assert.AreEqual("basic", settings.FullTextPolicy.DefaultSpec.StopWordListKind);
                Assert.IsNotNull(settings.FullTextPolicy.DefaultSpec.AddStopWords);
                Assert.AreEqual(2, settings.FullTextPolicy.DefaultSpec.AddStopWords.Count);
                Assert.IsTrue(settings.FullTextPolicy.DefaultSpec.AddStopWords.Contains("customword"));
                Assert.IsTrue(settings.FullTextPolicy.DefaultSpec.AddStopWords.Contains("anotherword"));
                Assert.IsNotNull(settings.FullTextPolicy.DefaultSpec.RemoveStopWords);
                Assert.AreEqual(1, settings.FullTextPolicy.DefaultSpec.RemoveStopWords.Count);
                Assert.AreEqual("the", settings.FullTextPolicy.DefaultSpec.RemoveStopWords[0]);

                // Insert a document and verify query still works with this policy config.
                Container container = containerResponse.Container;
                await container.CreateItemAsync(new { id = "1", pk = "1", text = "The cloud platform is great" });

                string query = "SELECT c.id FROM c WHERE FullTextContains(c.text, 'cloud')";
                FeedIterator<dynamic> feedIterator = container.GetItemQueryIterator<dynamic>(query);

                List<dynamic> results = new List<dynamic>();
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<dynamic> response = await feedIterator.ReadNextAsync();
                    results.AddRange(response);
                }

                Assert.AreEqual(1, results.Count, "Expected query to return the document matching 'cloud'");
            }
            finally
            {
                await databaseForFullText.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task TestFullTextSearchPolicyStandardPackageUpdateContainer()
        {
            Database databaseForFullText = await this.GetClient().CreateDatabaseAsync("fullTextUpdateDB",
                cancellationToken: this.cancellationToken);

            try
            {
                // Create with minimal config.
                FullTextDefaultSpec defaultSpec = new FullTextDefaultSpec
                {
                    Language = "en-US",
                    Tokenizer = "word",
                    Filters = new Collection<string> { "stop", "lowercase" },
                    StopWordListKind = "basic",
                };

                Collection<FullTextPath> fullTextPaths = new Collection<FullTextPath>()
                {
                    new FullTextPath() { Path = "/title" },
                };

                ContainerResponse containerResponse =
                    await databaseForFullText.DefineContainer("updateContainer", "/pk")
                        .WithFullTextPolicy(
                            package: "standard",
                            defaultSpec: defaultSpec,
                            fullTextPaths: fullTextPaths)
                        .Attach()
                        .WithIndexingPolicy()
                            .WithFullTextIndex()
                                .Path("/title")
                             .Attach()
                        .Attach()
                        .CreateAsync();

                Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);

                // Read and update: add a new full-text path.
                Container container = containerResponse.Container;
                ContainerProperties properties = containerResponse.Resource;

                properties.FullTextPolicy.FullTextPaths.Add(new FullTextPath { Path = "/body" });
                properties.IndexingPolicy.FullTextIndexes.Add(new FullTextIndexPath { Path = "/body" });

                ContainerResponse replaceResponse = await container.ReplaceContainerAsync(properties);
                Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);

                // Validate updated policy.
                ContainerProperties updatedSettings = replaceResponse.Resource;
                Assert.AreEqual("standard", updatedSettings.FullTextPolicy.Package);
                Assert.AreEqual(2, updatedSettings.FullTextPolicy.FullTextPaths.Count);
                Assert.AreEqual("/title", updatedSettings.FullTextPolicy.FullTextPaths[0].Path);
                Assert.AreEqual("/body", updatedSettings.FullTextPolicy.FullTextPaths[1].Path);
            }
            finally
            {
                await databaseForFullText.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task TestFullTextSearchPolicyStandardPackagePerPathOverrides()
        {
            Database databaseForFullText = await this.GetClient().CreateDatabaseAsync("fullTextOverridesDB",
                cancellationToken: this.cancellationToken);

            try
            {
                // DefaultSpec with stem filter; one path overrides with just lowercase.
                FullTextDefaultSpec defaultSpec = new FullTextDefaultSpec
                {
                    Language = "en-US",
                    Tokenizer = "word",
                    Filters = new Collection<string> { "stop", "lowercase", "stem" },
                    StopWordListKind = "basic",
                };

                Collection<FullTextPath> fullTextPaths = new Collection<FullTextPath>()
                {
                    new FullTextPath() { Path = "/title" },
                    new FullTextPath()
                    {
                        Path = "/tags",
                        Language = "en-US",
                        StopWordListKind = "none",
                    },
                    new FullTextPath()
                    {
                        Path = "/notes",
                        Tokenizer = "word",
                        Filters = new Collection<string> { "lowercase" },
                    },
                };

                ContainerResponse containerResponse =
                    await databaseForFullText.DefineContainer("overridesContainer", "/pk")
                        .WithFullTextPolicy(
                            package: "standard",
                            defaultSpec: defaultSpec,
                            fullTextPaths: fullTextPaths)
                        .Attach()
                        .WithIndexingPolicy()
                            .WithFullTextIndex().Path("/title").Attach()
                            .WithFullTextIndex().Path("/tags").Attach()
                            .WithFullTextIndex().Path("/notes").Attach()
                        .Attach()
                        .CreateAsync();

                Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
                ContainerProperties settings = containerResponse.Resource;

                // Validate per-path overrides round-trip.
                Assert.AreEqual(3, settings.FullTextPolicy.FullTextPaths.Count);

                // /title inherits from defaultSpec.
                Assert.AreEqual("/title", settings.FullTextPolicy.FullTextPaths[0].Path);
                Assert.IsNull(settings.FullTextPolicy.FullTextPaths[0].Tokenizer);

                // /tags has explicit language and stopWordListKind override.
                Assert.AreEqual("/tags", settings.FullTextPolicy.FullTextPaths[1].Path);
                Assert.AreEqual("en-US", settings.FullTextPolicy.FullTextPaths[1].Language);
                Assert.AreEqual("none", settings.FullTextPolicy.FullTextPaths[1].StopWordListKind);

                // /notes has explicit tokenizer and filters override.
                Assert.AreEqual("/notes", settings.FullTextPolicy.FullTextPaths[2].Path);
                Assert.AreEqual("word", settings.FullTextPolicy.FullTextPaths[2].Tokenizer);
                Assert.AreEqual(1, settings.FullTextPolicy.FullTextPaths[2].Filters.Count);
                Assert.AreEqual("lowercase", settings.FullTextPolicy.FullTextPaths[2].Filters[0]);
            }
            finally
            {
                await databaseForFullText.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task TestFullTextSearchPolicyInvalidPackageReturnsError()
        {
            Database databaseForFullText = await this.GetClient().CreateDatabaseAsync("fullTextInvalidDB",
                cancellationToken: this.cancellationToken);

            try
            {
                // Invalid package value should be rejected by the service.
                FullTextDefaultSpec defaultSpec = new FullTextDefaultSpec
                {
                    Language = "en-US",
                };

                Collection<FullTextPath> fullTextPaths = new Collection<FullTextPath>()
                {
                    new FullTextPath() { Path = "/text" },
                };

                try
                {
                    ContainerResponse containerResponse =
                        await databaseForFullText.DefineContainer("invalidPackageContainer", "/pk")
                            .WithFullTextPolicy(
                                package: "invalid_package",
                                defaultSpec: defaultSpec,
                                fullTextPaths: fullTextPaths)
                            .Attach()
                            .WithIndexingPolicy()
                                .WithFullTextIndex().Path("/text").Attach()
                            .Attach()
                            .CreateAsync();

                    Assert.Fail("Expected an exception for invalid package type");
                }
                catch (CosmosException ex)
                {
                    Assert.AreEqual(HttpStatusCode.BadRequest, ex.StatusCode,
                        $"Expected BadRequest for invalid package. Got: {ex.StatusCode}, Message: {ex.Message}");
                }
            }
            finally
            {
                await databaseForFullText.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task TestFullTextSearchPolicyAllValidFilterTypes()
        {
            Database databaseForFullText = await this.GetClient().CreateDatabaseAsync("fullTextFiltersDB",
                cancellationToken: this.cancellationToken);

            try
            {
                // All valid filters: stop, lowercase, stem, ascii.
                FullTextDefaultSpec defaultSpec = new FullTextDefaultSpec
                {
                    Language = "en-US",
                    Tokenizer = "word",
                    Filters = new Collection<string> { "stop", "lowercase", "stem", "ascii" },
                    StopWordListKind = "basic",
                };

                Collection<FullTextPath> fullTextPaths = new Collection<FullTextPath>()
                {
                    new FullTextPath() { Path = "/text" },
                };

                ContainerResponse containerResponse =
                    await databaseForFullText.DefineContainer("allFiltersContainer", "/pk")
                        .WithFullTextPolicy(
                            package: "standard",
                            defaultSpec: defaultSpec,
                            fullTextPaths: fullTextPaths)
                        .Attach()
                        .WithIndexingPolicy()
                            .WithFullTextIndex().Path("/text").Attach()
                        .Attach()
                        .CreateAsync();

                Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
                ContainerProperties settings = containerResponse.Resource;
                Assert.AreEqual(4, settings.FullTextPolicy.DefaultSpec.Filters.Count);
                Assert.IsTrue(settings.FullTextPolicy.DefaultSpec.Filters.Contains("stop"));
                Assert.IsTrue(settings.FullTextPolicy.DefaultSpec.Filters.Contains("lowercase"));
                Assert.IsTrue(settings.FullTextPolicy.DefaultSpec.Filters.Contains("stem"));
                Assert.IsTrue(settings.FullTextPolicy.DefaultSpec.Filters.Contains("ascii"));
            }
            finally
            {
                await databaseForFullText.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task TestFullTextSearchPolicyInvalidFilterReturnsError()
        {
            Database databaseForFullText = await this.GetClient().CreateDatabaseAsync("fullTextInvalidFilterDB",
                cancellationToken: this.cancellationToken);

            try
            {
                FullTextDefaultSpec defaultSpec = new FullTextDefaultSpec
                {
                    Language = "en-US",
                    Tokenizer = "word",
                    Filters = new Collection<string> { "stop", "invalid_filter" },
                    StopWordListKind = "basic",
                };

                Collection<FullTextPath> fullTextPaths = new Collection<FullTextPath>()
                {
                    new FullTextPath() { Path = "/text" },
                };

                try
                {
                    await databaseForFullText.DefineContainer("invalidFilterContainer", "/pk")
                        .WithFullTextPolicy(
                            package: "standard",
                            defaultSpec: defaultSpec,
                            fullTextPaths: fullTextPaths)
                        .Attach()
                        .WithIndexingPolicy()
                            .WithFullTextIndex().Path("/text").Attach()
                        .Attach()
                        .CreateAsync();

                    Assert.Fail("Expected an exception for invalid filter type");
                }
                catch (CosmosException ex)
                {
                    Assert.AreEqual(HttpStatusCode.BadRequest, ex.StatusCode,
                        $"Expected BadRequest for invalid filter. Got: {ex.StatusCode}, Message: {ex.Message}");
                }
            }
            finally
            {
                await databaseForFullText.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task TestFullTextSearchPolicyInvalidTokenizerReturnsError()
        {
            Database databaseForFullText = await this.GetClient().CreateDatabaseAsync("fullTextInvalidTokenizerDB",
                cancellationToken: this.cancellationToken);

            try
            {
                FullTextDefaultSpec defaultSpec = new FullTextDefaultSpec
                {
                    Language = "en-US",
                    Tokenizer = "invalid_tokenizer",
                };

                Collection<FullTextPath> fullTextPaths = new Collection<FullTextPath>()
                {
                    new FullTextPath() { Path = "/text" },
                };

                try
                {
                    await databaseForFullText.DefineContainer("invalidTokenizerContainer", "/pk")
                        .WithFullTextPolicy(
                            package: "standard",
                            defaultSpec: defaultSpec,
                            fullTextPaths: fullTextPaths)
                        .Attach()
                        .WithIndexingPolicy()
                            .WithFullTextIndex().Path("/text").Attach()
                        .Attach()
                        .CreateAsync();

                    Assert.Fail("Expected an exception for invalid tokenizer type");
                }
                catch (CosmosException ex)
                {
                    Assert.AreEqual(HttpStatusCode.BadRequest, ex.StatusCode,
                        $"Expected BadRequest for invalid tokenizer. Got: {ex.StatusCode}, Message: {ex.Message}");
                }
            }
            finally
            {
                await databaseForFullText.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task TestFullTextSearchPolicyFiltersWithLegacyPackageReturnsError()
        {
            Database databaseForFullText = await this.GetClient().CreateDatabaseAsync("fullTextLegacyFiltersDB",
                cancellationToken: this.cancellationToken);

            try
            {
                // Filters are only valid with "standard" package. Setting them on "legacy" should fail.
                Collection<FullTextPath> fullTextPaths = new Collection<FullTextPath>()
                {
                    new FullTextPath()
                    {
                        Path = "/text",
                        Language = "en-US",
                        Tokenizer = "word",
                        Filters = new Collection<string> { "stop", "lowercase" },
                    },
                };

                try
                {
                    await databaseForFullText.DefineContainer("legacyFiltersContainer", "/pk")
                        .WithFullTextPolicy(
                            package: "legacy",
                            defaultSpec: null,
                            fullTextPaths: fullTextPaths)
                        .Attach()
                        .WithIndexingPolicy()
                            .WithFullTextIndex().Path("/text").Attach()
                        .Attach()
                        .CreateAsync();

                    Assert.Fail("Expected an exception for filters with legacy package");
                }
                catch (CosmosException ex)
                {
                    Assert.AreEqual(HttpStatusCode.BadRequest, ex.StatusCode,
                        $"Expected BadRequest for filters on legacy package. Got: {ex.StatusCode}, Message: {ex.Message}");
                }
            }
            finally
            {
                await databaseForFullText.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task TestFullTextSearchPolicyAllStopWordListKinds()
        {
            Database databaseForFullText = await this.GetClient().CreateDatabaseAsync("fullTextStopWordKindsDB",
                cancellationToken: this.cancellationToken);

            try
            {
                // Test "none", "basic", and "extended" stopWordListKind values.
                string[] stopWordKinds = new[] { "none", "basic", "extended" };

                foreach (string kind in stopWordKinds)
                {
                    FullTextDefaultSpec defaultSpec = new FullTextDefaultSpec
                    {
                        Language = "en-US",
                        Tokenizer = "word",
                        Filters = new Collection<string> { "stop", "lowercase" },
                        StopWordListKind = kind,
                    };

                    Collection<FullTextPath> fullTextPaths = new Collection<FullTextPath>()
                    {
                        new FullTextPath() { Path = "/text" },
                    };

                    string containerName = $"stopWordKind_{kind}";
                    ContainerResponse containerResponse =
                        await databaseForFullText.DefineContainer(containerName, "/pk")
                            .WithFullTextPolicy(
                                package: "standard",
                                defaultSpec: defaultSpec,
                                fullTextPaths: fullTextPaths)
                            .Attach()
                            .WithIndexingPolicy()
                                .WithFullTextIndex().Path("/text").Attach()
                            .Attach()
                            .CreateAsync();

                    Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode,
                        $"Failed to create container with stopWordListKind: {kind}");
                    Assert.AreEqual(kind, containerResponse.Resource.FullTextPolicy.DefaultSpec.StopWordListKind,
                        $"StopWordListKind mismatch for: {kind}");
                }
            }
            finally
            {
                await databaseForFullText.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task TestFullTextSearchPolicyBothPackageTypesValid()
        {
            Database databaseForFullText = await this.GetClient().CreateDatabaseAsync("fullTextBothPackagesDB",
                cancellationToken: this.cancellationToken);

            try
            {
                // Test "legacy" package.
                ContainerResponse legacyResponse =
                    await databaseForFullText.DefineContainer("legacyContainer", "/pk")
                        .WithFullTextPolicy(
                            package: "legacy",
                            defaultSpec: null,
                            fullTextPaths: new Collection<FullTextPath>
                            {
                                new FullTextPath() { Path = "/text", Language = "en-US" },
                            })
                        .Attach()
                        .WithIndexingPolicy()
                            .WithFullTextIndex().Path("/text").Attach()
                        .Attach()
                        .CreateAsync();

                Assert.AreEqual(HttpStatusCode.Created, legacyResponse.StatusCode);
                Assert.AreEqual("legacy", legacyResponse.Resource.FullTextPolicy.Package);

                // Test "standard" package.
                FullTextDefaultSpec defaultSpec = new FullTextDefaultSpec
                {
                    Language = "en-US",
                    Tokenizer = "word",
                    Filters = new Collection<string> { "stop", "lowercase" },
                    StopWordListKind = "basic",
                };

                ContainerResponse standardResponse =
                    await databaseForFullText.DefineContainer("standardContainer", "/pk")
                        .WithFullTextPolicy(
                            package: "standard",
                            defaultSpec: defaultSpec,
                            fullTextPaths: new Collection<FullTextPath>
                            {
                                new FullTextPath() { Path = "/text" },
                            })
                        .Attach()
                        .WithIndexingPolicy()
                            .WithFullTextIndex().Path("/text").Attach()
                        .Attach()
                        .CreateAsync();

                Assert.AreEqual(HttpStatusCode.Created, standardResponse.StatusCode);
                Assert.AreEqual("standard", standardResponse.Resource.FullTextPolicy.Package);
            }
            finally
            {
                await databaseForFullText.DeleteAsync();
            }
        }
#endif

        [TestMethod]
        public async Task WithComputedProperties()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            var definitions = new[]
                {
                    new { Name = "lowerName", Query = "SELECT VALUE LOWER(c.name) FROM c" },
                    new { Name = "estimatedTax", Query = "SELECT VALUE c.salary * 0.2 FROM c" }
                };
            ContainerResponse containerResponse =
                await this.database.DefineContainer(containerName, partitionKeyPath)
                    .WithComputedProperties()
                        .WithComputedProperty(definitions[0].Name, definitions[0].Query)
                        .WithComputedProperty(definitions[1].Name, definitions[1].Query)
                        .Attach()
                    .CreateAsync();

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            Assert.AreEqual(2, containerResponse.Resource.ComputedProperties.Count);
            Assert.AreEqual(definitions[0].Name, containerResponse.Resource.ComputedProperties[0].Name);
            Assert.AreEqual(definitions[0].Query, containerResponse.Resource.ComputedProperties[0].Query);
            Assert.AreEqual(definitions[1].Name, containerResponse.Resource.ComputedProperties[1].Name);
            Assert.AreEqual(definitions[1].Query, containerResponse.Resource.ComputedProperties[1].Query);

            Container container = containerResponse;
            containerResponse = await container.ReadContainerAsync();
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            Assert.AreEqual(2, containerResponse.Resource.ComputedProperties.Count);
            Assert.AreEqual(definitions[0].Name, containerResponse.Resource.ComputedProperties[0].Name);
            Assert.AreEqual(definitions[0].Query, containerResponse.Resource.ComputedProperties[0].Query);
            Assert.AreEqual(definitions[1].Name, containerResponse.Resource.ComputedProperties[1].Name);
            Assert.AreEqual(definitions[1].Query, containerResponse.Resource.ComputedProperties[1].Query);

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task ThroughputTest()
        {
            int expectedThroughput = 2400;
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse
                = await this.database.DefineContainer(containerName, partitionKeyPath)
                        .CreateAsync(expectedThroughput);

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Container container = this.database.GetContainer(containerName);

            int? readThroughput = await container.ReadThroughputAsync();
            Assert.IsNotNull(readThroughput);
            Assert.AreEqual(expectedThroughput, readThroughput);

            containerResponse = await container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task ThroughputResponseTest()
        {
            int expectedThroughput = 2400;
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse
                = await this.database.DefineContainer(containerName, partitionKeyPath)
                        .CreateAsync(expectedThroughput);

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Container container = this.database.GetContainer(containerName);

            ThroughputResponse readThroughput = await container.ReadThroughputAsync(new RequestOptions());
            Assert.IsNotNull(readThroughput);
            Assert.AreEqual(expectedThroughput, readThroughput.Resource.Throughput);

            // Implicit conversion 
            ThroughputProperties throughputProperties = await container.ReadThroughputAsync(new RequestOptions());
            Assert.IsNotNull(throughputProperties);
            Assert.AreEqual(expectedThroughput, throughputProperties.Throughput);

            // simple API
            int? throughput = await container.ReadThroughputAsync();
            Assert.IsNotNull(throughput);
            Assert.AreEqual(expectedThroughput, throughput);

            containerResponse = await container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task TimeToLiveTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";
            int timeToLiveInSeconds = 10;
            ContainerResponse containerResponse = await this.database.DefineContainer(containerName, partitionKeyPath)
                .WithDefaultTimeToLive(timeToLiveInSeconds)
                .CreateAsync();

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Container container = containerResponse;
            ContainerProperties responseSettings = containerResponse;

            Assert.AreEqual(timeToLiveInSeconds, responseSettings.DefaultTimeToLive);

            ContainerResponse readResponse = await container.ReadContainerAsync();
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(timeToLiveInSeconds, readResponse.Resource.DefaultTimeToLive);

            JObject itemTest = JObject.FromObject(new { id = Guid.NewGuid().ToString(), users = "testUser42" });
            ItemResponse<JObject> createResponse = await container.CreateItemAsync<JObject>(item: itemTest);
            JObject responseItem = createResponse;
            Assert.IsNull(responseItem["ttl"]);

            containerResponse = await container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task NoPartitionedCreateFail()
        {
            string containerName = Guid.NewGuid().ToString();
            try
            {
                await this.database.DefineContainer(containerName, null)
                    .CreateAsync();
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }
        }

        [TestMethod]
        public async Task TimeToLivePropertyPath()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/user";
            int timeToLivetimeToLiveInSeconds = 10;

            ContainerResponse containerResponse;
            try
            {
                containerResponse = await this.database.DefineContainer(containerName, partitionKeyPath)
                    .WithTimeToLivePropertyPath("/creationDate")
                    .CreateAsync();
                Assert.Fail("CreateCollection with TtlPropertyPath and with no DefaultTimeToLive should have failed.");
            }
            catch (CosmosException exeption)
            {
                // expected because DefaultTimeToLive was not specified
                Assert.AreEqual(HttpStatusCode.BadRequest, exeption.StatusCode);
            }

            // Verify the container content.
            containerResponse = await this.database.DefineContainer(containerName, partitionKeyPath)
                   .WithTimeToLivePropertyPath("/creationDate")
                   .WithDefaultTimeToLive(timeToLivetimeToLiveInSeconds)
                   .CreateAsync();
            Container container = containerResponse;
            Assert.AreEqual(timeToLivetimeToLiveInSeconds, containerResponse.Resource.DefaultTimeToLive);
#pragma warning disable 0612
            Assert.AreEqual("/creationDate", containerResponse.Resource.TimeToLivePropertyPath);
#pragma warning restore 0612

            //Creating an item and reading before expiration
            var payload = new { id = "testId", user = "testUser", creationDate = ToEpoch(DateTime.UtcNow) };
            ItemResponse<dynamic> createItemResponse = await container.CreateItemAsync<dynamic>(payload);
            Assert.IsNotNull(createItemResponse.Resource);
            Assert.AreEqual(createItemResponse.StatusCode, HttpStatusCode.Created);
            ItemResponse<dynamic> readItemResponse = await container.ReadItemAsync<dynamic>(payload.id, new PartitionKey(payload.user));
            Assert.IsNotNull(readItemResponse.Resource);
            Assert.AreEqual(readItemResponse.StatusCode, HttpStatusCode.OK);

            containerResponse = await container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task WithClientEncryptionPolicyTest()
        {
            // create ClientEncryptionKeys
            DatabaseInlineCore databaseInlineCore = (DatabaseInlineCore)this.database;
            await TestCommon.CreateClientEncryptionKey("dekId1", databaseInlineCore);
            await TestCommon.CreateClientEncryptionKey("dekId2", databaseInlineCore);

            // version 2
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";
            ClientEncryptionIncludedPath path1 = new ClientEncryptionIncludedPath()
            {
                Path = partitionKeyPath,
                ClientEncryptionKeyId = "dekId1",
                EncryptionType = "Deterministic",
                EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256"
            };

            ClientEncryptionIncludedPath path2 = new ClientEncryptionIncludedPath()
            {
                Path = "/id",
                ClientEncryptionKeyId = "dekId2",
                EncryptionType = "Deterministic",
                EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
            };

            ContainerResponse containerResponse = await this.database.DefineContainer(containerName, partitionKeyPath)
                .WithClientEncryptionPolicy(policyFormatVersion:2)
                .WithIncludedPath(path1)
                .WithIncludedPath(path2)
                .Attach()
                .CreateAsync();

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Container container = containerResponse;
            ContainerProperties responseSettings = containerResponse;

            Assert.IsNotNull(responseSettings.ClientEncryptionPolicy);
            Assert.AreEqual(2, responseSettings.ClientEncryptionPolicy.IncludedPaths.Count());
            ClientEncryptionIncludedPath clientEncryptionIncludedPath = responseSettings.ClientEncryptionPolicy.IncludedPaths.First();
            Assert.IsTrue(this.VerifyClientEncryptionIncludedPath(path1, clientEncryptionIncludedPath));
            clientEncryptionIncludedPath = responseSettings.ClientEncryptionPolicy.IncludedPaths.Last();
            Assert.IsTrue(this.VerifyClientEncryptionIncludedPath(path2, clientEncryptionIncludedPath));

            ContainerResponse readResponse = await container.ReadContainerAsync();
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.IsNotNull(readResponse.Resource.ClientEncryptionPolicy);

            // version 1
            containerName = Guid.NewGuid().ToString();
            partitionKeyPath = "/users";
            path1 = new ClientEncryptionIncludedPath()
            {
                Path = "/path1",
                ClientEncryptionKeyId = "dekId1",
                EncryptionType = "Randomized",
                EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256"
            };

            path2 = new ClientEncryptionIncludedPath()
            {
                Path = "/path2",
                ClientEncryptionKeyId = "dekId2",
                EncryptionType = "Randomized",
                EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
            };
            
            containerResponse = await this.database.DefineContainer(containerName, partitionKeyPath)
                .WithClientEncryptionPolicy()
                .WithIncludedPath(path1)
                .WithIncludedPath(path2)
                .Attach()
                .CreateAsync();

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            container = containerResponse;
            responseSettings = containerResponse;

            Assert.IsNotNull(responseSettings.ClientEncryptionPolicy);
            Assert.AreEqual(2, responseSettings.ClientEncryptionPolicy.IncludedPaths.Count());
            clientEncryptionIncludedPath = responseSettings.ClientEncryptionPolicy.IncludedPaths.First();
            Assert.IsTrue(this.VerifyClientEncryptionIncludedPath(path1, clientEncryptionIncludedPath));
            clientEncryptionIncludedPath = responseSettings.ClientEncryptionPolicy.IncludedPaths.Last();
            Assert.IsTrue(this.VerifyClientEncryptionIncludedPath(path2, clientEncryptionIncludedPath));

            readResponse = await container.ReadContainerAsync();
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.IsNotNull(readResponse.Resource.ClientEncryptionPolicy);

            // update CEP and replace container
            readResponse.Resource.ClientEncryptionPolicy = null;
            try
            {
                await container.ReplaceContainerAsync(readResponse.Resource);

                Assert.Fail("ReplaceCollection with update to ClientEncryptionPolicy should have failed.");
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, ex.StatusCode);
                Assert.IsTrue(ex.Message.Contains("'clientEncryptionPolicy' cannot be changed as part of collection replace operation."));
            }
        }

        [TestMethod]
        public async Task WithClientEncryptionPolicyFailureTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";
            ClientEncryptionIncludedPath path1 = new ClientEncryptionIncludedPath()
            {
                ClientEncryptionKeyId = "key1",
                EncryptionType = "random",
                EncryptionAlgorithm = "LegacyAeadAes256CbcHmac256"
            };
            
            // Null value for Path
            try
            {
                ContainerResponse containerResponse = await this.database.DefineContainer(containerName, partitionKeyPath)
                    .WithClientEncryptionPolicy()
                    .WithIncludedPath(path1)
                    .Attach()
                    .CreateAsync();

                Assert.Fail("CreateCollection with invalid ClientEncryptionPolicy should have failed.");
            }
            catch (ArgumentNullException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Parameter 'Path'"));
            }

            path1.Path = "/path";

            // Invalid EncryptionType
            try
            {
                ContainerResponse containerResponse = await this.database.DefineContainer(containerName, partitionKeyPath)
                    .WithClientEncryptionPolicy()
                    .WithIncludedPath(path1)
                    .Attach()
                    .CreateAsync();

                Assert.Fail("CreateCollection with invalid ClientEncryptionPolicy should have failed.");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("EncryptionType should be either 'Deterministic' or 'Randomized'. "));
            }

            path1.EncryptionType = "Plaintext";

            // Invalid EncryptionType
            try
            {
                ContainerResponse containerResponse = await this.database.DefineContainer(containerName, partitionKeyPath)
                    .WithClientEncryptionPolicy()
                    .WithIncludedPath(path1)
                    .Attach()
                    .CreateAsync();

                Assert.Fail("CreateCollection with invalid ClientEncryptionPolicy should have failed.");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("EncryptionType should be either 'Deterministic' or 'Randomized'. "));
            }

            path1.EncryptionType = "Deterministic";

            // Invalid EncryptionAlgorithm
            try
            {
                ContainerResponse containerResponse = await this.database.DefineContainer(containerName, partitionKeyPath)
                    .WithClientEncryptionPolicy()
                    .WithIncludedPath(path1)
                    .Attach()
                    .CreateAsync();

                Assert.Fail("CreateCollection with invalid ClientEncryptionPolicy should have failed.");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("EncryptionAlgorithm should be 'AEAD_AES_256_CBC_HMAC_SHA256'. "));
            }

            // invalid policy version for partition key encryption
            path1.EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256";
            path1.Path = partitionKeyPath;
            try
            {
                ContainerResponse containerResponse = await this.database.DefineContainer(containerName, partitionKeyPath)
                    .WithClientEncryptionPolicy()
                    .WithIncludedPath(path1)
                    .Attach()
                    .CreateAsync();

                Assert.Fail("CreateCollection with invalid ClientEncryptionPolicy should have failed.");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Path: /users which is part of the partition key cannot be encrypted with PolicyFormatVersion: 1. Please use PolicyFormatVersion: 2."), ex.Message);
            }

            // invalid policy version for id encryption
            path1.Path = "/id";
            try
            {
                ContainerResponse containerResponse = await this.database.DefineContainer(containerName, partitionKeyPath)
                    .WithClientEncryptionPolicy()
                    .WithIncludedPath(path1)
                    .Attach()
                    .CreateAsync();

                Assert.Fail("CreateCollection with invalid ClientEncryptionPolicy should have failed.");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Path: /id cannot be encrypted with PolicyFormatVersion: 1. Please use PolicyFormatVersion: 2."), ex.Message);
            }

            // invalid encryption type for id encryption
            path1.EncryptionType = "Randomized";
            path1.Path = partitionKeyPath;
            try
            {
                ContainerResponse containerResponse = await this.database.DefineContainer(containerName, partitionKeyPath)
                    .WithClientEncryptionPolicy(policyFormatVersion:2)
                    .WithIncludedPath(path1)
                    .Attach()
                    .CreateAsync();

                Assert.Fail("CreateCollection with invalid ClientEncryptionPolicy should have failed.");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Path: /users which is part of the partition key has to be encrypted with Deterministic type Encryption."), ex.Message);
            }

            // invalid encryption type for id encryption
            path1.Path = "/id";
            try
            {
                ContainerResponse containerResponse = await this.database.DefineContainer(containerName, partitionKeyPath)
                    .WithClientEncryptionPolicy(policyFormatVersion:2)
                    .WithIncludedPath(path1)
                    .Attach()
                    .CreateAsync();

                Assert.Fail("CreateCollection with invalid ClientEncryptionPolicy should have failed.");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Only Deterministic encryption type is supported for path: /id."), ex.Message);
            }
        }

        [TestMethod]
        [DataRow("en-US")]
        [DataRow("fr-FR")]
        [DataRow("de-DE")]
        [DataRow("it-IT")]
        [DataRow("pt-BR")]
        [DataRow("pt-PT")]
        [DataRow("es-ES")]
        public async Task TestFullTextSearchPolicyWithAllSupportedDefaultLanguages(string defaultLanguage)
        {
            string fullTextPath1 = "/fts1", fullTextPath2 = "/fts2";

            string databaseName = "TestDatabaseFullTextPolicy";
            string containerName = "TestContainerFullTextPolicy_"+ defaultLanguage;

            CosmosClient client = this.GetClient();

            Database databaseForFullTextSearch = await client.CreateDatabaseIfNotExistsAsync(databaseName);
            try
            {
                string partitionKeyPath = "/pk";

                Collection<FullTextPath> fullTextPaths = new Collection<FullTextPath>()
                {
                    new FullTextPath()
                    {
                        Path = fullTextPath1,
                        Language = defaultLanguage,
                    },
                    new FullTextPath()
                    {
                        Path = fullTextPath2,
                        Language = defaultLanguage,
                    }
                };

                ContainerResponse containerResponse =
                    await databaseForFullTextSearch.DefineContainer(containerName, partitionKeyPath)
                        .WithFullTextPolicy(
                            defaultLanguage: defaultLanguage,
                            fullTextPaths: fullTextPaths)
                        .Attach()
                        .WithIndexingPolicy()
                            .WithFullTextIndex()
                                .Path(fullTextPath1)
                             .Attach()
                            .WithFullTextIndex()
                                .Path(fullTextPath2)
                             .Attach()
                        .Attach()
                        .CreateAsync();

                Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode,
                    $"Failed to create container with default language: {defaultLanguage}");
                Assert.AreEqual(containerName, containerResponse.Resource.Id);
                Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

                ContainerProperties containerSettings = containerResponse.Resource;

                // Validate FullText Policy
                Assert.IsNotNull(containerSettings.FullTextPolicy,
                    $"FullTextPolicy is null for language: {defaultLanguage}");
                Assert.AreEqual(defaultLanguage, containerSettings.FullTextPolicy.DefaultLanguage,
                    $"DefaultLanguage mismatch for: {defaultLanguage}");
                Assert.IsNotNull(containerSettings.FullTextPolicy.FullTextPaths);
                Assert.AreEqual(fullTextPaths.Count, containerSettings.FullTextPolicy.FullTextPaths.Count());

                // Validate each path has the correct language
                foreach (FullTextPath path in containerSettings.FullTextPolicy.FullTextPaths)
                {
                    Assert.AreEqual(defaultLanguage, path.Language,
                        $"Path language mismatch for default language: {defaultLanguage}");
                }

                // Validate Full Text Indexes
                Assert.IsNotNull(containerSettings.IndexingPolicy.FullTextIndexes);
                Assert.AreEqual(fullTextPaths.Count, containerSettings.IndexingPolicy.FullTextIndexes.Count());
                Assert.AreEqual(fullTextPath1, containerSettings.IndexingPolicy.FullTextIndexes[0].Path);
                Assert.AreEqual(fullTextPath2, containerSettings.IndexingPolicy.FullTextIndexes[1].Path);

                // Clean up container after test
                await containerResponse.Container.DeleteContainerAsync();
            }
            finally
            {
                await databaseForFullTextSearch.DeleteAsync();
            }
        }

        private bool VerifyClientEncryptionIncludedPath(ClientEncryptionIncludedPath expected, ClientEncryptionIncludedPath actual)
        {
            return expected.Path == actual.Path &&
                   expected.ClientEncryptionKeyId == actual.ClientEncryptionKeyId &&
                   expected.EncryptionType == actual.EncryptionType &&
                   expected.EncryptionAlgorithm == actual.EncryptionAlgorithm;
        }
    }    
}
