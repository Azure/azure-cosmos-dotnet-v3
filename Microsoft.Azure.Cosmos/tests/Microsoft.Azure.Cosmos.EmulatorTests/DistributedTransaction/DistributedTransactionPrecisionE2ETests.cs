// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using PartitionKey = Cosmos.PartitionKey;

    /// <summary>
    /// End-to-end tests that pin <b>type fidelity and precision retention</b> for documents driven
    /// through a Read distributed transaction followed by a Write distributed transaction against a
    /// live DTX-enabled account.
    ///
    /// These tests deliberately use "wonky" document shapes — boundary <see cref="long"/> values
    /// beyond 2^53, representative <see cref="double"/> extremes, high-precision numerics carried as
    /// strings, deep nesting, heterogeneous (mixed-type) arrays, empty objects/arrays, nulls, unicode
    /// / emoji text, and unusual property names — and assert every value survives the round trip
    /// (seed -&gt; Read DTx -&gt; Write DTx -&gt; Read DTx) byte-for-byte via <see cref="JToken.DeepEquals"/>.
    ///
    /// They also cover containers whose partition key <b>value type is not a string</b>: one container
    /// is keyed on a numeric partition key and another on a boolean partition key, exercising
    /// <see cref="PartitionKey(double)"/> and <see cref="PartitionKey(bool)"/> across the full
    /// read-then-write DTx flow.
    ///
    /// To run locally:
    ///     set COSMOS_DTX_ENDPOINT=https://your-account.documents.azure.com:443/
    ///     set COSMOS_DTX_KEY=your-master-key
    ///     dotnet test --filter "FullyQualifiedName~DistributedTransactionPrecisionE2ETests"
    ///
    /// This class runs in the "DistributedTransaction" test category and is NOT gated with
    /// [Ignore]. It requires the COSMOS_DTX_ENDPOINT / COSMOS_DTX_KEY environment variables
    /// pointing at a live DTX-enabled account (the public emulator does not implement
    /// /operations/dtc); without them the tests fail fast in TestInitialize.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    [TestCategory("DistributedTransaction")]
    public class DistributedTransactionPrecisionE2ETests
    {
        private const string DatabaseId = "DtxPrecisionE2ETestDb";
        private const string StringPkContainerId = "DtxPrecisionStringPk";
        private const string NumericPkContainerId = "DtxPrecisionNumericPk";
        private const string BooleanPkContainerId = "DtxPrecisionBooleanPk";
        private const string PartitionKeyPath = "/pk";

        private CosmosClient client;
        private Database database;
        private Container stringPkContainer;
        private Container numericPkContainer;
        private Container booleanPkContainer;

        [TestInitialize]
        public async Task TestInitialize()
        {
            string endpoint = Environment.GetEnvironmentVariable("COSMOS_DTX_ENDPOINT");
            string key = Environment.GetEnvironmentVariable("COSMOS_DTX_KEY");

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
            {
                Assert.Fail("COSMOS_DTX_ENDPOINT and COSMOS_DTX_KEY environment variables must be set.");
            }

            // Standard DTX auth model: account endpoint + primary master key.
            this.client = new CosmosClient(
                endpoint,
                key,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ConsistencyLevel = ConsistencyLevel.Session
                });

            this.database = (await this.client.CreateDatabaseIfNotExistsAsync(DatabaseId)).Database;

            // The partition key path is the same "/pk" in every container; what differs is the JSON
            // value type stored at that path (string, number, boolean), which is what these tests pin.
            this.stringPkContainer = (await this.database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(StringPkContainerId, PartitionKeyPath))).Container;
            this.numericPkContainer = (await this.database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(NumericPkContainerId, PartitionKeyPath))).Container;
            this.booleanPkContainer = (await this.database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(BooleanPkContainerId, PartitionKeyPath))).Container;
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.client != null)
            {
                try
                {
                    await this.client.GetDatabase(DatabaseId).DeleteAsync();
                }
                catch { /* ignore */ }

                this.client.Dispose();
            }
        }

        // ─── String partition key: heterogeneous wonky documents ─────────────────

        /// <summary>
        /// Seeds several structurally different "wonky" documents (boundary longs, double extremes,
        /// high-precision numerics as strings, deep nesting, mixed-type arrays, unicode, unusual keys),
        /// reads them all atomically in a single Read DTx, then writes those same documents back in a
        /// single Write DTx, and finally re-reads them in a Read DTx — asserting at every hop that the
        /// full document (type + precision) is retained exactly.
        /// </summary>
        [TestMethod]
        public async Task ReadThenWrite_WonkyDocuments_StringPartitionKey_PrecisionRetained()
        {
            // Three deliberately different shapes, all keyed on a string partition key.
            string pkA = $"wonky-num-{Guid.NewGuid():N}";
            string pkB = $"wonky-str-{Guid.NewGuid():N}";
            string pkC = $"wonky-nested-{Guid.NewGuid():N}";

            JObject docA = BuildNumericHeavyDocument(Guid.NewGuid().ToString(), new JValue(pkA));
            JObject docB = BuildTextHeavyDocument(Guid.NewGuid().ToString(), new JValue(pkB));
            JObject docC = BuildNestedDocument(Guid.NewGuid().ToString(), new JValue(pkC));

            await this.SeedAsync(this.stringPkContainer, docA, new PartitionKey(pkA));
            await this.SeedAsync(this.stringPkContainer, docB, new PartitionKey(pkB));
            await this.SeedAsync(this.stringPkContainer, docC, new PartitionKey(pkC));

            // ── Read DTx: fetch all three atomically under snapshot isolation. ──
            DistributedTransactionResponse readResponse = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.stringPkContainer, new PartitionKey(pkA), (string)docA["id"])
                .ReadItem(this.stringPkContainer, new PartitionKey(pkB), (string)docB["id"])
                .ReadItem(this.stringPkContainer, new PartitionKey(pkC), (string)docC["id"])
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(readResponse.IsSuccessStatusCode,
                $"Read DTx over the wonky documents should succeed. Got: {readResponse.StatusCode}");
            Assert.AreEqual(3, readResponse.Count);

            JObject readA = readResponse.GetOperationResultAtIndex<JObject>(0).Resource;
            JObject readB = readResponse.GetOperationResultAtIndex<JObject>(1).Resource;
            JObject readC = readResponse.GetOperationResultAtIndex<JObject>(2).Resource;
            readResponse.Dispose();

            AssertUserPropertiesRetained(docA, readA, "docA (numeric-heavy) after Read DTx");
            AssertUserPropertiesRetained(docB, readB, "docB (text-heavy) after Read DTx");
            AssertUserPropertiesRetained(docC, readC, "docC (nested) after Read DTx");

            // ── Write DTx: persist the exact documents that were read back. ──
            DistributedTransactionResponse writeResponse = await this.client
                .CreateDistributedWriteTransaction()
                .UpsertItem(this.stringPkContainer, new PartitionKey(pkA), (string)docA["id"], StripSystemProperties(readA))
                .UpsertItem(this.stringPkContainer, new PartitionKey(pkB), (string)docB["id"], StripSystemProperties(readB))
                .UpsertItem(this.stringPkContainer, new PartitionKey(pkC), (string)docC["id"], StripSystemProperties(readC))
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(writeResponse.IsSuccessStatusCode,
                $"Write DTx round-tripping the wonky documents should commit. Got: {writeResponse.StatusCode}");
            Assert.AreEqual(3, writeResponse.Count);
            writeResponse.Dispose();

            // ── Read DTx: verify the persisted state still matches the originals exactly. ──
            await ConfirmVisibleAsync(this.stringPkContainer, new PartitionKey(pkA), (string)docA["id"]);
            await ConfirmVisibleAsync(this.stringPkContainer, new PartitionKey(pkB), (string)docB["id"]);
            await ConfirmVisibleAsync(this.stringPkContainer, new PartitionKey(pkC), (string)docC["id"]);

            DistributedTransactionResponse verifyResponse = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.stringPkContainer, new PartitionKey(pkA), (string)docA["id"])
                .ReadItem(this.stringPkContainer, new PartitionKey(pkB), (string)docB["id"])
                .ReadItem(this.stringPkContainer, new PartitionKey(pkC), (string)docC["id"])
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(verifyResponse.IsSuccessStatusCode,
                $"Post-write Read DTx should succeed. Got: {verifyResponse.StatusCode}");

            AssertUserPropertiesRetained(docA, verifyResponse.GetOperationResultAtIndex<JObject>(0).Resource,
                "docA (numeric-heavy) after Write DTx round-trip");
            AssertUserPropertiesRetained(docB, verifyResponse.GetOperationResultAtIndex<JObject>(1).Resource,
                "docB (text-heavy) after Write DTx round-trip");
            AssertUserPropertiesRetained(docC, verifyResponse.GetOperationResultAtIndex<JObject>(2).Resource,
                "docC (nested) after Write DTx round-trip");

            verifyResponse.Dispose();
        }

        // ─── Numeric partition key ───────────────────────────────────────────────

        /// <summary>
        /// Exercises a container whose partition key value is a <b>number</b>. Two wonky documents keyed
        /// on distinct numeric partition keys are read atomically (Read DTx), written back (Write DTx),
        /// and re-read (Read DTx), asserting both the numeric-partition-key routing and full precision
        /// retention of the document bodies.
        /// </summary>
        [TestMethod]
        public async Task ReadThenWrite_WonkyDocuments_NumericPartitionKey_PrecisionRetained()
        {
            // Whole-number partition-key values keep number-PK routing unambiguous; the precision-
            // sensitive boundary values live in the document body, not in the routing key.
            const double pk1 = 42;
            const double pk2 = 1000000;

            JObject doc1 = BuildNumericHeavyDocument(Guid.NewGuid().ToString(), new JValue(pk1));
            JObject doc2 = BuildNestedDocument(Guid.NewGuid().ToString(), new JValue(pk2));

            await this.SeedAsync(this.numericPkContainer, doc1, new PartitionKey(pk1));
            await this.SeedAsync(this.numericPkContainer, doc2, new PartitionKey(pk2));

            // ── Read DTx across two numeric partition keys. ──
            DistributedTransactionResponse readResponse = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.numericPkContainer, new PartitionKey(pk1), (string)doc1["id"])
                .ReadItem(this.numericPkContainer, new PartitionKey(pk2), (string)doc2["id"])
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(readResponse.IsSuccessStatusCode,
                $"Read DTx over the numeric-PK documents should succeed. Got: {readResponse.StatusCode}");
            Assert.AreEqual(2, readResponse.Count);

            JObject read1 = readResponse.GetOperationResultAtIndex<JObject>(0).Resource;
            JObject read2 = readResponse.GetOperationResultAtIndex<JObject>(1).Resource;
            readResponse.Dispose();

            AssertUserPropertiesRetained(doc1, read1, "numeric-PK doc1 after Read DTx");
            AssertUserPropertiesRetained(doc2, read2, "numeric-PK doc2 after Read DTx");

            // ── Write DTx round-trip across the numeric partition keys. ──
            DistributedTransactionResponse writeResponse = await this.client
                .CreateDistributedWriteTransaction()
                .UpsertItem(this.numericPkContainer, new PartitionKey(pk1), (string)doc1["id"], StripSystemProperties(read1))
                .UpsertItem(this.numericPkContainer, new PartitionKey(pk2), (string)doc2["id"], StripSystemProperties(read2))
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(writeResponse.IsSuccessStatusCode,
                $"Write DTx round-tripping the numeric-PK documents should commit. Got: {writeResponse.StatusCode}");
            Assert.AreEqual(2, writeResponse.Count);
            writeResponse.Dispose();

            // ── Read DTx verification. ──
            await ConfirmVisibleAsync(this.numericPkContainer, new PartitionKey(pk1), (string)doc1["id"]);
            await ConfirmVisibleAsync(this.numericPkContainer, new PartitionKey(pk2), (string)doc2["id"]);

            DistributedTransactionResponse verifyResponse = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.numericPkContainer, new PartitionKey(pk1), (string)doc1["id"])
                .ReadItem(this.numericPkContainer, new PartitionKey(pk2), (string)doc2["id"])
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(verifyResponse.IsSuccessStatusCode,
                $"Post-write Read DTx over numeric PKs should succeed. Got: {verifyResponse.StatusCode}");

            AssertUserPropertiesRetained(doc1, verifyResponse.GetOperationResultAtIndex<JObject>(0).Resource,
                "numeric-PK doc1 after Write DTx round-trip");
            AssertUserPropertiesRetained(doc2, verifyResponse.GetOperationResultAtIndex<JObject>(1).Resource,
                "numeric-PK doc2 after Write DTx round-trip");

            verifyResponse.Dispose();
        }

        // ─── Boolean partition key ───────────────────────────────────────────────

        /// <summary>
        /// Exercises a container whose partition key value is a <b>boolean</b>. One document is keyed on
        /// <c>true</c> and another on <c>false</c>; both are read atomically (Read DTx), written back
        /// (Write DTx), and re-read (Read DTx), asserting boolean-partition-key routing and full
        /// precision retention across the round trip.
        /// </summary>
        [TestMethod]
        public async Task ReadThenWrite_WonkyDocuments_BooleanPartitionKey_PrecisionRetained()
        {
            JObject docTrue = BuildNumericHeavyDocument(Guid.NewGuid().ToString(), new JValue(true));
            JObject docFalse = BuildTextHeavyDocument(Guid.NewGuid().ToString(), new JValue(false));

            await this.SeedAsync(this.booleanPkContainer, docTrue, new PartitionKey(true));
            await this.SeedAsync(this.booleanPkContainer, docFalse, new PartitionKey(false));

            // ── Read DTx across the two boolean partition keys. ──
            DistributedTransactionResponse readResponse = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.booleanPkContainer, new PartitionKey(true), (string)docTrue["id"])
                .ReadItem(this.booleanPkContainer, new PartitionKey(false), (string)docFalse["id"])
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(readResponse.IsSuccessStatusCode,
                $"Read DTx over the boolean-PK documents should succeed. Got: {readResponse.StatusCode}");
            Assert.AreEqual(2, readResponse.Count);

            JObject readTrue = readResponse.GetOperationResultAtIndex<JObject>(0).Resource;
            JObject readFalse = readResponse.GetOperationResultAtIndex<JObject>(1).Resource;
            readResponse.Dispose();

            AssertUserPropertiesRetained(docTrue, readTrue, "boolean-PK doc[true] after Read DTx");
            AssertUserPropertiesRetained(docFalse, readFalse, "boolean-PK doc[false] after Read DTx");

            // ── Write DTx round-trip across the boolean partition keys. ──
            DistributedTransactionResponse writeResponse = await this.client
                .CreateDistributedWriteTransaction()
                .UpsertItem(this.booleanPkContainer, new PartitionKey(true), (string)docTrue["id"], StripSystemProperties(readTrue))
                .UpsertItem(this.booleanPkContainer, new PartitionKey(false), (string)docFalse["id"], StripSystemProperties(readFalse))
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(writeResponse.IsSuccessStatusCode,
                $"Write DTx round-tripping the boolean-PK documents should commit. Got: {writeResponse.StatusCode}");
            Assert.AreEqual(2, writeResponse.Count);
            writeResponse.Dispose();

            // ── Read DTx verification. ──
            await ConfirmVisibleAsync(this.booleanPkContainer, new PartitionKey(true), (string)docTrue["id"]);
            await ConfirmVisibleAsync(this.booleanPkContainer, new PartitionKey(false), (string)docFalse["id"]);

            DistributedTransactionResponse verifyResponse = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.booleanPkContainer, new PartitionKey(true), (string)docTrue["id"])
                .ReadItem(this.booleanPkContainer, new PartitionKey(false), (string)docFalse["id"])
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(verifyResponse.IsSuccessStatusCode,
                $"Post-write Read DTx over boolean PKs should succeed. Got: {verifyResponse.StatusCode}");

            AssertUserPropertiesRetained(docTrue, verifyResponse.GetOperationResultAtIndex<JObject>(0).Resource,
                "boolean-PK doc[true] after Write DTx round-trip");
            AssertUserPropertiesRetained(docFalse, verifyResponse.GetOperationResultAtIndex<JObject>(1).Resource,
                "boolean-PK doc[false] after Write DTx round-trip");

            verifyResponse.Dispose();
        }

        // ─── Wonky document builders ─────────────────────────────────────────────

        /// <summary>
        /// Numeric-heavy shape: boundary <see cref="long"/> values (incl. beyond 2^53), representative
        /// <see cref="double"/> extremes, and high-precision numerics carried as strings (a common
        /// real-world pattern that guarantees exact retention regardless of numeric storage).
        /// </summary>
        private static JObject BuildNumericHeavyDocument(string id, JValue pk)
        {
            return new JObject
            {
                ["id"] = id,
                ["pk"] = pk,
                ["int64Max"] = long.MaxValue,
                ["int64Min"] = long.MinValue,
                ["beyond2Pow53"] = 9007199254740993L,        // 2^53 + 1: not representable as a double
                ["negBeyond2Pow53"] = -9007199254740993L,
                ["zeroLong"] = 0L,
                ["pi"] = Math.PI,
                ["maxDouble"] = double.MaxValue,
                ["tinyDouble"] = 1e-300,
                ["negativeDouble"] = -2.718281828459045,
                ["highPrecisionMoney"] = "12345678901234567890.1234567890",
                ["scientificAsString"] = "6.02214076e23",
                ["longArray"] = new JArray { long.MaxValue, long.MinValue, 9007199254740993L, 0L },
                ["doubleArray"] = new JArray { Math.PI, 1e-300, double.MaxValue }
            };
        }

        /// <summary>
        /// Text/typed shape: unicode + emoji, escaped characters, empty string, explicit null, both
        /// boolean values, and a small nested object — the "grab bag" of scalar JSON types.
        /// </summary>
        private static JObject BuildTextHeavyDocument(string id, JValue pk)
        {
            return new JObject
            {
                ["id"] = id,
                ["pk"] = pk,
                ["unicode"] = "🚀🌌 café Ω ✓ — 日本語 \"quoted\" \t tab \\ backslash",
                ["emptyString"] = string.Empty,
                ["nullValue"] = JValue.CreateNull(),
                ["boolTrue"] = true,
                ["boolFalse"] = false,
                ["whitespaceOnly"] = "   ",
                ["numberLikeString"] = "007",
                ["weird key with spaces & symbols! 名前"] = "wonky-key-value",
                ["stringArray"] = new JArray { "", "a", "🌟", "  ", "null" }
            };
        }

        /// <summary>
        /// Deeply-nested shape with heterogeneous (mixed-type) arrays, empty containers, and boundary
        /// values buried several levels down — stresses structural fidelity, not just top-level scalars.
        /// </summary>
        private static JObject BuildNestedDocument(string id, JValue pk)
        {
            return new JObject
            {
                ["id"] = id,
                ["pk"] = pk,
                ["emptyObject"] = new JObject(),
                ["emptyArray"] = new JArray(),
                ["mixedArray"] = new JArray
                {
                    0L, -1L, "s", 2.71828, true, false, JValue.CreateNull(),
                    new JObject { ["k"] = "v", ["n"] = 9007199254740993L }
                },
                ["nested"] = new JObject
                {
                    ["level1"] = new JObject
                    {
                        ["level2"] = new JObject
                        {
                            ["level3"] = new JObject
                            {
                                ["deepLong"] = 9007199254740993L,
                                ["deepDouble"] = Math.PI,
                                ["deepUnicode"] = "🧬 deep 値",
                                ["deepArray"] = new JArray { 1L, "two", 3.5, true, JValue.CreateNull() }
                            }
                        }
                    }
                }
            };
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private async Task SeedAsync(Container targetContainer, JObject document, PartitionKey partitionKey)
        {
            await targetContainer.CreateItemAsync(document, partitionKey);
        }

        /// <summary>
        /// Asserts that every user-authored property of <paramref name="expected"/> is present in
        /// <paramref name="actual"/> with an exactly-equal value (type + precision) via
        /// <see cref="JToken.DeepEquals"/>. Cosmos system properties (id, pk, and underscore-prefixed
        /// metadata such as _etag/_ts/_rid) are intentionally skipped: id/pk routing is validated by the
        /// read landing on the correct document, and system metadata is server-owned.
        /// </summary>
        private static void AssertUserPropertiesRetained(JObject expected, JObject actual, string context)
        {
            Assert.IsNotNull(actual, $"[{context}] read-back document should not be null.");
            Assert.AreEqual((string)expected["id"], (string)actual["id"], $"[{context}] id should match.");

            foreach (JProperty property in expected.Properties())
            {
                if (property.Name == "id" || property.Name == "pk" || property.Name.StartsWith("_", StringComparison.Ordinal))
                {
                    continue;
                }

                JToken actualValue = actual[property.Name];
                Assert.IsNotNull(actualValue, $"[{context}] property '{property.Name}' should be present after the round trip.");
                Assert.IsTrue(
                    JToken.DeepEquals(property.Value, actualValue),
                    $"[{context}] property '{property.Name}' lost fidelity. Expected: {property.Value.ToString(Newtonsoft.Json.Formatting.None)} Actual: {actualValue.ToString(Newtonsoft.Json.Formatting.None)}");
            }
        }

        /// <summary>
        /// Returns a copy of <paramref name="source"/> with server-owned system properties (those with
        /// an underscore prefix) removed, so the document written back on the Write DTx carries only the
        /// user-authored payload just as the caller originally supplied it.
        /// </summary>
        private static JObject StripSystemProperties(JObject source)
        {
            JObject clone = (JObject)source.DeepClone();
            foreach (JProperty property in clone.Properties().ToList())
            {
                if (property.Name.StartsWith("_", StringComparison.Ordinal))
                {
                    property.Remove();
                }
            }

            return clone;
        }

        /// <summary>
        /// Confirms a just-written item is durably point-readable before it is read through a distributed
        /// transaction. On a slow single-region DTX account a freshly-committed document can briefly be
        /// invisible to the coordinator's Phase-1 snapshot; polling a point read to 200 first removes that
        /// read-your-write flake without weakening the round-trip precision assertions.
        /// </summary>
        private static async Task ConfirmVisibleAsync(Container container, PartitionKey partitionKey, string id)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                using ResponseMessage rm = await container.ReadItemStreamAsync(id, partitionKey);
                if (rm.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }

            Assert.Fail($"Seeded/written item id='{id}' did not become point-readable within the settle window.");
        }
    }
}
