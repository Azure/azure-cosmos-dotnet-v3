//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class CosmosContainerSettingsTests
    {
        [TestMethod]
        public void DefaultSerialization()
        {
            CosmosContainerSettings containerSettings =
                new CosmosContainerSettings("TestContainer", "/partitionKey")
                .WithIncludeIndexPath("/includepath1")
                .WithIncludeIndexPath("/includepath2")
                .WithExcludeIndexPath("/excludepath1")
                .WithExcludeIndexPath("/excludepath2")
                .WithCompositeIndex("/compPath1", "/compPath2")
                .WithCompositeIndex(CompositePath.Create("/property1", CompositePathSortOrder.Descending),
                    CompositePath.Create("/property2", CompositePathSortOrder.Descending))
                .WithUniqueKey("/uniqueueKey1", "/uniqueueKey2")
                .WithSpatialIndex("/spatialPath", SpatialType.Point);
        }

        [TestMethod]
        public void TestDefaultTtl()
        {
            CosmosContainerSettings containerSettings =
                new CosmosContainerSettings("TestContainer", "/partitionKey")
                .WithDefaultTimeToLive(TimeSpan.FromHours(1));

            Assert.AreEqual(containerSettings.DefaultTimeToLive, TimeSpan.FromHours(1).TotalSeconds);

            containerSettings =
                new CosmosContainerSettings("TestContainer", "/partitionKey")
                .WithDefaultTimeToLive(defaulTtlInSeconds: -1);

            Assert.AreEqual(-1, TimeSpan.FromHours(1).TotalSeconds);
        }

        [TestMethod]
        public void V2Way()
        {
            IndexingPolicy ip = new IndexingPolicy();
            ip.IncludedPaths.Add(new IncludedPath() { Path = "/includepath1", Indexes = CosmosContainerSettings.DefaultIndexes});
            ip.ExcludedPaths.Add(new ExcludedPath() { Path = "/excludepath1" });

            Collection<CompositePath> compositePath = new Collection<CompositePath>();
            compositePath.Add(new CompositePath() { Path = "/compositepath1", Order = CompositePathSortOrder.Ascending });
            compositePath.Add(new CompositePath() { Path = "/compositepath2", Order = CompositePathSortOrder.Ascending });
            ip.CompositeIndexes.Add(compositePath);

            SpatialSpec spatialSpec = new SpatialSpec();
            spatialSpec.Path = "/spatialpath1";
            spatialSpec.SpatialTypes = new Collection<SpatialType>();
            spatialSpec.SpatialTypes.Add(SpatialType.Point);
            ip.SpatialIndexes.Add(spatialSpec);

            UniqueKeyPolicy uniqueKeyPolicy = new UniqueKeyPolicy();
            UniqueKey uniqueKey = new UniqueKey();
            uniqueKey.Paths = new Collection<string>();
            uniqueKey.Paths.Add("/uniqueuekey1");
            uniqueKey.Paths.Add("/uniqueuekey2");
            uniqueKeyPolicy.UniqueKeys.Add(uniqueKey);

            CosmosContainerSettings testContainerSettings = new CosmosContainerSettings("TestContainer", "/partitionKey");
            testContainerSettings.IndexingPolicy = ip;
            testContainerSettings.UniqueKeyPolicy = uniqueKeyPolicy;
        }

        [TestMethod]
        public void DefaultIncludesPopulated()
        {
            CosmosContainerSettings containerSettings = new CosmosContainerSettings("TestContainer", "/partitionKey");
            Assert.IsNotNull(containerSettings.IndexingPolicy);

            containerSettings.IndexingPolicy = new IndexingPolicy();
            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);

            // HAKC: Work-around till BE fixes defautls 
            containerSettings.ValidateRequiredProperties();
            Assert.AreEqual(1, containerSettings.IndexingPolicy.IncludedPaths.Count);

            IncludedPath defaultEntry = containerSettings.IndexingPolicy.IncludedPaths[0];
            Assert.AreEqual(IndexingPolicy.DefaultPath, defaultEntry.Path);
            Assert.AreEqual(0, defaultEntry.Indexes.Count);
        }

        [TestMethod]
        public void DefaultIncludesShouldNotBePopulated()
        {
            CosmosContainerSettings containerSettings = new CosmosContainerSettings("TestContainer", "/partitionKey");
            Assert.IsNotNull(containerSettings.IndexingPolicy);

            // Any exclude path should not auto-generate default indexing
            containerSettings.IndexingPolicy = new IndexingPolicy();
            containerSettings.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath() { Path = "/some" });

            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);
            containerSettings.ValidateRequiredProperties();
            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);

            // None indexing mode should not auto-generate the default indexing 
            containerSettings.IndexingPolicy = new IndexingPolicy();
            containerSettings.IndexingPolicy.IndexingMode = IndexingMode.None;

            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);
            containerSettings.ValidateRequiredProperties();
            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);
        }

        [TestMethod]
        public void DefaultSameAsDocumentCollection()
        {
            CosmosContainerSettings containerSettings = new CosmosContainerSettings("TestContainer", "/partitionKey");

            DocumentCollection dc = new DocumentCollection()
            {
                Id = "TestContainer",
                PartitionKey = new PartitionKeyDefinition()
                {
                    Paths = new Collection<string>()
                    {
                        "/partitionKey"
                    }
                }
            };
            CosmosContainerSettingsTests.AssertSerializedPayloads(containerSettings, dc);
        }

        [TestMethod]
        public void DefaultIndexingPolicySameAsDocumentCollection()
        {
            CosmosContainerSettings containerSettings = new CosmosContainerSettings("TestContainer", "/partitionKey");
            containerSettings.IndexingPolicy = new IndexingPolicy();

            DocumentCollection dc = new DocumentCollection()
            {
                Id = "TestContainer",
                PartitionKey = new PartitionKeyDefinition()
                {
                    Paths = new Collection<string>()
                    {
                        "/partitionKey"
                    }
                }
            };
            Documents.IndexingPolicy ip = dc.IndexingPolicy;

            CosmosContainerSettingsTests.AssertSerializedPayloads(containerSettings, dc);
        }

        [TestMethod]
        public void ValidationTests()
        {
            CosmosContainerSettings containerSettings =
                new CosmosContainerSettings("TestContainer", "/partitionKey");

            CosmosContainerSettingsTests.AssertException<ArgumentNullException>(() => containerSettings.WithIncludeIndexPath(null));
            CosmosContainerSettingsTests.AssertException<ArgumentNullException>(() => containerSettings.WithIncludeIndexPath(string.Empty));

            CosmosContainerSettingsTests.AssertException<ArgumentNullException>(() => containerSettings.WithExcludeIndexPath(null));
            CosmosContainerSettingsTests.AssertException<ArgumentNullException>(() => containerSettings.WithExcludeIndexPath(string.Empty));

            CosmosContainerSettingsTests.AssertException<ArgumentNullException>(() => containerSettings.WithSpatialIndex(null));
            CosmosContainerSettingsTests.AssertException<ArgumentNullException>(() => CompositePath.Create("ABC", CompositePathSortOrder.Descending));

            CosmosContainerSettingsTests.AssertException<ArgumentOutOfRangeException>(() => containerSettings.WithCompositeIndex(string.Empty));
            CosmosContainerSettingsTests.AssertException<ArgumentOutOfRangeException>(() => containerSettings.WithCompositeIndex("abc", null));
            CosmosContainerSettingsTests.AssertException<ArgumentOutOfRangeException>(() => containerSettings.WithCompositeIndex(null, CompositePath.Create("ABC", CompositePathSortOrder.Descending)));
        }

        public static void AssertException<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch(T)
            {
            }
        }

        private static string SerializeDocumentCollection(DocumentCollection collection)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                collection.SaveTo(ms);

                ms.Position = 0;
                using (StreamReader sr = new StreamReader(ms))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        private static void AssertSerializedPayloads(CosmosContainerSettings settings, DocumentCollection documentCollection)
        {
            var jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            // HAKC: Work-around till BE fixes defautls 
            settings.ValidateRequiredProperties();

            string containerSerialized = JsonConvert.SerializeObject(settings, jsonSettings);
            string collectionSerialized = CosmosContainerSettingsTests.SerializeDocumentCollection(documentCollection);

            JObject containerJObject = JObject.Parse(containerSerialized);
            JObject collectionJObject = JObject.Parse(collectionSerialized);

            Assert.AreEqual(JsonConvert.SerializeObject(OrderProeprties(collectionJObject)), JsonConvert.SerializeObject(OrderProeprties(containerJObject)));
        }

        private static JObject OrderProeprties(JObject input)
        {
            var props = input.Properties().ToList();
            foreach (var prop in props)
            {
                prop.Remove();
            }

            foreach (var prop in props.OrderBy(p => p.Name))
            {
                input.Add(prop);
                if (prop.Value is JObject)
                    OrderProeprties((JObject)prop.Value);
            }

            return input;
        }
    }
}
