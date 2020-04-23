//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Tests
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosContainerSettingsTests
    {
        [TestMethod]
        public void DefaultIncludesPopulated()
        {
            CosmosContainerProperties containerSettings = new CosmosContainerProperties("TestContainer", "/partitionKey");
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
        public void ValidateSerialization()
        {
            CosmosContainerProperties containerSettings = new CosmosContainerProperties("TestContainer", "/partitionKey");
            Stream basic = MockCosmosUtil.Serializer.ToStream<CosmosContainerProperties>(containerSettings);
            CosmosContainerProperties response = MockCosmosUtil.Serializer.FromStream<CosmosContainerProperties>(basic);
            Assert.AreEqual(containerSettings.Id, response.Id);
            Assert.AreEqual(containerSettings.PartitionKeyPath, response.PartitionKeyPath);
        }

        [TestMethod]
        public void DefaultIncludesShouldNotBePopulated()
        {
            CosmosContainerProperties containerSettings = new CosmosContainerProperties("TestContainer", "/partitionKey");
            Assert.IsNotNull(containerSettings.IndexingPolicy);

            // Any exclude path should not auto-generate default indexing
            containerSettings.IndexingPolicy = new IndexingPolicy();
            containerSettings.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath() { Path = "/some" });

            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);
            containerSettings.ValidateRequiredProperties();
            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);

            // None indexing mode should not auto-generate the default indexing 
            containerSettings.IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.None
            };

            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);
            containerSettings.ValidateRequiredProperties();
            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);
        }

        [TestMethod]
        public void DefaultSameAsDocumentCollection()
        {
            CosmosContainerProperties containerSettings = new CosmosContainerProperties("TestContainer", "/partitionKey");

            Microsoft.Azure.Documents.DocumentCollection dc = new Microsoft.Azure.Documents.DocumentCollection()
            {
                Id = "TestContainer",
                PartitionKey = new Microsoft.Azure.Documents.PartitionKeyDefinition()
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
            CosmosContainerProperties containerSettings = new CosmosContainerProperties("TestContainer", "/partitionKey")
            {
                IndexingPolicy = new IndexingPolicy()
            };

            Microsoft.Azure.Documents.DocumentCollection dc = new Microsoft.Azure.Documents.DocumentCollection()
            {
                Id = "TestContainer",
                PartitionKey = new Microsoft.Azure.Documents.PartitionKeyDefinition()
                {
                    Paths = new Collection<string>()
                    {
                        "/partitionKey"
                    }
                }
            };
            Microsoft.Azure.Documents.IndexingPolicy ip = dc.IndexingPolicy;

            CosmosContainerSettingsTests.AssertSerializedPayloads(containerSettings, dc);
        }

        private static string SerializeDocumentCollection(Microsoft.Azure.Documents.DocumentCollection collection)
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

        private static void AssertSerializedPayloads(CosmosContainerProperties settings, Microsoft.Azure.Documents.DocumentCollection documentCollection)
        {
            // HAKC: Work-around till BE fixes defautls 
            settings.ValidateRequiredProperties();
            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions();
            CosmosTextJsonSerializer.InitializeRESTConverters(jsonSerializerOptions);
            string containerSerialized = JsonSerializer.Serialize(settings, jsonSerializerOptions);
            string collectionSerialized = CosmosContainerSettingsTests.SerializeDocumentCollection(documentCollection);

            Dictionary<string, object> containerJObject = JsonSerializer.Deserialize<Dictionary<string, object>>(containerSerialized, jsonSerializerOptions);
            Dictionary<string, object> collectionJObject = JsonSerializer.Deserialize<Dictionary<string, object>>(collectionSerialized, jsonSerializerOptions);

            CollectionAssert.AreEqual(containerJObject.Keys.OrderBy( k => k).ToList(), collectionJObject.Keys.OrderBy(k => k).ToList());
        }
    }
}
