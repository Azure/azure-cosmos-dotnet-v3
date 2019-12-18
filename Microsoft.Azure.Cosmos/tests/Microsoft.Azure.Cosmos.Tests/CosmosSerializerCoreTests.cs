//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
            SqlQuerySpec querySpec = new SqlQuerySpec("select * from T where T.id = @id");
            querySpec.Parameters = new SqlParameterCollection()
            {
                new SqlParameter("@id", "testValue")
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
        public void ValidateCustomSerializerNotUsedForInternalTypes()
        {
            CosmosSerializerHelper serializerHelper = new CosmosSerializerHelper(
               null,
               (item) => throw new ArgumentException("Should be using internal serializer"),
               (item) => throw new ArgumentException("Should be using internal serializer"));

            CosmosSerializerCore serializerCore = new CosmosSerializerCore(serializerHelper);

            this.TestSerialize(
                new ContainerProperties()
                {
                    Id = Guid.NewGuid().ToString()
                },
                serializerCore,
                (input, result) => string.Equals(input.Id, result.Id));

            this.TestSerialize(
                new DatabaseProperties()
                {
                    Id = Guid.NewGuid().ToString()
                },
                serializerCore,
                (input, result) => string.Equals(input.Id, result.Id));

            this.TestSerialize(
                new TriggerProperties()
                {
                    Id = Guid.NewGuid().ToString()
                },
                serializerCore,
                (input, result) => string.Equals(input.Id, result.Id));

            this.TestSerialize(
                new UserDefinedFunctionProperties()
                {
                    Id = Guid.NewGuid().ToString()
                },
                serializerCore,
                (input, result) => string.Equals(input.Id, result.Id));

            this.TestSerialize(
                new StoredProcedureProperties()
                {
                    Id = Guid.NewGuid().ToString(),
                    Body = "test body"
                },
                serializerCore,
                (input, result) => string.Equals(input.Id, result.Id));

            this.TestSerialize(
                new ConflictProperties()
                {
                    Id = Guid.NewGuid().ToString()
                },
                serializerCore,
                (input, result) => string.Equals(input.Id, result.Id));

            this.TestSerialize(
                new AccountProperties()
                {
                    Id = Guid.NewGuid().ToString()
                },
                serializerCore,
                (input, result) => string.Equals(input.Id, result.Id));

            PermissionProperties permissionProperties = JsonConvert.DeserializeObject<PermissionProperties>(
                "{\"id\":\"permissionId\"}");

            this.TestSerialize(
                permissionProperties,
                serializerCore,
                (input, result) => string.Equals(input.Id, result.Id));

            this.TestSerialize(
                new UserProperties(Guid.NewGuid().ToString()),
                serializerCore,
                (input, result) => string.Equals(input.Id, result.Id));

            ThroughputProperties throughputProperties = JsonConvert.DeserializeObject<ThroughputProperties>(
                "{\"_etag\":\"etagValue\"}");

            this.TestSerialize(
                new ThroughputProperties(),
                serializerCore,
                (input, result) => true);

            this.TestSerialize(
                new OfferV2(9001)
                {
                    Id = Guid.NewGuid().ToString(),
                },
                serializerCore,
                (input, result) => string.Equals(input.Id, result.Id));

            this.TestSerialize(
                new PartitionedQueryExecutionInfo()
                {
                    QueryInfo = new QueryInfo()
                    {
                        DistinctType = Cosmos.Query.Core.ExecutionComponent.Distinct.DistinctQueryType.Ordered,
                        Limit = 500
                    }
                },
                serializerCore,
                (input, result) => input.QueryInfo.Limit == result.QueryInfo.Limit && input.QueryInfo.DistinctType == result.QueryInfo.DistinctType);
        }

        private void TestSerialize<T>(
            T input,
            CosmosSerializerCore serializerCore,
            Func<T, T, bool> comparer)
        {
            Stream stream = serializerCore.ToStream(input);
            Assert.IsTrue(stream.CanRead);

            T result = serializerCore.FromStream<T>(stream);
            Assert.IsNotNull(result);
            Assert.IsFalse(stream.CanRead, "Stream should be disposed of");

            Assert.IsTrue(comparer(input, result));
        }
    }
}
