﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class SpatialTest
    {
        private PartitionKeyDefinition defaultPartitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };

        private class SpatialSampleClass
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }

            public Geometry Location { get; set; }
        }

        private readonly DocumentClient client;

        public SpatialTest()
        {
            this.client = TestCommon.CreateClient(true, defaultConsistencyLevel: ConsistencyLevel.Session);
            this.CleanUp();
        }

        [TestMethod]
        public async Task TestDistanceAndWithin()
        {
            await TestDistanceAndWithin(true);
        }

        [TestMethod]
        public async Task TestIsValid()
        {
            Database database = await this.client.CreateDatabaseAsync(
                new Database { Id = Guid.NewGuid().ToString("N") });

            DocumentCollection collectionDefinition = new DocumentCollection() { Id = Guid.NewGuid().ToString("N"), PartitionKey = defaultPartitionKeyDefinition };
            collectionDefinition.IndexingPolicy = new IndexingPolicy()
            {
                IncludedPaths = new Collection<IncludedPath>()
                    {
                        new IncludedPath()
                        {
                            Path = "/"
                        },
                        new IncludedPath()
                        {
                            Path = "/Location/?",
                            Indexes = new Collection<Documents.Index>()
                            {
                                new SpatialIndex(DataType.Point)
                            }
                        }
                    }
            };

            DocumentCollection collection = await this.client.CreateDocumentCollectionAsync(
                database.SelfLink,
                collectionDefinition);

            var invalidGeometry = new SpatialSampleClass
            {
                Location = new Point(20, 180),
                Id = Guid.NewGuid().ToString()
            };
            await this.client.CreateDocumentAsync(collection.SelfLink, invalidGeometry);

            var validGeometry = new SpatialSampleClass
            {
                Location = new Point(50, 50),
                Id = Guid.NewGuid().ToString()
            };
            await this.client.CreateDocumentAsync(collection.SelfLink, validGeometry);

            IOrderedQueryable<SpatialSampleClass> sampleClasses =
                this.client.CreateDocumentQuery<SpatialSampleClass>(collection.DocumentsLink, new FeedOptions() { EnableCrossPartitionQuery = true });

            SpatialSampleClass[] distanceQuery = sampleClasses
                .Where(f => f.Location.Distance(new Point(20, 180)) < 20000)
                .ToArray();

            Assert.AreEqual(0, distanceQuery.Count());

            SpatialSampleClass[] isNotValidQuery = sampleClasses
                .Where(f => !f.Location.IsValid())
                .ToArray();

            Assert.AreEqual(1, isNotValidQuery.Count());
            Assert.AreEqual(invalidGeometry.Location, isNotValidQuery[0].Location);

            IOrderedQueryable<SpatialSampleClass> invalidDetailed =
                this.client.CreateDocumentQuery<SpatialSampleClass>(collection.DocumentsLink, new FeedOptions() { EnableCrossPartitionQuery = true });

            var query = invalidDetailed
                .Where(f => !f.Location.IsValid()).Select(f => f.Location.IsValidDetailed());
            var isNotValidDetailedQuery = query.ToArray();

            Assert.AreEqual(1, isNotValidDetailedQuery.Count());
            Assert.AreEqual("Latitude values must be between -90 and 90 degrees.", isNotValidDetailedQuery[0].Reason);
            Assert.AreEqual(false, isNotValidDetailedQuery[0].IsValid);

            SpatialSampleClass[] isValidQuery = sampleClasses
                .Where(f => f.Location.IsValid())
                .ToArray();

            Assert.AreEqual(1, isValidQuery.Count());
            Assert.AreEqual(validGeometry.Location, isValidQuery[0].Location);
        }

        [TestMethod]
        public async Task TestDistanceAndWithinUsingIndex()
        {
            await TestDistanceAndWithin(false);
        }

        private async Task TestDistanceAndWithin(bool allowScan)
        {
            Database database = await this.client.CreateDatabaseAsync(new Database { Id = Guid.NewGuid().ToString("N") });

            DocumentCollection collectionDefinition = new DocumentCollection() { Id = Guid.NewGuid().ToString("N"), PartitionKey = defaultPartitionKeyDefinition };

            DocumentCollection collection;
            if (allowScan)
            {
                collection = await this.client.CreateDocumentCollectionAsync(database.SelfLink, collectionDefinition);
            }
            else
            {
                collectionDefinition.IndexingPolicy = new IndexingPolicy()
                {
                    IncludedPaths = new Collection<IncludedPath>()
                    {
                        new IncludedPath()
                        {
                            Path = "/"
                        },
                        new IncludedPath()
                        {
                            Path = "/Location/?",
                            Indexes = new Collection<Documents.Index>()
                            {
                                new SpatialIndex(DataType.Point)
                            }
                        }
                    }
                };

                collection = await this.client.CreateDocumentCollectionAsync(database.SelfLink, collectionDefinition);
            }

            var class1 = new SpatialSampleClass
            {
                Location = new Point(20, 20),
                Id = Guid.NewGuid().ToString()
            };
            await this.client.CreateDocumentAsync(collection.SelfLink, class1);

            var class2 = new SpatialSampleClass
            {
                Location = new Point(100, 100),
                Id = Guid.NewGuid().ToString()
            };
            await this.client.CreateDocumentAsync(collection.SelfLink, class2);

            IOrderedQueryable<SpatialSampleClass> sampleClasses =
                this.client.CreateDocumentQuery<SpatialSampleClass>(collection.DocumentsLink, new FeedOptions() { EnableScanInQuery = allowScan, EnableCrossPartitionQuery = true });

            SpatialSampleClass[] distanceQuery = sampleClasses
                .Where(f => f.Location.Distance(new Point(20.1, 20)) < 20000)
                .ToArray();

            Assert.AreEqual(1, distanceQuery.Count());

            Polygon polygon = new Polygon(
                new[]
                    {
                        new Position(10, 10),
                        new Position(30, 10),
                        new Position(30, 30),
                        new Position(10, 30),
                        new Position(10, 10),
                    });
            SpatialSampleClass[] withinQuery = sampleClasses
                .Where(f => f.Location.Within(polygon))
                .ToArray();

            Assert.AreEqual(1, withinQuery.Count());
        }

        private void CleanUp()
        {
            IEnumerable<Database> allDatabases = from database in this.client.CreateDatabaseQuery() select database;

            foreach (Database database in allDatabases)
            {
                this.client.DeleteDatabaseAsync(database.SelfLink).Wait();
            }
        }
    }
}
