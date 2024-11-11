//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class SettingsContractTests
    {
        [TestMethod]
        public void DatabaseSettingsDefaults()
        {
            DatabaseProperties dbSettings = new DatabaseProperties();

            Assert.IsNull(dbSettings.LastModified);
            Assert.IsNull(dbSettings.ResourceId);
            Assert.IsNull(dbSettings.Id);
            Assert.IsNull(dbSettings.ETag);

            SettingsContractTests.TypeAccessorGuard(typeof(DatabaseProperties), "Id");
        }

        [TestMethod]
        public void StoredProecdureSettingsDefaults()
        {
            StoredProcedureProperties dbSettings = new StoredProcedureProperties();

            Assert.IsNull(dbSettings.LastModified);
            Assert.IsNull(dbSettings.ResourceId);
            Assert.IsNull(dbSettings.Id);
            Assert.IsNull(dbSettings.ETag);

            SettingsContractTests.TypeAccessorGuard(typeof(StoredProcedureProperties), "Id", "Body");
        }

        [TestMethod]
        public void ConflictsSettingsDefaults()
        {
            ConflictProperties conflictSettings = new ConflictProperties();

            Assert.IsNull(conflictSettings.ResourceType);
            Assert.AreEqual(Cosmos.OperationKind.Invalid, conflictSettings.OperationKind);
            Assert.IsNull(conflictSettings.Id);

            SettingsContractTests.TypeAccessorGuard(typeof(ConflictProperties), "Id", "OperationKind", "ResourceType", "SourceResourceId");
        }

        [TestMethod]
        public void OperationKindMatchesDirect()
        {
            this.AssertEnums<Cosmos.OperationKind, Documents.OperationKind>();
        }

        [TestMethod]
        public void TriggerOperationMatchesDirect()
        {
            this.AssertEnumsContains<Cosmos.Scripts.TriggerOperation, Documents.TriggerOperation>();
        }

        [TestMethod]
        public void DatabaseStreamDeserialzieTest()
        {
            string dbId = "946ad017-14d9-4cee-8619-0cbc62414157";
            string rid = "vu9cAA==";
            string self = "dbs\\/vu9cAA==\\/";
            string etag = "00000000-0000-0000-f8ea-31d6e5f701d4";
            double ts = 1555923784;

            DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            DateTime expected = UnixStartTime.AddSeconds(ts);

            string testPyaload = "{\"id\":\"" + dbId
                    + "\",\"_rid\":\"" + rid
                    + "\",\"_self\":\"" + self
                    + "\",\"_etag\":\"" + etag
                    + "\",\"_colls\":\"colls\\/\",\"_users\":\"users\\/\",\"_ts\":" + ts + "}";

            DatabaseProperties deserializedPayload =
                JsonConvert.DeserializeObject<DatabaseProperties>(testPyaload);

            Assert.IsTrue(deserializedPayload.LastModified.HasValue);
            Assert.AreEqual(expected, deserializedPayload.LastModified.Value);
            Assert.AreEqual(dbId, deserializedPayload.Id);
            Assert.AreEqual(rid, deserializedPayload.ResourceId);
            Assert.AreEqual(etag, deserializedPayload.ETag);
        }

        [TestMethod]
        public void ContainerStreamDeserialzieTest()
        {
            string colId = "946ad017-14d9-4cee-8619-0cbc62414157";
            string rid = "vu9cAA==";
            string self = "dbs\\/vu9cAA==\\/cols\\/abc==\\/";
            string etag = "00000000-0000-0000-f8ea-31d6e5f701d4";
            double ts = 1555923784;

            DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            DateTime expected = UnixStartTime.AddSeconds(ts);

            string testPyaload = "{\"id\":\"" + colId
                    + "\",\"_rid\":\"" + rid
                    + "\",\"_self\":\"" + self
                    + "\",\"_etag\":\"" + etag
                    + "\",\"_colls\":\"colls\\/\",\"_users\":\"users\\/\",\"_ts\":" + ts + "}";

            ContainerProperties deserializedPayload =
                JsonConvert.DeserializeObject<ContainerProperties>(testPyaload);

            Assert.IsTrue(deserializedPayload.LastModified.HasValue);
            Assert.AreEqual(expected, deserializedPayload.LastModified.Value);
            Assert.AreEqual(colId, deserializedPayload.Id);
            Assert.AreEqual(rid, deserializedPayload.ResourceId);
            Assert.AreEqual(etag, deserializedPayload.ETag);
        }

        [TestMethod]
        public void StoredProcedureDeserialzieTest()
        {
            string colId = "946ad017-14d9-4cee-8619-0cbc62414157";
            string rid = "vu9cAA==";
            string self = "dbs\\/vu9cAA==\\/cols\\/abc==\\/sprocs\\/def==\\/";
            string etag = "00000000-0000-0000-f8ea-31d6e5f701d4";
            double ts = 1555923784;

            DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            DateTime expected = UnixStartTime.AddSeconds(ts);

            string testPyaload = "{\"id\":\"" + colId
                    + "\",\"_rid\":\"" + rid
                    + "\",\"_self\":\"" + self
                    + "\",\"_etag\":\"" + etag
                    + "\",\"_colls\":\"colls\\/\",\"_users\":\"users\\/\",\"_ts\":" + ts + "}";

            StoredProcedureProperties deserializedPayload =
                JsonConvert.DeserializeObject<StoredProcedureProperties>(testPyaload);

            Assert.IsTrue(deserializedPayload.LastModified.HasValue);
            Assert.AreEqual(expected, deserializedPayload.LastModified.Value);
            Assert.AreEqual(colId, deserializedPayload.Id);
            Assert.AreEqual(rid, deserializedPayload.ResourceId);
            Assert.AreEqual(etag, deserializedPayload.ETag);
        }

        [TestMethod]
        public void DatabaseSettingsSerializeTest()
        {
            string id = Guid.NewGuid().ToString();

            DatabaseProperties databaseSettings = new DatabaseProperties()
            {
                Id = id
            };

            Database db = new Database()
            {
                Id = id
            };

            string cosmosSerialized = SettingsContractTests.CosmosSerialize(databaseSettings);
            string directSerialized = SettingsContractTests.DirectSerialize(db);

            // Swap de-serialize and validate 
            DatabaseProperties dbDeserSettings = SettingsContractTests.CosmosDeserialize<DatabaseProperties>(directSerialized);
            Database dbDeser = SettingsContractTests.DirectDeSerialize<Database>(cosmosSerialized);

            Assert.AreEqual(dbDeserSettings.Id, dbDeser.Id);
            Assert.AreEqual(dbDeserSettings.Id, db.Id);
        }

        [TestMethod]
        public void DatabaseSettingsDeSerializeTest()
        {
            string dbResponsePayload = @"{
                _colls : 'dbs/6GoAAA==/colls/',
                _users: 'dbs/6GoAAA==/users/',
                 id: 'QuickStarts',
                _rid: '6GoAAA==',
                _self: 'dbs/6GoAAA==/',
                _ts: 1530581163,
                _etag: '00002000-0000-0000-0000-5b3ad0ab0000'
                }";

            DatabaseProperties databaseSettings = SettingsContractTests.CosmosDeserialize<DatabaseProperties>(dbResponsePayload);
            Database db = SettingsContractTests.DirectDeSerialize<Database>(dbResponsePayload);

            // Not all are exposed in CosmosDatabaseSettings
            // so lets only validate relevant parts
            Assert.AreEqual(db.Id, databaseSettings.Id);
            Assert.AreEqual(db.ETag, databaseSettings.ETag);
            Assert.AreEqual(db.ResourceId, databaseSettings.ResourceId);

            Assert.AreEqual("QuickStarts", databaseSettings.Id);
            Assert.AreEqual("00002000-0000-0000-0000-5b3ad0ab0000", databaseSettings.ETag);
            Assert.AreEqual("6GoAAA==", databaseSettings.ResourceId);
        }

        [TestMethod]
        public void ContainerSettingsSimpleTest()
        {
            string id = Guid.NewGuid().ToString();
            string pkPath = "/partitionKey";

            // Two equivalent definitions 
            ContainerProperties cosmosContainerSettings = new ContainerProperties(id, pkPath);

            DocumentCollection collection = new DocumentCollection()
            {
                Id = id,
                PartitionKey = new PartitionKeyDefinition()
                {
                    Paths = new Collection<string>() { pkPath },
                }
            };

            string cosmosSerialized = SettingsContractTests.CosmosSerialize(cosmosContainerSettings);
            string directSerialized = SettingsContractTests.DirectSerialize(collection);

            // Swap de-serialize and validate 
            ContainerProperties containerDeserSettings = SettingsContractTests.CosmosDeserialize<ContainerProperties>(directSerialized);
            DocumentCollection collectionDeser = SettingsContractTests.DirectDeSerialize<DocumentCollection>(cosmosSerialized);

            Assert.AreEqual(collection.Id, containerDeserSettings.Id);
            Assert.AreEqual(collection.PartitionKey.Paths[0], containerDeserSettings.PartitionKeyPath);

            Assert.AreEqual(cosmosContainerSettings.Id, collectionDeser.Id);
            Assert.AreEqual(cosmosContainerSettings.PartitionKeyPath, collectionDeser.PartitionKey.Paths[0]);
        }

        [TestMethod]
        public void ValidateAdditionalPropertiesAttributeInPropertiesFiles()
        {
            IEnumerable<Type> allClasses = from t in Assembly.GetAssembly(typeof(CosmosClient)).GetTypes()
                                           where t.IsClass &&
                                           t.IsPublic &&
                                           !t.IsAbstract
                                           where t.Name.EndsWith("Properties")
                                           select t;

            foreach (Type className in allClasses)
            {
                SettingsContractTests.ValidateAdditionalProperties(className);
            }

        }

        /// <summary>
        /// All property types must have an AdditionalProperties with newtonsoft attribute to ensure that an old SDK does not lose any fields that a newer contract may have.
        /// </summary>
        /// <param name="className"></param>
        private static void ValidateAdditionalProperties(Type className)
        {
            PropertyInfo property = className.GetProperty("AdditionalProperties", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsTrue(property != null, "AdditionalProperties property is not there for " + className);
            Assert.AreEqual("Newtonsoft.Json.JsonExtensionDataAttribute", property.CustomAttributes.First().AttributeType.FullName, "AdditionalProperties property is not Newtonsoft.JsonJsonExtensionDataAttribute");

            PropertyInfo[] propertyInfoArr = className.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (PropertyInfo propInfo in propertyInfoArr)
            {
                ValidateProperty(propInfo.PropertyType);
            }
        }

        private static void ValidateProperty(Type propInfoType)
        {
            if (propInfoType.ToString().Contains("Microsoft.Azure.Cosmos") &&
                (propInfoType.BaseType == null || propInfoType.BaseType.Name.Equals("Object")))
            {
                if (propInfoType.GenericTypeArguments.Length == 0)
                {
                    SettingsContractTests.ValidateAdditionalProperties(propInfoType);
                }
                else
                {
                    foreach (Type genericTypeArgs in propInfoType.GenericTypeArguments)
                    {
                        SettingsContractTests.ValidateProperty(genericTypeArgs);
                    }

                }
            }
        }

        [TestMethod]
        public void SettingsDeserializeWithAdditionalDataTest()
        {
            this.DeserializeWithAdditionalDataTest<StoredProcedureProperties>();
            this.DeserializeWithAdditionalDataTest<ConflictProperties>();
            this.DeserializeWithAdditionalDataTest<DatabaseProperties>();
            this.DeserializeWithAdditionalDataTest<PermissionProperties>();
            this.DeserializeWithAdditionalDataTest<UserDefinedFunctionProperties>();
            this.DeserializeWithAdditionalDataTest<UserProperties>();
        }

        [TestMethod]
        public void AccountPropertiesDeserializeWithAdditionalDataTest()
        {
            string cosmosSerialized = "{\"id\":\"2a9f501b-6948-4795-8fd1-797defb5c466\",\"writableLocations\":[],\"readableLocations\":[{\"name\":\"region1\",\"additionalRegion\":\"regionValue\",\"databaseAccountEndpoint\":null}],\"userConsistencyPolicy\":{\"defaultConsistencyLevel\":\"Strong\",\"maxStalenessPrefix\":0,\"additionalConsistency\":\"consistencyValue\",\"maxIntervalInSeconds\":1},\"addresses\":null,\"userReplicationPolicy\":null,\"systemReplicationPolicy\":null,\"readPolicy\":null,\"queryEngineConfiguration\":null,\"enableMultipleWriteLocations\":false}";

            JObject complexObject = JObject.FromObject(new { id = 1, name = new { fname = "fname", lname = "lname" } });

            // Adding additional information
            JObject jobject = JObject.Parse(cosmosSerialized);
            jobject.Add(new JProperty("simple string", "policy value"));
            jobject.Add(new JProperty("complex object", complexObject));

            // Serialized string
            cosmosSerialized = SettingsContractTests.CosmosSerialize(jobject);

            AccountProperties containerDeserSettings = SettingsContractTests.CosmosDeserialize<AccountProperties>(cosmosSerialized);

            Assert.AreEqual("2a9f501b-6948-4795-8fd1-797defb5c466", containerDeserSettings.Id);
            Assert.AreEqual(2, containerDeserSettings.AdditionalProperties.Count);
            Assert.AreEqual("policy value", (string)containerDeserSettings.AdditionalProperties["simple string"]);
            Assert.AreEqual(complexObject.ToString(), JObject.FromObject(containerDeserSettings.AdditionalProperties["complex object"]).ToString());

            Assert.AreEqual(1, containerDeserSettings.ReadableRegions.First().AdditionalProperties.Count);
            Assert.AreEqual("regionValue", containerDeserSettings.ReadableRegions.First().AdditionalProperties["additionalRegion"]);

            Assert.AreEqual(1, containerDeserSettings.Consistency.AdditionalProperties.Count);
            Assert.AreEqual("consistencyValue", containerDeserSettings.Consistency.AdditionalProperties["additionalConsistency"]);

        }

        [TestMethod]
        public void ContainerPropertiesDeserializeWithAdditionalDataTest()
        {
            string cosmosSerialized = "{\"indexingPolicy\":{\"automatic\":true,\"indexingMode\":\"Consistent\",\"additionalIndexPolicy\":\"indexpolicyvalue\",\"includedPaths\":[{\"path\":\"/included/path\",\"additionalIncludedPath\":\"includedPathValue\",\"indexes\":[]}],\"excludedPaths\":[{\"path\":\"/excluded/path\",\"additionalExcludedPath\":\"excludedPathValue\"}],\"compositeIndexes\":[[{\"path\":\"/composite/path\",\"additionalCompositeIndex\":\"compositeIndexValue\",\"order\":\"ascending\"}]],\"spatialIndexes\":[{\"path\":\"/spatial/path\",\"additionalSpatialIndexes\":\"spatialIndexValue\",\"types\":[]}],\"vectorIndexes\":[{\"path\":\"/vector1\",\"type\":\"flat\",\"additionalVectorIndex\":\"vectorIndexValue1\"},{\"path\":\"/vector2\",\"type\":\"quantizedFlat\",\"additionalVectorIndex\":\"vectorIndexValue2\"},{\"path\":\"/vector3\",\"type\":\"diskANN\"}],\"fullTextIndexes\":[{\"path\":\"/fullTextPath1\",\"additionalFullTextIndex\":\"fullTextIndexValue1\"},{\"path\":\"/fullTextPath2\",\"additionalFullTextIndex\":\"fullTextIndexValue2\"},{\"path\":\"/fullTextPath3\"}]},\"computedProperties\":[{\"name\":\"lowerName\",\"query\":\"SELECT VALUE LOWER(c.name) FROM c\"},{\"name\":\"estimatedTax\",\"query\":\"SELECT VALUE c.salary * 0.2 FROM c\"}],\"geospatialConfig\":{\"type\":\"Geography\",\"additionalGeospatialConfig\":\"geospatialConfigValue\"},\"uniqueKeyPolicy\":{\"additionalUniqueKeyPolicy\":\"uniqueKeyPolicyValue\",\"uniqueKeys\":[{\"paths\":[\"/unique/key/path/1\",\"/unique/key/path/2\"]}]},\"conflictResolutionPolicy\":{\"mode\":\"LastWriterWins\",\"additionalConflictResolutionPolicy\":\"conflictResolutionValue\"},\"clientEncryptionPolicy\":{\"includedPaths\":[{\"path\":\"/path\",\"clientEncryptionKeyId\":\"clientEncryptionKeyId\",\"encryptionType\":\"Randomized\",\"additionalIncludedPath\":\"includedPathValue\",\"encryptionAlgorithm\":\"AEAD_AES_256_CBC_HMAC_SHA256\"}],\"policyFormatVersion\":1,\"additionalEncryptionPolicy\":\"clientEncryptionpolicyValue\"},\"id\":\"2a9f501b-6948-4795-8fd1-797defb5c466\",\"partitionKey\":{\"paths\":[],\"kind\":\"Hash\"},\"vectorEmbeddingPolicy\":{\"vectorEmbeddings\":[{\"path\":\"/vector1\",\"dataType\":\"float32\",\"dimensions\":1200,\"distanceFunction\":\"cosine\"},{\"path\":\"/vector2\",\"dataType\":\"int8\",\"dimensions\":3,\"distanceFunction\":\"dotproduct\"},{\"path\":\"/vector3\",\"dataType\":\"uint8\",\"dimensions\":400,\"distanceFunction\":\"euclidean\"}]},\"fullTextPolicy\": {\"defaultLanguage\": \"en-US\",\"fullTextPaths\": [{\"path\": \"/fullTextPath1\",\"language\": \"en-US\"},{\"path\": \"/fullTextPath2\",\"language\": \"en-US\"},{\"path\": \"/fullTextPath3\",\"language\": \"en-US\"}]}}";

            JObject complexObject = JObject.FromObject(new { id = 1, name = new { fname = "fname", lname = "lname" } });

            // Adding additional information
            JObject jobject = JObject.Parse(cosmosSerialized);
            jobject.Add(new JProperty("simple string", "policy value"));
            jobject.Add(new JProperty("complex object", complexObject));

            // Serialized string
            cosmosSerialized = SettingsContractTests.CosmosSerialize(jobject);

            ContainerProperties containerProperties = SettingsContractTests.CosmosDeserialize<ContainerProperties>(cosmosSerialized);

            Assert.AreEqual("2a9f501b-6948-4795-8fd1-797defb5c466", containerProperties.Id);

            Assert.AreEqual(2, containerProperties.AdditionalProperties.Count);
            Assert.AreEqual("policy value", (string)containerProperties.AdditionalProperties["simple string"]);
            Assert.AreEqual(complexObject.ToString(), JObject.FromObject(containerProperties.AdditionalProperties["complex object"]).ToString());

            Assert.AreEqual(1, containerProperties.IndexingPolicy.AdditionalProperties.Count);
            Assert.AreEqual("indexpolicyvalue", containerProperties.IndexingPolicy.AdditionalProperties["additionalIndexPolicy"]);

            Assert.AreEqual(1, containerProperties.IndexingPolicy.SpatialIndexes[0].AdditionalProperties.Count);
            Assert.AreEqual("spatialIndexValue", containerProperties.IndexingPolicy.SpatialIndexes[0].AdditionalProperties["additionalSpatialIndexes"]);

            Assert.AreEqual(1, containerProperties.IndexingPolicy.CompositeIndexes[0][0].AdditionalProperties.Count);
            Assert.AreEqual("compositeIndexValue", containerProperties.IndexingPolicy.CompositeIndexes[0][0].AdditionalProperties["additionalCompositeIndex"]);

            Assert.AreEqual(1, containerProperties.IndexingPolicy.VectorIndexes[0].AdditionalProperties.Count);
            Assert.AreEqual("vectorIndexValue1", containerProperties.IndexingPolicy.VectorIndexes[0].AdditionalProperties["additionalVectorIndex"]);

            Assert.AreEqual(1, containerProperties.IndexingPolicy.VectorIndexes[1].AdditionalProperties.Count);
            Assert.AreEqual("vectorIndexValue2", containerProperties.IndexingPolicy.VectorIndexes[1].AdditionalProperties["additionalVectorIndex"]);

            Assert.IsNull(containerProperties.IndexingPolicy.VectorIndexes[2].AdditionalProperties);

            Assert.AreEqual(1, containerProperties.IndexingPolicy.FullTextIndexes[0].AdditionalProperties.Count);
            Assert.AreEqual("fullTextIndexValue1", containerProperties.IndexingPolicy.FullTextIndexes[0].AdditionalProperties["additionalFullTextIndex"]);

            Assert.AreEqual(1, containerProperties.IndexingPolicy.FullTextIndexes[1].AdditionalProperties.Count);
            Assert.AreEqual("fullTextIndexValue2", containerProperties.IndexingPolicy.FullTextIndexes[1].AdditionalProperties["additionalFullTextIndex"]);

            Assert.AreEqual(1, containerProperties.IndexingPolicy.IncludedPaths[0].AdditionalProperties.Count);
            Assert.AreEqual("includedPathValue", containerProperties.IndexingPolicy.IncludedPaths[0].AdditionalProperties["additionalIncludedPath"]);

            Assert.AreEqual(1, containerProperties.IndexingPolicy.ExcludedPaths[0].AdditionalProperties.Count);
            Assert.AreEqual("excludedPathValue", containerProperties.IndexingPolicy.ExcludedPaths[0].AdditionalProperties["additionalExcludedPath"]);

            Assert.AreEqual(1, containerProperties.GeospatialConfig.AdditionalProperties.Count);
            Assert.AreEqual("geospatialConfigValue", containerProperties.GeospatialConfig.AdditionalProperties["additionalGeospatialConfig"]);

            Assert.AreEqual(1, containerProperties.UniqueKeyPolicy.AdditionalProperties.Count);
            Assert.AreEqual("uniqueKeyPolicyValue", containerProperties.UniqueKeyPolicy.AdditionalProperties["additionalUniqueKeyPolicy"]);

            Assert.AreEqual(1, containerProperties.ConflictResolutionPolicy.AdditionalProperties.Count);
            Assert.AreEqual("conflictResolutionValue", containerProperties.ConflictResolutionPolicy.AdditionalProperties["additionalConflictResolutionPolicy"]);

            Assert.AreEqual(1, containerProperties.ClientEncryptionPolicy.AdditionalProperties.Count);
            Assert.AreEqual("clientEncryptionpolicyValue", containerProperties.ClientEncryptionPolicy.AdditionalProperties["additionalEncryptionPolicy"]);

            Assert.AreEqual(1, containerProperties.ClientEncryptionPolicy.IncludedPaths.First().AdditionalProperties.Count);
            Assert.AreEqual("includedPathValue", containerProperties.ClientEncryptionPolicy.IncludedPaths.First().AdditionalProperties["additionalIncludedPath"]);

            Assert.IsNotNull(containerProperties.VectorEmbeddingPolicy);
            Assert.AreEqual(3, containerProperties.VectorEmbeddingPolicy.Embeddings.Count);
            Assert.AreEqual("/vector1", containerProperties.VectorEmbeddingPolicy.Embeddings[0].Path);
            Assert.AreEqual(Cosmos.VectorDataType.Float32, containerProperties.VectorEmbeddingPolicy.Embeddings[0].DataType);
            Assert.AreEqual(1200, containerProperties.VectorEmbeddingPolicy.Embeddings[0].Dimensions);
            Assert.AreEqual(Cosmos.DistanceFunction.Cosine, containerProperties.VectorEmbeddingPolicy.Embeddings[0].DistanceFunction);

            Assert.IsNotNull(containerProperties.FullTextPolicy);
            Assert.AreEqual("en-US", containerProperties.FullTextPolicy.DefaultLanguage);
            Assert.AreEqual(3, containerProperties.FullTextPolicy.FullTextPaths.Count);

            Assert.AreEqual("/fullTextPath1", containerProperties.FullTextPolicy.FullTextPaths[0].Path);
            Assert.AreEqual("en-US", containerProperties.FullTextPolicy.FullTextPaths[0].Language);

            Assert.AreEqual("/fullTextPath2", containerProperties.FullTextPolicy.FullTextPaths[1].Path);
            Assert.AreEqual("en-US", containerProperties.FullTextPolicy.FullTextPaths[1].Language);

            Assert.AreEqual("/fullTextPath3", containerProperties.FullTextPolicy.FullTextPaths[2].Path);
            Assert.AreEqual("en-US", containerProperties.FullTextPolicy.FullTextPaths[2].Language);

            Assert.AreEqual(2, containerProperties.ComputedProperties.Count);
            Assert.AreEqual("lowerName", containerProperties.ComputedProperties[0].Name);
            Assert.AreEqual("SELECT VALUE LOWER(c.name) FROM c", containerProperties.ComputedProperties[0].Query);
            Assert.AreEqual("estimatedTax", containerProperties.ComputedProperties[1].Name);
            Assert.AreEqual("SELECT VALUE c.salary * 0.2 FROM c", containerProperties.ComputedProperties[1].Query);
        }

        [TestMethod]
        public void ClientEncryptionKeyPropertiesDeserializeWithAdditionalDataTest()
        {
            string cosmosSerialized = "{\"id\":\"id\",\"encryptionAlgorithm\":\"encryptionAlgorithm\",\"wrappedDataEncryptionKey\":\"AA==\",\"keyWrapMetadata\":{\"type\":\"type\",\"name\":\"name\",\"value\":\"value\", \"additional\":\"value\"}}";

            JObject complexObject = JObject.FromObject(new { id = 1, name = new { fname = "fname", lname = "lname" } });

            // Adding additional information
            JObject jobject = JObject.Parse(cosmosSerialized);
            jobject.Add(new JProperty("simple string", "policy value"));
            jobject.Add(new JProperty("complex object", complexObject));

            // Serialized string
            string modifiedCosmosSerialized = SettingsContractTests.CosmosSerialize(jobject);

            ClientEncryptionKeyProperties deserSettings = SettingsContractTests.CosmosDeserialize<ClientEncryptionKeyProperties>(modifiedCosmosSerialized);
            ClientEncryptionKeyProperties deserSettingsIntance2 = SettingsContractTests.CosmosDeserialize<ClientEncryptionKeyProperties>(modifiedCosmosSerialized);

            Assert.AreEqual("id", deserSettings.Id);
            Assert.AreEqual("encryptionAlgorithm", deserSettings.EncryptionAlgorithm);
            Assert.AreEqual(1, deserSettings.WrappedDataEncryptionKey.Length);

            Assert.AreEqual("type", deserSettings.EncryptionKeyWrapMetadata.Type);
            Assert.AreEqual("name", deserSettings.EncryptionKeyWrapMetadata.Name);
            Assert.AreEqual("value", deserSettings.EncryptionKeyWrapMetadata.Value);
            Assert.AreEqual(1, deserSettings.EncryptionKeyWrapMetadata.AdditionalProperties.Count);
            Assert.AreEqual("value", deserSettings.EncryptionKeyWrapMetadata.AdditionalProperties["additional"]);

            Assert.AreEqual(2, deserSettings.AdditionalProperties.Count);
            Assert.AreEqual("policy value", (string)deserSettings.AdditionalProperties["simple string"]);
            Assert.AreEqual(complexObject.ToString(), JObject.FromObject(deserSettings.AdditionalProperties["complex object"]).ToString());

            Assert.AreEqual(deserSettings, deserSettingsIntance2); // Testing equal function changes

            JObject newComplexObject = JObject.FromObject(new { id = 1, name = new { fname = "fname", lname = "lname1" } });
            // Adding additional information
            JObject jobject_for_non_equality = JObject.Parse(cosmosSerialized);
            jobject_for_non_equality.Add(new JProperty("simple string", "policy value"));
            jobject_for_non_equality.Add(new JProperty("complex object", newComplexObject));

            // Serialized string
            string modifiedForNonEqualityCheckCosmosSerialized = SettingsContractTests.CosmosSerialize(jobject_for_non_equality);
            ClientEncryptionKeyProperties deserSettingsIntance3 = SettingsContractTests.CosmosDeserialize<ClientEncryptionKeyProperties>(modifiedForNonEqualityCheckCosmosSerialized);

            Assert.AreNotEqual(deserSettingsIntance2, deserSettingsIntance3); // Testing equal function changes
        }

        [TestMethod]
        public void TriggerPropertiesDeserializeWithAdditionalDataTest()
        {
            TriggerProperties triggerProperties = new TriggerProperties
            {
                Body = "body"
            };

            string cosmosSerialized = SettingsContractTests.CosmosSerialize(triggerProperties);

            JObject complexObject = JObject.FromObject(new { id = 1, name = new { fname = "fname", lname = "lname" } });

            // Adding additional information
            JObject jobject = JObject.Parse(cosmosSerialized);
            jobject.Add(new JProperty("simple string", "policy value"));
            jobject.Add(new JProperty("complex object", complexObject));

            // Serialized string
            cosmosSerialized = SettingsContractTests.CosmosSerialize(jobject);

            TriggerProperties deserSettings = SettingsContractTests.CosmosDeserialize<TriggerProperties>(cosmosSerialized);

            Assert.AreEqual("body", deserSettings.Body);
            Assert.AreEqual(2, deserSettings.AdditionalProperties.Count);
            Assert.AreEqual("policy value", (string)deserSettings.AdditionalProperties["simple string"]);
            Assert.AreEqual(complexObject.ToString(), JObject.FromObject(deserSettings.AdditionalProperties["complex object"]).ToString());
        }

        [TestMethod]
        public void ThroughputPropertiesDeserializeWithAdditionalDataTest()
        {
            ThroughputProperties manualThroughputProperties = ThroughputProperties.CreateManualThroughput(1);
            ThroughputProperties autoscaleThroughputProperties = ThroughputProperties.CreateAutoscaleThroughput(2);

            string cosmosManualSerialized = SettingsContractTests.CosmosSerialize(manualThroughputProperties);
            string cosmosAutoscaleSerialized = SettingsContractTests.CosmosSerialize(autoscaleThroughputProperties);

            JObject complexObject = JObject.FromObject(new { id = 1, name = new { fname = "fname", lname = "lname" } });

            // Adding additional information
            JObject manualJobject = JObject.Parse(cosmosManualSerialized);
            manualJobject.Add(new JProperty("simple string", "policy value"));
            manualJobject.Add(new JProperty("complex object", complexObject));

            JObject autoscaleJobject = JObject.Parse(cosmosAutoscaleSerialized);
            autoscaleJobject.Add(new JProperty("simple string", "policy value"));
            autoscaleJobject.Add(new JProperty("complex object", complexObject));

            // Serialized string
            cosmosManualSerialized = SettingsContractTests.CosmosSerialize(manualJobject);
            cosmosAutoscaleSerialized = SettingsContractTests.CosmosSerialize(autoscaleJobject);

            ThroughputProperties manualDeserSettings = SettingsContractTests.CosmosDeserialize<ThroughputProperties>(cosmosManualSerialized);
            ThroughputProperties autoscaleDeserSettings = SettingsContractTests.CosmosDeserialize<ThroughputProperties>(cosmosAutoscaleSerialized);

            Assert.AreEqual(1, manualDeserSettings.Content.OfferThroughput);
            Assert.AreEqual(2, manualDeserSettings.AdditionalProperties.Count);
            Assert.AreEqual("policy value", (string)manualDeserSettings.AdditionalProperties["simple string"]);
            Assert.AreEqual(complexObject.ToString(), JObject.FromObject(manualDeserSettings.AdditionalProperties["complex object"]).ToString());

            Assert.AreEqual(2, autoscaleDeserSettings.Content.OfferAutoscaleSettings.MaxThroughput);
            Assert.AreEqual(2, autoscaleDeserSettings.AdditionalProperties.Count);
            Assert.AreEqual("policy value", (string)autoscaleDeserSettings.AdditionalProperties["simple string"]);
            Assert.AreEqual(complexObject.ToString(), JObject.FromObject(autoscaleDeserSettings.AdditionalProperties["complex object"]).ToString());
        }

        [TestMethod]
        public void BoundingBoxPropertiesDeserializeWithAdditionalDataTest()
        {
            BoundingBoxProperties boundingBoxProperties = new BoundingBoxProperties
            {
                Xmin = 10
            };

            string cosmosSerialized = SettingsContractTests.CosmosSerialize(boundingBoxProperties);

            JObject complexObject = JObject.FromObject(new { id = 1, name = new { fname = "fname", lname = "lname" } });

            // Adding additional information
            JObject jobject = JObject.Parse(cosmosSerialized);
            jobject.Add(new JProperty("simple string", "policy value"));
            jobject.Add(new JProperty("complex object", complexObject));

            // Serialized string
            cosmosSerialized = SettingsContractTests.CosmosSerialize(jobject);

            BoundingBoxProperties deserSettings = SettingsContractTests.CosmosDeserialize<BoundingBoxProperties>(cosmosSerialized);

            Assert.AreEqual(10, deserSettings.Xmin);
            Assert.AreEqual(2, deserSettings.AdditionalProperties.Count);
            Assert.AreEqual("policy value", (string)deserSettings.AdditionalProperties["simple string"]);
            Assert.AreEqual(complexObject.ToString(), JObject.FromObject(deserSettings.AdditionalProperties["complex object"]).ToString());
        }

        [TestMethod]
        public void OfferContentPropertiesDeserializeWithAdditionalDataTest()
        {
            OfferContentProperties offerContentProperties = OfferContentProperties.CreateManualOfferConent(1);

            string cosmosSerialized = SettingsContractTests.CosmosSerialize(offerContentProperties);

            JObject complexObject = JObject.FromObject(new { id = 1, name = new { fname = "fname", lname = "lname" } });

            // Adding additional information
            JObject jobject = JObject.Parse(cosmosSerialized);
            jobject.Add(new JProperty("simple string", "policy value"));
            jobject.Add(new JProperty("complex object", complexObject));

            // Serialized string
            cosmosSerialized = SettingsContractTests.CosmosSerialize(jobject);

            OfferContentProperties containerDeserSettings = SettingsContractTests.CosmosDeserialize<OfferContentProperties>(cosmosSerialized);

            Assert.AreEqual(1, containerDeserSettings.OfferThroughput);
            Assert.AreEqual(2, containerDeserSettings.AdditionalProperties.Count);
            Assert.AreEqual("policy value", (string)containerDeserSettings.AdditionalProperties["simple string"]);
            Assert.AreEqual(complexObject.ToString(), JObject.FromObject(containerDeserSettings.AdditionalProperties["complex object"]).ToString());
        }

        [TestMethod]
        public void OfferAutoscaleAutoUpgradePropertiesDeserializeWithAdditionalDataTest()
        {
            string cosmosSerialized = "{\"throughputPolicy\":{\"incrementPercent\":1, \"additional\":\"property\"}}";

            JObject complexObject = JObject.FromObject(new { id = 1, name = new { fname = "fname", lname = "lname" } });

            // Adding additional information
            JObject jobject = JObject.Parse(cosmosSerialized);
            jobject.Add(new JProperty("simple string", "policy value"));
            jobject.Add(new JProperty("complex object", complexObject));

            // Serialized string
            cosmosSerialized = SettingsContractTests.CosmosSerialize(jobject);

            OfferAutoscaleAutoUpgradeProperties containerDeserSettings = SettingsContractTests.CosmosDeserialize<OfferAutoscaleAutoUpgradeProperties>(cosmosSerialized);

            Assert.AreEqual(1, containerDeserSettings.ThroughputProperties.IncrementPercent);
            Assert.AreEqual(2, containerDeserSettings.AdditionalProperties.Count);
            Assert.AreEqual("policy value", (string)containerDeserSettings.AdditionalProperties["simple string"]);
            Assert.AreEqual(complexObject.ToString(), JObject.FromObject(containerDeserSettings.AdditionalProperties["complex object"]).ToString());
            Assert.AreEqual("property", (string)containerDeserSettings.ThroughputProperties.AdditionalProperties["additional"]);
        }

        private void DeserializeWithAdditionalDataTest<T>()
        {
            string cosmosSerialized = "{\"id\":\"2a9f501b-6948-4795-8fd1-797defb5c466\"}";

            JObject complexObject = JObject.FromObject(new { id = 1, name = new { fname = "fname", lname = "lname" } });

            // Adding additional information
            JObject jobject = JObject.Parse(cosmosSerialized);
            jobject.Add(new JProperty("simple string", "policy value"));
            jobject.Add(new JProperty("complex object", complexObject));

            // Serialized string
            cosmosSerialized = SettingsContractTests.CosmosSerialize(jobject);

            dynamic containerDeserSettings = SettingsContractTests.CosmosDeserialize<T>(cosmosSerialized);

            Assert.AreEqual("2a9f501b-6948-4795-8fd1-797defb5c466", containerDeserSettings.Id);
            Assert.AreEqual(2, containerDeserSettings.AdditionalProperties.Count);
            Assert.AreEqual("policy value", (string)containerDeserSettings.AdditionalProperties["simple string"]);
            Assert.AreEqual(complexObject.ToString(), JObject.FromObject(containerDeserSettings.AdditionalProperties["complex object"]).ToString());
        }

        [TestMethod]
        public void PartitionKeyDefinitionVersionValuesTest()
        {
            this.AssertEnums<Cosmos.PartitionKeyDefinitionVersion, Documents.PartitionKeyDefinitionVersion>();
        }

        [TestMethod]
        public void ContainerSettingsWithConflictResolution()
        {
            string id = Guid.NewGuid().ToString();
            string pkPath = "/partitionKey";

            // Two equivalent definitions 
            ContainerProperties cosmosContainerSettings = new ContainerProperties(id, pkPath)
            {
                ConflictResolutionPolicy = new Cosmos.ConflictResolutionPolicy()
                {
                    Mode = Cosmos.ConflictResolutionMode.Custom,
                    ResolutionPath = "/path",
                    ResolutionProcedure = "sp"
                }
            };

            DocumentCollection collection = new DocumentCollection()
            {
                Id = id,
                ConflictResolutionPolicy = new ConflictResolutionPolicy()
                {
                    Mode = ConflictResolutionMode.Custom,
                    ConflictResolutionPath = "/path",
                    ConflictResolutionProcedure = "sp"
                }
            };

            string cosmosSerialized = SettingsContractTests.CosmosSerialize(cosmosContainerSettings);
            string directSerialized = SettingsContractTests.DirectSerialize(collection);

            // Swap de-serialize and validate 
            _ = SettingsContractTests.CosmosDeserialize<ContainerProperties>(directSerialized);
            DocumentCollection collectionDeser = SettingsContractTests.DirectDeSerialize<DocumentCollection>(cosmosSerialized);

            Assert.AreEqual(cosmosContainerSettings.Id, collectionDeser.Id);
            Assert.AreEqual((int)cosmosContainerSettings.ConflictResolutionPolicy.Mode, (int)collectionDeser.ConflictResolutionPolicy.Mode);
            Assert.AreEqual(cosmosContainerSettings.ConflictResolutionPolicy.ResolutionPath, collectionDeser.ConflictResolutionPolicy.ConflictResolutionPath);
            Assert.AreEqual(cosmosContainerSettings.ConflictResolutionPolicy.ResolutionProcedure, collectionDeser.ConflictResolutionPolicy.ConflictResolutionProcedure);
        }

        [TestMethod]
        public void ContainerSettingsWithIndexingPolicyTest()
        {
            string id = Guid.NewGuid().ToString();
            string pkPath = "/partitionKey";

            // Two equivalent definitions 
            ContainerProperties cosmosContainerSettings = new ContainerProperties(id, pkPath);
            cosmosContainerSettings.IndexingPolicy.Automatic = true;
            cosmosContainerSettings.IndexingPolicy.IncludedPaths.Add(new Cosmos.IncludedPath() { Path = "/id1/*" });

            Cosmos.UniqueKey cuk1 = new Cosmos.UniqueKey();
            cuk1.Paths.Add("/u1");
            cosmosContainerSettings.UniqueKeyPolicy.UniqueKeys.Add(cuk1);

            DocumentCollection collection = new DocumentCollection()
            {
                Id = id,
                PartitionKey = new PartitionKeyDefinition()
                {
                    Paths = new Collection<string>() { pkPath },
                }
            };
            collection.IndexingPolicy.Automatic = true;
            collection.IndexingPolicy.IncludedPaths.Add(new Documents.IncludedPath() { Path = "/id1/*" });

            Documents.UniqueKey duk1 = new Documents.UniqueKey();
            duk1.Paths.Add("/u1");
            collection.UniqueKeyPolicy.UniqueKeys.Add(duk1);

            string cosmosSerialized = SettingsContractTests.CosmosSerialize(cosmosContainerSettings);
            string directSerialized = SettingsContractTests.DirectSerialize(collection);

            // Swap de-serialize and validate 
            ContainerProperties containerDeserSettings = SettingsContractTests.CosmosDeserialize<ContainerProperties>(directSerialized);
            DocumentCollection collectionDeser = SettingsContractTests.DirectDeSerialize<DocumentCollection>(cosmosSerialized);

            Assert.AreEqual(collection.Id, containerDeserSettings.Id);
            Assert.AreEqual(collection.PartitionKey.Paths[0], containerDeserSettings.PartitionKeyPath);
            Assert.AreEqual(collection.IndexingPolicy.Automatic, containerDeserSettings.IndexingPolicy.Automatic);
            Assert.AreEqual(collection.IndexingPolicy.IncludedPaths.Count, containerDeserSettings.IndexingPolicy.IncludedPaths.Count);
            Assert.AreEqual(collection.IndexingPolicy.IncludedPaths[0].Path, containerDeserSettings.IndexingPolicy.IncludedPaths[0].Path);
            Assert.AreEqual(collection.IndexingPolicy.IncludedPaths[0].Indexes.Count, containerDeserSettings.IndexingPolicy.IncludedPaths[0].Indexes.Count);
            Assert.AreEqual(collection.UniqueKeyPolicy.UniqueKeys.Count, containerDeserSettings.UniqueKeyPolicy.UniqueKeys.Count);
            Assert.AreEqual(collection.UniqueKeyPolicy.UniqueKeys[0].Paths.Count, containerDeserSettings.UniqueKeyPolicy.UniqueKeys[0].Paths.Count);
            Assert.AreEqual(collection.UniqueKeyPolicy.UniqueKeys[0].Paths[0], containerDeserSettings.UniqueKeyPolicy.UniqueKeys[0].Paths[0]);

            Assert.AreEqual(cosmosContainerSettings.Id, collectionDeser.Id);
            Assert.AreEqual(cosmosContainerSettings.PartitionKeyPath, collectionDeser.PartitionKey.Paths[0]);
            Assert.AreEqual(cosmosContainerSettings.IndexingPolicy.Automatic, collectionDeser.IndexingPolicy.Automatic);
            Assert.AreEqual(cosmosContainerSettings.IndexingPolicy.IncludedPaths.Count, collectionDeser.IndexingPolicy.IncludedPaths.Count);
            Assert.AreEqual(cosmosContainerSettings.IndexingPolicy.IncludedPaths[0].Path, collectionDeser.IndexingPolicy.IncludedPaths[0].Path);
            Assert.AreEqual(cosmosContainerSettings.IndexingPolicy.IncludedPaths[0].Indexes.Count, collectionDeser.IndexingPolicy.IncludedPaths[0].Indexes.Count);
            Assert.AreEqual(cosmosContainerSettings.UniqueKeyPolicy.UniqueKeys.Count, collectionDeser.UniqueKeyPolicy.UniqueKeys.Count);
            Assert.AreEqual(cosmosContainerSettings.UniqueKeyPolicy.UniqueKeys[0].Paths.Count, collectionDeser.UniqueKeyPolicy.UniqueKeys[0].Paths.Count);
            Assert.AreEqual(cosmosContainerSettings.UniqueKeyPolicy.UniqueKeys[0].Paths[0], collectionDeser.UniqueKeyPolicy.UniqueKeys[0].Paths[0]);
        }

        [TestMethod]
        public void ContainerSettingsDefaults()
        {
            string id = Guid.NewGuid().ToString();
            string pkPath = "/partitionKey";

#if PREVIEW
            SettingsContractTests.TypeAccessorGuard(typeof(ContainerProperties),
                "Id",
                "UniqueKeyPolicy",
                "DefaultTimeToLive",
                "AnalyticalStoreTimeToLiveInSeconds",
                "IndexingPolicy",
                "GeospatialConfig",
                "TimeToLivePropertyPath",
                "PartitionKeyPath",
                "PartitionKeyDefinitionVersion",
                "ComputedProperties",
                "ConflictResolutionPolicy",
                "ChangeFeedPolicy",
                "ClientEncryptionPolicy",
                "PartitionKeyPaths",
                "VectorEmbeddingPolicy",
                "FullTextPolicy");
#else
            SettingsContractTests.TypeAccessorGuard(typeof(ContainerProperties),
                "Id",
                "UniqueKeyPolicy",
                "DefaultTimeToLive",
                "AnalyticalStoreTimeToLiveInSeconds",
                "IndexingPolicy",
                "GeospatialConfig",
                "TimeToLivePropertyPath",
                "PartitionKeyPath",
                "PartitionKeyDefinitionVersion",
                "ConflictResolutionPolicy",
                "ClientEncryptionPolicy",
                "PartitionKeyPaths",
                "VectorEmbeddingPolicy",
                "FullTextPolicy");
#endif

            // Two equivalent definitions 
            ContainerProperties cosmosContainerSettings = new ContainerProperties(id, pkPath);

            Assert.AreEqual(id, cosmosContainerSettings.Id);
            Assert.AreEqual(pkPath, cosmosContainerSettings.PartitionKeyPath);

            Assert.IsNull(cosmosContainerSettings.ResourceId);
            Assert.IsNull(cosmosContainerSettings.LastModified);
            Assert.IsNull(cosmosContainerSettings.ETag);
            Assert.IsNull(cosmosContainerSettings.DefaultTimeToLive);

            Assert.IsNotNull(cosmosContainerSettings.IndexingPolicy);
            Assert.IsNotNull(cosmosContainerSettings.ChangeFeedPolicy);
            Assert.IsNotNull(cosmosContainerSettings.ConflictResolutionPolicy);
            Assert.IsTrue(object.ReferenceEquals(cosmosContainerSettings.IndexingPolicy, cosmosContainerSettings.IndexingPolicy));
            Assert.IsNotNull(cosmosContainerSettings.IndexingPolicy.IncludedPaths);
            Assert.IsTrue(object.ReferenceEquals(cosmosContainerSettings.IndexingPolicy.IncludedPaths, cosmosContainerSettings.IndexingPolicy.IncludedPaths));

            Assert.IsNotNull(cosmosContainerSettings.ComputedProperties);
            Assert.AreEqual(0, cosmosContainerSettings.ComputedProperties.Count);
            Assert.IsTrue(object.ReferenceEquals(cosmosContainerSettings.ComputedProperties, cosmosContainerSettings.ComputedProperties));

            Cosmos.IncludedPath ip = new Cosmos.IncludedPath();
            Assert.IsNotNull(ip.Indexes);

            Assert.IsNotNull(cosmosContainerSettings.UniqueKeyPolicy);
            Assert.IsTrue(object.ReferenceEquals(cosmosContainerSettings.UniqueKeyPolicy, cosmosContainerSettings.UniqueKeyPolicy));
            Assert.IsNotNull(cosmosContainerSettings.UniqueKeyPolicy.UniqueKeys);
            Assert.IsTrue(object.ReferenceEquals(cosmosContainerSettings.UniqueKeyPolicy.UniqueKeys, cosmosContainerSettings.UniqueKeyPolicy.UniqueKeys));

            Cosmos.UniqueKey uk = new Cosmos.UniqueKey();
            Assert.IsNotNull(uk.Paths);
        }

        [TestMethod]
        public async Task ContainerSettingsIndexTest()
        {
            string containerJsonString = "{\"indexingPolicy\":{\"automatic\":true,\"indexingMode\":\"Consistent\",\"includedPaths\":[{\"path\":\"/*\",\"indexes\":[{\"dataType\":\"Number\",\"precision\":-1,\"kind\":\"Range\"},{\"dataType\":\"String\",\"precision\":-1,\"kind\":\"Range\"}]}],\"excludedPaths\":[{\"path\":\"/\\\"_etag\\\"/?\"}],\"compositeIndexes\":[],\"spatialIndexes\":[],\"vectorIndexes\":[],\"fullTextIndexes\":[]},\"id\":\"MigrationTest\",\"partitionKey\":{\"paths\":[\"/id\"],\"kind\":\"Hash\"}}";

            CosmosJsonDotNetSerializer serializerCore = new CosmosJsonDotNetSerializer();
            ContainerProperties containerProperties = null;
            using (MemoryStream memory = new MemoryStream(Encoding.UTF8.GetBytes(containerJsonString)))
            {
                containerProperties = serializerCore.FromStream<ContainerProperties>(memory);
            }

            Assert.IsNotNull(containerProperties);
            Assert.AreEqual("MigrationTest", containerProperties.Id);

            string containerJsonAfterConversion = null;
            using (Stream stream = serializerCore.ToStream<ContainerProperties>(containerProperties))
            {
                using (StreamReader sr = new StreamReader(stream))
                {
                    containerJsonAfterConversion = await sr.ReadToEndAsync();
                }
            }

            Assert.AreEqual(containerJsonString, containerJsonAfterConversion);
        }

        [TestMethod]
        public void ContainerSettingsNullPartitionKeyTest()
        {
            ContainerProperties cosmosContainerSettings = new ContainerProperties("id", "/partitionKey")
            {
                PartitionKey = null
            };

            string cosmosSerialized = SettingsContractTests.CosmosSerialize(cosmosContainerSettings);
            Assert.IsFalse(cosmosSerialized.Contains("partitionKey"));
        }

        [TestMethod]
        [Ignore("This test will be enabled once the V2 DocumentCollection starts supporting the full text policy index.")]
        public async Task ContainerV2CompatTest()
        {
            string containerId = "SerializeContainerTest";
            DocumentCollection documentCollection = new DocumentCollection()
            {
                Id = containerId,
                PartitionKey = new PartitionKeyDefinition()
                {
                    Paths = new Collection<string>()
                    {
                        "/pkPath"
                    }
                },
                IndexingPolicy = new IndexingPolicy()
                {
                    IncludedPaths = new Collection<IncludedPath>()
                    {
                        new IncludedPath()
                        {
                            Path = "/*"
                        }
                    },
                    CompositeIndexes = new Collection<Collection<CompositePath>>()
                    {
                        new Collection<CompositePath>()
                        {
                            new CompositePath()
                            {
                                Path = "/address/test/*",
                                Order = CompositePathSortOrder.Ascending
                            },
                            new CompositePath()
                            {
                                Path = "/address/test2/*",
                                Order = CompositePathSortOrder.Ascending
                            }
                        }
                    },
                    SpatialIndexes = new Collection<SpatialSpec>()
                    {
                        new SpatialSpec()
                        {
                            Path = "/name/first/*",
                            SpatialTypes = new Collection<SpatialType>()
                            {
                                SpatialType.LineString
                            }
                        }
                    }
                },
            };


            string documentJsonString = null;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                documentCollection.SaveTo(memoryStream);
                memoryStream.Position = 0;
                using (StreamReader sr = new StreamReader(memoryStream))
                {
                    documentJsonString = await sr.ReadToEndAsync();
                }
            }

            Assert.IsNotNull(documentJsonString);

            string cosmosJsonString = null;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                documentCollection.SaveTo(memoryStream);
                memoryStream.Position = 0;

                CosmosJsonDotNetSerializer serializerCore = new CosmosJsonDotNetSerializer();
                ContainerProperties containerProperties = serializerCore.FromStream<ContainerProperties>(memoryStream);

                Assert.IsNotNull(containerProperties);
                Assert.AreEqual(containerId, containerProperties.Id);

                using (Stream stream = serializerCore.ToStream<ContainerProperties>(containerProperties))
                {
                    using (StreamReader sr = new StreamReader(stream))
                    {
                        cosmosJsonString = await sr.ReadToEndAsync();
                    }
                }
            }

            JObject jObjectDocumentCollection = JObject.Parse(documentJsonString);
            JObject jObjectContainer = JObject.Parse(cosmosJsonString);
            Assert.IsTrue(JToken.DeepEquals(jObjectDocumentCollection, jObjectContainer), $"v2:{documentJsonString}; v3:{cosmosJsonString}");
        }

        [TestMethod]
        public void CosmosAccountSettingsSerializationTest()
        {
            AccountProperties cosmosAccountSettings = new AccountProperties
            {
                Id = "someId",
                EnableMultipleWriteLocations = true,
                ResourceId = "/uri",
                ETag = "etag",
                WriteLocationsInternal = new Collection<AccountRegion>() { new AccountRegion() { Name = "region1", Endpoint = "endpoint1" } },
                ReadLocationsInternal = new Collection<AccountRegion>() { new AccountRegion() { Name = "region2", Endpoint = "endpoint2" } },
                AddressesLink = "link",
                Consistency = new AccountConsistency() { DefaultConsistencyLevel = Cosmos.ConsistencyLevel.BoundedStaleness },
                ReplicationPolicy = new ReplicationPolicy() { AsyncReplication = true },
                ReadPolicy = new ReadPolicy() { PrimaryReadCoefficient = 10 }
            };

            string cosmosSerialized = SettingsContractTests.CosmosSerialize(cosmosAccountSettings);

            AccountProperties accountDeserSettings = SettingsContractTests.CosmosDeserialize<AccountProperties>(cosmosSerialized);

            Assert.AreEqual(cosmosAccountSettings.Id, accountDeserSettings.Id);
            Assert.AreEqual(cosmosAccountSettings.EnableMultipleWriteLocations, accountDeserSettings.EnableMultipleWriteLocations);
            Assert.AreEqual(cosmosAccountSettings.ResourceId, accountDeserSettings.ResourceId);
            Assert.AreEqual(cosmosAccountSettings.ETag, accountDeserSettings.ETag);
            Assert.AreEqual(cosmosAccountSettings.WriteLocationsInternal[0].Name, accountDeserSettings.WriteLocationsInternal[0].Name);
            Assert.AreEqual(cosmosAccountSettings.WriteLocationsInternal[0].Endpoint, accountDeserSettings.WriteLocationsInternal[0].Endpoint);
            Assert.AreEqual(cosmosAccountSettings.ReadLocationsInternal[0].Name, accountDeserSettings.ReadLocationsInternal[0].Name);
            Assert.AreEqual(cosmosAccountSettings.ReadLocationsInternal[0].Endpoint, accountDeserSettings.ReadLocationsInternal[0].Endpoint);
            Assert.AreEqual(cosmosAccountSettings.AddressesLink, accountDeserSettings.AddressesLink);
            Assert.AreEqual(cosmosAccountSettings.Consistency.DefaultConsistencyLevel, accountDeserSettings.Consistency.DefaultConsistencyLevel);
            Assert.AreEqual(cosmosAccountSettings.ReplicationPolicy.AsyncReplication, accountDeserSettings.ReplicationPolicy.AsyncReplication);
            Assert.AreEqual(cosmosAccountSettings.ReadPolicy.PrimaryReadCoefficient, accountDeserSettings.ReadPolicy.PrimaryReadCoefficient);
        }

        [TestMethod]
        public void ConflictSettingsSerializeTest()
        {
            string id = Guid.NewGuid().ToString();

            ConflictProperties conflictSettings = new ConflictProperties()
            {
                Id = id,
                OperationKind = Cosmos.OperationKind.Create,
                ResourceType = typeof(StoredProcedureProperties)
            };

            Conflict conflict = new Conflict()
            {
                Id = id,
                OperationKind = OperationKind.Create,
                ResourceType = typeof(StoredProcedure)
            };

            string cosmosSerialized = SettingsContractTests.CosmosSerialize(conflictSettings);
            string directSerialized = SettingsContractTests.DirectSerialize(conflict);

            // Swap de-serialize and validate 
            ConflictProperties conflictDeserSettings = SettingsContractTests.CosmosDeserialize<ConflictProperties>(directSerialized);
            Conflict conflictDeser = SettingsContractTests.DirectDeSerialize<Conflict>(cosmosSerialized);

            Assert.AreEqual(conflictDeserSettings.Id, conflictDeser.Id);
            Assert.AreEqual((int)conflictDeserSettings.OperationKind, (int)conflictDeser.OperationKind);
            Assert.AreEqual(typeof(StoredProcedure), conflictDeser.ResourceType);
            Assert.AreEqual(typeof(StoredProcedureProperties), conflictDeserSettings.ResourceType);
            Assert.AreEqual(conflictDeserSettings.Id, conflict.Id);
        }

        [TestMethod]
        public void ConflictSettingsDeSerializeTest()
        {
            string conflictResponsePayload = @"{
                 id: 'Conflict1',
                 operationType: 'Replace',
                 resourceType: 'trigger'
                }";

            ConflictProperties conflictSettings = SettingsContractTests.CosmosDeserialize<ConflictProperties>(conflictResponsePayload);
            Conflict conflict = SettingsContractTests.DirectDeSerialize<Conflict>(conflictResponsePayload);

            Assert.AreEqual(conflict.Id, conflictSettings.Id);
            Assert.AreEqual((int)conflictSettings.OperationKind, (int)conflict.OperationKind);
            Assert.AreEqual(typeof(Trigger), conflict.ResourceType);
            Assert.AreEqual(typeof(TriggerProperties), conflictSettings.ResourceType);

            Assert.AreEqual("Conflict1", conflictSettings.Id);
        }

        [TestMethod]
        public void ChangeFeedPolicySerialization()
        {
            ContainerProperties containerSettings = new ContainerProperties("TestContainer", "/partitionKey");
            string serialization = JsonConvert.SerializeObject(containerSettings);
            Assert.IsFalse(serialization.Contains(Constants.Properties.ChangeFeedPolicy), "Change Feed Policy should not be included by default");

            TimeSpan desiredTimeSpan = TimeSpan.FromHours(1);
            containerSettings.ChangeFeedPolicy = new Cosmos.ChangeFeedPolicy() { FullFidelityRetention = desiredTimeSpan };
            string serializationWithValues = JsonConvert.SerializeObject(containerSettings);
            Assert.IsTrue(serializationWithValues.Contains(Constants.Properties.ChangeFeedPolicy), "Change Feed Policy should be included");
            Assert.IsTrue(serializationWithValues.Contains(Constants.Properties.LogRetentionDuration), "Change Feed Policy retention should be included");

            JObject parsed = JObject.Parse(serializationWithValues);
            JToken retentionValue = parsed[Constants.Properties.ChangeFeedPolicy][Constants.Properties.LogRetentionDuration];
            Assert.AreEqual(JTokenType.Integer, retentionValue.Type, "Change Feed Policy serialized retention should be an integer");
            Assert.AreEqual((int)desiredTimeSpan.TotalMinutes, retentionValue.Value<int>(), "Change Feed Policy serialized retention value incorrect");
        }

        [TestMethod]
        public void ChangeFeedPolicySerialization_Disabled()
        {
            ContainerProperties containerSettings = new ContainerProperties("TestContainer", "/partitionKey");
            string serialization = JsonConvert.SerializeObject(containerSettings);
            Assert.IsFalse(serialization.Contains(Constants.Properties.ChangeFeedPolicy), "Change Feed Policy should not be included by default");

            containerSettings.ChangeFeedPolicy = new Cosmos.ChangeFeedPolicy() { FullFidelityRetention = Cosmos.ChangeFeedPolicy.FullFidelityNoRetention };
            string serializationWithValues = JsonConvert.SerializeObject(containerSettings);
            Assert.IsTrue(serializationWithValues.Contains(Constants.Properties.ChangeFeedPolicy), "Change Feed Policy should be included");
            Assert.IsTrue(serializationWithValues.Contains(Constants.Properties.LogRetentionDuration), "Change Feed Policy retention should be included");

            JObject parsed = JObject.Parse(serializationWithValues);
            JToken retentionValue = parsed[Constants.Properties.ChangeFeedPolicy][Constants.Properties.LogRetentionDuration];
            Assert.AreEqual(JTokenType.Integer, retentionValue.Type, "Change Feed Policy serialized retention should be an integer");
            Assert.AreEqual(0, retentionValue.Value<int>(), "Change Feed Policy serialized retention value incorrect");
        }

        [TestMethod]
        public void ChangeFeedPolicySerialization_InvalidValues()
        {
            ContainerProperties containerSettings = new ContainerProperties("TestContainer", "/partitionKey");
            string serialization = JsonConvert.SerializeObject(containerSettings);
            Assert.IsFalse(serialization.Contains(Constants.Properties.ChangeFeedPolicy), "Change Feed Policy should not be included by default");

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Cosmos.ChangeFeedPolicy() { FullFidelityRetention = TimeSpan.FromSeconds(10) });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Cosmos.ChangeFeedPolicy() { FullFidelityRetention = TimeSpan.FromMilliseconds(10) });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Cosmos.ChangeFeedPolicy() { FullFidelityRetention = TimeSpan.FromSeconds(-10) });
        }

        [TestMethod]
        public void VectorEmbeddingPolicySerialization()
        {
            ContainerProperties containerSettings = new ContainerProperties("TestContainer", "/pk");
            string serialization = JsonConvert.SerializeObject(containerSettings);
            Assert.IsFalse(serialization.Contains("vectorEmbeddingPolicy"), "Vector Embedding Policy should not be included by default");

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

            Collection<Cosmos.Embedding> embeddings = new()
            {
                embedding1,
                embedding2,
            };

            containerSettings.VectorEmbeddingPolicy = new Cosmos.VectorEmbeddingPolicy(embeddings);

            string serializationWithValues = JsonConvert.SerializeObject(containerSettings);
            Assert.IsTrue(serializationWithValues.Contains("vectorEmbeddingPolicy"), "Vector Embedding Policy should be included.");
            Assert.IsTrue(serializationWithValues.Contains("distanceFunction"), "Vector Embedding Policy distance function should be included.");

            JObject parsed = JObject.Parse(serializationWithValues);
            JToken vectorEmbeddings = parsed["vectorEmbeddingPolicy"]["vectorEmbeddings"];
            Assert.AreEqual(JTokenType.Array, vectorEmbeddings.Type, "Vector Embedding Policy serialized vectorEmbeddings should be an array.");
            Assert.IsTrue(embedding1.Equals(vectorEmbeddings.Value<JArray>()[0].ToObject<Cosmos.Embedding>()));
            Assert.IsTrue(embedding2.Equals(vectorEmbeddings.Value<JArray>()[1].ToObject<Cosmos.Embedding>()));
        }

        [TestMethod]
        public void FullTextPolicySerialization()
        {
            ContainerProperties containerSettings = new ContainerProperties("TestContainer", "/pk");
            string serialization = JsonConvert.SerializeObject(containerSettings);
            Assert.IsFalse(serialization.Contains("fullTextPolicy"), "Full Text Policy should not be included by default");

            string defaultLanguage = "en-US", path1 = "/fts1", path2 = "/fts2", path3 = "/fts3";

            FullTextPath fullTextPath1 = new Cosmos.FullTextPath()
            {
                Path = path1,
                Language = "en-US",
            };

            FullTextPath fullTextPath2 = new Cosmos.FullTextPath()
            {
                Path = path2,
                Language = "en-US",
            };

            FullTextPath fullTextPath3 = new Cosmos.FullTextPath()
            {
                Path = path3,
                Language = "en-US",
            };

            Collection<FullTextPath> fullTextPaths = new Collection<FullTextPath>()
                {
                    fullTextPath1,
                    fullTextPath2,
                    fullTextPath3,
                };

            containerSettings.FullTextPolicy = new Cosmos.FullTextPolicy()
            {
                DefaultLanguage = defaultLanguage,
                FullTextPaths = fullTextPaths,
            };

            string serializationWithValues = JsonConvert.SerializeObject(containerSettings);
            Assert.IsTrue(serializationWithValues.Contains("fullTextPolicy"), "Full Text Policy should be included.");

            JObject parsed = JObject.Parse(serializationWithValues);
            JToken fullTextPathsDeSerialized = parsed["fullTextPolicy"]["fullTextPaths"];
            JToken fullTextLanguageDeSerialized = parsed["fullTextPolicy"]["defaultLanguage"];
            Assert.AreEqual(JTokenType.Array, fullTextPathsDeSerialized.Type, "Full Text Policy serialized paths should be an array.");
            Assert.AreEqual(JTokenType.String, fullTextLanguageDeSerialized.Type, "Full Text Policy serialized language should be a string.");
            Assert.IsTrue(fullTextPath1.Equals(fullTextPathsDeSerialized.Value<JArray>()[0].ToObject<Cosmos.FullTextPath>()));
            Assert.IsTrue(fullTextPath2.Equals(fullTextPathsDeSerialized.Value<JArray>()[1].ToObject<Cosmos.FullTextPath>()));
        }

        private static T CosmosDeserialize<T>(string payload)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                StreamWriter sw = new StreamWriter(ms, new UTF8Encoding(false, true), 1024, leaveOpen: true);
                sw.Write(payload);
                sw.Flush();

                ms.Position = 0;
                return CosmosResource.FromStream<T>(ms);
            }
        }

        private static T DirectDeSerialize<T>(string payload) where T : JsonSerializable, new()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                StreamWriter sw = new StreamWriter(ms, new UTF8Encoding(false, true), 1024, leaveOpen: true);
                sw.Write(payload);
                sw.Flush();

                ms.Position = 0;
                return JsonSerializable.LoadFrom<T>(ms);
            }
        }

        private static string CosmosSerialize(object input)
        {
            using (Stream stream = CosmosResource.ToStream(input))
            {
                using (StreamReader sr = new StreamReader(stream))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        private static string DirectSerialize<T>(T input) where T : JsonSerializable
        {
            using (MemoryStream ms = new MemoryStream())
            {
                input.SaveTo(ms);
                ms.Position = 0;

                using (StreamReader sr = new StreamReader(ms))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        private static void TypeAccessorGuard(Type input, params string[] publicSettable)
        {
            // All properties are public readable only by-default
            PropertyInfo[] allProperties = input.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (PropertyInfo pInfo in allProperties)
            {
                MethodInfo[] accessors = pInfo.GetAccessors();
                foreach (MethodInfo m in accessors)
                {
                    if (m.ReturnType == typeof(void))
                    {
                        // Set accessor 
                        bool publicSetAllowed = publicSettable.Where(e => m.Name.EndsWith("_" + e)).Any();
                        Assert.AreEqual(publicSetAllowed, m.IsPublic, m.ToString());
                        Assert.IsFalse(m.IsVirtual, m.ToString());
                    }
                    else
                    {
                        // get accessor 
                        Assert.IsTrue(m.IsPublic, m.ToString());
                        Assert.IsFalse(m.IsVirtual, m.ToString());
                    }
                }
            }
        }

        private void AssertEnums<TFirstEnum, TSecondEnum>() where TFirstEnum : struct, IConvertible where TSecondEnum : struct, IConvertible
        {
            string[] allCosmosEntries = Enum.GetNames(typeof(TFirstEnum));
            string[] allDocumentsEntries = Enum.GetNames(typeof(TSecondEnum));

            CollectionAssert.AreEqual(allCosmosEntries, allDocumentsEntries);

            foreach (string entry in allCosmosEntries)
            {

                Enum.TryParse<TFirstEnum>(entry, out TFirstEnum cosmosVersion);
                Enum.TryParse<TSecondEnum>(entry, out TSecondEnum documentssVersion);

                Assert.AreEqual(Convert.ToInt32(documentssVersion), Convert.ToInt32(cosmosVersion));
            }
        }

        private void AssertEnumsContains<TFirstEnum, TSecondEnum>() where TFirstEnum : struct, IConvertible where TSecondEnum : struct, IConvertible
        {
            string[] allCosmosEntries = Enum.GetNames(typeof(TFirstEnum));
            string[] allDocumentsEntries = Enum.GetNames(typeof(TSecondEnum));

            foreach (string entry in allDocumentsEntries)
            {
                Assert.IsTrue(allCosmosEntries.Contains(entry));
            }
        }
    }
}