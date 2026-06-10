//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Tests;
    using Microsoft.Data.Encryption.Cryptography.Serializers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Round-trip compatibility verification between the two internal JSON processors
    /// (JsonProcessor.Newtonsoft and JsonProcessor.Stream) and the released preview08
    /// (Newtonsoft-only) decryptor.
    ///
    /// Scenarios covered (see compat-analysis-reports/roundtrip-report.md):
    ///  a) Encrypt Newtonsoft -> Decrypt Stream
    ///  b) Encrypt Stream     -> Decrypt Newtonsoft
    ///  c) Same-processor controls (N->N, S->S)
    ///  d) Downgrade simulation: payloads produced at HEAD decrypted by a verbatim
    ///     replica of the preview08 decryptor (see <see cref="Preview08Decryptor"/>)
    ///  e) Interleaved/concurrent switching between processors (shared-state probes)
    ///  f) `_ei` envelope shape equality and property-order tolerance
    /// </summary>
    [TestClass]
    public class CrossProcessorCompatibilityTests
    {
        private const string DekId = "crossProcessorDek";

        private static Mock<Encryptor> mockEncryptor;

        // ---- Edge-case document definition ------------------------------------------------

        // C# string values; they are JSON-encoded via JsonConvert.ToString when the document
        // text is built, so the on-the-wire JSON contains real escape sequences.
        private static readonly string UnicodeString = "h\u00e9llo w\u00f6rld \U0001F600\U0001F389 \uD83D\uDE03 \u4F60\u597D end";
        private static readonly string SpecialCharsString = "quote:\" backslash:\\ slash:/ nl:\n tab:\t cr:\r bs:\b ff:\f ctrl:\u0001 unit:\u001F del:\u007F";

        private static readonly Lazy<string> LargeString = new Lazy<string>(() =>
        {
            // > 64KB of chars (and even more UTF-8 bytes) so the Stream processor has to
            // cross several internal read-buffer boundaries, including in the middle of
            // multi-byte code points and escape sequences.
            StringBuilder sb = new StringBuilder(80_000);
            int i = 0;
            while (sb.Length < 70_000)
            {
                sb.Append("seg").Append(i++).Append("-\u00e9\u00fc\U0001F600-\"\\\n\t|");
            }

            return sb.ToString();
        });

        private static readonly List<string> EdgeCasePathsToEncrypt = new List<string>
        {
            "/StrAscii",
            "/StrUnicode",
            "/StrSpecial",
            "/StrEmpty",
            "/LongZero",
            "/LongOne",
            "/LongMinusOne",
            "/LongMax",
            "/LongMin",
            "/DblZero",
            "/DblNegHalf",
            "/DblMax",
            "/DblEpsilon",
            "/DblTiny",
            "/Dbl17Digits",
            "/DblNegZero",
            "/DblSci",
            "/BoolTrue",
            "/BoolFalse",
            "/NullProp",
            "/NestedObj",
            "/ArrPrim",
            "/ArrObj",
            "/ArrEmpty",
            "/ArrNested",
            "/LargeStr",
        };

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _ = context;
            mockEncryptor = TestEncryptorFactory.CreateMde(DekId, out _);
        }

        /// <summary>
        /// Builds the edge-case document as raw JSON text so the exact number literals are
        /// controlled by the test (not by a serializer).
        /// </summary>
        /// <param name="includeNullsInsideEncryptedContainers">
        /// When true (default) the encrypted NestedObj/ArrPrim values contain JSON nulls,
        /// which triggers KNOWN FAILURE FINDING-1 on the Stream encrypt path (see
        /// roundtrip-report.md). Tests that probe orthogonal concerns (state contamination,
        /// envelope shape) pass false to keep their signal independent of that known bug.
        /// </param>
        private static string BuildEdgeCaseJson(bool includeNullsInsideEncryptedContainers = true)
        {
            StringBuilder sb = new StringBuilder(160 * 1024);
            sb.Append('{');
            sb.Append("\"id\":\"edge-doc-1\",");

            // Properties NOT in PathsToEncrypt - must be left intact.
            sb.Append("\"PlainStr\":\"plain passthrough value\",");
            sb.Append("\"PlainDouble\":1E-300,");

            // Strings.
            sb.Append("\"StrAscii\":\"The quick brown fox; 1234567890.\",");
            sb.Append("\"StrUnicode\":").Append(JsonConvert.ToString(UnicodeString)).Append(',');
            sb.Append("\"StrSpecial\":").Append(JsonConvert.ToString(SpecialCharsString)).Append(',');
            sb.Append("\"StrEmpty\":\"\",");

            // Longs.
            sb.Append("\"LongZero\":0,");
            sb.Append("\"LongOne\":1,");
            sb.Append("\"LongMinusOne\":-1,");
            sb.Append("\"LongMax\":9223372036854775807,");
            sb.Append("\"LongMin\":-9223372036854775808,");

            // Doubles.
            sb.Append("\"DblZero\":0.0,");
            sb.Append("\"DblNegHalf\":-0.5,");
            sb.Append("\"DblMax\":1.7976931348623157E+308,");
            sb.Append("\"DblEpsilon\":5E-324,");
            sb.Append("\"DblTiny\":1E-300,");
            sb.Append("\"Dbl17Digits\":0.1234567890123456789,");
            sb.Append("\"DblNegZero\":-0.0,");
            sb.Append("\"DblSci\":1.25E+10,");

            // Booleans + null.
            sb.Append("\"BoolTrue\":true,");
            sb.Append("\"BoolFalse\":false,");
            sb.Append("\"NullProp\":null,");

            // Nested object (multi-level) and arrays.
            if (includeNullsInsideEncryptedContainers)
            {
                sb.Append("\"NestedObj\":{\"level1\":{\"level2\":{\"level3\":[\"deep\",42,{\"flag\":true}],\"emptyObj\":{}},\"innerNull\":null},\"num\":0.5},");
                sb.Append("\"ArrPrim\":[1,-2.5,\"three\",true,null],");
            }
            else
            {
                sb.Append("\"NestedObj\":{\"level1\":{\"level2\":{\"level3\":[\"deep\",42,{\"flag\":true}],\"emptyObj\":{}}},\"num\":0.5},");
                sb.Append("\"ArrPrim\":[1,-2.5,\"three\",true],");
            }

            sb.Append("\"ArrObj\":[{\"a\":1},{\"b\":\"two\"}],");
            sb.Append("\"ArrEmpty\":[],");
            sb.Append("\"ArrNested\":[[1,2],[[3]],[]],");

            // Large string crossing buffer boundaries.
            sb.Append("\"LargeStr\":").Append(JsonConvert.ToString(LargeString.Value));

            sb.Append('}');
            return sb.ToString();
        }

        // ---- Processor matrix --------------------------------------------------------------

        public static IEnumerable<object[]> ProcessorMatrix
        {
            get
            {
                yield return new object[] { (int)JsonProcessor.Newtonsoft, (int)JsonProcessor.Newtonsoft };
#if NET8_0_OR_GREATER
                yield return new object[] { (int)JsonProcessor.Newtonsoft, (int)JsonProcessor.Stream };
                yield return new object[] { (int)JsonProcessor.Stream, (int)JsonProcessor.Newtonsoft };
                yield return new object[] { (int)JsonProcessor.Stream, (int)JsonProcessor.Stream };
#endif
            }
        }

        // ---- Helpers -----------------------------------------------------------------------

        private static EncryptionOptions CreateOptions(IEnumerable<string> paths)
        {
            return new EncryptionOptions
            {
                DataEncryptionKeyId = DekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = paths.ToList(),
            };
        }

        private static async Task<MemoryStream> EncryptToMemoryAsync(string json, JsonProcessor processor, IEnumerable<string> pathsToEncrypt)
        {
            Stream encrypted = await EncryptionProcessor.EncryptAsync(
                new MemoryStream(Encoding.UTF8.GetBytes(json)),
                mockEncryptor.Object,
                CreateOptions(pathsToEncrypt),
                processor,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            MemoryStream result = new MemoryStream();
            encrypted.Position = 0;
            await encrypted.CopyToAsync(result);
            await encrypted.DisposeAsync();
            result.Position = 0;
            return result;
        }

        private static async Task<(string json, DecryptionContext context)> DecryptToStringAsync(MemoryStream encrypted, JsonProcessor processor)
        {
            // Always hand the decryptor an independent stream copy so the same encrypted
            // payload can be decrypted multiple times by different processors.
            MemoryStream input = new MemoryStream(encrypted.ToArray());

            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptAsync(
                input,
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                RequestOptionsOverrideHelper.Create(processor),
                CancellationToken.None);

            using StreamReader reader = new StreamReader(decrypted);
            string json = await reader.ReadToEndAsync();
            return (json, context);
        }

        private static JsonProcessor ResolveJsonProcessor(int value)
        {
            if (!Enum.IsDefined(typeof(JsonProcessor), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Invalid JsonProcessor value supplied to test.");
            }

            return (JsonProcessor)value;
        }

        // ---- JSON-semantic deep equality ----------------------------------------------------

        private static void AssertJsonEquivalent(string expectedJson, string actualJson, string scenario)
        {
            using JsonDocument expected = JsonDocument.Parse(expectedJson);
            using JsonDocument actual = JsonDocument.Parse(actualJson);
            AssertElementsEqual(expected.RootElement, actual.RootElement, "$", scenario);
        }

        private static void AssertElementsEqual(JsonElement expected, JsonElement actual, string path, string scenario)
        {
            switch (expected.ValueKind)
            {
                case JsonValueKind.Object:
                    Assert.AreEqual(JsonValueKind.Object, actual.ValueKind, $"[{scenario}] kind mismatch at {path}");
                    Dictionary<string, JsonElement> expectedProps = expected.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                    Dictionary<string, JsonElement> actualProps = actual.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                    CollectionAssert.AreEquivalent(
                        expectedProps.Keys.ToList(),
                        actualProps.Keys.ToList(),
                        $"[{scenario}] property-name sets differ at {path}");
                    foreach (KeyValuePair<string, JsonElement> kv in expectedProps)
                    {
                        AssertElementsEqual(kv.Value, actualProps[kv.Key], path + "." + kv.Key, scenario);
                    }

                    break;

                case JsonValueKind.Array:
                    Assert.AreEqual(JsonValueKind.Array, actual.ValueKind, $"[{scenario}] kind mismatch at {path}");
                    JsonElement[] expectedItems = expected.EnumerateArray().ToArray();
                    JsonElement[] actualItems = actual.EnumerateArray().ToArray();
                    Assert.AreEqual(expectedItems.Length, actualItems.Length, $"[{scenario}] array length mismatch at {path}");
                    for (int i = 0; i < expectedItems.Length; i++)
                    {
                        AssertElementsEqual(expectedItems[i], actualItems[i], path + "[" + i + "]", scenario);
                    }

                    break;

                case JsonValueKind.String:
                    Assert.AreEqual(JsonValueKind.String, actual.ValueKind, $"[{scenario}] kind mismatch at {path}");
                    Assert.AreEqual(expected.GetString(), actual.GetString(), $"[{scenario}] string mismatch at {path}");
                    break;

                case JsonValueKind.Number:
                    Assert.AreEqual(JsonValueKind.Number, actual.ValueKind, $"[{scenario}] kind mismatch at {path}");
                    AssertNumbersEqual(expected.GetRawText(), actual.GetRawText(), path, scenario);
                    break;

                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    Assert.AreEqual(expected.ValueKind, actual.ValueKind, $"[{scenario}] literal mismatch at {path}");
                    break;

                default:
                    Assert.Fail($"[{scenario}] unexpected JSON kind {expected.ValueKind} at {path}");
                    break;
            }
        }

        private static bool IsIntegralNumberText(string raw)
        {
            return raw.IndexOf('.') < 0 && raw.IndexOf('e') < 0 && raw.IndexOf('E') < 0;
        }

        private static void AssertNumbersEqual(string expectedRaw, string actualRaw, string path, string scenario)
        {
            if (IsIntegralNumberText(expectedRaw) && IsIntegralNumberText(actualRaw))
            {
                // Integral (long) literals must round-trip with the exact same decimal text:
                // this is the textual fidelity the legacy (Newtonsoft) path guarantees.
                Assert.AreEqual(expectedRaw, actualRaw, $"[{scenario}] integral number text mismatch at {path}");
                return;
            }

            // Floating point literals may be reformatted by the writer (e.g. 0.0 -> 0,
            // -0.0 -> -0, 1.25E+10 -> 12500000000.0) but must round-trip to the exact same
            // IEEE-754 double bits.
            double expectedValue = double.Parse(expectedRaw, NumberStyles.Float, CultureInfo.InvariantCulture);
            double actualValue = double.Parse(actualRaw, NumberStyles.Float, CultureInfo.InvariantCulture);
            Assert.AreEqual(
                BitConverter.DoubleToInt64Bits(expectedValue),
                BitConverter.DoubleToInt64Bits(actualValue),
                $"[{scenario}] double bits mismatch at {path}: expected '{expectedRaw}', actual '{actualRaw}'");
        }

        // ---- (a), (b), (c): cross-processor round-trip matrix -------------------------------

        [TestMethod]
        [DynamicData(nameof(ProcessorMatrix))]
        public async Task RoundTrip_EdgeCaseDocument_AcrossProcessors(int encryptProcessorValue, int decryptProcessorValue)
        {
            JsonProcessor encryptProcessor = ResolveJsonProcessor(encryptProcessorValue);
            JsonProcessor decryptProcessor = ResolveJsonProcessor(decryptProcessorValue);
            string scenario = $"Encrypt:{encryptProcessor}->Decrypt:{decryptProcessor}";

            // FINDING-1 (see KnownFailure_Finding1_* tests): the Stream encrypt path corrupts
            // _ep when an encrypted object/array contains a JSON null, so Stream-encrypt rows
            // use the null-free document variant to keep the rest of the matrix signal clean.
            bool includeNullsInsideEncryptedContainers = encryptProcessor == JsonProcessor.Newtonsoft;
            string original = BuildEdgeCaseJson(includeNullsInsideEncryptedContainers);

            MemoryStream encrypted = await EncryptToMemoryAsync(original, encryptProcessor, EdgeCasePathsToEncrypt);

            // Sanity on the encrypted payload: _ei present, plaintext passthrough intact,
            // encrypted strings replaced with base64.
            string encryptedJson = Encoding.UTF8.GetString(encrypted.ToArray());
            using (JsonDocument encryptedDoc = JsonDocument.Parse(encryptedJson))
            {
                Assert.IsTrue(encryptedDoc.RootElement.TryGetProperty(Constants.EncryptedInfo, out _), $"[{scenario}] _ei missing");
                Assert.AreEqual("plain passthrough value", encryptedDoc.RootElement.GetProperty("PlainStr").GetString(), $"[{scenario}] passthrough property modified by encryption");
                Assert.AreNotEqual("The quick brown fox; 1234567890.", encryptedDoc.RootElement.GetProperty("StrAscii").GetString(), $"[{scenario}] property was not encrypted");
                Assert.AreEqual(JsonValueKind.Null, encryptedDoc.RootElement.GetProperty("NullProp").ValueKind, $"[{scenario}] null property must not be encrypted");
            }

            (string decrypted, DecryptionContext context) = await DecryptToStringAsync(encrypted, decryptProcessor);

            Assert.IsNotNull(context, $"[{scenario}] decryption context missing");
            AssertJsonEquivalent(original, decrypted, scenario);

            // Null-valued paths are skipped during encryption; everything else must be reported.
            List<string> expectedDecryptedPaths = EdgeCasePathsToEncrypt.Where(p => p != "/NullProp").ToList();
            CollectionAssert.AreEquivalent(
                expectedDecryptedPaths,
                context.DecryptionInfoList[0].PathsDecrypted.ToList(),
                $"[{scenario}] decrypted path set mismatch");
        }

        // ---- (d): downgrade simulation - replicated preview08 decryptor ----------------------

        [TestMethod]
        public async Task Downgrade_Preview08Decryptor_ReadsNewtonsoftEncryptedPayload()
        {
            await this.DowngradeRoundTripCoreAsync(JsonProcessor.Newtonsoft);
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        public async Task Downgrade_Preview08Decryptor_ReadsStreamEncryptedPayload()
        {
            await this.DowngradeRoundTripCoreAsync(JsonProcessor.Stream);
        }
#endif

        private async Task DowngradeRoundTripCoreAsync(JsonProcessor encryptProcessor)
        {
            string scenario = $"Encrypt:{encryptProcessor}->Decrypt:preview08";

            // FINDING-1: Stream-encrypt rows use the null-free variant (a null inside an
            // encrypted container corrupts _ep and crashes the preview08 decryptor - that
            // known failure is pinned by KnownFailure_Finding1_Preview08Decrypt).
            string original = BuildEdgeCaseJson(includeNullsInsideEncryptedContainers: encryptProcessor == JsonProcessor.Newtonsoft);

            MemoryStream encrypted = await EncryptToMemoryAsync(original, encryptProcessor, EdgeCasePathsToEncrypt);

            (JObject decryptedDoc, List<string> pathsDecrypted) = await Preview08Decryptor.DecryptAsync(
                new MemoryStream(encrypted.ToArray()),
                mockEncryptor.Object,
                CancellationToken.None);

            string decryptedJson = decryptedDoc.ToString(Formatting.None);
            AssertJsonEquivalent(original, decryptedJson, scenario);

            List<string> expectedDecryptedPaths = EdgeCasePathsToEncrypt.Where(p => p != "/NullProp").ToList();
            CollectionAssert.AreEquivalent(expectedDecryptedPaths, pathsDecrypted, $"[{scenario}] decrypted path set mismatch");
        }

        // ---- (e): switching between processors (shared state probes) -------------------------

#if NET8_0_OR_GREATER
        [TestMethod]
        public async Task Switching_InterleavedAcrossProcessors_NoStateContamination()
        {
            // Null-free variant: this test probes shared-state contamination, which must not
            // be conflated with FINDING-1 (nulls inside encrypted containers on Stream path).
            string original = BuildEdgeCaseJson(includeNullsInsideEncryptedContainers: false);
            JsonProcessor[] processors = { JsonProcessor.Newtonsoft, JsonProcessor.Stream };

            // The processor instances behind EncryptionProcessor are process-wide statics;
            // alternating encrypt/decrypt across both processors repeatedly exercises any
            // pooled-buffer or cached state shared between them.
            for (int i = 0; i < 16; i++)
            {
                JsonProcessor encryptProcessor = processors[i % 2];
                JsonProcessor decryptProcessor = processors[(i / 2) % 2];
                string scenario = $"iteration {i}: {encryptProcessor}->{decryptProcessor}";

                MemoryStream encrypted = await EncryptToMemoryAsync(original, encryptProcessor, EdgeCasePathsToEncrypt);
                (string decrypted, DecryptionContext context) = await DecryptToStringAsync(encrypted, decryptProcessor);

                Assert.IsNotNull(context, scenario);
                AssertJsonEquivalent(original, decrypted, scenario);
            }
        }

        [TestMethod]
        public async Task Switching_ConcurrentMixedProcessors_NoStateContamination()
        {
            // Null-free variant - see Switching_InterleavedAcrossProcessors_NoStateContamination.
            string original = BuildEdgeCaseJson(includeNullsInsideEncryptedContainers: false);
            JsonProcessor[] processors = { JsonProcessor.Newtonsoft, JsonProcessor.Stream };

            IEnumerable<Task> tasks = Enumerable.Range(0, 32).Select(async i =>
            {
                JsonProcessor encryptProcessor = processors[i % 2];
                JsonProcessor decryptProcessor = processors[(i / 2) % 2];
                string scenario = $"concurrent task {i}: {encryptProcessor}->{decryptProcessor}";

                MemoryStream encrypted = await EncryptToMemoryAsync(original, encryptProcessor, EdgeCasePathsToEncrypt);
                (string decrypted, DecryptionContext context) = await DecryptToStringAsync(encrypted, decryptProcessor);

                Assert.IsNotNull(context, scenario);
                AssertJsonEquivalent(original, decrypted, scenario);
            });

            await Task.WhenAll(tasks);
        }
#endif

        // ---- (f): _ei envelope shape ---------------------------------------------------------

#if NET8_0_OR_GREATER
        [TestMethod]
        public async Task EnvelopeShape_IdenticalAcrossProcessors()
        {
            // Null-free variant: FINDING-1 corrupts the Stream-side _ep with null entries
            // when encrypted containers hold nulls; that is pinned by KnownFailure_Finding1_*.
            string original = BuildEdgeCaseJson(includeNullsInsideEncryptedContainers: false);

            MemoryStream newtonsoftPayload = await EncryptToMemoryAsync(original, JsonProcessor.Newtonsoft, EdgeCasePathsToEncrypt);
            MemoryStream streamPayload = await EncryptToMemoryAsync(original, JsonProcessor.Stream, EdgeCasePathsToEncrypt);

            using JsonDocument newtonsoftDoc = JsonDocument.Parse(newtonsoftPayload.ToArray());
            using JsonDocument streamDoc = JsonDocument.Parse(streamPayload.ToArray());

            JsonElement newtonsoftEi = newtonsoftDoc.RootElement.GetProperty(Constants.EncryptedInfo);
            JsonElement streamEi = streamDoc.RootElement.GetProperty(Constants.EncryptedInfo);

            // Property names of the envelope, in document order. Both serializers emit the
            // declaration order of EncryptionProperties (_ef,_en,_ea,_ed,_ep); assert that
            // remains true so the wire format stays byte-compatible with preview08 ordering.
            List<string> newtonsoftNames = newtonsoftEi.EnumerateObject().Select(p => p.Name).ToList();
            List<string> streamNames = streamEi.EnumerateObject().Select(p => p.Name).ToList();
            CollectionAssert.AreEqual(newtonsoftNames, streamNames, "envelope property order differs between processors");
            CollectionAssert.AreEquivalent(
                new List<string> { Constants.EncryptionFormatVersion, Constants.EncryptionDekId, Constants.EncryptionAlgorithm, Constants.EncryptedData, Constants.EncryptedPaths },
                newtonsoftNames,
                "envelope property set changed");

            // Scalar envelope values.
            Assert.AreEqual(EncryptionFormatVersion.Mde, newtonsoftEi.GetProperty(Constants.EncryptionFormatVersion).GetInt32());
            Assert.AreEqual(EncryptionFormatVersion.Mde, streamEi.GetProperty(Constants.EncryptionFormatVersion).GetInt32());
            Assert.AreEqual(DekId, newtonsoftEi.GetProperty(Constants.EncryptionDekId).GetString());
            Assert.AreEqual(DekId, streamEi.GetProperty(Constants.EncryptionDekId).GetString());
            Assert.AreEqual(CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized, newtonsoftEi.GetProperty(Constants.EncryptionAlgorithm).GetString());
            Assert.AreEqual(CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized, streamEi.GetProperty(Constants.EncryptionAlgorithm).GetString());
            Assert.AreEqual(JsonValueKind.Null, newtonsoftEi.GetProperty(Constants.EncryptedData).ValueKind);
            Assert.AreEqual(JsonValueKind.Null, streamEi.GetProperty(Constants.EncryptedData).ValueKind);

            // _ep sets (order may legitimately differ: Newtonsoft path emits PathsToEncrypt
            // order, Stream path emits document order; both decryptors are path-set based).
            List<string> newtonsoftPaths = newtonsoftEi.GetProperty(Constants.EncryptedPaths).EnumerateArray().Select(e => e.GetString()).ToList();
            List<string> streamPaths = streamEi.GetProperty(Constants.EncryptedPaths).EnumerateArray().Select(e => e.GetString()).ToList();
            CollectionAssert.AreEquivalent(newtonsoftPaths, streamPaths, "_ep sets differ between processors");

            // Value encodings: with the deterministic test cipher (+1 per byte), scalar
            // properties must produce byte-identical ciphertext from both processors
            // (same TypeMarker byte + same serialized plaintext bytes). Object/array
            // plaintext is a JSON re-serialization that may differ in formatting between
            // writers, so only the TypeMarker byte is asserted there.
            Dictionary<string, TypeMarkerExpectation> expectations = new Dictionary<string, TypeMarkerExpectation>
            {
                { "StrAscii", new TypeMarkerExpectation(2, exactCiphertext: true) },
                { "StrUnicode", new TypeMarkerExpectation(2, exactCiphertext: true) },
                { "StrSpecial", new TypeMarkerExpectation(2, exactCiphertext: true) },
                { "StrEmpty", new TypeMarkerExpectation(2, exactCiphertext: true) },
                { "LargeStr", new TypeMarkerExpectation(2, exactCiphertext: true) },
                { "LongZero", new TypeMarkerExpectation(4, exactCiphertext: true) },
                { "LongOne", new TypeMarkerExpectation(4, exactCiphertext: true) },
                { "LongMinusOne", new TypeMarkerExpectation(4, exactCiphertext: true) },
                { "LongMax", new TypeMarkerExpectation(4, exactCiphertext: true) },
                { "LongMin", new TypeMarkerExpectation(4, exactCiphertext: true) },
                { "DblZero", new TypeMarkerExpectation(3, exactCiphertext: true) },
                { "DblNegHalf", new TypeMarkerExpectation(3, exactCiphertext: true) },
                { "DblMax", new TypeMarkerExpectation(3, exactCiphertext: true) },
                { "DblEpsilon", new TypeMarkerExpectation(3, exactCiphertext: true) },
                { "DblTiny", new TypeMarkerExpectation(3, exactCiphertext: true) },
                { "Dbl17Digits", new TypeMarkerExpectation(3, exactCiphertext: true) },
                { "DblNegZero", new TypeMarkerExpectation(3, exactCiphertext: true) },
                { "DblSci", new TypeMarkerExpectation(3, exactCiphertext: true) },
                { "BoolTrue", new TypeMarkerExpectation(5, exactCiphertext: true) },
                { "BoolFalse", new TypeMarkerExpectation(5, exactCiphertext: true) },
                { "NestedObj", new TypeMarkerExpectation(7, exactCiphertext: false) },
                { "ArrPrim", new TypeMarkerExpectation(6, exactCiphertext: false) },
                { "ArrObj", new TypeMarkerExpectation(6, exactCiphertext: false) },
                { "ArrEmpty", new TypeMarkerExpectation(6, exactCiphertext: false) },
                { "ArrNested", new TypeMarkerExpectation(6, exactCiphertext: false) },
            };

            foreach (KeyValuePair<string, TypeMarkerExpectation> kv in expectations)
            {
                byte[] newtonsoftCipher = Convert.FromBase64String(newtonsoftDoc.RootElement.GetProperty(kv.Key).GetString());
                byte[] streamCipher = Convert.FromBase64String(streamDoc.RootElement.GetProperty(kv.Key).GetString());

                Assert.AreEqual(kv.Value.TypeMarker, newtonsoftCipher[0], $"Newtonsoft TypeMarker mismatch for {kv.Key}");
                Assert.AreEqual(kv.Value.TypeMarker, streamCipher[0], $"Stream TypeMarker mismatch for {kv.Key}");

                if (kv.Value.ExactCiphertext)
                {
                    CollectionAssert.AreEqual(newtonsoftCipher, streamCipher, $"ciphertext bytes differ between processors for scalar property {kv.Key}");
                }
            }
        }

        private readonly struct TypeMarkerExpectation
        {
            public TypeMarkerExpectation(byte typeMarker, bool exactCiphertext)
            {
                this.TypeMarker = typeMarker;
                this.ExactCiphertext = exactCiphertext;
            }

            public byte TypeMarker { get; }

            public bool ExactCiphertext { get; }
        }
#endif

        [TestMethod]
        public async Task EnvelopeShape_Preview08Reader_ToleratesEiPropertyReordering_NewtonsoftPayload()
        {
            await this.EnvelopeReorderingCoreAsync(JsonProcessor.Newtonsoft);
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        public async Task EnvelopeShape_Preview08Reader_ToleratesEiPropertyReordering_StreamPayload()
        {
            await this.EnvelopeReorderingCoreAsync(JsonProcessor.Stream);
        }
#endif

        private async Task EnvelopeReorderingCoreAsync(JsonProcessor encryptProcessor)
        {
            string scenario = $"Encrypt:{encryptProcessor}->reordered _ei->preview08";

            // FINDING-1: Stream rows need the null-free variant (see KnownFailure_Finding1_*).
            string original = BuildEdgeCaseJson(includeNullsInsideEncryptedContainers: encryptProcessor == JsonProcessor.Newtonsoft);

            MemoryStream encrypted = await EncryptToMemoryAsync(original, encryptProcessor, EdgeCasePathsToEncrypt);
            string encryptedJson = Encoding.UTF8.GetString(encrypted.ToArray());

            // The baseline (preview08) reader deserializes _ei by property name, so any
            // property ordering must be tolerated. Prove it by feeding both the original
            // ordering and a fully reversed ordering through the replicated reader.
            string reorderedJson = ReverseEiPropertyOrder(encryptedJson);
            Assert.AreNotEqual(encryptedJson, reorderedJson, "reordering must produce a different document text");

            foreach (string payload in new[] { encryptedJson, reorderedJson })
            {
                (JObject decryptedDoc, _) = await Preview08Decryptor.DecryptAsync(
                    new MemoryStream(Encoding.UTF8.GetBytes(payload)),
                    mockEncryptor.Object,
                    CancellationToken.None);

                AssertJsonEquivalent(original, decryptedDoc.ToString(Formatting.None), scenario);
            }
        }

        private static string ReverseEiPropertyOrder(string encryptedJson)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };
            JObject doc = JsonConvert.DeserializeObject<JObject>(encryptedJson, settings);
            JObject ei = (JObject)doc[Constants.EncryptedInfo];
            JObject reversed = new JObject(ei.Properties().Reverse().Select(p => new JProperty(p.Name, p.Value.DeepClone())).ToArray());
            doc[Constants.EncryptedInfo] = reversed;
            return doc.ToString(Formatting.None);
        }

        // ---- Escape-sequence fidelity ---------------------------------------------------------
        //
        // Directly-encrypted string values are unescaped via Utf8JsonReader.CopyString on the
        // Stream path, so escape fidelity through the crypto path itself works on all
        // processors and is verified here. Escape handling of NON-encrypted positions on the
        // Stream path is broken (FINDING-2) and pinned by the KnownFailure_Finding2_* tests.

        private const string EscapeProbeValue = "a\"b\\c\nd\te\rf\bg\fh\u0001i\u001Fj/k";

        private static string BuildEscapeProbeJson()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"id\":\"escape-probe\",");
            sb.Append("\"PlainStr\":\"escape free passthrough\",");
            sb.Append("\"EncStr\":").Append(JsonConvert.ToString(EscapeProbeValue)).Append(',');
            sb.Append("\"EncObj\":{\"inner\":").Append(JsonConvert.ToString(EscapeProbeValue)).Append("},");
            sb.Append("\"EncArr\":[").Append(JsonConvert.ToString(EscapeProbeValue)).Append(']');
            sb.Append('}');
            return sb.ToString();
        }

        private static readonly List<string> EscapeProbePaths = new List<string> { "/EncStr", "/EncObj", "/EncArr" };

        public static IEnumerable<object[]> EscapeProbeMatrix
        {
            get
            {
                // Newtonsoft-encrypt rows only: Stream-encrypt rows fail on escapes nested
                // inside encrypted containers (FINDING-2c, pinned separately).
                yield return new object[] { (int)JsonProcessor.Newtonsoft, (int)JsonProcessor.Newtonsoft };
#if NET8_0_OR_GREATER
                yield return new object[] { (int)JsonProcessor.Newtonsoft, (int)JsonProcessor.Stream };
                yield return new object[] { (int)JsonProcessor.Stream, (int)JsonProcessor.Newtonsoft };
                yield return new object[] { (int)JsonProcessor.Stream, (int)JsonProcessor.Stream };
#endif
            }
        }

        [TestMethod]
        [DynamicData(nameof(EscapeProbeMatrix))]
        public async Task RoundTrip_EscapedStrings_DirectlyEncryptedValues(int encryptProcessorValue, int decryptProcessorValue)
        {
            JsonProcessor encryptProcessor = ResolveJsonProcessor(encryptProcessorValue);
            JsonProcessor decryptProcessor = ResolveJsonProcessor(decryptProcessorValue);
            string scenario = $"escape probe Encrypt:{encryptProcessor}->Decrypt:{decryptProcessor}";

            // Only the directly-encrypted string property for Stream-encrypt rows; container
            // payloads with nested escapes are corrupted by the Stream encryptor (FINDING-2c).
            bool streamEncrypt = encryptProcessor != JsonProcessor.Newtonsoft;
            string original = streamEncrypt
                ? "{\"id\":\"escape-probe\",\"PlainStr\":\"escape free passthrough\",\"EncStr\":" + JsonConvert.ToString(EscapeProbeValue) + "}"
                : BuildEscapeProbeJson();
            List<string> paths = streamEncrypt ? new List<string> { "/EncStr" } : EscapeProbePaths;

            MemoryStream encrypted = await EncryptToMemoryAsync(original, encryptProcessor, paths);

            (string decrypted, DecryptionContext context) = await DecryptToStringAsync(encrypted, decryptProcessor);
            Assert.IsNotNull(context, scenario);
            AssertJsonEquivalent(original, decrypted, scenario);
        }

        [TestMethod]
        public async Task Downgrade_Preview08Decryptor_ReadsEscapeProbe_NewtonsoftPayload()
        {
            string scenario = "escape probe Encrypt:Newtonsoft->Decrypt:preview08";
            string original = BuildEscapeProbeJson();

            MemoryStream encrypted = await EncryptToMemoryAsync(original, JsonProcessor.Newtonsoft, EscapeProbePaths);

            (JObject decryptedDoc, _) = await Preview08Decryptor.DecryptAsync(
                new MemoryStream(encrypted.ToArray()),
                mockEncryptor.Object,
                CancellationToken.None);

            AssertJsonEquivalent(original, decryptedDoc.ToString(Formatting.None), scenario);
        }

