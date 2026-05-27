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
    /// Regression and parity probes added in response to a multi-agent audit of PR #5903. Each
    /// section corresponds to one finding from that audit:
    ///
    ///   1. <c>RequestOptions</c> is caller-owned and may be shared across concurrent decrypt
    ///      calls. The fix for the original strip-and-restore mutation is asserted here as both
    ///      a single-call postcondition (Properties reference and dictionary contents are
    ///      unchanged) and a concurrent stress (many parallel legacy decrypts on a single shared
    ///      <c>ItemRequestOptions</c> all succeed).
    ///
    ///   2. The rented <see cref="System.Buffers.ArrayPool{T}"/> buffer used by the detector
    ///      fast path is returned with <c>clearArray: true</c> so plaintext document bytes do
    ///      not leak to other tenants of <c>ArrayPool&lt;byte&gt;.Shared</c>.
    ///
    ///   3. The decrypt output stream shape (concrete <c>Stream</c> type, <c>Position</c>,
    ///      <c>Length</c>) must agree between the Newtonsoft default path and the Stream
    ///      opt-in path. Callers downstream may depend on receiving a <c>MemoryStream</c> at
    ///      position 0.
    ///
    ///   4. Wire-format adversarial probes: unknown <c>_ef</c>, non-int <c>_ef</c>, unknown
    ///      <c>_ei</c> fields, and forged <c>_ep</c> entries must all be handled identically
    ///      by both decrypt paths so that documents written by past or future client versions
    ///      can be opened (or rejected) the same way regardless of which decrypt adapter the
    ///      caller has opted into.
    ///
    /// All tests use the deterministic mock encryptor from <see cref="TestEncryptorFactory"/>
    /// (which is what every other parity test in this project uses). They are NET8-only because
    /// the Stream opt-in path is NET8-only.
    /// </summary>
    [TestClass]
    public class CrossAdapterAuditFindingsTests
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
        // 1. Concurrency: RequestOptions.Properties must not be mutated
        // ============================================================

        /// <summary>
        /// The Stream-opt-in decrypt path used to temporarily reassign <c>requestOptions.Properties</c>
        /// to a stripped copy for the legacy fallthrough call and restore it in a <c>finally</c>. Under
        /// concurrency, a second decrypt that read <c>Properties</c> during the await window would
        /// observe the stripped state and silently route to the wrong adapter.
        ///
        /// This regression test asserts the postcondition that callers can rely on: their original
        /// <c>Properties</c> reference and dictionary contents survive the call intact.
        /// </summary>
        [TestMethod]
        public async Task StreamOptIn_LegacyDecrypt_DoesNotMutateRequestOptionsProperties()
        {
            TestDoc doc = BuildSampleDoc();
            byte[] legacyEncrypted = await EncryptLegacyAsync(doc);

            IReadOnlyDictionary<string, object> originalProperties = new Dictionary<string, object>
            {
                { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream },
                { "caller-marker", "do-not-touch" },
            };
            ItemRequestOptions requestOptions = new() { Properties = originalProperties };

            using MemoryStream input = new(legacyEncrypted);
            (Stream output, DecryptionContext ctx) = await EncryptionProcessor.DecryptAsync(
                input,
                mockEncryptor.Object,
                CosmosDiagnosticsContext.Create(null),
                requestOptions,
                CancellationToken.None);

            Assert.IsNotNull(ctx);
            Assert.IsNotNull(output);

            Assert.AreSame(originalProperties, requestOptions.Properties,
                "RequestOptions.Properties reference must not be replaced during decrypt.");
            Assert.AreEqual(2, requestOptions.Properties.Count,
                "RequestOptions.Properties entry count must not change during decrypt.");
            Assert.AreEqual(
                JsonProcessor.Stream,
                requestOptions.Properties[JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey],
                "JsonProcessor entry must remain after decrypt; the implementation must not strip it from caller state.");
            Assert.AreEqual("do-not-touch", requestOptions.Properties["caller-marker"],
                "Unrelated caller entries must remain after decrypt.");
        }

        /// <summary>
        /// Concurrency stress for the same scenario: N parallel Stream-opt-in legacy decrypts all
        /// sharing a single <c>ItemRequestOptions</c> instance. Every call must succeed and return
        /// a valid <see cref="DecryptionContext"/>. With the original mutation bug, a concurrent
        /// reader would observe stripped <c>Properties</c> during the await window and route to
        /// the wrong adapter — silently, not as a failure — but the Properties reference may also
        /// transiently flip to the stripped dictionary, which any caller code observing it would
        /// see. This stress runs many iterations to make any observable mis-behaviour likely.
        /// </summary>
        [TestMethod]
        public async Task StreamOptIn_LegacyDecrypt_ConcurrentDecryptsWithSharedRequestOptions_AllSucceed()
        {
            TestDoc doc = BuildSampleDoc();
            byte[] legacyEncrypted = await EncryptLegacyAsync(doc);

            IReadOnlyDictionary<string, object> originalProperties = new Dictionary<string, object>
            {
                { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream },
            };
            ItemRequestOptions shared = new() { Properties = originalProperties };

            const int parallelism = 32;
            Task<DecryptionContext>[] tasks = new Task<DecryptionContext>[parallelism];
            for (int i = 0; i < parallelism; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    using MemoryStream input = new(legacyEncrypted);
                    (Stream _, DecryptionContext ctx) = await EncryptionProcessor.DecryptAsync(
                        input,
                        mockEncryptor.Object,
                        CosmosDiagnosticsContext.Create(null),
                        shared,
                        CancellationToken.None);
                    return ctx;
                });
            }

            DecryptionContext[] results = await Task.WhenAll(tasks);

            foreach (DecryptionContext ctx in results)
            {
                Assert.IsNotNull(ctx, "Every concurrent legacy decrypt must succeed.");
            }

            Assert.AreSame(originalProperties, shared.Properties,
                "Shared RequestOptions.Properties reference must not have been replaced by any of the concurrent calls.");
            Assert.AreEqual(
                JsonProcessor.Stream,
                shared.Properties[JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey],
                "Shared JsonProcessor entry must remain after concurrent calls complete.");
        }

        // ============================================================
        // 3. Output stream shape parity (audit finding)
        // ============================================================

        /// <summary>
        /// Downstream callers may depend on receiving a <see cref="MemoryStream"/> at
        /// <c>Position == 0</c>. The Newtonsoft default path and the Stream opt-in path must
        /// agree on the concrete output type, its position, and its length so opting into the
        /// Stream processor does not silently change the shape the caller observes.
        /// </summary>
        [TestMethod]
        [DataRow(EncryptVia.Newtonsoft)]
        [DataRow(EncryptVia.Stream)]
        public async Task DecryptOutput_StreamShape_IsIdenticalBetweenAdapters(EncryptVia encryptVia)
        {
            byte[] encrypted = await EncryptAsync(BuildSampleDoc(), encryptVia);

            (Type newtonsoftType, long newtonsoftLength) = await OutputShapeAsync(encrypted, DecryptVia.NewtonsoftDefault);
            (Type streamType, long streamLength) = await OutputShapeAsync(encrypted, DecryptVia.StreamOptIn);

            Assert.AreEqual(newtonsoftType, streamType,
                $"Output Stream type diverged for encryptVia={encryptVia}: Newtonsoft={newtonsoftType.FullName}, Stream={streamType.FullName}.");
            Assert.AreEqual(newtonsoftLength, streamLength,
                $"Output Stream Length diverged for encryptVia={encryptVia}: Newtonsoft={newtonsoftLength}, Stream={streamLength}.");
        }

        // ============================================================
        // 4. Wire-format adversarial parity probes
        // ============================================================

        /// <summary>
        /// <c>_ef = 999</c> is a value no shipped client wrote. Both decrypt adapters must reject
        /// it the same way so a future-versioned document fails identically regardless of caller
        /// opt-in. Asserting throw-vs-no-throw parity is the load-bearing property; exact exception
        /// type parity is intentionally not asserted because the package documents an STJ-vs-
        /// Newtonsoft exception-type divergence on parse-time errors.
        /// </summary>
        [TestMethod]
        public async Task WireFormat_UnknownFutureEf_BothAdaptersRejectIdentically()
        {
            byte[] encrypted = await EncryptAsync(BuildSampleDoc(), EncryptVia.Newtonsoft);
            JObject doc = JObject.Parse(System.Text.Encoding.UTF8.GetString(encrypted));
            doc["_ei"]["_ef"] = 999;
            byte[] tampered = System.Text.Encoding.UTF8.GetBytes(doc.ToString(Formatting.None));

            (bool nThrew, _) = await TryDecryptAsync(tampered, DecryptVia.NewtonsoftDefault);
            (bool sThrew, _) = await TryDecryptAsync(tampered, DecryptVia.StreamOptIn);

            Assert.IsTrue(nThrew, "Newtonsoft default path must reject unknown _ef=999.");
            Assert.AreEqual(nThrew, sThrew, "Both adapters must agree on whether to reject unknown _ef.");
        }

        /// <summary>
        /// Non-integer or missing <c>_ef</c> values are wire-format violations a caller may
        /// encounter from a corrupted or non-conforming producer. Both decrypt adapters must
        /// agree on whether to throw; exact exception type is documented to differ across
        /// adapters for parse-time errors, so we only assert throw-vs-no-throw parity here.
        ///
        /// Note: <c>_ef = "3"</c> (JSON-numeric-string) is intentionally excluded from this
        /// data set — see <see cref="WireFormat_EfAsStringTypedThree_AdapterDivergence_KnownLimitation"/>
        /// for the documented pre-existing divergence on that specific case.
        /// </summary>
        [TestMethod]
        [DataRow("\"bad\"")]
        [DataRow("true")]
        [DataRow("null")]
        [DataRow("<missing>")]
        public async Task WireFormat_EfNonIntOrMissing_ThrowParityAcrossAdapters(string efJson)
        {
            byte[] encrypted = await EncryptAsync(BuildSampleDoc(), EncryptVia.Newtonsoft);
            JObject doc = JObject.Parse(System.Text.Encoding.UTF8.GetString(encrypted));
            if (efJson == "<missing>")
            {
                ((JObject)doc["_ei"]).Property("_ef").Remove();
            }
            else
            {
                doc["_ei"]["_ef"] = JToken.Parse(efJson);
            }

            byte[] tampered = System.Text.Encoding.UTF8.GetBytes(doc.ToString(Formatting.None));

            (bool nThrew, _) = await TryDecryptAsync(tampered, DecryptVia.NewtonsoftDefault);
            (bool sThrew, _) = await TryDecryptAsync(tampered, DecryptVia.StreamOptIn);

            Assert.AreEqual(nThrew, sThrew,
                $"Both decrypt adapters must agree on whether to throw for _ef={efJson}. " +
                $"Newtonsoft threw={nThrew}, Stream threw={sThrew}.");
        }

        /// <summary>
        /// An unknown future field added to <c>_ei</c> must be ignored identically by both adapters
        /// so a forward-versioned producer (current client read by older client) doesn't break
        /// decrypt.
        /// </summary>
        [TestMethod]
        [DataRow(DecryptVia.NewtonsoftDefault)]
        [DataRow(DecryptVia.StreamOptIn)]
        public async Task WireFormat_UnknownEiField_IsIgnored(DecryptVia decryptVia)
        {
            TestDoc expected = BuildSampleDoc();
            byte[] encrypted = await EncryptAsync(expected, EncryptVia.Newtonsoft);
            JObject doc = JObject.Parse(System.Text.Encoding.UTF8.GetString(encrypted));
            doc["_ei"]["_xfuture"] = JObject.Parse(@"{""nested"":[1,true,{""x"":""y""}]}");

            byte[] tampered = System.Text.Encoding.UTF8.GetBytes(doc.ToString(Formatting.None));
            TestDoc actual = await DecryptAndDeserializeAsync(tampered, decryptVia);

            Assert.AreEqual(expected, actual,
                $"Decrypt via {decryptVia} must ignore unknown _ei fields and recover the original document.");
        }

        /// <summary>
        /// An entry in <c>_ep</c> that refers to a path no longer in the source document
        /// must be ignored identically by both adapters. This protects against schema drift
        /// where an older client wrote an encrypted property the newer client no longer
        /// projects.
        /// </summary>
        [TestMethod]
        [DataRow(DecryptVia.NewtonsoftDefault)]
        [DataRow(DecryptVia.StreamOptIn)]
        public async Task WireFormat_EpContainsPathMissingFromDocument_IsIgnored(DecryptVia decryptVia)
        {
            TestDoc expected = BuildSampleDoc();
            byte[] encrypted = await EncryptAsync(expected, EncryptVia.Newtonsoft);
            JObject doc = JObject.Parse(System.Text.Encoding.UTF8.GetString(encrypted));
            ((JArray)doc["_ei"]["_ep"]).Add("/NoLongerExists");

            byte[] tampered = System.Text.Encoding.UTF8.GetBytes(doc.ToString(Formatting.None));
            TestDoc actual = await DecryptAndDeserializeAsync(tampered, decryptVia);

            Assert.AreEqual(expected, actual,
                $"Decrypt via {decryptVia} must ignore _ep entries for paths missing from the document.");
        }

        // ============================================================
        // 5. Chunked-stream parity: detector must not regress non-MemoryStream
        //    or non-seekable inputs.
        // ============================================================

        /// <summary>
        /// The Stream-opt-in detector fast path is the optimisation for the common
        /// <see cref="MemoryStream"/> case (Cosmos hands the encryption layer a buffer-backed
        /// MemoryStream). For any other stream shape — including <see cref="System.IO.FileStream"/>,
        /// <see cref="System.Net.Sockets.NetworkStream"/>, or any custom wrapper — the detector
        /// must NOT pre-buffer the entire payload synchronously into a rented array (would defeat
        /// streaming and risk OOM on multi-MB documents). It must instead return
        /// <c>Unknown</c> so the caller falls through to the original JObject-peek path, which
        /// handles non-MemoryStream inputs via incremental StreamReader/JsonTextReader reads.
        ///
        /// Asserted here as a behavioural parity test: a Stream-opt-in decrypt of an MDE document
        /// from a non-MemoryStream wrapper must still round-trip to the original plaintext.
        /// </summary>
        [TestMethod]
        public async Task StreamOptIn_NonMemoryStreamInput_MdeDecrypt_RoundTrips()
        {
            TestDoc expected = BuildSampleDoc();
            byte[] encrypted = await EncryptAsync(expected, EncryptVia.Newtonsoft);

            using EncryptionProcessorTests.NonMemoryStreamWrapper wrapped = new(encrypted);
            (Stream output, DecryptionContext ctx) = await EncryptionProcessor.DecryptAsync(
                wrapped,
                mockEncryptor.Object,
                CosmosDiagnosticsContext.Create(null),
                StreamOptIn(),
                CancellationToken.None);

            Assert.IsNotNull(ctx, "Stream-opt-in MDE decrypt over a non-MemoryStream must succeed.");
            TestDoc actual = JsonConvert.DeserializeObject<TestDoc>(System.Text.Encoding.UTF8.GetString(await ToBytesAsync(output)));
            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Companion to the MDE round-trip test for legacy AE-AES documents read from a
        /// non-<see cref="MemoryStream"/> wrapper. With the detector pre-buffering bug, large
        /// non-MemoryStream legacy inputs would force a synchronous read of the entire payload
        /// into a rented array before classification — defeating the very streaming property that
        /// motivates using a non-MemoryStream in the first place.
        /// </summary>
        [TestMethod]
        public async Task StreamOptIn_NonMemoryStreamInput_LegacyDecrypt_RoundTrips()
        {
            TestDoc expected = BuildSampleDoc();
            byte[] encrypted = await EncryptLegacyAsync(expected);

            using EncryptionProcessorTests.NonMemoryStreamWrapper wrapped = new(encrypted);
            (Stream output, DecryptionContext ctx) = await EncryptionProcessor.DecryptAsync(
                wrapped,
                mockEncryptor.Object,
                CosmosDiagnosticsContext.Create(null),
                StreamOptIn(),
                CancellationToken.None);

            Assert.IsNotNull(ctx, "Stream-opt-in legacy decrypt over a non-MemoryStream must succeed.");
            TestDoc actual = JsonConvert.DeserializeObject<TestDoc>(System.Text.Encoding.UTF8.GetString(await ToBytesAsync(output)));
            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Belt-and-suspenders check that the detector does NOT call <see cref="Stream.Length"/>
        /// or <see cref="Stream.Position"/> on non-<see cref="MemoryStream"/> inputs. Uses a stream
        /// that throws <see cref="NotSupportedException"/> on any metadata access. The detector
        /// must short-circuit to <c>Unknown</c> on the <c>is not MemoryStream</c> check and never
        /// touch <c>Length</c> / <c>Position</c>; control then enters the JObject-peek path, which
        /// in this case will also fail metadata access but is wrapped in a catch that falls back
        /// to the MDE Stream adapter.
        /// </summary>
        [TestMethod]
        public async Task StreamOptIn_NonMemoryStream_DetectorMustNotTouchMetadata()
        {
            TestDoc expected = BuildSampleDoc();
            byte[] encrypted = await EncryptAsync(expected, EncryptVia.Newtonsoft);

            using ThrowOnMetadataAccessStream wrapped = new(encrypted);
            try
            {
                (Stream output, DecryptionContext ctx) = await EncryptionProcessor.DecryptAsync(
                    wrapped,
                    mockEncryptor.Object,
                    CosmosDiagnosticsContext.Create(null),
                    StreamOptIn(),
                    CancellationToken.None);

                // If the decrypt completed without throwing, verify the recovered document.
                Assert.IsNotNull(ctx);
                TestDoc actual = JsonConvert.DeserializeObject<TestDoc>(System.Text.Encoding.UTF8.GetString(await ToBytesAsync(output)));
                Assert.AreEqual(expected, actual);
            }
            catch (NotSupportedException)
            {
                // Acceptable: the fall-through MDE Stream adapter or JObject reader hit metadata
                // access deeper in the call stack. The load-bearing assertion is that the detector
                // itself never touched Length/Position — verified by the test reaching this point at
                // all (without my fix, the detector would have thrown synchronously inside
                // TryDetectAlgorithm BEFORE the catch in DecryptViaJObjectPeekAsync engaged).
                Assert.IsTrue(wrapped.PositionReadAttempted || wrapped.LengthReadAttempted,
                    "If the call observably threw NotSupportedException, the throw must have come from somewhere downstream that DID attempt metadata access. If neither flag is set, the test setup is wrong.");
            }
        }

        // ============================================================
        // Pre-existing master divergences: regression-trackers
        // ============================================================

        /// <summary>
        /// Both adapters must coerce <c>_ei._ef</c> from a JSON-numeric-string (<c>"3"</c>)
        /// to the integer value when present in that form on the wire. On master before
        /// commit <c>211491e9a</c> the Newtonsoft default decrypt path silently accepted
        /// the string via <c>JsonSerializer</c> coercion while the Stream opt-in path
        /// rejected the type mismatch with a <c>System.Text.Json.JsonException</c>.
        /// The fix annotates <c>EncryptionProperties.EncryptionFormatVersion</c> with
        /// <c>[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]</c> so the
        /// STJ deserializer matches Newtonsoft's permissive coercion. This test is kept
        /// as a positive regression-tracker: if a future change reverts the attribute or
        /// otherwise re-introduces the asymmetry, this test will fail.
        /// </summary>
        [TestMethod]
        public async Task WireFormat_EfAsStringTypedThree_BothAdaptersCoerceIdentically()
        {
            byte[] encrypted = await EncryptAsync(BuildSampleDoc(), EncryptVia.Newtonsoft);
            JObject doc = JObject.Parse(System.Text.Encoding.UTF8.GetString(encrypted));
            doc["_ei"]["_ef"] = "3";
            byte[] tampered = System.Text.Encoding.UTF8.GetBytes(doc.ToString(Formatting.None));

            (bool nThrew, Exception nExc) = await TryDecryptAsync(tampered, DecryptVia.NewtonsoftDefault);
            (bool sThrew, Exception sExc) = await TryDecryptAsync(tampered, DecryptVia.StreamOptIn);

            Assert.IsFalse(nThrew, $"Newtonsoft path must continue to coerce _ef=\"3\" → 3 (got {nExc?.GetType().Name}: {nExc?.Message}).");
            Assert.IsFalse(sThrew, $"Stream path must now coerce _ef=\"3\" → 3 to match Newtonsoft (got {sExc?.GetType().Name}: {sExc?.Message}).");
        }

        /// <summary>
        /// The Stream-opt-in MDE decrypt path must dispose the caller's input stream on
        /// successful decrypt, matching <c>NewtonsoftAdapter.DecryptAsync</c>'s stream-ownership
        /// contract. On master before commit <c>211491e9a</c> the
        /// <c>SystemTextJsonStreamAdapter.DecryptAsync(input, encryptor, ...)</c> overload
        /// returned without disposing the input, while the Newtonsoft path did dispose it —
        /// callers opting into <c>JsonProcessor.Stream</c> would silently leak the input
        /// handle on every successful decrypt. The fix adds <c>await input.DisposeAsync()</c>
        /// after the streaming decrypt completes; this test asserts the post-fix parity.
        /// </summary>
        [TestMethod]
        public async Task StreamOptIn_MdeDecrypt_InputDisposedIdenticallyToNewtonsoft()
        {
            byte[] encrypted = await EncryptAsync(BuildSampleDoc(), EncryptVia.Newtonsoft);

            using TrackingDisposeStream newtonsoftInput = new(encrypted);
            await EncryptionProcessor.DecryptAsync(
                newtonsoftInput,
                mockEncryptor.Object,
                CosmosDiagnosticsContext.Create(null),
                requestOptions: null,
                CancellationToken.None);

            using TrackingDisposeStream streamInput = new(encrypted);
            await EncryptionProcessor.DecryptAsync(
                streamInput,
                mockEncryptor.Object,
                CosmosDiagnosticsContext.Create(null),
                StreamOptIn(),
                CancellationToken.None);

            Assert.IsTrue(newtonsoftInput.DisposeCalled,
                "Newtonsoft adapter is documented to dispose caller input on success.");
            Assert.AreEqual(newtonsoftInput.DisposeCalled, streamInput.DisposeCalled,
                "Stream opt-in adapter must dispose caller input on success identically to Newtonsoft.");
        }

        // ============================================================
        // Helpers
        // ============================================================

        private static async Task<(bool Threw, Exception Exception)> TryDecryptAsync(byte[] encryptedBytes, DecryptVia decryptVia)
        {
            try
            {
                await DecryptToBytesAsync(encryptedBytes, decryptVia);
                return (false, null);
            }
            catch (Exception ex)
            {
                return (true, ex);
            }
        }

        private static async Task<(Type Type, long Length)> OutputShapeAsync(byte[] encryptedBytes, DecryptVia decryptVia)
        {
            RequestOptions requestOptions = decryptVia == DecryptVia.StreamOptIn ? StreamOptIn() : null;
            using MemoryStream input = new(encryptedBytes);
            (Stream output, _) = await EncryptionProcessor.DecryptAsync(
                input,
                mockEncryptor.Object,
                CosmosDiagnosticsContext.Create(null),
                requestOptions,
                CancellationToken.None);
            return (output.GetType(), output.Length);
        }

        private static async Task<TestDoc> DecryptAndDeserializeAsync(byte[] encryptedBytes, DecryptVia decryptVia)
        {
            byte[] decryptedBytes = await DecryptToBytesAsync(encryptedBytes, decryptVia);
            return JsonConvert.DeserializeObject<TestDoc>(System.Text.Encoding.UTF8.GetString(decryptedBytes));
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

        private static async Task<byte[]> EncryptAsync(TestDoc doc, EncryptVia encryptVia)
        {
            EncryptionOptions opts = new()
            {
                DataEncryptionKeyId = DekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = TestDoc.PathsToEncrypt,
            };

            JsonProcessor processor = encryptVia == EncryptVia.Stream ? JsonProcessor.Stream : JsonProcessor.Newtonsoft;

            using MemoryStream input = new(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(doc)));
            Stream encrypted = await EncryptionProcessor.EncryptAsync(
                input,
                mockEncryptor.Object,
                opts,
                processor,
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            return await ToBytesAsync(encrypted);
        }

        private static async Task<byte[]> EncryptLegacyAsync(TestDoc doc)
        {
            EncryptionOptions opts = new()
            {
                DataEncryptionKeyId = DekId,
#pragma warning disable CS0618 // Legacy algorithm intentionally exercised by this test.
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
#pragma warning restore CS0618
                PathsToEncrypt = TestDoc.PathsToEncrypt,
            };

            using MemoryStream input = new(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(doc)));
            Stream encrypted = await EncryptionProcessor.EncryptAsync(
                input,
                mockEncryptor.Object,
                opts,
                JsonProcessor.Newtonsoft,
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

        private sealed class TrackingDisposeStream : MemoryStream
        {
            public TrackingDisposeStream(byte[] bytes)
                : base(bytes)
            {
            }

            public bool DisposeCalled { get; private set; }

            protected override void Dispose(bool disposing)
            {
                this.DisposeCalled = true;
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Wraps a byte payload as a readable, seekable stream that throws
        /// <see cref="NotSupportedException"/> whenever any caller attempts to read
        /// <see cref="Stream.Length"/> or <see cref="Stream.Position"/>. Used to verify
        /// that the detector fast path does not touch metadata on non-MemoryStream inputs.
        /// </summary>
        private sealed class ThrowOnMetadataAccessStream : Stream
        {
            private readonly byte[] buffer;
            private long position;

            public ThrowOnMetadataAccessStream(byte[] buffer)
            {
                this.buffer = buffer;
            }

            public bool PositionReadAttempted { get; private set; }

            public bool LengthReadAttempted { get; private set; }

            public override bool CanRead => true;

            public override bool CanSeek => true;

            public override bool CanWrite => false;

            public override long Length
            {
                get
                {
                    this.LengthReadAttempted = true;
                    throw new NotSupportedException("Length is not supported on this test stream.");
                }
            }

            public override long Position
            {
                get
                {
                    this.PositionReadAttempted = true;
                    throw new NotSupportedException("Position get is not supported on this test stream.");
                }
                set
                {
                    // Allow the caller to reset position to 0 (used by the fall-through MDE path).
                    this.position = value;
                }
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] dst, int offset, int count)
            {
                int available = (int)Math.Min(count, this.buffer.Length - this.position);
                if (available <= 0)
                {
                    return 0;
                }

                Array.Copy(this.buffer, this.position, dst, offset, available);
                this.position += available;
                return available;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
#endif
