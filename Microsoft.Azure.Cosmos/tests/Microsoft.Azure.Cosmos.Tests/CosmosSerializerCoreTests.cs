//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class CosmosSerializerCoreTests
    {
        [TestMethod]
        public void ValidateSqlQuerySpecSerialization()
        {
            int toCount = 0;
            int fromCount = 0;

            CosmosSerializerHelper serializerHelper = new CosmosSerializerHelper(
                null,
                (input) => fromCount++,
                (input) => toCount++);

            CosmosSerializerCore serializerCore = new CosmosSerializerCore(serializerHelper);
            SqlQuerySpec querySpec = new SqlQuerySpec("select * from T where T.id = @id")
            {
                Parameters = new SqlParameterCollection()
            {
                new SqlParameter("@id", "testValue")
            }
            };

            try
            {
                serializerCore.ToStream<SqlQuerySpec>(querySpec);
                Assert.Fail("ToStream should throw exception");
            }
            catch (ArgumentException e)
            {
                Assert.IsNotNull(e);
            }

            try
            {
                serializerCore.FromStream<SqlQuerySpec>(new MemoryStream());
                Assert.Fail("FromStream should throw exception");
            }
            catch (ArgumentException e)
            {
                Assert.IsNotNull(e);
            }

            Assert.AreEqual(0, toCount);
            Assert.AreEqual(0, fromCount);

            using (Stream stream = serializerCore.ToStreamSqlQuerySpec(querySpec, ResourceType.Offer)) { }

            Assert.AreEqual(0, toCount);
            Assert.AreEqual(0, fromCount);

            List<ResourceType> publicQuerySupportedTypes = new List<ResourceType>()
            {
                ResourceType.Database,
                ResourceType.Collection,
                ResourceType.Document,
                ResourceType.Trigger,
                ResourceType.UserDefinedFunction,
                ResourceType.StoredProcedure,
                ResourceType.Permission,
                ResourceType.User,
                ResourceType.Conflict
            };

            foreach (ResourceType resourceType in publicQuerySupportedTypes)
            {
                toCount = 0;

                using (Stream stream = serializerCore.ToStreamSqlQuerySpec(querySpec, resourceType))
                {
                    Assert.AreEqual(1, toCount);
                    Assert.AreEqual(0, fromCount);
                }
            }
        }

        [TestMethod]
        public void ValidatePatchOperationSerialization()
        {
            int toCount = 0;
            int fromCount = 0;

            CosmosSerializerHelper serializerHelper = new CosmosSerializerHelper(
                null,
                (input) => fromCount++,
                (input) => toCount++);

            CosmosSerializerCore serializerCore = new CosmosSerializerCore(serializerHelper);
            List<PatchOperation> patch = new List<PatchOperation>()
            {
                PatchOperation.Remove("/removePath")
            };

            Assert.AreEqual(0, toCount);

            PatchItemRequestOptions patchRequestOptions = new PatchItemRequestOptions();

            // custom serializer is not used since operation type is Remove, which doesnt have "value" param to serialize
            using (Stream stream = serializerCore.ToStream(new PatchSpec(patch, patchRequestOptions))) { }
            Assert.AreEqual(0, toCount);

            patch.Add(PatchOperation.Add("/addPath", "addValue"));
            // custom serializer is used since there is Add operation type also
            using (Stream stream = serializerCore.ToStream(new PatchSpec(patch, patchRequestOptions))) { }
            Assert.AreEqual(1, toCount);

            patch.Clear();
            toCount = 0;
            patch.Add(PatchOperation.Add("/addPath", new CosmosJsonDotNetSerializer().ToStream("addValue")));
            // custom serializer is not used since the input value is of type stream
            using (Stream stream = serializerCore.ToStream(new PatchSpec(patch, patchRequestOptions))) { }
            Assert.AreEqual(0, toCount);
        }

        [TestMethod]
        public void ValidateCustomSerializerNotUsedForInternalTypes()
        {
            CosmosSerializerHelper serializerHelper = new CosmosSerializerHelper(
               null,
               (item) => throw new ArgumentException("Should be using internal serializer"),
               (item) => throw new ArgumentException("Should be using internal serializer"));

            CosmosSerializerCore serializerCore = new CosmosSerializerCore(serializerHelper);

            string id = "testId";
            this.TestProperty<AccountProperties>(
                serializerCore,
                id,
                $@"{{""id"":""{id}"",""writableLocations"":[],""readableLocations"":[],""userConsistencyPolicy"":null,""addresses"":null,""userReplicationPolicy"":null,""systemReplicationPolicy"":null,""readPolicy"":null,""queryEngineConfiguration"":null,""enableMultipleWriteLocations"":false}}");

            this.TestProperty<DatabaseProperties>(
                serializerCore,
                id,
                $@"{{""id"":""{id}""}}");

            this.TestProperty<ContainerProperties>(
                serializerCore,
                id,
                $@"{{""id"":""{id}"",""partitionKey"":{{""paths"":[],""kind"":""Hash""}}}}");

            this.TestProperty<StoredProcedureProperties>(
                serializerCore,
                id,
                $@"{{""body"":""bodyCantBeNull"",""id"":""{id}""}}");

            this.TestProperty<TriggerProperties>(
                serializerCore,
                id,
                $@"{{""body"":null,""triggerType"":""Pre"",""triggerOperation"":""All"",""id"":""{id}""}}");

            this.TestProperty<UserDefinedFunctionProperties>(
                serializerCore,
                id,
                $@"{{""body"":null,""id"":""{id}""}}");

            this.TestProperty<UserProperties>(
                serializerCore,
                id,
                $@"{{""id"":""{id}"",""_permissions"":null}}");

            this.TestProperty<PermissionProperties>(
                serializerCore,
                id,
                $@"{{""id"":""{id}"",""resource"":null,""permissionMode"":0}}");

            this.TestProperty<ConflictProperties>(
                serializerCore,
                id,
                $@"{{""id"":""{id}"",""operationType"":""Invalid"",""resourceType"":null,""resourceId"":null,""content"":null,""conflict_lsn"":0}}");

            // Throughput doesn't have an id.
            string defaultThroughputJson = @"{}";
            ThroughputProperties property = JsonConvert.DeserializeObject<ThroughputProperties>(defaultThroughputJson);
            Assert.IsNull(property.Throughput);
            string propertyJson = JsonConvert.SerializeObject(property);
            Assert.AreEqual(defaultThroughputJson, propertyJson);
        }

        private void TestProperty<T>(
            CosmosSerializerCore serializerCore,
            string id,
            string defaultJson)
        {
            dynamic property = serializerCore.FromStream<T>(new MemoryStream(Encoding.UTF8.GetBytes(defaultJson)));
            Assert.AreEqual(id, property.Id);
            using (Stream stream = serializerCore.ToStream<T>(property))
            {
                using (StreamReader sr = new StreamReader(stream))
                {
                    string propertyJson = sr.ReadToEnd();
                    Assert.AreEqual(defaultJson, propertyJson);
                }
            }
        }
    }
}