#if NET8_0_OR_GREATER
        // ---- KNOWN FAILURES: real product findings, kept failing on purpose --------------------
        //
        // Each test below asserts the CORRECT (baseline-compatible) behavior and currently
        // FAILS against HEAD. Per the compatibility-verification charter these stay in place
        // as executable documentation of the findings; product-code fixes are out of scope.
        // Full root-cause analysis: .worktree\compat-analysis-reports\roundtrip-report.md.

        /// <summary>
        /// FINDING-1 (severity HIGH): StreamProcessor.Encryptor.cs - the JsonTokenType.Null
        /// case unconditionally clears encryptPropertyName, even while buffering an encryption
        /// payload. A JSON null INSIDE an encrypted object/array therefore wipes the pending
        /// path: _ep receives a JSON null entry instead of the container path, and the
        /// container remains undecryptable by every decryptor (HEAD Newtonsoft throws
        /// NullReferenceException on path.Substring, preview08 likewise; HEAD Stream leaves
        /// the property as base64). The Newtonsoft encrypt path handles the same document
        /// correctly.
        /// </summary>
        [TestMethod]
        public async Task KnownFailure_Finding1_NullInsideEncryptedContainer_CorruptsEncryptedPaths()
        {
            string json = "{\"id\":\"f1\",\"EncObj\":{\"a\":1,\"b\":null,\"c\":2},\"EncArr\":[1,null,2]}";

            MemoryStream encrypted = await EncryptToMemoryAsync(json, JsonProcessor.Stream, new List<string> { "/EncObj", "/EncArr" });

            using JsonDocument doc = JsonDocument.Parse(encrypted.ToArray());
            List<string> encryptedPaths = doc.RootElement
                .GetProperty(Constants.EncryptedInfo)
                .GetProperty(Constants.EncryptedPaths)
                .EnumerateArray()
                .Select(e => e.GetString())
                .ToList();

            CollectionAssert.AreEquivalent(
                new List<string> { "/EncObj", "/EncArr" },
                encryptedPaths,
                "FINDING-1: Stream encryptor must record container paths in _ep even when the container holds JSON nulls");
        }

        /// <summary>
        /// FINDING-2a (severity HIGH): StreamProcessor.Encryptor.cs writes non-encrypted
        /// string values with writer.WriteStringValue(reader.ValueSpan). ValueSpan is the RAW
        /// (still escaped) token text, which Utf8JsonWriter re-escapes - double-escaping every
        /// passthrough string that contains escape sequences (quotes, backslashes, control
        /// chars, \uXXXX). The persisted document is corrupted at encryption time.
        /// </summary>
        [TestMethod]
        public async Task KnownFailure_Finding2a_StreamEncrypt_DoubleEscapesPassthroughStrings()
        {
            string json = "{\"id\":\"f2a\",\"Plain\":" + JsonConvert.ToString(EscapeProbeValue) + ",\"Enc\":\"x\"}";

            MemoryStream encrypted = await EncryptToMemoryAsync(json, JsonProcessor.Stream, new List<string> { "/Enc" });

            using JsonDocument doc = JsonDocument.Parse(encrypted.ToArray());
            Assert.AreEqual(
                EscapeProbeValue,
                doc.RootElement.GetProperty("Plain").GetString(),
                "FINDING-2a: passthrough string with escape sequences must survive Stream encryption unchanged");
        }

        /// <summary>
        /// FINDING-2b (severity HIGH): StreamProcessor.Decryptor.cs has the same defect on
        /// the decrypt side - passthrough strings are written from the raw escaped span and
        /// get double-escaped when decrypting with JsonProcessor.Stream, corrupting documents
        /// that were encrypted correctly (here: by the Newtonsoft path).
        /// </summary>
        [TestMethod]
        public async Task KnownFailure_Finding2b_StreamDecrypt_DoubleEscapesPassthroughStrings()
        {
            string json = "{\"id\":\"f2b\",\"Plain\":" + JsonConvert.ToString(EscapeProbeValue) + ",\"Enc\":\"x\"}";

            MemoryStream encrypted = await EncryptToMemoryAsync(json, JsonProcessor.Newtonsoft, new List<string> { "/Enc" });
            (string decrypted, DecryptionContext context) = await DecryptToStringAsync(encrypted, JsonProcessor.Stream);
            Assert.IsNotNull(context);

            using JsonDocument doc = JsonDocument.Parse(decrypted);
            Assert.AreEqual(
                EscapeProbeValue,
                doc.RootElement.GetProperty("Plain").GetString(),
                "FINDING-2b: passthrough string with escape sequences must survive Stream decryption unchanged");
        }

        /// <summary>
        /// FINDING-2c (severity HIGH): the same raw-span defect applies while the Stream
        /// encryptor buffers an encrypted object/array payload: nested strings containing
        /// escape sequences are double-escaped INSIDE the ciphertext, so every decryptor
        /// (including preview08) reproduces the corrupted value.
        /// </summary>
        [TestMethod]
        public async Task KnownFailure_Finding2c_StreamEncrypt_DoubleEscapesStringsInsideEncryptedContainers()
        {
            string json = "{\"id\":\"f2c\",\"EncObj\":{\"inner\":" + JsonConvert.ToString(EscapeProbeValue) + "}}";

            MemoryStream encrypted = await EncryptToMemoryAsync(json, JsonProcessor.Stream, new List<string> { "/EncObj" });
            (string decrypted, DecryptionContext context) = await DecryptToStringAsync(encrypted, JsonProcessor.Newtonsoft);
            Assert.IsNotNull(context);

            using JsonDocument doc = JsonDocument.Parse(decrypted);
            Assert.AreEqual(
                EscapeProbeValue,
                doc.RootElement.GetProperty("EncObj").GetProperty("inner").GetString(),
                "FINDING-2c: nested string with escape sequences inside an encrypted container must round-trip unchanged");
        }

        /// <summary>
        /// FINDING-3 (severity MEDIUM): StreamProcessor.Encryptor.cs matches property names
        /// against the paths-to-encrypt table at EVERY depth. While buffering an encrypted
        /// container payload, a NESTED property whose name matches another path-to-encrypt
        /// overwrites encryptPropertyName, so _ep records the wrong path for the container
        /// (duplicate of the nested name, container path missing).
        /// </summary>
        [TestMethod]
        public async Task KnownFailure_Finding3_NestedNameCollision_RecordsWrongPathInEp()
        {
            string json = "{\"id\":\"f3\",\"A\":{\"B\":1},\"B\":2}";

            MemoryStream encrypted = await EncryptToMemoryAsync(json, JsonProcessor.Stream, new List<string> { "/A", "/B" });

            using JsonDocument doc = JsonDocument.Parse(encrypted.ToArray());
            List<string> encryptedPaths = doc.RootElement
                .GetProperty(Constants.EncryptedInfo)
                .GetProperty(Constants.EncryptedPaths)
                .EnumerateArray()
                .Select(e => e.GetString())
                .ToList();

            CollectionAssert.AreEquivalent(
                new List<string> { "/A", "/B" },
                encryptedPaths,
                "FINDING-3: _ep must record each top-level encrypted path exactly once");
        }

        /// <summary>
        /// FINDING-4 (severity MEDIUM): StreamProcessor.Decryptor.cs matches encrypted-path
        /// names at EVERY depth, so a nested plaintext property whose name matches an
        /// encrypted top-level path is treated as ciphertext. With non-base64 content the
        /// decryptor throws; with coincidentally valid base64 it would silently corrupt data.
        /// The Newtonsoft decryptor (HEAD and preview08) only touches top-level properties
        /// and handles this document correctly.
        /// </summary>
        [TestMethod]
        public async Task KnownFailure_Finding4_StreamDecrypt_NestedNameCollision_TreatsPlaintextAsCiphertext()
        {
            string json = "{\"id\":\"f4\",\"Sec\":\"top secret\",\"Obj\":{\"Sec\":\"plain nested!\"}}";

            MemoryStream encrypted = await EncryptToMemoryAsync(json, JsonProcessor.Newtonsoft, new List<string> { "/Sec" });
            (string decrypted, DecryptionContext context) = await DecryptToStringAsync(encrypted, JsonProcessor.Stream);
            Assert.IsNotNull(context);

            using JsonDocument doc = JsonDocument.Parse(decrypted);
            Assert.AreEqual("top secret", doc.RootElement.GetProperty("Sec").GetString());
            Assert.AreEqual(
                "plain nested!",
                doc.RootElement.GetProperty("Obj").GetProperty("Sec").GetString(),
                "FINDING-4: nested plaintext property sharing an encrypted path name must be passed through untouched");
        }
