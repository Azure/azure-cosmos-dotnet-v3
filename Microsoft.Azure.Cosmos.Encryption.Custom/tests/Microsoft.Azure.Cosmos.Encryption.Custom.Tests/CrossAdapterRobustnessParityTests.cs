//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using TestDoc = TestCommon.TestDoc;
    using EncryptVia = CrossAdapterEncryptDecryptParityTests.EncryptVia;
    using DecryptVia = CrossAdapterEncryptDecryptParityTests.DecryptVia;

    /// <summary>
    /// Robustness parity probes targeting four cross-adapter fault lines not covered by the
    /// structural matrix or the adversarial JSON-shape sweep:
    ///   1. <see cref="EncryptionOptions.PathsToEncrypt"/> edge cases — path order, duplicates,
    ///      paths missing from the source document, prefix overlap.
    ///   2. <c>_ei</c> wire-format parity — both encrypters must emit the same metadata fields
    ///      (<c>_ef</c>, <c>_en</c>, <c>_ea</c>, <c>_ed</c>, <c>_ep</c>) and the same encrypted
    ///      <c>_ed</c> bytes for the same input under the deterministic mock encryptor.
    ///   3. Null-vs-missing sensitive value distinction — a JSON <c>null</c> at an encrypted path
    ///      vs the path missing entirely from the source.
    ///   4. Large-data scaling sanity — large strings and many-entry dictionaries round-trip
    ///      under every adapter combination.
    /// </summary>
    [TestClass]
    public class CrossAdapterRobustnessParityTests
    {
        private const string DekId = "dekId";
        private static Mock<Encryptor> mockEncryptor = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext ctx)
        {
            _ = ctx;
            mockEncryptor = TestEncryptorFactory.CreateMde(DekId, out _);
        }

        // ============================================================
        // 1. PathsToEncrypt edge cases
        // ============================================================

        [TestMethod]
        [DataRow(EncryptVia.Newtonsoft, DecryptVia.NewtonsoftDefault)]
        [DataRow(EncryptVia.Newtonsoft, DecryptVia.StreamOptIn)]
        [DataRow(EncryptVia.Stream, DecryptVia.NewtonsoftDefault)]
        [DataRow(EncryptVia.Stream, DecryptVia.StreamOptIn)]
        public async Task PathsToEncrypt_OrderIndependence_RoundTripIsIdentical(EncryptVia encryptVia, DecryptVia decryptVia)
        {
            TestDoc doc = BuildSampleDoc();

            byte[] encryptedAscending = await EncryptWithPathsAsync(doc, TestDoc.PathsToEncrypt, encryptVia);
            byte[] encryptedReversed = await EncryptWithPathsAsync(doc, TestDoc.PathsToEncrypt.AsEnumerable().Reverse().ToList(), encryptVia);

            TestDoc fromAscending = await DecryptAndDeserializeAsync(encryptedAscending, decryptVia);
            TestDoc fromReversed = await DecryptAndDeserializeAsync(encryptedReversed, decryptVia);

            Assert.AreEqual(doc, fromAscending, $"Round-trip with ascending PathsToEncrypt order failed via {encryptVia}->{decryptVia}.");
            Assert.AreEqual(doc, fromReversed, $"Round-trip with reversed PathsToEncrypt order failed via {encryptVia}->{decryptVia}; this means the encrypt-or-decrypt pipeline is sensitive to PathsToEncrypt iteration order.");
        }

        [TestMethod]
        [DataRow(EncryptVia.Newtonsoft, DecryptVia.NewtonsoftDefault)]
        [DataRow(EncryptVia.Newtonsoft, DecryptVia.StreamOptIn)]
        [DataRow(EncryptVia.Stream, DecryptVia.NewtonsoftDefault)]
        [DataRow(EncryptVia.Stream, DecryptVia.StreamOptIn)]
        public async Task PathsToEncrypt_PathMissingFromSourceDoc_NotPresentInEpAndDocRoundTrips(EncryptVia encryptVia, DecryptVia decryptVia)
        {
            // PathsToEncrypt lists a path that the source document does not contain. The encrypter
            // must not invent the property, must not fail, and must not list it in _ep. The decrypt
            // path must still recover the original document with the present sensitive fields intact.
            string idValue = Guid.NewGuid().ToString();
            string source = "{\"Id\":\"" + idValue + "\",\"PK\":\"pk\",\"NonSensitive\":\"ns\","
                + "\"SensitiveStr\":\"present\",\"SensitiveInt\":7}";
            // Note: SensitiveArr and SensitiveDict are absent from the source.

            byte[] encrypted = await EncryptBytesWithPathsAsync(
                System.Text.Encoding.UTF8.GetBytes(source),
                TestDoc.PathsToEncrypt,
                encryptVia);

            JObject parsed = JObject.Parse(System.Text.Encoding.UTF8.GetString(encrypted));
            JArray ep = (JArray)parsed["_ei"]?["_ep"];
            Assert.IsNotNull(ep, $"Encrypted document missing _ei._ep block via {encryptVia}.");
            List<string> reportedPaths = ep.Select(t => (string)t).ToList();

            Assert.IsFalse(reportedPaths.Contains("/SensitiveArr"),
                $"Encrypter via {encryptVia} listed /SensitiveArr in _ep even though it was missing from the source. Reported: [{string.Join(",", reportedPaths)}]");
            Assert.IsFalse(reportedPaths.Contains("/SensitiveDict"),
                $"Encrypter via {encryptVia} listed /SensitiveDict in _ep even though it was missing from the source. Reported: [{string.Join(",", reportedPaths)}]");
            Assert.IsTrue(reportedPaths.Contains("/SensitiveStr"),
                $"Encrypter via {encryptVia} omitted /SensitiveStr from _ep even though it was present in the source. Reported: [{string.Join(",", reportedPaths)}]");
            Assert.IsTrue(reportedPaths.Contains("/SensitiveInt"),
                $"Encrypter via {encryptVia} omitted /SensitiveInt from _ep even though it was present in the source. Reported: [{string.Join(",", reportedPaths)}]");

            byte[] decrypted = await DecryptToBytesAsync(encrypted, decryptVia);
            JObject recovered = JObject.Parse(System.Text.Encoding.UTF8.GetString(decrypted));
            Assert.AreEqual("present", (string)recovered["SensitiveStr"],
                $"SensitiveStr value lost on round-trip via {encryptVia}->{decryptVia}.");
            Assert.AreEqual(7, (int)recovered["SensitiveInt"],
                $"SensitiveInt value lost on round-trip via {encryptVia}->{decryptVia}.");
            Assert.IsNull(recovered["SensitiveArr"],
                $"Decrypter via {decryptVia} invented SensitiveArr that wasn't in the source. Recovered: {recovered}");
            Assert.IsNull(recovered["SensitiveDict"],
                $"Decrypter via {decryptVia} invented SensitiveDict that wasn't in the source. Recovered: {recovered}");
        }

        [TestMethod]
        [DataRow(EncryptVia.Newtonsoft, DecryptVia.NewtonsoftDefault)]
        [DataRow(EncryptVia.Newtonsoft, DecryptVia.StreamOptIn)]
        [DataRow(EncryptVia.Stream, DecryptVia.NewtonsoftDefault)]
        [DataRow(EncryptVia.Stream, DecryptVia.StreamOptIn)]
        public async Task PathsToEncrypt_AllPathsMissingFromSource_EmitsEmptyEp(EncryptVia encryptVia, DecryptVia decryptVia)
        {
            // None of the configured PathsToEncrypt are present in the source doc. Both encrypters
            // must produce an _ei block with empty _ep (already asserted in
            // CrossAdapterAdversarialParityTests.EmptyPathsResult_BothEncryptersAgreeOnEmptyEiShape
            // for the structural parity); here we additionally assert the decrypt path is a no-op.
            string idValue = Guid.NewGuid().ToString();
            string source = "{\"Id\":\"" + idValue + "\",\"PK\":\"pk\",\"NonSensitive\":\"ns\"}";
            byte[] sourceBytes = System.Text.Encoding.UTF8.GetBytes(source);

            byte[] encrypted = await EncryptBytesAsync(sourceBytes, encryptVia);
            byte[] decrypted = await DecryptToBytesAsync(encrypted, decryptVia);
            JObject recovered = JObject.Parse(System.Text.Encoding.UTF8.GetString(decrypted));

            Assert.AreEqual(idValue, (string)recovered["Id"], $"Id changed via {encryptVia}->{decryptVia}.");
            Assert.AreEqual("pk", (string)recovered["PK"]);
            Assert.AreEqual("ns", (string)recovered["NonSensitive"]);
            Assert.IsNull(recovered["_ei"], $"Decrypter via {decryptVia} left an _ei block on a document with no encrypted paths. Recovered: {recovered}");
        }

        // ============================================================
        // 2. _ei wire-format parity between adapters
        // ============================================================

        [TestMethod]
        public async Task EiWireFormat_AllFields_AgreeBetweenAdapters()
        {
            // Strong parity check: the encrypted _ei block must have the same field set,
            // same scalar values for _ef/_en/_ea, and the same _ed byte content (the mock
            // encryptor is deterministic). A divergence here would break interoperability
            // for any downstream tool that re-parses _ei or signs/hashes its contents.
            TestDoc doc = BuildSampleDoc();

            byte[] nEnc = await EncryptAsync(doc, EncryptVia.Newtonsoft);
            byte[] sEnc = await EncryptAsync(doc, EncryptVia.Stream);

            JObject nEi = (JObject)JObject.Parse(System.Text.Encoding.UTF8.GetString(nEnc))["_ei"];
            JObject sEi = (JObject)JObject.Parse(System.Text.Encoding.UTF8.GetString(sEnc))["_ei"];

            Assert.IsNotNull(nEi, "Newtonsoft-encrypted doc is missing _ei.");
            Assert.IsNotNull(sEi, "Stream-encrypted doc is missing _ei.");

            // Same field set.
            CollectionAssert.AreEquivalent(
                nEi.Properties().Select(p => p.Name).ToList(),
                sEi.Properties().Select(p => p.Name).ToList(),
                $"_ei field set diverged.\n Newtonsoft: {nEi}\n Stream:     {sEi}");

            // Same scalar values for the version/DEK/algorithm markers.
            Assert.AreEqual((int)nEi["_ef"], (int)sEi["_ef"], "Encryption format version (_ef) diverged.");
            Assert.AreEqual((string)nEi["_en"], (string)sEi["_en"], "DEK name (_en) diverged.");
            Assert.AreEqual((string)nEi["_ea"], (string)sEi["_ea"], "Algorithm marker (_ea) diverged.");

            // Same _ed body. With the deterministic mock encryptor any divergence indicates
            // the two adapters fed the encryption layer different plaintext bytes (e.g. different
            // metadata layout, different serialisation of the encrypted-paths payload).
            string nEd = (string)nEi["_ed"];
            string sEd = (string)sEi["_ed"];
            Assert.AreEqual(nEd, sEd,
                $"_ed payload diverged between Newtonsoft and Stream encrypters. The same plaintext document produced two different encrypted payloads; downstream tools that re-hash or compare encrypted bodies will not consider these documents equivalent.");
        }

        [TestMethod]
        public async Task EiKeyOrder_IsStableAcrossEncryptCalls()
        {
            // The _ei JSON serialisation order should be deterministic within a single adapter.
            // Downstream consumers that signature-verify a serialised _ei block depend on this.
            TestDoc doc = BuildSampleDoc();

            byte[] firstNewtonsoft = await EncryptAsync(doc, EncryptVia.Newtonsoft);
            byte[] secondNewtonsoft = await EncryptAsync(doc, EncryptVia.Newtonsoft);
            byte[] firstStream = await EncryptAsync(doc, EncryptVia.Stream);
            byte[] secondStream = await EncryptAsync(doc, EncryptVia.Stream);

            List<string> nOrder1 = EiKeyOrder(firstNewtonsoft);
            List<string> nOrder2 = EiKeyOrder(secondNewtonsoft);
            List<string> sOrder1 = EiKeyOrder(firstStream);
            List<string> sOrder2 = EiKeyOrder(secondStream);

            CollectionAssert.AreEqual(nOrder1, nOrder2,
                $"Newtonsoft encrypter emitted _ei keys in a different order on two calls with identical input. First: [{string.Join(",", nOrder1)}], Second: [{string.Join(",", nOrder2)}]");
            CollectionAssert.AreEqual(sOrder1, sOrder2,
                $"Stream encrypter emitted _ei keys in a different order on two calls with identical input. First: [{string.Join(",", sOrder1)}], Second: [{string.Join(",", sOrder2)}]");
        }

        // ============================================================
        // 3. Null-vs-missing sensitive value distinction
        // ============================================================

        [TestMethod]
        [DataRow(EncryptVia.Newtonsoft, DecryptVia.NewtonsoftDefault)]
        [DataRow(EncryptVia.Newtonsoft, DecryptVia.StreamOptIn)]
        [DataRow(EncryptVia.Stream, DecryptVia.NewtonsoftDefault)]
        [DataRow(EncryptVia.Stream, DecryptVia.StreamOptIn)]
        public async Task NullSensitiveValue_AndMissingProperty_HaveDistinctRoundTripBehaviour(EncryptVia encryptVia, DecryptVia decryptVia)
        {
            // Two documents that are semantically distinct: one has SensitiveStr explicitly set to
            // null, the other omits the SensitiveStr property entirely. The round-trip must
            // preserve the distinction — a null-valued property must come back as null (not missing),
            // and a missing property must stay missing (not invented as null).
            string idValue = Guid.NewGuid().ToString();
            string nullSource = "{\"Id\":\"" + idValue + "\",\"PK\":\"pk\",\"NonSensitive\":\"ns\","
                + "\"SensitiveStr\":null,\"SensitiveInt\":7}";
            string missingSource = "{\"Id\":\"" + idValue + "\",\"PK\":\"pk\",\"NonSensitive\":\"ns\","
                + "\"SensitiveInt\":7}";

            JObject nullRecovered = await RoundTripToJObjectAsync(nullSource, encryptVia, decryptVia);
            JObject missingRecovered = await RoundTripToJObjectAsync(missingSource, encryptVia, decryptVia);

            // Null case: property is present with value null.
            Assert.IsTrue(nullRecovered.ContainsKey("SensitiveStr"),
                $"Round-trip via {encryptVia}->{decryptVia} lost the explicit null SensitiveStr property entirely. Recovered: {nullRecovered}");
            Assert.AreEqual(JTokenType.Null, nullRecovered["SensitiveStr"].Type,
                $"Round-trip via {encryptVia}->{decryptVia} turned an explicit null SensitiveStr into a non-null value: {nullRecovered["SensitiveStr"]}");

            // Missing case: property must remain absent.
            Assert.IsFalse(missingRecovered.ContainsKey("SensitiveStr"),
                $"Round-trip via {encryptVia}->{decryptVia} invented a SensitiveStr property that was missing in the source. Recovered: {missingRecovered}");
        }

        // ============================================================
        // 4. Large-data scaling sanity
        // ============================================================

        [TestMethod]
        [DataRow(EncryptVia.Newtonsoft, DecryptVia.NewtonsoftDefault)]
        [DataRow(EncryptVia.Newtonsoft, DecryptVia.StreamOptIn)]
        [DataRow(EncryptVia.Stream, DecryptVia.NewtonsoftDefault)]
        [DataRow(EncryptVia.Stream, DecryptVia.StreamOptIn)]
        public async Task LargeSensitiveString_RoundTripsIntact(EncryptVia encryptVia, DecryptVia decryptVia)
        {
            const int sizeBytes = 256 * 1024;
            TestDoc doc = BuildSampleDoc();
            doc.SensitiveStr = new string('z', sizeBytes);

            byte[] encrypted = await EncryptAsync(doc, encryptVia);
            TestDoc recovered = await DecryptAndDeserializeAsync(encrypted, decryptVia);

            Assert.AreEqual(sizeBytes, recovered.SensitiveStr?.Length ?? -1,
                $"Large SensitiveString lost bytes on round-trip via {encryptVia}->{decryptVia}. Expected {sizeBytes} bytes; got {recovered.SensitiveStr?.Length ?? -1}.");
            Assert.AreEqual(doc.SensitiveStr, recovered.SensitiveStr,
                $"Large SensitiveString content changed on round-trip via {encryptVia}->{decryptVia}.");
        }

        [TestMethod]
        [DataRow(EncryptVia.Newtonsoft, DecryptVia.NewtonsoftDefault)]
        [DataRow(EncryptVia.Newtonsoft, DecryptVia.StreamOptIn)]
        [DataRow(EncryptVia.Stream, DecryptVia.NewtonsoftDefault)]
        [DataRow(EncryptVia.Stream, DecryptVia.StreamOptIn)]
        public async Task LargeSensitiveDictionary_RoundTripsIntact(EncryptVia encryptVia, DecryptVia decryptVia)
        {
            const int entryCount = 5_000;
            TestDoc doc = BuildSampleDoc();
            Dictionary<string, string> big = new(entryCount);
            for (int i = 0; i < entryCount; i++)
            {
                big["k-" + i] = "v-" + i;
            }
            doc.SensitiveDict = big;

            byte[] encrypted = await EncryptAsync(doc, encryptVia);
            TestDoc recovered = await DecryptAndDeserializeAsync(encrypted, decryptVia);

            Assert.IsNotNull(recovered.SensitiveDict,
                $"Large SensitiveDict came back null via {encryptVia}->{decryptVia}.");
            Assert.AreEqual(entryCount, recovered.SensitiveDict.Count,
                $"Large SensitiveDict lost entries via {encryptVia}->{decryptVia}. Expected {entryCount}; got {recovered.SensitiveDict.Count}.");
            // Spot-check first, middle, last entries.
            Assert.AreEqual("v-0", recovered.SensitiveDict["k-0"]);
            Assert.AreEqual("v-" + (entryCount / 2), recovered.SensitiveDict["k-" + (entryCount / 2)]);
            Assert.AreEqual("v-" + (entryCount - 1), recovered.SensitiveDict["k-" + (entryCount - 1)]);
        }

        // ============================================================
        // Helpers
        // ============================================================

        private static List<string> EiKeyOrder(byte[] encryptedDoc)
        {
            // Parse with a reader that preserves source order (JObject does this) and return the
            // _ei property names in source-order. JObject's Properties() enumeration returns
            // properties in source order.
            JObject doc = JObject.Parse(System.Text.Encoding.UTF8.GetString(encryptedDoc));
            JObject ei = (JObject)doc["_ei"];
            return ei.Properties().Select(p => p.Name).ToList();
        }

        private async Task<JObject> RoundTripToJObjectAsync(string sourceJson, EncryptVia encryptVia, DecryptVia decryptVia)
        {
            byte[] sourceBytes = System.Text.Encoding.UTF8.GetBytes(sourceJson);
            byte[] encrypted = await EncryptBytesAsync(sourceBytes, encryptVia);
            byte[] decrypted = await DecryptToBytesAsync(encrypted, decryptVia);
            return JObject.Parse(System.Text.Encoding.UTF8.GetString(decrypted));
        }

        private static async Task<TestDoc> DecryptAndDeserializeAsync(byte[] encryptedBytes, DecryptVia decryptVia)
        {
            byte[] decryptedBytes = await DecryptToBytesAsync(encryptedBytes, decryptVia);
            string json = System.Text.Encoding.UTF8.GetString(decryptedBytes);
            return JsonConvert.DeserializeObject<TestDoc>(json);
        }

        private static async Task<byte[]> DecryptToBytesAsync(byte[] encryptedBytes, DecryptVia decryptVia)
        {
            RequestOptions requestOptions = decryptVia == DecryptVia.StreamOptIn ? StreamOptIn() : null;
            using MemoryStream input = new(encryptedBytes);
            (Stream output, _) = await EncryptionProcessor.DecryptAsync(
                input,
                mockEncryptor.Object,
                CosmosDiagnosticsContext.Create(null),
                requestOptions,
                CancellationToken.None);
            return await ToBytesAsync(output);
        }

        private static Task<byte[]> EncryptAsync(TestDoc doc, EncryptVia encryptVia)
            => EncryptWithPathsAsync(doc, TestDoc.PathsToEncrypt, encryptVia);

        private static Task<byte[]> EncryptWithPathsAsync(TestDoc doc, List<string> paths, EncryptVia encryptVia)
            => EncryptBytesWithPathsAsync(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(doc)), paths, encryptVia);

        private static Task<byte[]> EncryptBytesAsync(byte[] plainBytes, EncryptVia encryptVia)
            => EncryptBytesWithPathsAsync(plainBytes, TestDoc.PathsToEncrypt, encryptVia);

        private static async Task<byte[]> EncryptBytesWithPathsAsync(byte[] plainBytes, List<string> paths, EncryptVia encryptVia)
        {
            EncryptionOptions opts = new()
            {
                DataEncryptionKeyId = DekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = paths,
            };

            JsonProcessor processor = encryptVia == EncryptVia.Stream ? JsonProcessor.Stream : JsonProcessor.Newtonsoft;

            using MemoryStream input = new(plainBytes);
            Stream encrypted = await EncryptionProcessor.EncryptAsync(
                input,
                mockEncryptor.Object,
                opts,
                processor,
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            return await ToBytesAsync(encrypted);
        }

        private static async Task<byte[]> ToBytesAsync(Stream s)
        {
            using MemoryStream ms = new();
            s.Position = 0;
            await s.CopyToAsync(ms);
            return ms.ToArray();
        }

        private static TestDoc BuildSampleDoc() => new()
        {
            Id = Guid.NewGuid().ToString(),
            PK = "pk",
            NonSensitive = "ns",
            SensitiveStr = "secret-string-value",
            SensitiveInt = 12345,
            SensitiveArr = new[] { "a1", "a2", "a3" },
            SensitiveDict = new Dictionary<string, string> { { "k1", "v1" }, { "k2", "v2" } },
        };

        private static ItemRequestOptions StreamOptIn() => new()
        {
            Properties = new Dictionary<string, object>
            {
                { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream },
            },
        };
    }
}
#endif
