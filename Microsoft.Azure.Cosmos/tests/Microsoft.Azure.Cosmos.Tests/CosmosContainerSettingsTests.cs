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
