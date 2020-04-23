//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using Azure.Cosmos.Spatial;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Threading.Tasks;

    // Similar tests to CosmosContainerTests but with Fluent syntax
    [TestClass]
    public class ContainerSettingsTests : BaseCosmosClientHelper
    {
        private static long ToEpoch(DateTime dateTime) => (long)(dateTime - new DateTime(1970, 1, 1)).TotalSeconds;

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
            CosmosContainerProperties containerProperties = new CosmosContainerProperties(Guid.NewGuid().ToString(), "/users")
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
                }
            };

            CosmosTextJsonSerializer serializer = CosmosTextJsonSerializer.CreatePropertiesSerializer();
            Stream stream = serializer.ToStream(containerProperties);
            CosmosContainerProperties deserialziedTest = serializer.FromStream<CosmosContainerProperties>(stream);

            CosmosContainerResponse response = await this.database.CreateContainerAsync(containerProperties);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.GetRawResponse().Headers);
            Assert.IsNotNull(response.GetRawResponse().Headers.GetActivityId());
            Assert.IsTrue(response.GetRawResponse().Headers.GetRequestCharge() > 0);

            CosmosContainerProperties responseProperties = response.Value;
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
            Assert.AreEqual(4, spatialPath.SpatialTypes.Count);
        }

        [TestMethod]
        public async Task ContainerNegativeSpatialIndexTest()
        {
            CosmosContainerProperties containerProperties = new CosmosContainerProperties(Guid.NewGuid().ToString(), "/users")
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
                CosmosContainerResponse response = await this.database.CreateContainerAsync(containerProperties);
                Assert.Fail("Should require spatial type");
            }
            catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.BadRequest)
            {
                Assert.IsTrue(ce.Message.Contains("The spatial data types array cannot be empty. Assign at least one spatial type for the 'types' array for the path"));
            }
        }

        //[TestMethod]
        //public async Task ContainerMigrationTest()
        //{
        //    string containerName = "MigrationIndexTest";
        //    Microsoft.Azure.Documents.Index index1 = new Microsoft.Azure.Documents.RangeIndex(Microsoft.Azure.Documents.DataType.String, -1);
        //    Microsoft.Azure.Documents.Index index2 = new Microsoft.Azure.Documents.RangeIndex(Microsoft.Azure.Documents.DataType.Number, -1);
        //    Microsoft.Azure.Documents.DocumentCollection documentCollection = new Microsoft.Azure.Documents.DocumentCollection()
        //    {
        //        Id = containerName,
        //        IndexingPolicy = new Microsoft.Azure.Documents.IndexingPolicy()
        //        {
        //            IncludedPaths = new Collection<Microsoft.Azure.Documents.IncludedPath>()
        //            {
        //                new Microsoft.Azure.Documents.IncludedPath()
        //                {
        //                    Path = "/*",
        //                    Indexes = new Collection<Microsoft.Azure.Documents.Index>()
        //                    {
        //                        index1,
        //                        index2
        //                    }
        //                }
        //            }
        //        }
        //    };

        //    Microsoft.Azure.Documents.DocumentCollection createResponse = await NonPartitionedContainerHelper.CreateNonPartitionedContainer(this.database, documentCollection);

        //    // Verify the collection was created with deprecated Index objects
        //    Assert.AreEqual(2, createResponse.IndexingPolicy.IncludedPaths.First().Indexes.Count);
        //    Microsoft.Azure.Documents.Index createIndex = createResponse.IndexingPolicy.IncludedPaths.First().Indexes.First();
        //    Assert.AreEqual(index1.Kind, createIndex.Kind);

        //    // Verify v3 can add composite indexes and update the container
        //    Container container = this.database.GetContainer(containerName);
        //    CosmosContainerProperties containerProperties = await container.ReadContainerAsync();
        //    string cPath0 = "/address/city";
        //    string cPath1 = "/address/state";
        //    containerProperties.IndexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>()
        //    {
        //        new CompositePath()
        //        {
        //            Path= cPath0,
        //            Order = CompositePathSortOrder.Descending
        //        },
        //        new CompositePath()
        //        {
        //            Path= cPath1,
        //            Order = CompositePathSortOrder.Ascending
        //        }
        //    });

        //    containerProperties.IndexingPolicy.SpatialIndexes.Add(
        //        new SpatialPath()
        //        {
        //            Path = "/address/test/*",
        //            SpatialTypes = new Collection<SpatialType>() { SpatialType.Point }
        //        });

        //    CosmosContainerProperties propertiesAfterReplace = await container.ReplaceContainerAsync(containerProperties);
        //    Assert.AreEqual(0, propertiesAfterReplace.IndexingPolicy.IncludedPaths.First().Indexes.Count);
        //    Assert.AreEqual(1, propertiesAfterReplace.IndexingPolicy.CompositeIndexes.Count);
        //    Collection<CompositePath> compositePaths = propertiesAfterReplace.IndexingPolicy.CompositeIndexes.First();
        //    Assert.AreEqual(2, compositePaths.Count);
        //    CompositePath compositePath0 = compositePaths.ElementAt(0);
        //    CompositePath compositePath1 = compositePaths.ElementAt(1);
        //    Assert.IsTrue(string.Equals(cPath0, compositePath0.Path) || string.Equals(cPath1, compositePath0.Path));
        //    Assert.IsTrue(string.Equals(cPath0, compositePath1.Path) || string.Equals(cPath1, compositePath1.Path));

        //    Assert.AreEqual(1, propertiesAfterReplace.IndexingPolicy.SpatialIndexes.Count);
        //    Assert.AreEqual("/address/test/*", propertiesAfterReplace.IndexingPolicy.SpatialIndexes.First().Path);
        //}

        [TestMethod]
        public async Task PartitionedCRUDTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            CosmosContainerResponse containerResponse =
                await this.database.DefineContainer(containerName, partitionKeyPath)
                    .WithIndexingPolicy()
                        .WithIndexingMode(IndexingMode.None)
                        .WithAutomaticIndexing(false)
                        .Attach()
                    .CreateAsync();

            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(containerName, containerResponse.Value.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Value.PartitionKey.Paths.First());
            CosmosContainer container = containerResponse;
            Assert.AreEqual(IndexingMode.None, containerResponse.Value.IndexingPolicy.IndexingMode);
            Assert.IsFalse(containerResponse.Value.IndexingPolicy.Automatic);

            containerResponse = await container.ReadContainerAsync();
            Assert.AreEqual((int)HttpStatusCode.OK, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(containerName, containerResponse.Value.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Value.PartitionKey.Paths.First());
            Assert.AreEqual(IndexingMode.None, containerResponse.Value.IndexingPolicy.IndexingMode);
            Assert.IsFalse(containerResponse.Value.IndexingPolicy.Automatic);

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual((int)HttpStatusCode.NoContent, containerResponse.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task WithUniqueKeys()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            CosmosContainerResponse containerResponse =
                await this.database.DefineContainer(containerName, partitionKeyPath)
                    .WithUniqueKey()
                        .Path("/attribute1")
                        .Path("/attribute2")
                        .Attach()
                    .CreateAsync();

            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(containerName, containerResponse.Value.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Value.PartitionKey.Paths.First());
            CosmosContainer container = containerResponse;
            Assert.AreEqual(1, containerResponse.Value.UniqueKeyPolicy.UniqueKeys.Count);
            Assert.AreEqual(2, containerResponse.Value.UniqueKeyPolicy.UniqueKeys[0].Paths.Count);
            Assert.AreEqual("/attribute1", containerResponse.Value.UniqueKeyPolicy.UniqueKeys[0].Paths[0]);
            Assert.AreEqual("/attribute2", containerResponse.Value.UniqueKeyPolicy.UniqueKeys[0].Paths[1]);

            containerResponse = await container.ReadContainerAsync();
            Assert.AreEqual((int)HttpStatusCode.OK, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(containerName, containerResponse.Value.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Value.PartitionKey.Paths.First());
            Assert.AreEqual(1, containerResponse.Value.UniqueKeyPolicy.UniqueKeys.Count);
            Assert.AreEqual(2, containerResponse.Value.UniqueKeyPolicy.UniqueKeys[0].Paths.Count);
            Assert.AreEqual("/attribute1", containerResponse.Value.UniqueKeyPolicy.UniqueKeys[0].Paths[0]);
            Assert.AreEqual("/attribute2", containerResponse.Value.UniqueKeyPolicy.UniqueKeys[0].Paths[1]);

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual((int)HttpStatusCode.NoContent, containerResponse.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task TestConflictResolutionPolicy()
        {
            CosmosDatabase databaseForConflicts = await this.cosmosClient.CreateDatabaseAsync("conflictResolutionContainerTest",
                cancellationToken: this.cancellationToken);

            try
            {
                string containerName = "conflictResolutionContainerTest";
                string partitionKeyPath = "/users";

                CosmosContainerResponse containerResponse =
                    await databaseForConflicts.DefineContainer(containerName, partitionKeyPath)
                        .WithConflictResolution()
                            .WithLastWriterWinsResolution("/lww")
                            .Attach()
                        .CreateAsync();

                Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
                Assert.AreEqual(containerName, containerResponse.Value.Id);
                Assert.AreEqual(partitionKeyPath, containerResponse.Value.PartitionKey.Paths.First());
                CosmosContainerProperties containerSettings = containerResponse.Value;
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

                Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
                Assert.AreEqual(containerName, containerResponse.Value.Id);
                Assert.AreEqual(partitionKeyPath, containerResponse.Value.PartitionKey.Paths.First());
                containerSettings = containerResponse.Value;
                Assert.IsNotNull(containerSettings.ConflictResolutionPolicy);
                Assert.AreEqual(ConflictResolutionMode.Custom, containerSettings.ConflictResolutionPolicy.Mode);
                Assert.AreEqual(UriFactory.CreateStoredProcedureUri(databaseForConflicts.Id, containerName, sprocName), containerSettings.ConflictResolutionPolicy.ResolutionProcedure);
                Assert.IsTrue(string.IsNullOrEmpty(containerSettings.ConflictResolutionPolicy.ResolutionPath));
            }
            finally
            {
                await databaseForConflicts.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task WithIndexingPolicy()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            CosmosContainerResponse containerResponse =
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

            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(containerName, containerResponse.Value.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Value.PartitionKey.Paths.First());
            CosmosContainer container = containerResponse;
            Assert.AreEqual(2, containerResponse.Value.IndexingPolicy.IncludedPaths.Count);
            Assert.AreEqual("/included1/*", containerResponse.Value.IndexingPolicy.IncludedPaths[0].Path);
            Assert.AreEqual("/included2/*", containerResponse.Value.IndexingPolicy.IncludedPaths[1].Path);
            Assert.AreEqual("/*", containerResponse.Value.IndexingPolicy.ExcludedPaths[0].Path);
            Assert.AreEqual(1, containerResponse.Value.IndexingPolicy.CompositeIndexes.Count);
            Assert.AreEqual("/composite1", containerResponse.Value.IndexingPolicy.CompositeIndexes[0][0].Path);
            Assert.AreEqual("/composite2", containerResponse.Value.IndexingPolicy.CompositeIndexes[0][1].Path);
            Assert.AreEqual(CompositePathSortOrder.Descending, containerResponse.Value.IndexingPolicy.CompositeIndexes[0][1].Order);

            containerResponse = await container.ReadContainerAsync();
            Assert.AreEqual((int)HttpStatusCode.OK, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(containerName, containerResponse.Value.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Value.PartitionKey.Paths.First());
            Assert.AreEqual(2, containerResponse.Value.IndexingPolicy.IncludedPaths.Count);
            Assert.AreEqual("/included1/*", containerResponse.Value.IndexingPolicy.IncludedPaths[0].Path);
            Assert.AreEqual("/included2/*", containerResponse.Value.IndexingPolicy.IncludedPaths[1].Path);
            Assert.AreEqual("/*", containerResponse.Value.IndexingPolicy.ExcludedPaths[0].Path);
            Assert.AreEqual(1, containerResponse.Value.IndexingPolicy.CompositeIndexes.Count);
            Assert.AreEqual("/composite1", containerResponse.Value.IndexingPolicy.CompositeIndexes[0][0].Path);
            Assert.AreEqual("/composite2", containerResponse.Value.IndexingPolicy.CompositeIndexes[0][1].Path);
            Assert.AreEqual(CompositePathSortOrder.Descending, containerResponse.Value.IndexingPolicy.CompositeIndexes[0][1].Order);

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual((int)HttpStatusCode.NoContent, containerResponse.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task ThroughputTest()
        {
            int expectedThroughput = 2400;
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            CosmosContainerResponse containerResponse
                = await this.database.DefineContainer(containerName, partitionKeyPath)
                        .CreateAsync(expectedThroughput);

            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
            CosmosContainer container = this.database.GetContainer(containerName);

            int? readThroughput = await container.ReadThroughputAsync();
            Assert.IsNotNull(readThroughput);
            Assert.AreEqual(expectedThroughput, readThroughput);

            containerResponse = await container.DeleteContainerAsync();
            Assert.AreEqual((int)HttpStatusCode.NoContent, containerResponse.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task ThroughputResponseTest()
        {
            int expectedThroughput = 2400;
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            CosmosContainerResponse containerResponse
                = await this.database.DefineContainer(containerName, partitionKeyPath)
                        .CreateAsync(expectedThroughput);

            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
            CosmosContainer container = this.database.GetContainer(containerName);

            ThroughputResponse readThroughput = await container.ReadThroughputAsync(new RequestOptions());
            Assert.IsNotNull(readThroughput);
            Assert.AreEqual(expectedThroughput, readThroughput.Value.Throughput);

            // Implicit conversion 
            ThroughputProperties throughputProperties = await container.ReadThroughputAsync(new RequestOptions());
            Assert.IsNotNull(throughputProperties);
            Assert.AreEqual(expectedThroughput, throughputProperties.Throughput);

            // simple API
            int? throughput = await container.ReadThroughputAsync();
            Assert.IsNotNull(throughput);
            Assert.AreEqual(expectedThroughput, throughput);

            containerResponse = await container.DeleteContainerAsync();
            Assert.AreEqual((int)HttpStatusCode.NoContent, containerResponse.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task TimeToLiveTest()
        {
            CultureInfo defaultCultureInfo = System.Threading.Thread.CurrentThread.CurrentCulture;

            CultureInfo[] cultureInfoList = new CultureInfo[]
            {
                defaultCultureInfo,
                System.Globalization.CultureInfo.GetCultureInfo("fr-FR")
            };

            foreach (CultureInfo cultureInfo in cultureInfoList)
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = cultureInfo;
                string containerName = Guid.NewGuid().ToString();
                string partitionKeyPath = "/users";
                int timeToLiveInSeconds = 10;
                CosmosContainerResponse containerResponse = await this.database.DefineContainer(containerName, partitionKeyPath)
                    .WithDefaultTimeToLive(timeToLiveInSeconds)
                    .CreateAsync();

                Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
                CosmosContainer container = containerResponse;
                CosmosContainerProperties responseSettings = containerResponse;

                Assert.AreEqual(timeToLiveInSeconds, responseSettings.DefaultTimeToLive);

                CosmosContainerResponse readResponse = await container.ReadContainerAsync();
                Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
                Assert.AreEqual(timeToLiveInSeconds, readResponse.Value.DefaultTimeToLive);

                Dictionary<string, object> itemTest = new Dictionary<string, object>();
                itemTest["id"] = Guid.NewGuid().ToString();
                itemTest["users"] = "testUser42";
                ItemResponse<Dictionary<string, object>> createResponse = await container.CreateItemAsync<Dictionary<string, object>>(item: itemTest);
                Dictionary<string, object> responseItem = createResponse;
                Assert.IsFalse(responseItem.ContainsKey("ttl"));

                containerResponse = await container.DeleteContainerAsync();
                Assert.AreEqual((int)HttpStatusCode.NoContent, containerResponse.GetRawResponse().Status);
            }
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
    }
}
