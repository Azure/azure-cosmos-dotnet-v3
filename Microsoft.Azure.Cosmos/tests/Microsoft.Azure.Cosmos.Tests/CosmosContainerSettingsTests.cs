//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
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
            ContainerProperties containerSettings = new ContainerProperties("TestContainer", "/partitionKey");
            Assert.IsNotNull(containerSettings.IndexingPolicy);

            containerSettings.IndexingPolicy = new Cosmos.IndexingPolicy();
            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);

            // HAKC: Work-around till BE fixes defaults 
            containerSettings.ValidateRequiredProperties();
            Assert.AreEqual(1, containerSettings.IndexingPolicy.IncludedPaths.Count);

            Cosmos.IncludedPath defaultEntry = containerSettings.IndexingPolicy.IncludedPaths[0];
            Assert.AreEqual(Cosmos.IndexingPolicy.DefaultPath, defaultEntry.Path);
            Assert.AreEqual(0, defaultEntry.Indexes.Count);

            Assert.IsNotNull(containerSettings.ComputedProperties);
            Assert.AreEqual(0, containerSettings.ComputedProperties.Count);
        }

        [TestMethod]
        public void ValidateSerialization()
        {
            ContainerProperties containerSettings = new ContainerProperties("TestContainer", "/partitionKey");
            Stream basic = MockCosmosUtil.Serializer.ToStream<ContainerProperties>(containerSettings);
            ContainerProperties response = MockCosmosUtil.Serializer.FromStream<ContainerProperties>(basic);
            Assert.AreEqual(containerSettings.Id, response.Id);
            Assert.AreEqual(containerSettings.PartitionKeyPath, response.PartitionKeyPath);
        }

        [TestMethod]
        public void ValidateClientEncryptionPolicyDeserialization()
        {
            Cosmos.ClientEncryptionPolicy policy = MockCosmosUtil.Serializer.FromStream<Cosmos.ClientEncryptionPolicy>(new MemoryStream(
                Encoding.UTF8.GetBytes("{ 'policyFormatVersion': 2, 'newproperty': 'value'  }")));
            Assert.AreEqual(2, policy.PolicyFormatVersion);
        }

        [TestMethod]
        public void DefaultIncludesShouldNotBePopulated()
        {
            ContainerProperties containerSettings = new ContainerProperties("TestContainer", "/partitionKey");
            Assert.IsNotNull(containerSettings.IndexingPolicy);

            // Any exclude path should not auto-generate default indexing
            containerSettings.IndexingPolicy = new Cosmos.IndexingPolicy();
            containerSettings.IndexingPolicy.ExcludedPaths.Add(new Cosmos.ExcludedPath() { Path = "/some" });

            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);
            containerSettings.ValidateRequiredProperties();
            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);

            // None indexing mode should not auto-generate the default indexing 
            containerSettings.IndexingPolicy = new Cosmos.IndexingPolicy
            {
                IndexingMode = Cosmos.IndexingMode.None
            };

            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);
            containerSettings.ValidateRequiredProperties();
            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);
        }

        [TestMethod]
        public void DefaultSameAsDocumentCollection()
        {
            ContainerProperties containerSettings = new ContainerProperties("TestContainer", "/partitionKey");

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
        [Ignore("This test will be enabled once the V2 DocumentCollection starts supporting the full text indexing policy.")]
        public void DefaultIndexingPolicySameAsDocumentCollection()
        {
            ContainerProperties containerSettings = new ContainerProperties("TestContainer", "/partitionKey")
            {
                IndexingPolicy = new Cosmos.IndexingPolicy()
            };

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
            _ = dc.IndexingPolicy;

            CosmosContainerSettingsTests.AssertSerializedPayloads(containerSettings, dc);
        }

        [TestMethod]
        public void ValidateIncludedPathSerialization()
        {
            ContainerProperties containerSettings = new ContainerProperties("TestContainer", "/partitionKey")
            {
                IndexingPolicy = new Cosmos.IndexingPolicy()
            };

            containerSettings.IndexingPolicy.IncludedPaths.Add(new Cosmos.IncludedPath()
            {
                Path = "/textprop/?",
            });
            containerSettings.IndexingPolicy.IncludedPaths.Add(new Cosmos.IncludedPath()
            {
                Path = "/listprop/?",
                IsFullIndex = true,
            });

            using (Stream stream = MockCosmosUtil.Serializer.ToStream<ContainerProperties>(containerSettings))
            {
                StreamReader reader = new StreamReader(stream);
                string content = reader.ReadToEnd();

                Match match = Regex.Match(content, "\"includedPaths\":\\[(.+?)\\],\"excludedPaths\"");
                Assert.IsTrue(match.Success, "IncludedPaths not found in serialized content");

                // verify IncludedPath ignores null IsFullIndex
                string includedPaths = match.Groups[1].Value;
                string delimiter = "},{";
                int position = includedPaths.IndexOf(delimiter);
                string textPropIncludedPath = includedPaths[..(position + 1)];
                string listPropIncludedPath = includedPaths[(position + delimiter.Length - 1)..];

                Assert.AreEqual("{\"path\":\"/textprop/?\",\"indexes\":[]}", textPropIncludedPath);
                Assert.AreEqual("{\"path\":\"/listprop/?\",\"indexes\":[],\"isFullIndex\":true}", listPropIncludedPath);

                // verify deserialization
                stream.Position = 0;
                containerSettings = MockCosmosUtil.Serializer.FromStream<ContainerProperties>(stream);
                Assert.IsNull(containerSettings.IndexingPolicy.IncludedPaths[0].IsFullIndex, "textprop IsFullIndex is not null");
                Assert.IsTrue((bool)containerSettings.IndexingPolicy.IncludedPaths[1].IsFullIndex, "listprop IsFullIndex is not set to true");
            }
        }

        [TestMethod]
        public void SettingPKShouldNotResetVersion()
        {
            ContainerProperties containerProperties = new()
            {
                Id = "test",
                PartitionKeyDefinitionVersion = Cosmos.PartitionKeyDefinitionVersion.V2,
                PartitionKeyPath = "/id"
            };

            Assert.AreEqual(Cosmos.PartitionKeyDefinitionVersion.V2, containerProperties.PartitionKeyDefinitionVersion);
        }

        [TestMethod]
        public void ValidateVectorEmbeddingsAndIndexes()
        {
            Cosmos.Embedding embedding1 = new()
            {
                Path = "/vector1",
                DataType = Cosmos.VectorDataType.Int8,
                DistanceFunction = Cosmos.DistanceFunction.DotProduct,
                Dimensions = 1200,
            };

            Cosmos.Embedding embedding2 = new()
            {
                Path = "/vector2",
                DataType = Cosmos.VectorDataType.Uint8,
                DistanceFunction = Cosmos.DistanceFunction.Cosine,
                Dimensions = 3,
            };

            Cosmos.Embedding embedding3 = new()
            {
                Path = "/vector3",
                DataType = Cosmos.VectorDataType.Float32,
                DistanceFunction = Cosmos.DistanceFunction.Euclidean,
                Dimensions = 400,
            };

            Collection<Cosmos.Embedding> embeddings = new Collection<Cosmos.Embedding>()
            {
                embedding1,
                embedding2,
                embedding3,
            };

            ContainerProperties containerSettings = new ContainerProperties(id: "TestContainer", partitionKeyPath: "/partitionKey")
            {
                VectorEmbeddingPolicy = new(embeddings),
                IndexingPolicy = new Cosmos.IndexingPolicy()
                {
                    VectorIndexes = new()
                    {
                        new Cosmos.VectorIndexPath()
                        {
                            Path = "/vector1",
                            Type = Cosmos.VectorIndexType.Flat,
                        },
                        new Cosmos.VectorIndexPath()
                        {
                            Path = "/vector2",
                            Type = Cosmos.VectorIndexType.QuantizedFlat,
                            VectorIndexShardKey = new[] { "/Country" },
                            QuantizationByteSize = 3,
                        },
                        new Cosmos.VectorIndexPath()
                        {
                            Path = "/vector3",
                            Type = Cosmos.VectorIndexType.DiskANN,
                            VectorIndexShardKey = new[] { "/ZipCode" },
                            QuantizationByteSize = 2,
                            IndexingSearchListSize = 5,
                        }
                    },
                },
            };

            Assert.IsNotNull(containerSettings.IndexingPolicy);
            Assert.IsNotNull(containerSettings.VectorEmbeddingPolicy);
            Assert.IsNotNull(containerSettings.IndexingPolicy.VectorIndexes);

            Cosmos.VectorEmbeddingPolicy embeddingPolicy = containerSettings.VectorEmbeddingPolicy;
            Assert.IsNotNull(embeddingPolicy.Embeddings);
            Assert.AreEqual(embeddings.Count, embeddingPolicy.Embeddings.Count());
            CollectionAssert.AreEquivalent(embeddings, embeddingPolicy.Embeddings.ToList());

            Collection<Cosmos.VectorIndexPath> vectorIndexes = containerSettings.IndexingPolicy.VectorIndexes;
            Assert.AreEqual("/vector1", vectorIndexes[0].Path);
            Assert.AreEqual(Cosmos.VectorIndexType.Flat, vectorIndexes[0].Type);
            Assert.AreEqual("/vector2", vectorIndexes[1].Path);
            Assert.AreEqual(Cosmos.VectorIndexType.QuantizedFlat, vectorIndexes[1].Type);
            Assert.AreEqual(3, vectorIndexes[1].QuantizationByteSize);
            CollectionAssert.AreEqual(new string[] { "/Country" }, vectorIndexes[1].VectorIndexShardKey);

            Assert.AreEqual("/vector3", vectorIndexes[2].Path);
            Assert.AreEqual(Cosmos.VectorIndexType.DiskANN, vectorIndexes[2].Type);
            Assert.AreEqual(2, vectorIndexes[2].QuantizationByteSize);
            Assert.AreEqual(5, vectorIndexes[2].IndexingSearchListSize);
            CollectionAssert.AreEqual(new string[] { "/ZipCode" }, vectorIndexes[2].VectorIndexShardKey);
        }

        [TestMethod]
        public void ValidateFullTextPathsAndIndexes()
        {
            string defaultLanguage = "en-US", fullTextPath1 = "/fts1", fullTextPath2 = "/fts2", fullTextPath3 = "/fts3";

            Collection<FullTextPath> fullTextPaths = new Collection<FullTextPath>()
                {
                    new Cosmos.FullTextPath()
                    {
                        Path = fullTextPath1,
                        Language = "en-US",
                    },
                    new Cosmos.FullTextPath()
                    {
                        Path = fullTextPath2,
                        Language = "en-US",
                    },
                    new Cosmos.FullTextPath()
                    {
                        Path = fullTextPath3,
                        Language = "en-US",
                    },
                };

            ContainerProperties containerSettings = new ContainerProperties(id: "TestContainer", partitionKeyPath: "/partitionKey")
            {
                FullTextPolicy = new()
                {
                    DefaultLanguage = defaultLanguage,
                    FullTextPaths = fullTextPaths
                },
                IndexingPolicy = new Cosmos.IndexingPolicy()
                {
                    FullTextIndexes = new()
                    {
                        new Cosmos.FullTextIndexPath()
                        {
                            Path = fullTextPath1,
                        },
                        new Cosmos.FullTextIndexPath()
                        {
                            Path = fullTextPath2,
                        },
                        new Cosmos.FullTextIndexPath()
                        {
                            Path = fullTextPath3,
                        }
                    },
                },
            };

            Assert.IsNotNull(containerSettings.IndexingPolicy);
            Assert.IsNotNull(containerSettings.FullTextPolicy);
            Assert.IsNotNull(containerSettings.IndexingPolicy.FullTextIndexes);

            Cosmos.FullTextPolicy fullTextPolicy = containerSettings.FullTextPolicy;
            Assert.IsNotNull(fullTextPolicy.FullTextPaths);
            Assert.AreEqual(fullTextPaths.Count, fullTextPolicy.FullTextPaths.Count());
            Assert.AreEqual(fullTextPaths[0].Path, fullTextPolicy.FullTextPaths[0].Path);
            Assert.AreEqual(fullTextPaths[0].Language, fullTextPolicy.FullTextPaths[0].Language);

            CollectionAssert.AreEquivalent(fullTextPaths, fullTextPolicy.FullTextPaths.ToList());

            Collection<Cosmos.FullTextIndexPath> fullTextIndexes = containerSettings.IndexingPolicy.FullTextIndexes;
            Assert.AreEqual("/fts1", fullTextIndexes[0].Path);
            Assert.AreEqual("/fts2", fullTextIndexes[1].Path);
            Assert.AreEqual("/fts3", fullTextIndexes[2].Path);
        }

        [TestMethod]
        public void ValidateFullTextPathsAndIndexesWithDefaultLanguage()
        {
            string defaultLanguage = "en-US", fullTextPath1 = "/fts1", fullTextPath2 = "/fts2", fullTextPath3 = "/fts3";
            ContainerProperties containerSettings = new ContainerProperties(id: "TestContainer", partitionKeyPath: "/partitionKey")
            {
                FullTextPolicy = new()
                {
                    DefaultLanguage = defaultLanguage,
                    FullTextPaths = new Collection<FullTextPath>()
                },
                IndexingPolicy = new Cosmos.IndexingPolicy()
                {
                    FullTextIndexes = new()
                    {
                        new Cosmos.FullTextIndexPath()
                        {
                            Path = fullTextPath1,
                        },
                        new Cosmos.FullTextIndexPath()
                        {
                            Path = fullTextPath2,
                        },
                        new Cosmos.FullTextIndexPath()
                        {
                            Path = fullTextPath3,
                        }
                    },
                },
            };

            Assert.IsNotNull(containerSettings.IndexingPolicy);
            Assert.IsNotNull(containerSettings.FullTextPolicy);
            Assert.IsNotNull(containerSettings.IndexingPolicy.FullTextIndexes);

            Cosmos.FullTextPolicy fullTextPolicy = containerSettings.FullTextPolicy;
            Assert.AreEqual(0, fullTextPolicy.FullTextPaths.Count);

            Collection<Cosmos.FullTextIndexPath> fullTextIndexes = containerSettings.IndexingPolicy.FullTextIndexes;
            Assert.AreEqual("/fts1", fullTextIndexes[0].Path);
            Assert.AreEqual("/fts2", fullTextIndexes[1].Path);
            Assert.AreEqual("/fts3", fullTextIndexes[2].Path);
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

        private static void AssertSerializedPayloads(ContainerProperties settings, DocumentCollection documentCollection)
        {
            JsonSerializerSettings jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            // HAKC: Work-around till BE fixes defaults 
            settings.ValidateRequiredProperties();

            string containerSerialized = JsonConvert.SerializeObject(settings, jsonSettings);
            string collectionSerialized = CosmosContainerSettingsTests.SerializeDocumentCollection(documentCollection);

            JObject containerJObject = JObject.Parse(containerSerialized);
            JObject collectionJObject = JObject.Parse(collectionSerialized);

            Assert.AreEqual(JsonConvert.SerializeObject(OrderProeprties(collectionJObject)), JsonConvert.SerializeObject(OrderProeprties(containerJObject)));
        }

        private static JObject OrderProeprties(JObject input)
        {
            System.Collections.Generic.List<JProperty> props = input.Properties().ToList();
            foreach (JProperty prop in props)
            {
                prop.Remove();
            }

            foreach (JProperty prop in props.OrderBy(p => p.Name))
            {
                input.Add(prop);
                if (prop.Value is JObject)
                {
                    OrderProeprties((JObject)prop.Value);
                }
            }

            return input;
        }
    }
}