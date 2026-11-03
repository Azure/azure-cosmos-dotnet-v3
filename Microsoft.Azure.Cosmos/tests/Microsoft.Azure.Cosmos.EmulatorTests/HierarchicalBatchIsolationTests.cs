//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("Batch")]
    public class HierarchicalBatchIsolationTests : BaseCosmosClientHelper
    {
        [TestInitialize]
        public async Task InitializeAsync()
        {
            await this.TestInit();
        }

        [TestCleanup]
        public async Task CleanupAsync()
        {
            await this.TestCleanup();
        }

        [DataTestMethod]
        [DataRow(2)]
        [DataRow(3)]
        [Timeout(60000)]
        public async Task CompleteKeysIsolateAtomicWritesAndRejectIncompletePrefixes(int depth)
        {
            Assert.IsTrue(depth == 2 || depth == 3);
            ContainerProperties properties = new ContainerProperties
            {
                Id = Guid.NewGuid().ToString(),
                PartitionKeyPaths = new[] { "/pk", "/child", "/leaf" }.Take(depth).ToArray(),
            };
            Container container = await this.database.CreateContainerAsync(properties, throughput: 400);
            string id = Guid.NewGuid().ToString();
            Dictionary<string, PartitionKey> keys = new Dictionary<string, PartitionKey>();
            foreach (string component in new[] { "null", "undefined", "number" })
            {
                PartitionKeyBuilder builder = new PartitionKeyBuilder().Add("tenant");
                JObject item = new JObject { ["id"] = id, ["pk"] = "tenant", ["value"] = component };
                switch (component)
                {
                    case "null":
                        builder.AddNullValue();
                        item["child"] = JValue.CreateNull();
                        break;
                    case "undefined":
                        builder.AddNoneType();
                        break;
                    case "number":
                        builder.Add(42);
                        item["child"] = 42;
                        break;
                }

                if (depth == 3)
                {
                    builder.Add(false);
                    item["leaf"] = false;
                }

                PartitionKey key = builder.Build();
                keys.Add(component, key);
                using TransactionalBatchResponse response = await container.CreateTransactionalBatch(key)
                    .CreateItem(item)
                    .ReadItem(id)
                    .ExecuteAsync(this.cancellationToken);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, response.ErrorMessage);
                Assert.AreEqual(2, response.Count);
                Assert.AreEqual(HttpStatusCode.Created, response[0].StatusCode);
                Assert.AreEqual(HttpStatusCode.OK, response[1].StatusCode);
                Validate(response.GetOperationResultAtIndex<JObject>(1).Resource, component);
            }

            foreach (KeyValuePair<string, PartitionKey> entry in keys)
            {
                ItemResponse<JObject> persisted = await container.ReadItemAsync<JObject>(
                    id, entry.Value, cancellationToken: this.cancellationToken);
                Validate(persisted.Resource, entry.Key);
            }

            for (int prefixLength = 1; prefixLength < depth; prefixLength++)
            {
                PartitionKeyBuilder prefix = new PartitionKeyBuilder().Add("tenant");
                if (prefixLength == 2)
                {
                    prefix.Add(42);
                }

                string rejectedId = id + "-prefix-" + prefixLength;
                JObject item = new JObject
                {
                    ["id"] = rejectedId, ["pk"] = "tenant", ["child"] = 42, ["value"] = "must-not-persist",
                };
                if (depth == 3)
                {
                    item["leaf"] = false;
                }

                using TransactionalBatchResponse rejected = await container.CreateTransactionalBatch(prefix.Build())
                    .CreateItem(item)
                    .ExecuteAsync(this.cancellationToken);
                Assert.AreEqual(HttpStatusCode.BadRequest, rejected.StatusCode, rejected.ErrorMessage);
                using ResponseMessage absent = await container.ReadItemStreamAsync(
                    rejectedId, keys["number"], cancellationToken: this.cancellationToken);
                Assert.AreEqual(HttpStatusCode.NotFound, absent.StatusCode, "A rejected prefix batch must not commit an item.");
            }

            void Validate(JObject item, string component)
            {
                Assert.AreEqual(id, (string)item["id"]);
                Assert.AreEqual("tenant", (string)item["pk"]);
                Assert.AreEqual(component, (string)item["value"]);
                if (component == "undefined")
                {
                    Assert.IsNull(item.Property("child"));
                }
                else if (component == "null")
                {
                    Assert.AreEqual(JTokenType.Null, item["child"].Type);
                }
                else
                {
                    Assert.AreEqual(42, (int)item["child"]);
                }

                if (depth == 3)
                {
                    Assert.IsFalse((bool)item["leaf"]);
                }
            }
        }
    }
}