#endif

#if NET8_0_OR_GREATER
        // ---- Characterization: numbers beyond long range -------------------------------------

        /// <summary>
        /// Characterization test (documented divergence, see roundtrip-report.md): a JSON
        /// integer literal that overflows Int64 is REJECTED by the Newtonsoft encrypt path
        /// (JToken.ToObject&lt;long&gt; overflows) but silently encrypted as a double (with
        /// precision loss) by the Stream encrypt path. This test pins the current behavior
        /// of both paths so any silent change is caught.
        /// </summary>
        [TestMethod]
        public async Task Characterization_IntegerBeyondLongRange_DivergesBetweenProcessors()
        {
            string json = "{\"id\":\"big-int\",\"Big\":9223372036854775808}";
            List<string> paths = new List<string> { "/Big" };

            // Newtonsoft path: rejects the document.
            await Assert.ThrowsExceptionAsync<OverflowException>(async () =>
            {
                using MemoryStream ms = await EncryptToMemoryAsync(json, JsonProcessor.Newtonsoft, paths);
            });

            // Stream path: silently coerces to double (TypeMarker.Double) and round-trips
            // the coerced value, not the original integer text.
            MemoryStream encrypted = await EncryptToMemoryAsync(json, JsonProcessor.Stream, paths);
            (string decrypted, DecryptionContext context) = await DecryptToStringAsync(encrypted, JsonProcessor.Stream);
            Assert.IsNotNull(context);

            using JsonDocument doc = JsonDocument.Parse(decrypted);
            double roundTripped = doc.RootElement.GetProperty("Big").GetDouble();
            Assert.AreEqual(9223372036854775808d, roundTripped, "Stream path must preserve the coerced double value");
        }
