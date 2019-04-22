//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class SettingsContractTests
    {
        [TestMethod]
        public void DatabaseSettingsDefaults()
        {
            CosmosDatabaseSettings dbSettings = new CosmosDatabaseSettings();

            Assert.IsNull(dbSettings.LastModified);
            Assert.IsNull(dbSettings.ResourceId);
            Assert.IsNull(dbSettings.Id);
            Assert.IsNull(dbSettings.ETag);

            SettingsContractTests.TypeAccessorGuard(typeof(CosmosDatabaseSettings), "Id");
        }

        [TestMethod]
        public void StoredProecdureSettingsDefaults()
        {
            CosmosStoredProcedureSettings dbSettings = new CosmosStoredProcedureSettings();

            Assert.IsNull(dbSettings.LastModified);
            Assert.IsNull(dbSettings.ResourceId);
            Assert.IsNull(dbSettings.Id);
            Assert.IsNull(dbSettings.ETag);

            SettingsContractTests.TypeAccessorGuard(typeof(CosmosStoredProcedureSettings), "Id", "Body");
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

            CosmosDatabaseSettings deserializedPayload = 
                JsonConvert.DeserializeObject<CosmosDatabaseSettings>(testPyaload);

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

            CosmosContainerSettings deserializedPayload =
                JsonConvert.DeserializeObject<CosmosContainerSettings>(testPyaload);

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

            CosmosStoredProcedureSettings deserializedPayload =
                JsonConvert.DeserializeObject<CosmosStoredProcedureSettings>(testPyaload);

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

            CosmosDatabaseSettings databaseSettings = new CosmosDatabaseSettings()
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
            CosmosDatabaseSettings dbDeserSettings = SettingsContractTests.CosmosDeserialize<CosmosDatabaseSettings>(directSerialized);
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

            CosmosDatabaseSettings databaseSettings = SettingsContractTests.CosmosDeserialize<CosmosDatabaseSettings>(dbResponsePayload);
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
            CosmosContainerSettings cosmosContainerSettings = new CosmosContainerSettings(id, pkPath);
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
            CosmosContainerSettings containerDeserSettings = SettingsContractTests.CosmosDeserialize<CosmosContainerSettings>(directSerialized);
            DocumentCollection collectionDeser = SettingsContractTests.DirectDeSerialize<DocumentCollection>(cosmosSerialized);

            Assert.AreEqual(collection.Id, containerDeserSettings.Id);
            Assert.AreEqual(collection.PartitionKey.Paths[0], containerDeserSettings.PartitionKeyPath);

            Assert.AreEqual(cosmosContainerSettings.Id, collectionDeser.Id);
            Assert.AreEqual(cosmosContainerSettings.PartitionKeyPath, collectionDeser.PartitionKey.Paths[0]);
        }

        [TestMethod]
        public void ContainerSettingsWithIndexingPolicyTest()
        {
            string id = Guid.NewGuid().ToString();
            string pkPath = "/partitionKey";

            // Two equivalent definitions 
            CosmosContainerSettings cosmosContainerSettings = new CosmosContainerSettings(id, pkPath);
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
            CosmosContainerSettings containerDeserSettings = SettingsContractTests.CosmosDeserialize<CosmosContainerSettings>(directSerialized);
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

            SettingsContractTests.TypeAccessorGuard(typeof(CosmosContainerSettings), "Id", "UniqueKeyPolicy", "DefaultTimeToLive", "IndexingPolicy");

            // Two equivalent definitions 
            CosmosContainerSettings cosmosContainerSettings = new CosmosContainerSettings(id, pkPath);

            Assert.AreEqual(id, cosmosContainerSettings.Id);
            Assert.AreEqual(pkPath, cosmosContainerSettings.PartitionKeyPath);

            Assert.IsNull(cosmosContainerSettings.ResourceId);
            Assert.IsNull(cosmosContainerSettings.LastModified);
            Assert.IsNull(cosmosContainerSettings.ETag);
            Assert.IsNull(cosmosContainerSettings.DefaultTimeToLive);

            Assert.IsNotNull(cosmosContainerSettings.IndexingPolicy);
            Assert.IsTrue(object.ReferenceEquals(cosmosContainerSettings.IndexingPolicy, cosmosContainerSettings.IndexingPolicy));
            Assert.IsNotNull(cosmosContainerSettings.IndexingPolicy.IncludedPaths);
            Assert.IsTrue(object.ReferenceEquals(cosmosContainerSettings.IndexingPolicy.IncludedPaths, cosmosContainerSettings.IndexingPolicy.IncludedPaths));

            Cosmos.IncludedPath ip = new Cosmos.IncludedPath();
            Assert.IsNotNull(ip.Indexes);

            Assert.IsNotNull(cosmosContainerSettings.UniqueKeyPolicy);
            Assert.IsTrue(object.ReferenceEquals(cosmosContainerSettings.UniqueKeyPolicy, cosmosContainerSettings.UniqueKeyPolicy));
            Assert.IsNotNull(cosmosContainerSettings.UniqueKeyPolicy.UniqueKeys);
            Assert.IsTrue(object.ReferenceEquals(cosmosContainerSettings.UniqueKeyPolicy.UniqueKeys, cosmosContainerSettings.UniqueKeyPolicy.UniqueKeys));

            Cosmos.UniqueKey uk = new Cosmos.UniqueKey();
            Assert.IsNotNull(uk.Paths);
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

        private static T DirectDeSerialize<T>(string payload) where T: JsonSerializable, new()
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

        private static string DirectSerialize<T>(T input) where T: JsonSerializable
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
            PropertyInfo[] allProperties = input.GetProperties(BindingFlags.Instance|BindingFlags.Public);
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
                        Assert.AreEqual(publicSetAllowed, m.IsVirtual, m.ToString());
                    }
                    else
                    {
                        // get accessor 
                        Assert.IsTrue(m.IsPublic, m.ToString());
                        Assert.IsTrue(m.IsVirtual, m.ToString());
                    }
                }
            }
        }
    }
}
