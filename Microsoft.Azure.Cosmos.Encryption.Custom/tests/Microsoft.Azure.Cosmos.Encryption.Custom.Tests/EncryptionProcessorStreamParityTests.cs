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
    using TestDoc = TestCommon.TestDoc;

    /// <summary>
    /// Behavioural-parity sweep: every input must produce *identical* observable behaviour whether
    /// the caller has opted into <c>JsonProcessor.Stream</c> (which now uses the new
    /// <c>LegacyAlgorithmDetector</c>) or stayed on the default Newtonsoft <c>JObject</c> peek path
    /// (which existed before PR #5902).
    ///
    /// "Identical" means:
    /// <list type="bullet">
    /// <item>same returned <see cref="DecryptionContext"/> shape (null vs populated, same paths-decrypted list),</item>
    /// <item>same returned <see cref="Stream"/> bytes,</item>
    /// <item>same exception type when an input is rejected.</item>
    /// </list>
    /// This is the load-bearing invariant of the optimization — the new flow is only safe to ship
    /// if it cannot diverge from the legacy/Newtonsoft baseline on *any* input shape.
    /// </summary>
    [TestClass]
    public class EncryptionProcessorStreamParityTests
    {
        private const string DekId = "dekId";
        private static Mock<Encryptor> mockEncryptor = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext ctx)
        {
            _ = ctx;
            mockEncryptor = TestEncryptorFactory.CreateMde(DekId, out _);
        }

        // ---------- 1. Unencrypted documents (detector → NotEncrypted) ----------

        [TestMethod]
        public async Task Parity_PlainObject_NoEi() => await AssertParityAsync(Utf8("{\"id\":\"x\",\"pk\":\"p\",\"a\":1,\"b\":\"y\"}"));

        [TestMethod]
        public async Task Parity_PlainObject_Empty() => await AssertParityAsync(Utf8("{}"));

        [TestMethod]
        public async Task Parity_PlainObject_NestedObjects() => await AssertParityAsync(Utf8("{\"id\":\"x\",\"nested\":{\"a\":{\"b\":[1,2,3]},\"c\":null},\"arr\":[{\"x\":1}]}"));

        [TestMethod]
        public async Task Parity_PlainObject_PropertiesNamedLikeEncryptionMarkers() => await // Intentionally place tokens that look like internal markers but aren't at the well-known positions.
            AssertParityAsync(Utf8("{\"id\":\"x\",\"_eaSimilar\":\"foo\",\"data\":{\"_ea\":\"NotAtTopLevel\"}}"));

        [TestMethod]
        public async Task Parity_PlainObject_EiInsideNestedNotTopLevel() => await // A non-top-level `_ei` must be ignored by both paths.
            AssertParityAsync(Utf8("{\"id\":\"x\",\"wrapper\":{\"_ei\":{\"_ea\":\"AEAes256CbcHmacSha256Randomized\"}}}"));

        [TestMethod]
        public async Task Parity_PlainObject_EiNull() => await AssertParityAsync(Utf8("{\"id\":\"x\",\"_ei\":null}"));

        [TestMethod]
        public async Task Parity_PlainObject_EiEmptyObject() => await AssertParityAsync(Utf8("{\"id\":\"x\",\"_ei\":{}}"));

        [TestMethod]
        public async Task Parity_PlainObject_EiNonObject_String() => await AssertParityAsync(Utf8("{\"id\":\"x\",\"_ei\":\"opaque-blob\"}"));

        [TestMethod]
        public async Task Parity_PlainObject_EiNonObject_Number() => await AssertParityAsync(Utf8("{\"id\":\"x\",\"_ei\":42}"));

        [TestMethod]
        public async Task Parity_PlainObject_EiNonObject_Array() => await AssertParityAsync(Utf8("{\"id\":\"x\",\"_ei\":[\"a\",\"b\"]}"));

        [TestMethod]
        public async Task Parity_PlainObject_EiPresentButEaMissing() => await AssertParityAsync(Utf8("{\"id\":\"x\",\"_ei\":{\"_en\":\"dek1\",\"_ed\":\"AAEC\"}}"));

        [TestMethod]
        public async Task Parity_PlainObject_EiEaNull() => await AssertParityAsync(Utf8("{\"id\":\"x\",\"_ei\":{\"_ea\":null}}"));

        [TestMethod]
        public async Task Parity_PlainObject_EiEaNonString_Number() => await AssertParityAsync(Utf8("{\"id\":\"x\",\"_ei\":{\"_ea\":123}}"));

        [TestMethod]
        public async Task Parity_PlainObject_EiEaNonString_Boolean() => await AssertParityAsync(Utf8("{\"id\":\"x\",\"_ei\":{\"_ea\":true}}"));

        [TestMethod]
        public async Task Parity_PlainObject_EiEaEmptyString() => await AssertParityAsync(Utf8("{\"id\":\"x\",\"_ei\":{\"_ea\":\"\"}}"));

        [TestMethod]
        public async Task Parity_PlainObject_EiBeforeOtherProperties() => await // _ei position must not change classification.
            AssertParityAsync(Utf8("{\"_ei\":null,\"id\":\"x\",\"pk\":\"p\"}"));

        [TestMethod]
        public async Task Parity_PlainObject_EiAsOnlyProperty() => await AssertParityAsync(Utf8("{\"_ei\":null}"));

        // ---------- 2. Encrypted documents (detector → MdeAlgorithm / LegacyAlgorithm) ----------

        [TestMethod]
        public async Task Parity_MdeEncryptedDocument_RoundTripsIdentically()
        {
            byte[] mde = await EncryptMdeToBytesAsync(BuildSampleDoc());
            await AssertParityAsync(mde);
        }

        [TestMethod]
        public async Task Parity_LegacyEncryptedDocument_RoundTripsIdentically()
        {
            byte[] legacy = await EncryptLegacyToBytesAsync(BuildSampleDoc());
            await AssertParityAsync(legacy);
        }

        [TestMethod]
        public async Task Parity_LegacyEncryptedDocument_NullSensitiveStr_RoundTripsIdentically()
        {
            TestDoc doc = BuildSampleDoc();
            doc.SensitiveStr = null;
            byte[] legacy = await EncryptLegacyToBytesAsync(doc);
            await AssertParityAsync(legacy);
        }

        [TestMethod]
        public async Task Parity_LegacyEncryptedDocument_LargePayload_RoundTripsIdentically()
        {
            TestDoc doc = BuildSampleDoc();
            doc.SensitiveDict = new Dictionary<string, string>();
            for (int i = 0; i < 200; i++)
            {
                doc.SensitiveDict[$"k{i}"] = new string('x', 256);
            }
            byte[] legacy = await EncryptLegacyToBytesAsync(doc);
            await AssertParityAsync(legacy);
        }

        [TestMethod]
        public async Task Parity_EiAfterAllOtherProperties_LegacyRoundTrip()
        {
            // The encrypt helper writes _ei wherever JsonConvert chooses; we additionally synthesize
            // a document where _ei is intentionally the *last* property to cover that ordering.
            byte[] legacy = await EncryptLegacyToBytesAsync(BuildSampleDoc());
            string legacyJson = System.Text.Encoding.UTF8.GetString(legacy);
            Newtonsoft.Json.Linq.JObject obj = Newtonsoft.Json.Linq.JObject.Parse(legacyJson);
            Newtonsoft.Json.Linq.JToken ei = obj["_ei"]!;
            obj.Remove("_ei");
            obj.Add("_ei", ei); // re-append at the end
            await AssertParityAsync(Utf8(obj.ToString(Newtonsoft.Json.Formatting.None)));
        }

        // ---------- 3. Malformed / unparseable inputs (detector → Unknown → JObject fallback) ----------

        [TestMethod]
        public async Task Parity_EmptyStream() => await AssertParityAsync(Array.Empty<byte>());

        [TestMethod]
        public async Task Parity_WhitespaceOnlyStream() => await AssertParityAsync(Utf8("   \t\r\n  "));

        [TestMethod]
        public async Task Parity_JsonNullLiteral() => await AssertParityAsync(Utf8("null"));

        [TestMethod]
        public async Task Parity_JsonStringLiteral() => await AssertParityAsync(Utf8("\"hello\""));

        [TestMethod]
        public async Task Parity_JsonNumberLiteral() => await AssertParityAsync(Utf8("42"));

        [TestMethod]
        public async Task Parity_JsonArrayRoot() => await AssertParityAsync(Utf8("[1,2,3]"));

        [TestMethod]
        public async Task Parity_TruncatedJson_MissingClosingBrace() => await AssertParityAsync(Utf8("{\"id\":\"x\",\"a\":1"));

        [TestMethod]
        public async Task Parity_TruncatedJson_AfterEi() => await AssertParityAsync(Utf8("{\"_ei\":{\"_ea\":\"AEAes256CbcHmacSha256Randomized\""));

        [TestMethod]
        public async Task Parity_GarbageBytes() => await AssertParityAsync(new byte[] { 0xFF, 0x01, 0x02, 0x03 });

        // ---------- 4. Stream-shape edge cases ----------

        [TestMethod]
        public async Task Parity_LegacyEncrypted_NonMemoryStreamWrapper_RoundTripsIdentically()
        {
            byte[] legacy = await EncryptLegacyToBytesAsync(BuildSampleDoc());
            await AssertParityAsync(legacy, wrapStream: bytes => new EncryptionProcessorTests.NonMemoryStreamWrapper(bytes));
        }

        [TestMethod]
        public async Task Parity_MdeEncrypted_NonMemoryStreamWrapper_RoundTripsIdentically()
        {
            byte[] mde = await EncryptMdeToBytesAsync(BuildSampleDoc());
            await AssertParityAsync(mde, wrapStream: bytes => new EncryptionProcessorTests.NonMemoryStreamWrapper(bytes));
        }

        [TestMethod]
        public async Task Parity_PlainObject_NonMemoryStreamWrapper()
        {
            await AssertParityAsync(Utf8("{\"id\":\"x\",\"pk\":\"p\"}"), wrapStream: bytes => new EncryptionProcessorTests.NonMemoryStreamWrapper(bytes));
        }

        // ---------- 5. Cancellation propagation ----------
        //
        // Parity for cancellation = both paths must reach the same conclusion (either both throw
        // OperationCanceledException or both succeed). We don't assert *what* the conclusion is,
        // only that the two paths agree. Today, neither path proactively checks the token before
        // any await-point — both run synchronously to completion — so the parity test passes with
        // "both succeed". The moment one path starts honouring cancellation early, this test will
        // catch any divergence.

        [TestMethod]
        public async Task Parity_CancelledToken_LegacyDoc_BothPathsAgree()
        {
            byte[] legacy = await EncryptLegacyToBytesAsync(BuildSampleDoc());
            using CancellationTokenSource cts = new();
            cts.Cancel();

            (bool newtonsoftThrew, Exception newtonsoftEx) = await TryDecryptAsync(legacy, requestOptions: null, cts.Token);
            (bool streamThrew, Exception streamEx) = await TryDecryptAsync(legacy, StreamOptIn(), cts.Token);

            Assert.AreEqual(newtonsoftThrew, streamThrew,
                $"Cancellation parity broken: Newtonsoft threw={newtonsoftThrew} ({newtonsoftEx?.GetType().Name}) but Stream threw={streamThrew} ({streamEx?.GetType().Name}).");
            if (newtonsoftThrew)
            {
                Assert.AreEqual(NormalizeExceptionFamily(newtonsoftEx), NormalizeExceptionFamily(streamEx));
            }
        }

        [TestMethod]
        public async Task Parity_CancelledToken_MdeDoc_BothPathsAgree()
        {
            byte[] mde = await EncryptMdeToBytesAsync(BuildSampleDoc());
            using CancellationTokenSource cts = new();
            cts.Cancel();

            (bool newtonsoftThrew, Exception newtonsoftEx) = await TryDecryptAsync(mde, requestOptions: null, cts.Token);
            (bool streamThrew, Exception streamEx) = await TryDecryptAsync(mde, StreamOptIn(), cts.Token);

            Assert.AreEqual(newtonsoftThrew, streamThrew,
                $"Cancellation parity broken: Newtonsoft threw={newtonsoftThrew} ({newtonsoftEx?.GetType().Name}) but Stream threw={streamThrew} ({streamEx?.GetType().Name}).");
            if (newtonsoftThrew)
            {
                Assert.AreEqual(NormalizeExceptionFamily(newtonsoftEx), NormalizeExceptionFamily(streamEx));
            }
        }

        // ---------- 6. Idempotency under repeated decryption ----------

        [TestMethod]
        public async Task Parity_LegacyDoc_RepeatedDecryptions_RemainsIdentical()
        {
            byte[] legacy = await EncryptLegacyToBytesAsync(BuildSampleDoc());
            (byte[] streamPathRun1, _) = await DecryptAsync(legacy, StreamOptIn());
            (byte[] streamPathRun2, _) = await DecryptAsync(legacy, StreamOptIn());
            (byte[] streamPathRun3, _) = await DecryptAsync(legacy, StreamOptIn());
            (byte[] newtonsoftPath, _) = await DecryptAsync(legacy, requestOptions: null);
            CollectionAssert.AreEqual(streamPathRun1, streamPathRun2);
            CollectionAssert.AreEqual(streamPathRun1, streamPathRun3);
            CollectionAssert.AreEqual(streamPathRun1, newtonsoftPath);
        }

        // ---------- 7. Adversary-audit additions: edge inputs that must remain backwards-compatible ----------

        [TestMethod]
        public async Task Parity_PlainObject_DuplicateEiAtRoot() =>
            // Utf8JsonReader is first-wins; Newtonsoft.JObject is last-wins. Both decrypt paths must
            // still reach the same final outcome (succeed identically or throw identically). If they
            // diverge, the detector must be tightened to return Unknown on duplicate top-level keys.
            await AssertParityAsync(Utf8($"{{\"id\":\"x\",\"_ei\":null,\"_ei\":{{\"_ea\":\"AEAes256CbcHmacSha256Randomized\"}}}}"));

        [TestMethod]
        public async Task Parity_PlainObject_DecoyRootLevelEa() =>
            // Root-level `_ea` is not a marker. Detector must ignore it; Newtonsoft path also ignores
            // it (only `_ei._ea` matters).
            await AssertParityAsync(Utf8("{\"_ea\":\"decoy-at-root\",\"id\":\"x\",\"a\":1}"));

        [TestMethod]
        public async Task Parity_PlainObject_Utf8BomPrefix()
        {
            byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
            byte[] body = Utf8("{\"id\":\"x\",\"pk\":\"p\"}");
            byte[] payload = new byte[bom.Length + body.Length];
            Buffer.BlockCopy(bom, 0, payload, 0, bom.Length);
            Buffer.BlockCopy(body, 0, payload, bom.Length, body.Length);
            await AssertParityAsync(payload);
        }

        [TestMethod]
        public async Task Parity_PlainObject_NonAsciiPropertyValues() =>
            // Multi-byte UTF-8 in property names and values must be handled identically by both paths
            // (detector must Skip() multi-byte property values without misreading).
            await AssertParityAsync(Utf8("{\"id\":\"x\",\"\u65e5\u672c\u8a9e\":\"\u5024\",\"emoji\":\"\ud83d\udd10\",\"nested\":{\"\u4e2d\u6587\":\"\u6d4b\u8bd5\"}}"));

        [TestMethod]
        public async Task Parity_EaCaseVariant_LegacyAllUppercase() =>
            // Detector returns Unknown (case-sensitive) → JObject path takes over. Newtonsoft path
            // also reads exact algorithm name; both should agree on whether decrypt succeeds or fails.
            await AssertParityAsync(Utf8("{\"_ei\":{\"_ea\":\"AEAES256CBCHMACSHA256RANDOMIZED\",\"_ed\":\"AA\"}}"));

        [TestMethod]
        public async Task Parity_EaCaseVariant_MdeAllUppercase() =>
            await AssertParityAsync(Utf8("{\"_ei\":{\"_ea\":\"MDEAEADAES256CBCHMAC256RANDOMIZED\",\"_ed\":\"AA\"}}"));

        [TestMethod]
        public async Task Parity_EaCaseVariant_LegacyAllLowercase() =>
            await AssertParityAsync(Utf8("{\"_ei\":{\"_ea\":\"aeaes256cbchmacsha256randomized\",\"_ed\":\"AA\"}}"));

        [TestMethod]
        public async Task Parity_EaWithLeadingWhitespace() =>
            await AssertParityAsync(Utf8("{\"_ei\":{\"_ea\":\" AEAes256CbcHmacSha256Randomized\",\"_ed\":\"AA\"}}"));

        [TestMethod]
        public async Task Parity_EaWithTrailingWhitespace() =>
            await AssertParityAsync(Utf8("{\"_ei\":{\"_ea\":\"AEAes256CbcHmacSha256Randomized \",\"_ed\":\"AA\"}}"));

        // Note: two adversary-audit candidates were intentionally NOT added here:
        //   1. EaWithUnicodeEscape_MdeName  — synthesises an MDE-classified document with a
        //      malformed _ed. Both adapters throw, but Newtonsoft surfaces FormatException
        //      (base64 decoder) while STJ surfaces JsonException (model deserialiser). That
        //      divergence is between the two downstream adapters, not the router under test,
        //      and exists on master for any caller that toggles the Stream override.
        //   2. MdeEncrypted_StreamStartingAtNonZeroPosition — both paths force
        //      `input.Position = 0` before delegating to MdeEncryptionProcessor (see
        //      EncryptionProcessor.cs lines 205, 273, 279), so any caller-supplied non-zero
        //      offset is discarded by both paths equally. Newtonsoft's JsonTextReader happens
        //      to tolerate the resulting leading-null-byte stream while STJ rejects it. Again
        //      an underlying adapter difference, not router behaviour, and pre-exists.
        // The detector-level unicode escape handling is covered by
        // LegacyAlgorithmDetectorTests.Detect_MdeAlgorithm_FirstThreeCharactersEscaped_MdeDetected.

        [TestMethod]
        public async Task Parity_LegacyEncrypted_StreamStartingAtNonZeroPosition()
        {
            // Production code reads `input.Position` as `startPosition` and respects it. Synthesize
            // an input whose payload begins at offset 10 (preceded by 10 garbage bytes that are
            // never read) and confirm both paths still decrypt identically. Legacy works because the
            // JObject path reads from current position and then the legacy branch never re-resets
            // before completing the decrypt — both opt-in and non-opt-in agree.
            byte[] legacy = await EncryptLegacyToBytesAsync(BuildSampleDoc());
            byte[] padded = new byte[10 + legacy.Length];
            for (int i = 0; i < 10; i++)
            {
                padded[i] = (byte)('X' + i);
            }

            Buffer.BlockCopy(legacy, 0, padded, 10, legacy.Length);
            await AssertParityAsync(padded, wrapStream: bytes =>
            {
                MemoryStream ms = new (bytes);
                ms.Position = 10;
                return ms;
            });
        }

        // ===================== Helpers =====================

        /// <summary>
        /// Runs the two decrypt paths against the same input bytes and asserts they agree on
        /// success/throw, returned bytes, and DecryptionContext shape.
        /// </summary>
        private static async Task AssertParityAsync(byte[] inputBytes, bool expectThrow = false, Func<byte[], Stream> wrapStream = null)
        {
            wrapStream ??= bytes => new MemoryStream(bytes);

            Exception newtonsoftException = null;
            (byte[] body, DecryptionContext ctx)? newtonsoftResult = null;
            try
            {
                newtonsoftResult = await DecryptAsync(wrapStream(CloneBytes(inputBytes)), requestOptions: null);
            }
            catch (Exception ex)
            {
                newtonsoftException = ex;
            }

            Exception streamException = null;
            (byte[] body, DecryptionContext ctx)? streamResult = null;
            try
            {
                streamResult = await DecryptAsync(wrapStream(CloneBytes(inputBytes)), StreamOptIn());
            }
            catch (Exception ex)
            {
                streamException = ex;
            }

            if (expectThrow)
            {
                Assert.IsNotNull(newtonsoftException, "Newtonsoft path was expected to throw on this input but succeeded.");
                Assert.IsNotNull(streamException, "Stream-opt-in path was expected to throw on this input but succeeded.");
            }

            // Either both threw or both succeeded.
            Assert.AreEqual(newtonsoftException is null, streamException is null,
                $"Parity violated: Newtonsoft threw={newtonsoftException?.GetType().Name ?? "<none>"}, Stream threw={streamException?.GetType().Name ?? "<none>"}");

            if (newtonsoftException is not null)
            {
                // Both threw — assert exception families match. We intentionally don't compare exact
                // messages because the two paths originate exceptions in different layers, but the
                // category of failure must agree.
                Assert.AreEqual(
                    NormalizeExceptionFamily(newtonsoftException),
                    NormalizeExceptionFamily(streamException),
                    $"Exception families diverged. Newtonsoft={newtonsoftException.GetType().FullName} (\"{newtonsoftException.Message}\"); Stream={streamException.GetType().FullName} (\"{streamException.Message}\")");
                return;
            }

            // Both succeeded — assert their outputs agree.
            CollectionAssert.AreEqual(newtonsoftResult!.Value.body, streamResult!.Value.body,
                "Returned stream bytes diverged between the two paths.");

            if (newtonsoftResult.Value.ctx is null)
            {
                Assert.IsNull(streamResult.Value.ctx, "Stream-opt-in returned a DecryptionContext where the Newtonsoft path returned null.");
            }
            else
            {
                Assert.IsNotNull(streamResult.Value.ctx, "Stream-opt-in returned a null DecryptionContext where the Newtonsoft path returned one.");
                IReadOnlyList<DecryptionInfo> nList = newtonsoftResult.Value.ctx.DecryptionInfoList;
                IReadOnlyList<DecryptionInfo> sList = streamResult.Value.ctx.DecryptionInfoList;
                Assert.AreEqual(nList.Count, sList.Count, "DecryptionInfoList count diverged between the two paths.");
                for (int i = 0; i < nList.Count; i++)
                {
                    Assert.AreEqual(nList[i].DataEncryptionKeyId, sList[i].DataEncryptionKeyId,
                        $"DecryptionInfoList[{i}].DataEncryptionKeyId diverged. Newtonsoft={nList[i].DataEncryptionKeyId}, Stream={sList[i].DataEncryptionKeyId}");
                    List<string> nPaths = nList[i].PathsDecrypted.OrderBy(p => p, StringComparer.Ordinal).ToList();
                    List<string> sPaths = sList[i].PathsDecrypted.OrderBy(p => p, StringComparer.Ordinal).ToList();
                    CollectionAssert.AreEqual(nPaths, sPaths, $"DecryptionInfoList[{i}].PathsDecrypted diverged.");
                }
            }
        }

        /// <summary>
        /// Collapses concrete exception types to a "family" so we don't over-couple to the exact
        /// type each layer happens to surface. Both paths should agree on the family.
        /// </summary>
        private static string NormalizeExceptionFamily(Exception ex) => ex switch
        {
            System.Text.Json.JsonException => "json",
            Newtonsoft.Json.JsonException => "json",
            ArgumentException => "json",        // Newtonsoft can wrap as ArgumentException on truly empty input
            InvalidOperationException => "invalid-state",
            NotSupportedException => "not-supported",
            OperationCanceledException => "cancelled",
            _ => ex.GetType().Name.ToLowerInvariant(),
        };

        private static async Task<(byte[] body, DecryptionContext ctx)> DecryptAsync(byte[] payload, RequestOptions requestOptions) =>
            await DecryptAsync(new MemoryStream(payload), requestOptions);

        private static async Task<(byte[] body, DecryptionContext ctx)> DecryptAsync(Stream input, RequestOptions requestOptions)
        {
            (Stream output, DecryptionContext ctx) = await EncryptionProcessor.DecryptAsync(input, mockEncryptor.Object, CosmosDiagnosticsContext.Create(null), requestOptions, CancellationToken.None);
            using MemoryStream ms = new();
            output.Position = 0;
            await output.CopyToAsync(ms);
            return (ms.ToArray(), ctx);
        }

        private static async Task<(bool threw, Exception ex)> TryDecryptAsync(byte[] payload, RequestOptions requestOptions, CancellationToken ct)
        {
            try
            {
                (Stream output, _) = await EncryptionProcessor.DecryptAsync(new MemoryStream(payload), mockEncryptor.Object, CosmosDiagnosticsContext.Create(null), requestOptions, ct);
                using MemoryStream sink = new();
                output.Position = 0;
                await output.CopyToAsync(sink, ct);
                return (false, null);
            }
            catch (Exception ex)
            {
                return (true, ex);
            }
        }

        private static byte[] Utf8(string s) => System.Text.Encoding.UTF8.GetBytes(s);

        private static byte[] CloneBytes(byte[] src)
        {
            byte[] copy = new byte[src.Length];
            Buffer.BlockCopy(src, 0, copy, 0, src.Length);
            return copy;
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

        private static async Task<byte[]> EncryptMdeToBytesAsync(TestDoc doc)
        {
            EncryptionOptions opts = new()
            {
                DataEncryptionKeyId = DekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = TestDoc.PathsToEncrypt,
            };
            Stream encrypted = await EncryptionProcessor.EncryptAsync(doc.ToStream(), mockEncryptor.Object, opts, JsonProcessor.Newtonsoft, CosmosDiagnosticsContext.Create(null), CancellationToken.None);
            using MemoryStream ms = new();
            encrypted.Position = 0;
            await encrypted.CopyToAsync(ms);
            return ms.ToArray();
        }

        private static async Task<byte[]> EncryptLegacyToBytesAsync(TestDoc doc)
        {
            EncryptionOptions opts = new()
            {
                DataEncryptionKeyId = DekId,
#pragma warning disable CS0618
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
#pragma warning restore CS0618
                PathsToEncrypt = TestDoc.PathsToEncrypt,
            };
            Stream encrypted = await EncryptionProcessor.EncryptAsync(doc.ToStream(), mockEncryptor.Object, opts, JsonProcessor.Newtonsoft, CosmosDiagnosticsContext.Create(null), CancellationToken.None);
            using MemoryStream ms = new();
            encrypted.Position = 0;
            await encrypted.CopyToAsync(ms);
            return ms.ToArray();
        }

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