#endif

        // ---- Replicated preview08 (baseline) decryptor ----------------------------------------

        /// <summary>
        /// Verbatim replica of the MDE decrypt path of the released preview08
        /// EncryptionProcessor, from:
        /// .worktree\compat-baseline\Microsoft.Azure.Cosmos.Encryption.Custom\src\EncryptionProcessor.cs
        /// (commit d8d58047e).
        ///
        /// Adaptations relative to the baseline source (all faithfulness-preserving):
        ///  1. The baseline compiles against Microsoft.Data.Encryption.Cryptography 1.2.0;
        ///     this test assembly resolves 2.0.0-pre015 (the version HEAD ships with). The
        ///     same SqlSerializerFactory/SqlVarCharSerializer APIs are used; the SQL binary
        ///     formats (bit/float/bigint/varchar) are identical across these versions - HEAD
        ///     itself depends on that to decrypt baseline payloads.
        ///  2. The baseline deserializes `_ei` into its internal EncryptionProperties class;
        ///     here a private POCO with the same [JsonProperty] wire names (_ef/_en/_ea/_ed/_ep)
        ///     is used so the replica does not depend on HEAD's type.
        ///  3. The baseline private TypeMarker enum is copied verbatim.
        ///  4. The surrounding stream/dispose plumbing (DecryptAsync entry point) is reduced
        ///     to the document-level decrypt; the JSON reader settings (DateParseHandling.None,
        ///     MaxDepth 64) match the baseline RetrieveItem exactly.
        /// </summary>
        private static class Preview08Decryptor
        {
            private static readonly SqlSerializerFactory SqlSerializerFactory = new SqlSerializerFactory();

            // UTF-8 encoding.
            private static readonly SqlVarCharSerializer SqlVarCharSerializer = new SqlVarCharSerializer(size: -1, codePageCharacterEncoding: 65001);

            private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings()
            {
                DateParseHandling = DateParseHandling.None,
            };

            private enum TypeMarker : byte
            {
                Null = 1, // not used
                String = 2,
                Double = 3,
                Long = 4,
                Boolean = 5,
                Array = 6,
                Object = 7,
            }

            private sealed class Preview08EncryptionProperties
            {
                [JsonProperty(PropertyName = "_ef")]
                public int EncryptionFormatVersion { get; set; }

                [JsonProperty(PropertyName = "_en")]
                public string DataEncryptionKeyId { get; set; }

                [JsonProperty(PropertyName = "_ea")]
                public string EncryptionAlgorithm { get; set; }

                [JsonProperty(PropertyName = "_ed")]
                public byte[] EncryptedData { get; set; }

                [JsonProperty(PropertyName = "_ep")]
                public List<string> EncryptedPaths { get; set; }
            }

            internal static async Task<(JObject document, List<string> pathsDecrypted)> DecryptAsync(
                Stream input,
                Encryptor encryptor,
                CancellationToken cancellationToken)
            {
                JObject itemJObj = RetrieveItem(input);
                JObject encryptionPropertiesJObj = RetrieveEncryptionProperties(itemJObj);

                Assert.IsNotNull(encryptionPropertiesJObj, "preview08 decryptor: _ei must be present in the payload");

                Preview08EncryptionProperties encryptionProperties = encryptionPropertiesJObj.ToObject<Preview08EncryptionProperties>();
                Assert.AreEqual(
                    CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                    encryptionProperties.EncryptionAlgorithm,
                    "preview08 decryptor replica only covers the MDE algorithm");

                List<string> pathsDecrypted = await MdeEncAlgoDecryptObjectAsync(itemJObj, encryptor, encryptionProperties, cancellationToken);
                return (itemJObj, pathsDecrypted);
            }

            private static async Task<List<string>> MdeEncAlgoDecryptObjectAsync(
                JObject document,
                Encryptor encryptor,
                Preview08EncryptionProperties encryptionProperties,
                CancellationToken cancellationToken)
            {
                JObject plainTextJObj = new JObject();
                foreach (string path in encryptionProperties.EncryptedPaths)
                {
                    string propertyName = path.Substring(1);
                    if (!document.TryGetValue(propertyName, out JToken propertyValue))
                    {
                        continue;
                    }

                    byte[] cipherTextWithTypeMarker = propertyValue.ToObject<byte[]>();

                    if (cipherTextWithTypeMarker == null)
                    {
                        continue;
                    }

                    byte[] cipherText = new byte[cipherTextWithTypeMarker.Length - 1];
                    Buffer.BlockCopy(cipherTextWithTypeMarker, 1, cipherText, 0, cipherTextWithTypeMarker.Length - 1);

                    byte[] plainText = await MdeEncAlgoDecryptPropertyAsync(
                        encryptionProperties,
                        cipherText,
                        encryptor,
                        cancellationToken);

                    DeserializeAndAddProperty(
                        (TypeMarker)cipherTextWithTypeMarker[0],
                        plainText,
                        plainTextJObj,
                        propertyName);
                }

                List<string> pathsDecrypted = new List<string>();
                foreach (JProperty property in plainTextJObj.Properties())
                {
                    document[property.Name] = property.Value;
                    pathsDecrypted.Add("/" + property.Name);
                }

                document.Remove(Constants.EncryptedInfo);
                return pathsDecrypted;
            }

            private static async Task<byte[]> MdeEncAlgoDecryptPropertyAsync(
                Preview08EncryptionProperties encryptionProperties,
                byte[] cipherText,
                Encryptor encryptor,
                CancellationToken cancellationToken)
            {
                if (encryptionProperties.EncryptionFormatVersion != 3)
                {
                    throw new NotSupportedException($"Unknown encryption format version: {encryptionProperties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
                }

                byte[] plainText = await encryptor.DecryptAsync(
                    cipherText,
                    encryptionProperties.DataEncryptionKeyId,
                    encryptionProperties.EncryptionAlgorithm,
                    cancellationToken);

                if (plainText == null)
                {
                    throw new InvalidOperationException($"{nameof(Encryptor)} returned null plainText from {nameof(DecryptAsync)}.");
                }

                return plainText;
            }

            private static void DeserializeAndAddProperty(
                TypeMarker typeMarker,
                byte[] serializedBytes,
                JObject jObject,
                string key)
            {
                switch (typeMarker)
                {
                    case TypeMarker.Boolean:
                        jObject.Add(key, SqlSerializerFactory.GetDefaultSerializer<bool>().Deserialize(serializedBytes));
                        break;
                    case TypeMarker.Double:
                        jObject.Add(key, SqlSerializerFactory.GetDefaultSerializer<double>().Deserialize(serializedBytes));
                        break;
                    case TypeMarker.Long:
                        jObject.Add(key, SqlSerializerFactory.GetDefaultSerializer<long>().Deserialize(serializedBytes));
                        break;
                    case TypeMarker.String:
                        jObject.Add(key, SqlVarCharSerializer.Deserialize(serializedBytes));
                        break;
                    case TypeMarker.Array:
                        jObject.Add(key, JsonConvert.DeserializeObject<JArray>(SqlVarCharSerializer.Deserialize(serializedBytes), JsonSerializerSettings));
                        break;
                    case TypeMarker.Object:
                        jObject.Add(key, JsonConvert.DeserializeObject<JObject>(SqlVarCharSerializer.Deserialize(serializedBytes), JsonSerializerSettings));
                        break;
                    default:
                        Assert.Fail(string.Format("Unexpected type marker {0}", typeMarker));
                        break;
                }
            }

            private static JObject RetrieveItem(
                Stream input)
            {
                JObject itemJObj;
                using (StreamReader sr = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
                using (JsonTextReader jsonTextReader = new JsonTextReader(sr))
                {
                    JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings()
                    {
                        DateParseHandling = DateParseHandling.None,
                        MaxDepth = 64, // https://github.com/advisories/GHSA-5crp-9r3c-p9vr
                    };

                    itemJObj = Newtonsoft.Json.JsonSerializer.Create(jsonSerializerSettings).Deserialize<JObject>(jsonTextReader);
                }

                return itemJObj;
            }

            private static JObject RetrieveEncryptionProperties(
                JObject item)
            {
                JProperty encryptionPropertiesJProp = item.Property(Constants.EncryptedInfo);
                JObject encryptionPropertiesJObj = null;
                if (encryptionPropertiesJProp?.Value != null && encryptionPropertiesJProp.Value.Type == JTokenType.Object)
                {
                    encryptionPropertiesJObj = (JObject)encryptionPropertiesJProp.Value;
                }

                return encryptionPropertiesJObj;
            }
        }
    }
}
