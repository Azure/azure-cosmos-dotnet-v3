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

            // Warm-up: run a throwaway Write DTx against each freshly-created container before any
            // Read DTx. A brand-new DTX collection can return 408 (RequestTimeout) on its very first
            // distributed transaction while the coordinator/DTC Prepare path bootstraps; a prior Write
            // DTx on the same container warms that path so the read-oriented tests below don't pay the
            // cold-start timeout. Best-effort — failures here do not fail the run.
            await this.WarmUpContainerAsync(this.stringPkContainer, new PartitionKey("warmup"), new JValue("warmup"));
            await this.WarmUpContainerAsync(this.numericPkContainer, new PartitionKey(0d), new JValue(0d));
            await this.WarmUpContainerAsync(this.booleanPkContainer, new PartitionKey(true), new JValue(true));
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

        // ─── Extra shapes: deep nesting, arrays-of-objects, wide/flat, jagged, extreme scalars ─────

        /// <summary>
        /// A single document nested 32 objects deep, carrying boundary longs, high-precision
        /// decimals-as-strings, and unicode at the leaf. Read DTx -&gt; Write DTx -&gt; Read DTx must
        /// retain the entire tree structurally, byte-for-byte.
        /// </summary>
        [TestMethod]
        public async Task ReadThenWrite_VeryDeeplyNested_StringPk_PrecisionRetained()
        {
            string pk = $"deep-{Guid.NewGuid():N}";
            JObject doc = BuildVeryDeeplyNestedDocument(Guid.NewGuid().ToString(), new JValue(pk), depth: 32);

            await this.RunReadThenWriteRoundTripAsync(
                this.stringPkContainer,
                new[] { ((string)doc["id"], new PartitionKey(pk), doc) },
                "very-deeply-nested (32 levels)");
        }

        /// <summary>
        /// An "order"-shaped document whose payload is an array of heterogeneous nested objects
        /// (line items with mixed-type tag arrays, nested meta objects, empty containers, nulls, and
        /// high-precision money-as-strings). Stresses fidelity of arrays-of-objects through the DTx
        /// read-then-write round trip.
        /// </summary>
        [TestMethod]
        public async Task ReadThenWrite_ArrayOfObjects_StringPk_PrecisionRetained()
        {
            string pk = $"order-{Guid.NewGuid():N}";
            JObject doc = BuildArrayOfObjectsDocument(Guid.NewGuid().ToString(), new JValue(pk));

            await this.RunReadThenWriteRoundTripAsync(
                this.stringPkContainer,
                new[] { ((string)doc["id"], new PartitionKey(pk), doc) },
                "array-of-objects (order)");
        }

        /// <summary>
        /// A very "wide" flat document with 250 top-level properties whose value types alternate across
        /// long / double / unicode-string / bool / null. Stresses breadth (large property surface) rather
        /// than depth through the DTx round trip.
        /// </summary>
        [TestMethod]
        public async Task ReadThenWrite_WideFlatDocument_StringPk_PrecisionRetained()
        {
            string pk = $"wide-{Guid.NewGuid():N}";
            JObject doc = BuildWideFlatDocument(Guid.NewGuid().ToString(), new JValue(pk), propertyCount: 250);

            await this.RunReadThenWriteRoundTripAsync(
                this.stringPkContainer,
                new[] { ((string)doc["id"], new PartitionKey(pk), doc) },
                "wide-flat (250 properties)");
        }

        /// <summary>
        /// Ragged / jagged arrays: rows of differing length and element type, arrays whose elements are
        /// themselves empty or nested containers, and triple-nested arrays. Stresses structural fidelity
        /// of irregular array shapes through the DTx round trip.
        /// </summary>
        [TestMethod]
        public async Task ReadThenWrite_JaggedArrays_StringPk_PrecisionRetained()
        {
            string pk = $"jagged-{Guid.NewGuid():N}";
            JObject doc = BuildJaggedArraysDocument(Guid.NewGuid().ToString(), new JValue(pk));

            await this.RunReadThenWriteRoundTripAsync(
                this.stringPkContainer,
                new[] { ((string)doc["id"], new PartitionKey(pk), doc) },
                "jagged-arrays");
        }

        /// <summary>
        /// Extreme scalar values on a <b>numeric</b> partition key: proven-safe live double/long extremes
        /// plus the riskier values (subnormals, negative zero, arbitrary-precision decimals) carried as
        /// strings, alongside ISO-8601 / GUID / base64 / long-repeated-string encoded scalars. Verifies
        /// numeric-PK routing and exact scalar retention through the DTx round trip.
        /// </summary>
        [TestMethod]
        public async Task ReadThenWrite_ExtremeScalars_NumericPk_PrecisionRetained()
        {
            const double pk = 7;
            JObject doc = BuildExtremeScalarsDocument(Guid.NewGuid().ToString(), new JValue(pk));

            await this.RunReadThenWriteRoundTripAsync(
                this.numericPkContainer,
                new[] { ((string)doc["id"], new PartitionKey(pk), doc) },
                "extreme-scalars (numeric PK)");
        }

        /// <summary>
        /// A single DTx spanning several structurally-different shapes (deep tree, array-of-objects, jagged
        /// arrays) across both <c>true</c> and <c>false</c> <b>boolean</b> partition keys — combining
        /// fan-out breadth, mixed document shapes, and boolean-PK routing in one read-then-write round trip.
        /// </summary>
        [TestMethod]
        public async Task ReadThenWrite_HeterogeneousShapes_BooleanPk_PrecisionRetained()
        {
            JObject deep = BuildVeryDeeplyNestedDocument(Guid.NewGuid().ToString(), new JValue(true), depth: 16);
            JObject order = BuildArrayOfObjectsDocument(Guid.NewGuid().ToString(), new JValue(false));
            JObject jagged = BuildJaggedArraysDocument(Guid.NewGuid().ToString(), new JValue(true));

            await this.RunReadThenWriteRoundTripAsync(
                this.booleanPkContainer,
                new[]
                {
                    ((string)deep["id"], new PartitionKey(true), deep),
                    ((string)order["id"], new PartitionKey(false), order),
                    ((string)jagged["id"], new PartitionKey(true), jagged),
                },
                "heterogeneous shapes across boolean PK");
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

        /// <summary>
        /// A single document nested <paramref name="depth"/> objects deep. Every level carries a
        /// <c>marker</c> ordinal so structural drift at any level is detectable, and the leaf carries
        /// boundary longs, a double, unicode, a high-precision decimal-as-string, and a mixed-type array.
        /// </summary>
        private static JObject BuildVeryDeeplyNestedDocument(string id, JValue pk, int depth)
        {
            JObject cursor = new JObject
            {
                ["deepLong"] = 9007199254740993L,           // 2^53 + 1
                ["deepDouble"] = Math.PI,
                ["deepUnicode"] = "🧬 bottom 値",
                ["deepMoney"] = "99999999999999999999.99999999999999999999",
                ["deepArray"] = new JArray { 1L, "two", 3.5, true, JValue.CreateNull() }
            };

            for (int level = depth; level > 0; level--)
            {
                cursor = new JObject
                {
                    ["marker"] = (long)level,
                    [$"level{level}"] = cursor
                };
            }

            return new JObject
            {
                ["id"] = id,
                ["pk"] = pk,
                ["depth"] = (long)depth,
                ["tree"] = cursor
            };
        }

        /// <summary>
        /// An "order"-shaped document whose payload is an array of heterogeneous nested objects: line items
        /// with mixed-type tag arrays, nested <c>meta</c> objects (incl. empty objects and nulls), boundary
        /// quantities, and money carried as high-precision strings. Stresses arrays-of-objects fidelity.
        /// </summary>
        private static JObject BuildArrayOfObjectsDocument(string id, JValue pk)
        {
            return new JObject
            {
                ["id"] = id,
                ["pk"] = pk,
                ["orderNumber"] = "ORD-🚀-" + id,
                ["lineItems"] = new JArray
                {
                    new JObject
                    {
                        ["sku"] = "SKU-🚀-001",
                        ["qty"] = 9007199254740993L,
                        ["unitPrice"] = "19.990000000000000001",
                        ["tags"] = new JArray { "sale", string.Empty, "日本語", JValue.CreateNull() },
                        ["meta"] = new JObject { ["gift"] = true, ["note"] = JValue.CreateNull() }
                    },
                    new JObject
                    {
                        ["sku"] = "SKU-002",
                        ["qty"] = 0L,
                        ["unitPrice"] = "0.00",
                        ["tags"] = new JArray(),
                        ["meta"] = new JObject()
                    },
                    new JObject
                    {
                        ["sku"] = "SKU-003",
                        ["qty"] = -1L,
                        ["unitPrice"] = "-12345678901234567890.0987654321",
                        ["tags"] = new JArray { "backorder" },
                        ["meta"] = new JObject
                        {
                            ["nested"] = new JObject { ["a"] = new JArray { 1L, 2L, 3L } }
                        }
                    }
                },
                ["totals"] = new JObject
                {
                    ["count"] = 3L,
                    ["grandTotal"] = "20.000000000000000001",
                    ["currency"] = "USD"
                }
            };
        }

        /// <summary>
        /// A very "wide" flat document: <paramref name="propertyCount"/> top-level properties whose value
        /// types cycle across long / double / unicode-string / bool / null. Stresses breadth (a large
        /// property surface) rather than nesting depth.
        /// </summary>
        private static JObject BuildWideFlatDocument(string id, JValue pk, int propertyCount)
        {
            JObject doc = new JObject
            {
                ["id"] = id,
                ["pk"] = pk,
                ["propertyCount"] = (long)propertyCount
            };

            for (int i = 0; i < propertyCount; i++)
            {
                switch (i % 5)
                {
                    case 0: doc[$"p_{i:D4}_long"] = i + 9007199254740993L; break;
                    case 1: doc[$"p_{i:D4}_double"] = i + 0.5; break;
                    case 2: doc[$"p_{i:D4}_str"] = $"value-{i}-日本語-🌟"; break;
                    case 3: doc[$"p_{i:D4}_bool"] = i % 2 == 0; break;
                    default: doc[$"p_{i:D4}_null"] = JValue.CreateNull(); break;
                }
            }

            return doc;
        }

        /// <summary>
        /// Ragged / jagged arrays: rows of differing length and element type, arrays whose elements are
        /// themselves empty or nested containers, and triple-nested arrays. Stresses structural fidelity of
        /// irregular array shapes.
        /// </summary>
        private static JObject BuildJaggedArraysDocument(string id, JValue pk)
        {
            return new JObject
            {
                ["id"] = id,
                ["pk"] = pk,
                ["matrix"] = new JArray
                {
                    new JArray { 1L, 2L, 3L },
                    new JArray { "a", "b" },
                    new JArray(),
                    new JArray { new JArray { 9007199254740993L, "deep" }, new JObject { ["k"] = "v" } }
                },
                ["arrayOfContainers"] = new JArray
                {
                    new JObject(),
                    new JArray(),
                    new JObject { ["empty"] = new JArray() },
                    new JArray { new JObject { ["x"] = JValue.CreateNull() } }
                },
                ["scalarsThenArray"] = new JArray
                {
                    true, false, JValue.CreateNull(), 0L, string.Empty,
                    new JArray { new JArray { new JArray { "triple-nested" } } }
                }
            };
        }

        /// <summary>
        /// Extreme scalar values. Live numeric values are restricted to the envelope already proven to
        /// round-trip exactly for this account (boundary longs, double extremes, tiny/normal doubles);
        /// the riskier values (subnormals, negative zero, arbitrary-precision decimals) are carried as
        /// strings so exactness is guaranteed regardless of the server's numeric storage. Also includes
        /// common encoded-scalar payloads (ISO-8601, GUID, base64, a multi-KB repeated string).
        /// </summary>
        private static JObject BuildExtremeScalarsDocument(string id, JValue pk)
        {
            return new JObject
            {
                ["id"] = id,
                ["pk"] = pk,
                ["int64Max"] = long.MaxValue,
                ["int64Min"] = long.MinValue,
                ["beyond2Pow53"] = 9007199254740993L,
                ["maxDouble"] = double.MaxValue,
                ["minNormalDouble"] = 2.2250738585072014e-308,
                ["tinyDouble"] = 1e-300,
                ["epsilonAsString"] = "4.9406564584124654e-324",
                ["negativeZeroAsString"] = "-0",
                ["bigDecimalAsString"] = "123456789012345678901234567890.123456789012345678901234567890",
                // Carried with a guard prefix on purpose: a raw ISO-8601 string is auto-parsed by
                // Newtonsoft (DateParseHandling) when the read-back is deserialized into a JObject, which
                // normalizes the timezone offset to local time (+00:00 -> -04:00) on the client side. That
                // normalization is orthogonal to DTx precision (the stored value is unchanged), so the
                // prefix keeps this as an opaque, precise string that still exercises the fractional-second
                // and offset characters without tripping the client-side date-parse footgun.
                ["timestampText"] = "ts:2026-07-11T23:59:59.9999999+00:00",
                ["guid"] = "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
                ["base64Blob"] = "SGVsbG8sIPCfmoAg5pel5pys6Kqe",
                ["longRepeatedString"] = new string('x', 4096)
            };
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private async Task SeedAsync(Container targetContainer, JObject document, PartitionKey partitionKey)
        {
            await targetContainer.CreateItemAsync(document, partitionKey);
        }

        /// <summary>
        /// Runs the full precision round trip for a set of documents against a single container:
        /// point-seed each document, Read DTx them all (asserting fidelity), Write DTx (upsert) the exact
        /// read-back documents, confirm visibility, then Read DTx once more and re-assert fidelity against
        /// the originals. Supports a variable number of items so tests can exercise single-doc and
        /// multi-doc (fan-out) transactions with the same logic.
        /// </summary>
        private async Task RunReadThenWriteRoundTripAsync(
            Container targetContainer,
            (string Id, PartitionKey Pk, JObject Doc)[] items,
            string context)
        {
            foreach ((string Id, PartitionKey Pk, JObject Doc) item in items)
            {
                await this.SeedAsync(targetContainer, item.Doc, item.Pk);
            }

            // Read DTx: fetch every document atomically and assert fidelity.
            DistributedReadTransaction readTransaction = this.client.CreateDistributedReadTransaction();
            foreach ((string Id, PartitionKey Pk, JObject Doc) item in items)
            {
                readTransaction = readTransaction.ReadItem(targetContainer, item.Pk, item.Id);
            }

            JObject[] readBacks = new JObject[items.Length];
            using (DistributedTransactionResponse readResponse = await readTransaction.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.IsTrue(
                    readResponse.IsSuccessStatusCode,
                    $"[{context}] Read DTx should succeed. Got: {readResponse.StatusCode}");
                Assert.AreEqual(items.Length, readResponse.Count, $"[{context}] Read DTx operation count mismatch.");

                for (int i = 0; i < items.Length; i++)
                {
                    readBacks[i] = readResponse.GetOperationResultAtIndex<JObject>(i).Resource;
                    AssertUserPropertiesRetained(items[i].Doc, readBacks[i], $"{context} item[{i}] after Read DTx");
                }
            }

            // Write DTx: round-trip the exact documents that were just read back.
            DistributedWriteTransaction writeTransaction = this.client.CreateDistributedWriteTransaction();
            for (int i = 0; i < items.Length; i++)
            {
                writeTransaction = writeTransaction.UpsertItem(
                    targetContainer, items[i].Pk, items[i].Id, StripSystemProperties(readBacks[i]));
            }

            using (DistributedTransactionResponse writeResponse = await writeTransaction.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.IsTrue(
                    writeResponse.IsSuccessStatusCode,
                    $"[{context}] Write DTx should commit. Got: {writeResponse.StatusCode}");
                Assert.AreEqual(items.Length, writeResponse.Count, $"[{context}] Write DTx operation count mismatch.");
            }

            // Read DTx verification: persisted state still matches the originals exactly.
            foreach ((string Id, PartitionKey Pk, JObject Doc) item in items)
            {
                await ConfirmVisibleAsync(targetContainer, item.Pk, item.Id);
            }

            DistributedReadTransaction verifyTransaction = this.client.CreateDistributedReadTransaction();
            foreach ((string Id, PartitionKey Pk, JObject Doc) item in items)
            {
                verifyTransaction = verifyTransaction.ReadItem(targetContainer, item.Pk, item.Id);
            }

            using (DistributedTransactionResponse verifyResponse = await verifyTransaction.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.IsTrue(
                    verifyResponse.IsSuccessStatusCode,
                    $"[{context}] Post-write Read DTx should succeed. Got: {verifyResponse.StatusCode}");
                Assert.AreEqual(items.Length, verifyResponse.Count, $"[{context}] Post-write Read DTx operation count mismatch.");

                for (int i = 0; i < items.Length; i++)
                {
                    AssertUserPropertiesRetained(
                        items[i].Doc,
                        verifyResponse.GetOperationResultAtIndex<JObject>(i).Resource,
                        $"{context} item[{i}] after Write DTx round-trip");
                }
            }
        }

        /// <summary>
        /// Best-effort warm-up: runs a single throwaway Write distributed transaction (an upsert of a
        /// disposable warm-up document) against <paramref name="targetContainer"/> so the coordinator/DTC
        /// Prepare path on the freshly-created collection is initialized before any Read DTx runs. A brand
        /// new DTX collection can surface a 408 (RequestTimeout) on its first distributed transaction while
        /// that path bootstraps; a prior Write DTx removes that cold-start timeout. Retries a few times on
        /// transient failure and never asserts — warm-up must not fail the run, the real tests report status.
        /// </summary>
        private async Task WarmUpContainerAsync(Container targetContainer, PartitionKey warmUpKey, JValue partitionKeyValue)
        {
            string warmUpId = $"dtx-warmup-{Guid.NewGuid():N}";
            JObject warmUpDoc = new JObject
            {
                ["id"] = warmUpId,
                ["pk"] = partitionKeyValue,
                ["_dtxWarmUp"] = true
            };

            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    using DistributedTransactionResponse response = await this.client
                        .CreateDistributedWriteTransaction()
                        .UpsertItem(targetContainer, warmUpKey, warmUpId, warmUpDoc)
                        .CommitTransactionAsync(CancellationToken.None);

                    if (response.IsSuccessStatusCode)
                    {
                        return;
                    }
                }
                catch (CosmosException)
                {
                    // Swallow: warm-up is best-effort. Fall through to the retry delay.
                }

                await Task.Delay(TimeSpan.FromSeconds(2));
            }
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
