// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;
    using PartitionKey = Cosmos.PartitionKey;

    /// <summary>
    /// Live tests requiring COSMOS_DTX_ENDPOINT and COSMOS_DTX_KEY.
    /// Each case owns a unique database; no existing account data is read or changed.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    [TestCategory("DistributedTransactionLive")]
    public class DistributedTransactionSerializationE2ETests
    {
        private CosmosClient admin;
        private string databaseId;
        private string originalBinary;
        private bool databaseCreated;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public async Task Initialize()
        {
            this.originalBinary = Environment.GetEnvironmentVariable("AZURE_COSMOS_BINARY_ENCODING_ENABLED");
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COSMOS_DTX_ENDPOINT"))
                || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COSMOS_DTX_KEY")))
            {
                Assert.Inconclusive("Set COSMOS_DTX_ENDPOINT and COSMOS_DTX_KEY to run live tests.");
            }

            Environment.SetEnvironmentVariable("AZURE_COSMOS_BINARY_ENCODING_ENABLED", "False");
            this.admin = this.CreateClient(new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway });
            this.databaseId = "DtxSerialization-" + Guid.NewGuid().ToString("N");
            Database database = (await this.admin.CreateDatabaseAsync(this.databaseId)).Database;
            this.databaseCreated = true;
            this.TestContext.WriteLine("Owned database: " + this.databaseId);
            await database.CreateContainerAsync(new ContainerProperties("a", "/pk"), throughput: 400);
            await database.CreateContainerAsync(new ContainerProperties("b", "/pk"), throughput: 400);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            try
            {
                if (this.databaseCreated)
                {
                    await this.admin.GetDatabase(this.databaseId).DeleteAsync();
                    using ResponseMessage deleted = await this.admin.GetDatabase(this.databaseId).ReadStreamAsync();
                    Assert.AreEqual(HttpStatusCode.NotFound, deleted.StatusCode);
                    this.TestContext.WriteLine("Deleted and verified absent: " + this.databaseId);
                }
            }
            finally
            {
                this.admin?.Dispose();
                Environment.SetEnvironmentVariable("AZURE_COSMOS_BINARY_ENCODING_ENABLED", this.originalBinary);
            }
        }

        [TestMethod]
        [DataRow(false, false, false)]
        [DataRow(false, true, false)]
        [DataRow(true, false, false)]
        [DataRow(true, true, false)]
        [DataRow(false, false, true)]
        [DataRow(false, true, true)]
        [DataRow(true, false, true)]
        [DataRow(true, true, true)]
        public async Task ConfiguredSerialization(bool binary, bool options, bool direct)
        {
            Environment.SetEnvironmentVariable("AZURE_COSMOS_BINARY_ENCODING_ENABLED", binary.ToString());
            CaptureHandler capture = new CaptureHandler();
            using CosmosClient client = this.CreateClient(new CosmosClientOptions
            {
                ConnectionMode = direct ? ConnectionMode.Direct : ConnectionMode.Gateway,
                SerializerOptions = options ? new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                    IgnoreNullValues = true,
                    Indented = true,
                } : null,
            }, capture);
            await this.RunMixedAsync(client, capture, options, custom: false);
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(true, true)]
        public async Task CustomSerialization(bool binaryOutput, bool direct)
        {
            Environment.SetEnvironmentVariable("AZURE_COSMOS_BINARY_ENCODING_ENABLED", "True");
            TrackingSerializer serializer = new TrackingSerializer(binaryOutput);
            CaptureHandler capture = new CaptureHandler();
            using CosmosClient client = this.CreateClient(new CosmosClientOptions
            {
                ConnectionMode = direct ? ConnectionMode.Direct : ConnectionMode.Gateway,
                Serializer = serializer,
            }, capture);
            await this.RunMixedAsync(client, capture, camelCase: true, custom: true);
            Assert.IsTrue(serializer.Streams.Count > 0);
            Assert.IsTrue(serializer.Streams.TrueForAll(stream => stream.Disposed), "SDK-owned custom streams must be disposed.");
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task StreamPayloads(bool binary, bool seekable)
        {
            Environment.SetEnvironmentVariable("AZURE_COSMOS_BINARY_ENCODING_ENABLED", "True");
            CaptureHandler capture = new CaptureHandler();
            using CosmosClient client = this.CreateClient(new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
            }, capture);
            Container a = client.GetContainer(this.databaseId, "a");
            Container b = client.GetContainer(this.databaseId, "b");
            await this.SeedAsync("a", "replace");
            await this.SeedAsync("b", "patch");
            using Stream create = Payload("{\"id\":\"create\",\"pk\":\"create\",\"name\":\"stream\"}", binary, seekable);
            using Stream replace = Payload("{\"id\":\"replace\",\"pk\":\"replace\",\"name\":\"stream\"}", binary, seekable);
            using Stream upsert = Payload("{\"id\":\"upsert\",\"pk\":\"upsert\",\"name\":\"stream\"}", binary, seekable);
            using Stream patch = Payload("{\"operations\":[{\"op\":\"set\",\"path\":\"/name\",\"value\":\"stream\"}]}", binary, seekable);
            using DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItemStream(a, new PartitionKey("create"), "create", create)
                .ReplaceItemStream(a, new PartitionKey("replace"), "replace", replace)
                .UpsertItemStream(b, new PartitionKey("upsert"), "upsert", upsert)
                .PatchItemStream(b, new PartitionKey("patch"), "patch", patch)
                .ExecuteTransactionAsync();
            this.AssertSuccess(response, 4);
            Assert.IsTrue(create.CanRead && replace.CanRead && upsert.CanRead && patch.CanRead);
            foreach (string id in new[] { "create", "replace", "upsert", "patch" })
            {
                JObject stored = await this.ReadAsync(id == "create" || id == "replace" ? "a" : "b", id);
                Assert.AreEqual("stream", (string)stored["name"]);
            }

            Assert.AreEqual(1, capture.Requests);
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task PatchValueStreams(bool binary, bool seekable)
        {
            Environment.SetEnvironmentVariable("AZURE_COSMOS_BINARY_ENCODING_ENABLED", "True");
            CaptureHandler capture = new CaptureHandler();
            using CosmosClient client = this.CreateClient(new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
            }, capture);
            await this.SeedAsync("a", "patch");
            using Stream value = Payload("{\"nested\":[1,true,null,\"\\u03a9\"]}", binary, seekable, prefix: seekable);
            using DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .PatchItem(client.GetContainer(this.databaseId, "a"), new PartitionKey("patch"), "patch",
                    new[] { PatchOperation.Set("/streamValue", value) })
                .ExecuteTransactionAsync();
            this.AssertSuccess(response, 1);
            Assert.IsFalse(value.CanRead, "Patch value streams are owned by the serializer.");
            JObject stored = await this.ReadAsync("a", "patch");
            Assert.IsTrue(JToken.DeepEquals(JToken.Parse("{\"nested\":[1,true,null,\"\\u03a9\"]}"), stored["streamValue"]));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task RejectedPatchRollsBackOtherWrites(bool staleEtag)
        {
            Environment.SetEnvironmentVariable("AZURE_COSMOS_BINARY_ENCODING_ENABLED", "True");
            CaptureHandler capture = new CaptureHandler();
            using CosmosClient client = this.CreateClient(new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
                MaxRetryAttemptsOnAbortedTransactions = 0,
            }, capture);
            await this.SeedAsync("a", "patch");
            DistributedTransactionPatchItemRequestOptions options = new DistributedTransactionPatchItemRequestOptions();
            if (staleEtag)
            {
                JObject old = await this.ReadAsync("a", "patch");
                options.IfMatchEtag = (string)old["_etag"];
                await this.admin.GetContainer(this.databaseId, "a").PatchItemAsync<JObject>("patch", new PartitionKey("patch"),
                    new[] { PatchOperation.Set("/revision", 2) });
            }
            else
            {
                options.FilterPredicate = "FROM c WHERE c.count = -1";
            }

            using DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(client.GetContainer(this.databaseId, "b"), new PartitionKey("rollback"), "rollback",
                    new { id = "rollback", pk = "rollback" })
                .PatchItem(client.GetContainer(this.databaseId, "a"), new PartitionKey("patch"), "patch",
                    new[] { PatchOperation.Set("/count", 99) }, options)
                .ExecuteTransactionAsync();
            Assert.IsTrue(response.IsTransactionAborted, $"Expected a durable abort, got {(int)response.StatusCode}.");
            Assert.AreEqual(2, response.Count);
            this.TestContext.WriteLine("Expected rejection: " + (int)response.StatusCode);
            JObject stored = await this.ReadAsync("a", "patch");
            Assert.AreEqual(1, (int)stored["count"]);
            using ResponseMessage absent = await this.admin.GetContainer(this.databaseId, "b")
                .ReadItemStreamAsync("rollback", new PartitionKey("rollback"));
            Assert.AreEqual(HttpStatusCode.NotFound, absent.StatusCode);
            Assert.AreEqual(1, capture.Requests);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task LargeNestedPayloadAndNumericEdges(bool direct)
        {
            Environment.SetEnvironmentVariable("AZURE_COSMOS_BINARY_ENCODING_ENABLED", "True");
            CaptureHandler capture = new CaptureHandler();
            using CosmosClient client = this.CreateClient(new CosmosClientOptions
            {
                ConnectionMode = direct ? ConnectionMode.Direct : ConnectionMode.Gateway,
                SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
            }, capture);
            Container container = client.GetContainer(this.databaseId, "a");
            string large = new string('x', 128 * 1024) + "\u03a9";
            JObject nested = new JObject { ["leaf"] = true };
            for (int i = 0; i < 20; i++)
            {
                nested = new JObject { ["child"] = nested };
            }

            Dictionary<string, object> values = new Dictionary<string, object>
            {
                ["large"] = large,
                ["nested"] = nested,
                ["safeInteger"] = 9007199254740991L,
                ["negative"] = -42.125,
                ["zero"] = 0,
                ["emptyObject"] = new { },
                ["emptyArray"] = Array.Empty<int>(),
                ["bytes"] = new byte[] { 0, 1, 254, 255 },
                ["guid"] = Guid.Parse("12345678-1234-1234-1234-123456789abc"),
                ["timestamp"] = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc),
            };
            Dictionary<string, object> document = new Dictionary<string, object>(values)
            {
                ["id"] = "large",
                ["pk"] = "large",
            };
            await this.SeedAsync("b", "patch");
            List<PatchOperation> operations = new List<PatchOperation>();
            foreach (KeyValuePair<string, object> entry in values)
            {
                operations.Add(PatchOperation.Set("/" + entry.Key, entry.Value));
            }

            using DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(container, new PartitionKey("large"), "large", document)
                .PatchItem(client.GetContainer(this.databaseId, "b"), new PartitionKey("patch"), "patch", operations)
                .ExecuteTransactionAsync();
            this.AssertSuccess(response, 2);
            JObject created = await this.ReadAsync("a", "large");
            JObject patched = await this.ReadAsync("b", "patch");
            foreach (string key in values.Keys)
            {
                Assert.IsTrue(JToken.DeepEquals(created[key], patched[key]), "Mismatch for " + key);
            }

            Assert.AreEqual(large, (string)patched["large"]);
            Assert.IsTrue(JToken.DeepEquals(nested, patched["nested"]));
            Assert.AreEqual(9007199254740991L, (long)patched["safeInteger"]);
            Assert.AreEqual(-42.125, (double)patched["negative"]);
            Assert.AreEqual(0, (int)patched["zero"]);
            Assert.AreEqual(0, ((JObject)patched["emptyObject"]).Count);
            Assert.AreEqual(0, ((JArray)patched["emptyArray"]).Count);
            Assert.AreEqual("AAH+/w==", (string)patched["bytes"]);
            Assert.AreEqual("12345678-1234-1234-1234-123456789abc", (string)patched["guid"]);
            Assert.AreEqual((DateTime)values["timestamp"], ((DateTime)patched["timestamp"]).ToUniversalTime());
            using ResponseMessage raw = await this.admin.GetContainer(this.databaseId, "b").ReadItemStreamAsync("patch", new PartitionKey("patch"));
            raw.EnsureSuccessStatusCode();
            using System.Text.Json.JsonDocument json = await System.Text.Json.JsonDocument.ParseAsync(raw.Content);
            Assert.AreEqual((DateTime)values["timestamp"], json.RootElement.GetProperty("timestamp").GetDateTime().ToUniversalTime());
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task BinaryPointAndBatchDocumentsRemainSupported(bool direct)
        {
            Environment.SetEnvironmentVariable("AZURE_COSMOS_BINARY_ENCODING_ENABLED", "True");
            CaptureHandler capture = new CaptureHandler();
            using CosmosClient client = this.CreateClient(new CosmosClientOptions
            {
                ConnectionMode = direct ? ConnectionMode.Direct : ConnectionMode.Gateway,
                SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
            }, capture);
            Container a = client.GetContainer(this.databaseId, "a");
            ItemRequestOptions noBody = new ItemRequestOptions { EnableContentResponseOnWrite = false };
            await a.CreateItemAsync(new Document("point"), new PartitionKey("point"), noBody);
            await a.ReplaceItemAsync(new Document("point") { DisplayName = "replaced" }, "point", new PartitionKey("point"), noBody);
            await a.UpsertItemAsync(new Document("point") { DisplayName = "upserted" }, new PartitionKey("point"), noBody);
            Assert.AreEqual("upserted", (string)(await this.ReadAsync("a", "point"))["displayName"]);

            using (TransactionalBatchResponse create = await a.CreateTransactionalBatch(new PartitionKey("batch"))
                .CreateItem(new Document("batch")).ExecuteAsync())
            {
                Assert.IsTrue(create.IsSuccessStatusCode, "Batch create: " + create.StatusCode);
            }

            using (TransactionalBatchResponse replace = await a.CreateTransactionalBatch(new PartitionKey("batch"))
                .ReplaceItem("batch", new Document("batch") { DisplayName = "replaced" }).ExecuteAsync())
            {
                Assert.IsTrue(replace.IsSuccessStatusCode, "Batch replace: " + replace.StatusCode);
            }

            using (TransactionalBatchResponse upsert = await a.CreateTransactionalBatch(new PartitionKey("batch"))
                .UpsertItem(new Document("batch") { DisplayName = "upserted" }).ExecuteAsync())
            {
                Assert.IsTrue(upsert.IsSuccessStatusCode, "Batch upsert: " + upsert.StatusCode);
            }

            Assert.AreEqual("upserted", (string)(await this.ReadAsync("a", "batch"))["displayName"]);
            Assert.AreEqual(3, capture.BinaryPointRequests, "All point document bodies must retain binary encoding.");
        }

        [TestMethod]
        public async Task InvalidInputAndCancellationDoNotDispatch()
        {
            Environment.SetEnvironmentVariable("AZURE_COSMOS_BINARY_ENCODING_ENABLED", "True");
            CaptureHandler capture = new CaptureHandler();
            using CosmosClient client = this.CreateClient(new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
            }, capture);
            Container container = client.GetContainer(this.databaseId, "a");
            await this.SeedAsync("a", "patch");
            Assert.ThrowsException<ArgumentNullException>(() => client.CreateDistributedWriteTransaction()
                .PatchItem(container, new PartitionKey("patch"), "patch", Array.Empty<PatchOperation>()));

            using Stream encoded = Payload("{\"value\":42}", binary: true, seekable: true);
            using MemoryStream buffer = new MemoryStream();
            encoded.CopyTo(buffer);
            byte[] truncated = buffer.ToArray();
            Array.Resize(ref truncated, truncated.Length - 1);
            using TrackedStream invalid = new TrackedStream(truncated);
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => client.CreateDistributedWriteTransaction()
                .PatchItem(container, new PartitionKey("patch"), "patch", new[] { PatchOperation.Set("/invalid", invalid) })
                .ExecuteTransactionAsync());
            Assert.IsTrue(invalid.Disposed);
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsExceptionAsync<CosmosOperationCanceledException>(() => client.CreateDistributedWriteTransaction()
                .CreateItem(container, new PartitionKey("cancelled"), "cancelled", new Document("cancelled"))
                .ExecuteTransactionAsync(cancellation.Token));
            Assert.AreEqual(0, capture.Requests);
            Assert.IsNull((await this.ReadAsync("a", "patch"))["invalid"]);
            using ResponseMessage absent = await container.ReadItemStreamAsync("cancelled", new PartitionKey("cancelled"));
            Assert.AreEqual(HttpStatusCode.NotFound, absent.StatusCode);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task PatchServiceErrorRollsBackOtherWrites(bool missingDocument)
        {
            Environment.SetEnvironmentVariable("AZURE_COSMOS_BINARY_ENCODING_ENABLED", "True");
            CaptureHandler capture = new CaptureHandler();
            using CosmosClient client = this.CreateClient(new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
                MaxRetryAttemptsOnAbortedTransactions = 0,
            }, capture);
            await this.SeedAsync("a", "patch");
            string target = missingDocument ? "absent" : "patch";
            using DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(client.GetContainer(this.databaseId, "b"), new PartitionKey("rollback"), "rollback", new Document("rollback"))
                .PatchItem(client.GetContainer(this.databaseId, "a"), new PartitionKey(target), target,
                    new[] { PatchOperation.Replace("/missingPath", 10) })
                .ExecuteTransactionAsync();
            Assert.IsTrue(response.IsTransactionAborted, $"Expected a durable abort, got {(int)response.StatusCode}.");
            Assert.AreEqual(2, response.Count);
            this.TestContext.WriteLine("Expected service rejection: " + (int)response.StatusCode);
            using ResponseMessage absent = await this.admin.GetContainer(this.databaseId, "b").ReadItemStreamAsync("rollback", new PartitionKey("rollback"));
            Assert.AreEqual(HttpStatusCode.NotFound, absent.StatusCode);
            Assert.AreEqual(1, (int)(await this.ReadAsync("a", "patch"))["count"]);
            Assert.AreEqual(1, capture.Requests);
        }

        private async Task RunMixedAsync(CosmosClient client, CaptureHandler capture, bool camelCase, bool custom)
        {
            Container a = client.GetContainer(this.databaseId, "a");
            Container b = client.GetContainer(this.databaseId, "b");
            foreach (string id in new[] { "replace", "patch", "delete", "existing" })
            {
                await this.SeedAsync(id == "existing" ? "b" : "a", id);
            }

            using DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(a, new PartitionKey("create"), "create", new Document("create"))
                .ReplaceItem(a, new PartitionKey("replace"), "replace", new Document("replace"))
                .UpsertItem(b, new PartitionKey("upsert"), "upsert", new Document("upsert"))
                .UpsertItem(b, new PartitionKey("existing"), "existing", new Document("existing"))
                .PatchItem(a, new PartitionKey("patch"), "patch", new List<PatchOperation>
                {
                    PatchOperation.Add("/details", new Details()),
                    PatchOperation.Set("/text", "quote\" slash\\ newline\n \u03a9 \u4e2d \ud83d\ude00"),
                    PatchOperation.Replace("/count", 42),
                    PatchOperation.Set("/enabled", true),
                    PatchOperation.Set<object>("/nullable", null),
                    PatchOperation.Increment("/increment", 1.5),
                    PatchOperation.Remove("/old"),
                    PatchOperation.Move("/source", "/destination"),
                    PatchOperation.Set("/array", new object[] { 1, false, null, "x" }),
                    PatchOperation.Set("/empty", ""),
                }, new DistributedTransactionPatchItemRequestOptions { FilterPredicate = "FROM c WHERE c.count = 1" })
                .DeleteItem(a, new PartitionKey("delete"), "delete")
                .ExecuteTransactionAsync();
            this.AssertSuccess(response, 6);
            foreach (string id in new[] { "create", "replace", "upsert", "existing" })
            {
                JObject stored = await this.ReadAsync(id == "upsert" || id == "existing" ? "b" : "a", id);
                Assert.AreEqual("name", (string)stored[camelCase ? "displayName" : "DisplayName"]);
                Assert.IsNull(stored[camelCase ? "DisplayName" : "displayName"]);
                JToken marker = stored[camelCase ? "marker" : "Marker"];
                Assert.AreEqual(custom ? "converted" : "original", custom ? (string)marker : (string)marker[camelCase ? "value" : "Value"]);
                Assert.AreEqual(7, (int)stored[camelCase ? "details" : "Details"][camelCase ? "innerValue" : "InnerValue"]);
                Assert.AreEqual(1234567890.125m, (decimal)stored[camelCase ? "amount" : "Amount"]);
                if (camelCase && !custom)
                {
                    Assert.IsNull(stored["optional"]);
                }
            }

            JObject patched = await this.ReadAsync("a", "patch");
            Assert.AreEqual(7, (int)patched["details"][camelCase ? "innerValue" : "InnerValue"]);
            Assert.AreEqual("quote\" slash\\ newline\n \u03a9 \u4e2d \ud83d\ude00", (string)patched["text"]);
            Assert.AreEqual(42, (int)patched["count"]);
            Assert.IsTrue((bool)patched["enabled"]);
            Assert.AreEqual(JTokenType.Null, patched["nullable"].Type);
            Assert.AreEqual(1.5, (double)patched["increment"]);
            Assert.IsNull(patched["old"]);
            Assert.IsNull(patched["source"]);
            Assert.AreEqual("moved", (string)patched["destination"]);
            Assert.IsTrue(JToken.DeepEquals(JArray.Parse("[1,false,null,\"x\"]"), patched["array"]));
            Assert.AreEqual("", (string)patched["empty"]);
            using ResponseMessage deleted = await this.admin.GetContainer(this.databaseId, "a").ReadItemStreamAsync("delete", new PartitionKey("delete"));
            Assert.AreEqual(HttpStatusCode.NotFound, deleted.StatusCode);
            Assert.AreEqual(1, capture.Requests, "All cases must traverse live DTx transport.");
        }

        private CosmosClient CreateClient(CosmosClientOptions options, CaptureHandler capture = null)
        {
            options.RequestTimeout = TimeSpan.FromSeconds(30);
            if (capture != null)
            {
                options.CustomHandlers.Add(capture);
            }

            return new CosmosClient(Environment.GetEnvironmentVariable("COSMOS_DTX_ENDPOINT"),
                Environment.GetEnvironmentVariable("COSMOS_DTX_KEY"), options);
        }

        private async Task SeedAsync(string container, string id)
        {
            await this.admin.GetContainer(this.databaseId, container).CreateItemAsync(
                new { id, pk = id, count = 1, old = "remove", source = "moved" }, new PartitionKey(id));
        }

        private async Task<JObject> ReadAsync(string container, string id)
        {
            return (await this.admin.GetContainer(this.databaseId, container).ReadItemAsync<JObject>(id, new PartitionKey(id))).Resource;
        }

        private void AssertSuccess(DistributedTransactionResponse response, int count)
        {
            this.TestContext.WriteLine($"DTx status={(int)response.StatusCode}, count={response.Count}, activity={response.ActivityId}");
            Assert.IsTrue(response.IsSuccessStatusCode, $"Envelope status={(int)response.StatusCode}, activity={response.ActivityId}");
            Assert.AreEqual(count, response.Count);
            for (int i = 0; i < count; i++)
            {
                Assert.IsTrue(response[i].IsSuccessStatusCode, $"Operation {i}: {(int)response[i].StatusCode}");
            }
        }

        private static Stream Payload(string json, bool binary, bool seekable, bool prefix = false)
        {
            using Stream encoded = CosmosSerializationUtil.ConvertToStreamUsingJsonSerializationFormat(
                Encoding.UTF8.GetBytes(json), binary ? JsonSerializationFormat.Binary : JsonSerializationFormat.Text);
            using MemoryStream buffer = new MemoryStream();
            if (prefix)
            {
                buffer.WriteByte(0);
            }

            encoded.CopyTo(buffer);
            if (!seekable)
            {
                return new TrackedStream(buffer.ToArray());
            }

            return new MemoryStream(buffer.ToArray()) { Position = prefix ? 1 : 0 };
        }

        private sealed class CaptureHandler : RequestHandler
        {
            public int Requests { get; private set; }
            public int BinaryPointRequests { get; private set; }

            public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
            {
                if (request.OperationType == Microsoft.Azure.Documents.OperationType.CommitDistributedTransaction)
                {
                    long position = request.Content.Position;
                    using (System.Text.Json.JsonDocument json = await System.Text.Json.JsonDocument.ParseAsync(request.Content, cancellationToken: cancellationToken))
                    {
                        Assert.IsTrue(json.RootElement.GetProperty("operations").GetArrayLength() > 0);
                    }

                    request.Content.Position = position;
                    this.Requests++;
                }
                else if (request.ResourceType == Microsoft.Azure.Documents.ResourceType.Document
                    && (request.OperationType == Microsoft.Azure.Documents.OperationType.Create
                        || request.OperationType == Microsoft.Azure.Documents.OperationType.Replace
                        || request.OperationType == Microsoft.Azure.Documents.OperationType.Upsert))
                {
                    long position = request.Content.Position;
                    if (request.Content.ReadByte() == (byte)JsonSerializationFormat.Binary)
                    {
                        this.BinaryPointRequests++;
                    }

                    request.Content.Position = position;
                }

                return await base.SendAsync(request, cancellationToken);
            }
        }

        private sealed class Document
        {
            public Document(string id)
            {
                this.Id = id;
                this.Pk = id;
            }

            [JsonProperty("id")]
            public string Id { get; set; }
            [JsonProperty("pk")]
            public string Pk { get; set; }
            public string DisplayName { get; set; } = "name";
            public Details Details { get; set; } = new Details();
            public decimal Amount { get; set; } = 1234567890.125m;
            public string Optional { get; set; }
            public Marker Marker { get; set; } = new Marker();
        }

        private sealed class Details
        {
            public int InnerValue { get; set; } = 7;
        }

        private sealed class Marker
        {
            public string Value { get; set; } = "original";
        }

        private sealed class MarkerConverter : JsonConverter<Marker>
        {
            private readonly string output;
            public MarkerConverter(string output) => this.output = output;
            public override void WriteJson(Newtonsoft.Json.JsonWriter writer, Marker value, Newtonsoft.Json.JsonSerializer serializer) => writer.WriteValue(this.output);
            public override Marker ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, Marker existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer) => throw new NotSupportedException();
        }

        private sealed class TrackingSerializer : CosmosSerializer
        {
            private readonly CosmosJsonDotNetSerializer inner;
            public TrackingSerializer(bool binary)
            {
                this.inner = new CosmosJsonDotNetSerializer(new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    Converters = { new MarkerConverter("converted") },
                }, binary);
            }

            public List<TrackedStream> Streams { get; } = new List<TrackedStream>();
            public override T FromStream<T>(Stream stream) => this.inner.FromStream<T>(stream);
            public override Stream ToStream<T>(T input)
            {
                using Stream encoded = this.inner.ToStream(input);
                using MemoryStream buffer = new MemoryStream();
                encoded.CopyTo(buffer);
                TrackedStream stream = new TrackedStream(buffer.ToArray());
                this.Streams.Add(stream);
                return stream;
            }
        }

        private sealed class TrackedStream : Stream
        {
            private readonly MemoryStream inner;
            public TrackedStream(byte[] content) => this.inner = new MemoryStream(content);
            public bool Disposed { get; private set; }
            public override bool CanRead => !this.Disposed;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override int Read(byte[] buffer, int offset, int count) => this.inner.Read(buffer, offset, count);
            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.Disposed = true;
                    this.inner.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
