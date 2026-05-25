//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;
    using TestDoc = TestCommon.TestDoc;
#if NET8_0_OR_GREATER
    using Microsoft.Azure.Cosmos;
#endif

    [TestClass]
    public class EncryptionProcessorTests
    {
        private static Mock<Encryptor> mockEncryptor;
        private const string DekId = "dekId";

        [ClassInitialize]
        public static void ClassInitialize(TestContext ctx)
        {
            _ = ctx;
            mockEncryptor = TestEncryptorFactory.CreateMde(DekId, out _);
        }

#if NET8_0_OR_GREATER
        private static EncryptionOptions CreateMdeOptions()
        {
            return new()
            {
                DataEncryptionKeyId = DekId,
#pragma warning disable CS0618
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
#pragma warning restore CS0618
                PathsToEncrypt = TestDoc.PathsToEncrypt,
            };
        }

        [TestMethod]
        public async Task EncryptDecrypt_StreamProcessor_WithProvidedOutput()
        {
            TestDoc doc = TestDoc.Create();
            EncryptionOptions opts = CreateMdeOptions();
            
            // Capture activities to validate scopes are created
            List<Activity> capturedActivities = new List<Activity>();
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => { lock (capturedActivities) { capturedActivities.Add(activity); } }
            };
            ActivitySource.AddActivityListener(listener);
            
            CosmosDiagnosticsContext diagEncrypt = CosmosDiagnosticsContext.Create(null);
            MemoryStream encrypted = new();
            await EncryptionProcessor.EncryptAsync(doc.ToStream(), encrypted, mockEncryptor.Object, opts, JsonProcessor.Stream, diagEncrypt, CancellationToken.None);
            encrypted.Position = 0;

            CosmosDiagnosticsContext diagDecrypt = CosmosDiagnosticsContext.Create(null);
            MemoryStream decryptedOut = new();
            ItemRequestOptions requestOptions = new() { Properties = new Dictionary<string, object> { { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream } } };
            DecryptionContext ctx = await EncryptionProcessor.DecryptAsync(encrypted, decryptedOut, mockEncryptor.Object, diagDecrypt, requestOptions, CancellationToken.None);

            decryptedOut.Position = 0;
            JObject decryptedObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(decryptedOut);
            Assert.AreEqual(doc.SensitiveStr, decryptedObj.Property(nameof(TestDoc.SensitiveStr)).Value.Value<string>());
            Assert.IsNull(decryptedObj.Property(Constants.EncryptedInfo));
            Assert.IsNotNull(ctx);
            Assert.IsTrue(ctx.DecryptionInfoList.First().PathsDecrypted.All(p => TestDoc.PathsToEncrypt.Contains(p)));
            
            // Validate diagnostic scopes were created
            string expectedEncryptScope = CosmosDiagnosticsContext.ScopeEncryptModeSelectionPrefix + JsonProcessor.Stream;
            string expectedDecryptScope = CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream;
            lock (capturedActivities)
            {
                Assert.IsTrue(capturedActivities.Any(a => a.DisplayName == expectedEncryptScope),
                    $"Expected encrypt scope '{expectedEncryptScope}' not found. Activities: {string.Join(", ", capturedActivities.Select(a => a.DisplayName))}");
                Assert.IsTrue(capturedActivities.Any(a => a.DisplayName == expectedDecryptScope),
                    $"Expected decrypt scope '{expectedDecryptScope}' not found. Activities: {string.Join(", ", capturedActivities.Select(a => a.DisplayName))}");
            }
        }

    [TestMethod]
    public async Task Encrypt_NewtonsoftProcessor_Works()
    {
        TestDoc doc = TestDoc.Create();
        EncryptionOptions opts = CreateMdeOptions();
        
        // Capture activities to validate scopes are created
        List<Activity> capturedActivities = new List<Activity>();
        using ActivityListener listener = new ActivityListener
        {
            ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => { lock (capturedActivities) { capturedActivities.Add(activity); } }
        };
        ActivitySource.AddActivityListener(listener);
        
        CosmosDiagnosticsContext diagEncrypt = CosmosDiagnosticsContext.Create(null);
        Stream encrypted = await EncryptionProcessor.EncryptAsync(doc.ToStream(), mockEncryptor.Object, opts, JsonProcessor.Newtonsoft, diagEncrypt, CancellationToken.None);

        Assert.IsNotNull(encrypted);
        encrypted.Dispose();
        
        // Validate Newtonsoft encrypt scope was created
        string expectedEncryptScope = CosmosDiagnosticsContext.ScopeEncryptModeSelectionPrefix + JsonProcessor.Newtonsoft;
        lock (capturedActivities)
        {
            Assert.IsTrue(capturedActivities.Any(a => a.DisplayName == expectedEncryptScope),
                $"Expected Newtonsoft encrypt scope '{expectedEncryptScope}' not found. Activities: {string.Join(", ", capturedActivities.Select(a => a.DisplayName))}");
        }
    }

        [TestMethod]
        public async Task Decrypt_StreamSelection_FallbackWhenUnencrypted()
        {
            string json = "{\"id\":\"id1\",\"pk\":\"pk1\",\"NonSensitive\":\"v\"}"; // no _ei
            MemoryStream input = new(System.Text.Encoding.UTF8.GetBytes(json));
            CosmosDiagnosticsContext ctxDiag = CosmosDiagnosticsContext.Create(null);
            ItemRequestOptions opts = new() { Properties = new Dictionary<string, object> { { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream } } };
            (Stream result, DecryptionContext ctxDec) = await EncryptionProcessor.DecryptAsync(input, mockEncryptor.Object, ctxDiag, opts, CancellationToken.None);
            Assert.IsNull(ctxDec);
            Assert.AreEqual(0, result.Position);
        }
#endif

#if NET8_0_OR_GREATER
        [TestMethod]
        public async Task Decrypt_StreamSelection_LegacyAlgorithm_FallsBackToNewtonsoft()
        {
            TestDoc doc = TestDoc.Create();
            EncryptionOptions legacy = new()
            {
                DataEncryptionKeyId = DekId,
#pragma warning disable CS0618
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
#pragma warning restore CS0618
                PathsToEncrypt = TestDoc.PathsToEncrypt,
            };
            Stream legacyEncrypted = await EncryptionProcessor.EncryptAsync(doc.ToStream(), mockEncryptor.Object, legacy, JsonProcessor.Newtonsoft, CosmosDiagnosticsContext.Create(null), CancellationToken.None);
            legacyEncrypted.Position = 0;

            ItemRequestOptions opts = new() { Properties = new Dictionary<string, object> { { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" } } };
            CosmosDiagnosticsContext diag = CosmosDiagnosticsContext.Create(null);

            // Legacy algorithm should decrypt successfully by falling back to the legacy decryption path
            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptAsync(legacyEncrypted, mockEncryptor.Object, diag, opts, CancellationToken.None);

            Assert.IsNotNull(decrypted);
            Assert.IsNotNull(context);
            decrypted.Position = 0;
            TestDoc result = TestCommon.FromStream<TestDoc>(decrypted);
            Assert.AreEqual(doc, result);
        }

        [TestMethod]
        public async Task DecryptProvidedOutput_StreamSelection_LegacyAlgorithm_Throws()
        {
            TestDoc doc = TestDoc.Create();
            EncryptionOptions legacy = new()
            {
                DataEncryptionKeyId = DekId,
#pragma warning disable CS0618
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
#pragma warning restore CS0618
                PathsToEncrypt = TestDoc.PathsToEncrypt,
            };
            Stream legacyEncrypted = await EncryptionProcessor.EncryptAsync(doc.ToStream(), mockEncryptor.Object, legacy, JsonProcessor.Newtonsoft, CosmosDiagnosticsContext.Create(null), CancellationToken.None);
            legacyEncrypted.Position = 0;

            ItemRequestOptions opts = new() { Properties = new Dictionary<string, object> { { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" } } };
            CosmosDiagnosticsContext diag = CosmosDiagnosticsContext.Create(null);
            MemoryStream output = new();

            NotSupportedException exception = await Assert.ThrowsExceptionAsync<NotSupportedException>(async () =>
            {
                await EncryptionProcessor.DecryptAsync(legacyEncrypted, output, mockEncryptor.Object, diag, opts, CancellationToken.None);
            });

            Assert.IsTrue(exception.Message.Contains("not supported"), $"Unexpected exception message: {exception.Message}");
#pragma warning disable CS0618
            Assert.IsTrue(exception.Message.Contains(CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized), $"Exception should mention the unsupported algorithm");
#pragma warning restore CS0618
        }

        [TestMethod]
        public async Task Encrypt_LegacyAlgorithm_StreamProcessor_Throws()
        {
            TestDoc doc = TestDoc.Create();
            EncryptionItemRequestOptions ro = new() 
            { 
                EncryptionOptions = new()
                {
                    DataEncryptionKeyId = DekId,
#pragma warning disable CS0618
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
#pragma warning restore CS0618
                    PathsToEncrypt = TestDoc.PathsToEncrypt,
                },
                Properties = new Dictionary<string, object> { { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" } } 
            };

            CosmosDiagnosticsContext diag = CosmosDiagnosticsContext.Create(null);

            try
            {
                await EncryptionProcessor.EncryptAsync(doc.ToStream(), mockEncryptor.Object, ro, diag, CancellationToken.None);
                Assert.Fail("Expected NotSupportedException for legacy algorithm with Stream processor override.");
            }
            catch (NotSupportedException ex)
            {
                Assert.IsTrue(ex.Message.IndexOf("not supported", StringComparison.OrdinalIgnoreCase) >= 0, $"Unexpected message: {ex.Message}");
            }
        }

        [TestMethod]
        public async Task Decrypt_StreamSelection_MdeAlgorithm_RoundTrips()
        {
            TestDoc doc = TestDoc.Create();
            EncryptionOptions opts = CreateMdeOptions();
            Stream encrypted = await EncryptionProcessor.EncryptAsync(doc.ToStream(), mockEncryptor.Object, opts, JsonProcessor.Newtonsoft, CosmosDiagnosticsContext.Create(null), CancellationToken.None);
            encrypted.Position = 0;

            ItemRequestOptions ro = new() { Properties = new Dictionary<string, object> { { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream } } };
            (Stream decrypted, DecryptionContext ctx) = await EncryptionProcessor.DecryptAsync(encrypted, mockEncryptor.Object, CosmosDiagnosticsContext.Create(null), ro, CancellationToken.None);

            Assert.IsNotNull(ctx, "Stream-opt-in MDE fast path must produce a DecryptionContext.");
            decrypted.Position = 0;
            TestDoc result = TestCommon.FromStream<TestDoc>(decrypted);
            Assert.AreEqual(doc, result, "Round-tripped document must equal the original.");
        }

        [TestMethod]
        public async Task Decrypt_StreamSelection_MalformedJson_FallsBackSafely()
        {
            byte[] malformed = System.Text.Encoding.UTF8.GetBytes("{ not valid json");
            MemoryStream input = new(malformed);
            ItemRequestOptions ro = new() { Properties = new Dictionary<string, object> { { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream } } };

            // The detector classifies malformed input as Unknown and routes to the existing
            // JObject-peek path (DecryptViaJObjectPeekAsync). For malformed JSON the JObject parse
            // itself throws, so the JObject path's catch branch keeps the original requestOptions
            // for the fallthrough MDE call (so async-only streams still get the streaming adapter).
            // Both opt-in and non-opt-in callers must end up throwing — collapse the concrete
            // exception types into a family because the two paths use different JSON parsers.

            (bool optInThrew, Exception optInEx) = await TryDecryptToCompletionAsync(new MemoryStream(malformed), ro);
            (bool baselineThrew, Exception baselineEx) = await TryDecryptToCompletionAsync(new MemoryStream(malformed), requestOptions: null);

            Assert.AreEqual(baselineThrew, optInThrew,
                $"Stream-opt-in diverged from baseline on malformed JSON. baseline={baselineEx?.GetType().Name ?? "<none>"}, opt-in={optInEx?.GetType().Name ?? "<none>"}");
            if (baselineThrew)
            {
                Assert.AreEqual(NormalizeExceptionFamily(baselineEx), NormalizeExceptionFamily(optInEx),
                    $"Exception families differ. baseline={baselineEx.GetType().Name}, opt-in={optInEx.GetType().Name}");
            }
        }

        private static string NormalizeExceptionFamily(Exception ex)
        {
            return ex switch
            {
                Newtonsoft.Json.JsonException => "json",
                System.Text.Json.JsonException => "json",
                InvalidOperationException => "invalid-state",
                NotSupportedException => "not-supported",
                OperationCanceledException => "cancelled",
                _ => ex.GetType().FullName,
            };
        }

        private static async Task<(bool threw, Exception ex)> TryDecryptToCompletionAsync(Stream input, RequestOptions requestOptions)
        {
            try
            {
                (Stream output, _) = await EncryptionProcessor.DecryptAsync(input, mockEncryptor.Object, CosmosDiagnosticsContext.Create(null), requestOptions, CancellationToken.None);
                using MemoryStream sink = new();
                output.Position = 0;
                await output.CopyToAsync(sink);
                return (false, null);
            }
            catch (Exception ex)
            {
                return (true, ex);
            }
        }

        [TestMethod]
        public async Task Decrypt_StreamSelection_NonMemoryStream_StillRoundTrips()
        {
            // Validates the ArrayPool<byte> fallback branch in TryDetectAlgorithm by wrapping
            // the encrypted payload in a seekable stream that is not a MemoryStream.
            TestDoc doc = TestDoc.Create();
            EncryptionOptions opts = CreateMdeOptions();
            Stream encrypted = await EncryptionProcessor.EncryptAsync(doc.ToStream(), mockEncryptor.Object, opts, JsonProcessor.Newtonsoft, CosmosDiagnosticsContext.Create(null), CancellationToken.None);
            encrypted.Position = 0;
            byte[] bytes = new byte[encrypted.Length];
            int read = encrypted.Read(bytes, 0, bytes.Length);
            Assert.AreEqual(bytes.Length, read);
            using NonMemoryStreamWrapper wrapped = new(bytes);

            ItemRequestOptions ro = new() { Properties = new Dictionary<string, object> { { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream } } };
            (Stream decrypted, DecryptionContext ctx) = await EncryptionProcessor.DecryptAsync(wrapped, mockEncryptor.Object, CosmosDiagnosticsContext.Create(null), ro, CancellationToken.None);

            Assert.IsNotNull(ctx);
            decrypted.Position = 0;
            TestDoc result = TestCommon.FromStream<TestDoc>(decrypted);
            Assert.AreEqual(doc, result);
        }

        [TestMethod]
        public async Task LegacyDecrypt_StreamOptIn_RoundTrips_PreservesAllProperties()
        {
            // Round-trip a legacy AE-AES encrypted document through the Stream-opt-in decrypt
            // path and verify that *every* field — encrypted and non-encrypted — survives.
            TestDoc doc = TestDoc.Create();
            Stream legacyEncrypted = await EncryptWithLegacy(doc);

            ItemRequestOptions ro = StreamOptIn();
            (Stream decrypted, DecryptionContext ctx) = await EncryptionProcessor.DecryptAsync(legacyEncrypted, mockEncryptor.Object, CosmosDiagnosticsContext.Create(null), ro, CancellationToken.None);

            Assert.IsNotNull(ctx);
            decrypted.Position = 0;
            TestDoc result = TestCommon.FromStream<TestDoc>(decrypted);
            Assert.AreEqual(doc, result);
            Assert.AreEqual(doc.Id, result.Id);
            Assert.AreEqual(doc.PK, result.PK);
            Assert.AreEqual(doc.NonSensitive, result.NonSensitive);
            Assert.AreEqual(doc.SensitiveStr, result.SensitiveStr);
            Assert.AreEqual(doc.SensitiveInt, result.SensitiveInt);
            CollectionAssert.AreEqual(doc.SensitiveArr, result.SensitiveArr);
            CollectionAssert.AreEqual(doc.SensitiveDict, result.SensitiveDict);
        }

        [TestMethod]
        public async Task LegacyDecrypt_StreamOptIn_NullSensitiveStr_RoundTrips()
        {
            TestDoc doc = TestDoc.Create();
            doc.SensitiveStr = null;
            Stream legacyEncrypted = await EncryptWithLegacy(doc);

            (Stream decrypted, DecryptionContext ctx) = await EncryptionProcessor.DecryptAsync(legacyEncrypted, mockEncryptor.Object, CosmosDiagnosticsContext.Create(null), StreamOptIn(), CancellationToken.None);

            Assert.IsNotNull(ctx);
            decrypted.Position = 0;
            TestDoc result = TestCommon.FromStream<TestDoc>(decrypted);
            Assert.IsNull(result.SensitiveStr);
            Assert.AreEqual(doc.SensitiveInt, result.SensitiveInt);
            CollectionAssert.AreEqual(doc.SensitiveArr, result.SensitiveArr);
        }

        [TestMethod]
        public async Task LegacyDecrypt_StreamOptIn_NonMemoryStream_RoundTrips()
        {
            // Exercises the ArrayPool fallback in TryDetectAlgorithm for the legacy-algorithm classification.
            TestDoc doc = TestDoc.Create();
            Stream legacyEncrypted = await EncryptWithLegacy(doc);
            byte[] bytes = new byte[legacyEncrypted.Length];
            legacyEncrypted.Position = 0;
            int read = legacyEncrypted.Read(bytes, 0, bytes.Length);
            Assert.AreEqual(bytes.Length, read);
            using NonMemoryStreamWrapper wrapped = new(bytes);

            (Stream decrypted, DecryptionContext ctx) = await EncryptionProcessor.DecryptAsync(wrapped, mockEncryptor.Object, CosmosDiagnosticsContext.Create(null), StreamOptIn(), CancellationToken.None);

            Assert.IsNotNull(ctx);
            decrypted.Position = 0;
            TestDoc result = TestCommon.FromStream<TestDoc>(decrypted);
            Assert.AreEqual(doc, result);
        }

        [TestMethod]
        public async Task LegacyDecrypt_StreamOptIn_ProducesSameResultAsNewtonsoftPath()
        {
            // Golden parity: legacy doc decrypted via Stream-opt-in must equal the same doc
            // decrypted via the original Newtonsoft path (no opt-in). Validates that the new
            // router does not perturb any decrypted bytes.
            TestDoc doc = TestDoc.Create();
            byte[] cipherBytes = await EncryptWithLegacyToBytes(doc);

            (Stream viaNewtonsoft, DecryptionContext ctxNewtonsoft) = await EncryptionProcessor.DecryptAsync(
                new MemoryStream(cipherBytes),
                mockEncryptor.Object,
                CosmosDiagnosticsContext.Create(null),
                requestOptions: null,
                CancellationToken.None);

            (Stream viaStreamOptIn, DecryptionContext ctxStreamOptIn) = await EncryptionProcessor.DecryptAsync(
                new MemoryStream(cipherBytes),
                mockEncryptor.Object,
                CosmosDiagnosticsContext.Create(null),
                StreamOptIn(),
                CancellationToken.None);

            viaNewtonsoft.Position = 0;
            viaStreamOptIn.Position = 0;
            TestDoc fromNewtonsoft = TestCommon.FromStream<TestDoc>(viaNewtonsoft);
            TestDoc fromStreamOptIn = TestCommon.FromStream<TestDoc>(viaStreamOptIn);
            Assert.AreEqual(fromNewtonsoft, fromStreamOptIn, "Stream-opt-in legacy decrypt must produce the same plaintext as the Newtonsoft path.");
            Assert.AreEqual(ctxNewtonsoft.DecryptionInfoList.First().DataEncryptionKeyId, ctxStreamOptIn.DecryptionInfoList.First().DataEncryptionKeyId);
            CollectionAssert.AreEquivalent(
                ctxNewtonsoft.DecryptionInfoList.First().PathsDecrypted.ToList(),
                ctxStreamOptIn.DecryptionInfoList.First().PathsDecrypted.ToList(),
                "Stream-opt-in legacy decrypt must report the same decrypted paths as the Newtonsoft path.");
        }

        [TestMethod]
        public async Task LegacyDecrypt_StreamOptIn_DecryptionContext_ListsAllEncryptedPaths()
        {
            TestDoc doc = TestDoc.Create();
            Stream legacyEncrypted = await EncryptWithLegacy(doc);

            (_, DecryptionContext ctx) = await EncryptionProcessor.DecryptAsync(legacyEncrypted, mockEncryptor.Object, CosmosDiagnosticsContext.Create(null), StreamOptIn(), CancellationToken.None);

            Assert.IsNotNull(ctx);
            Assert.AreEqual(1, ctx.DecryptionInfoList.Count);
            DecryptionInfo info = ctx.DecryptionInfoList[0];
            Assert.AreEqual(DekId, info.DataEncryptionKeyId);
            CollectionAssert.AreEquivalent(TestDoc.PathsToEncrypt, info.PathsDecrypted.ToList());
        }

        [TestMethod]
        public async Task LegacyDecrypt_StreamOptIn_LargeDocument_RoundTrips()
        {
            // Forces a multi-kilobyte payload so the ArrayPool / TryGetBuffer path operates on a
            // realistic buffer size rather than just the prefix.
            TestDoc doc = TestDoc.Create();
            string padding = new string('a', 8 * 1024);
            doc.SensitiveDict = new Dictionary<string, string>
            {
                { "bulk1", padding },
                { "bulk2", padding },
                { "bulk3", padding },
            };

            Stream legacyEncrypted = await EncryptWithLegacy(doc);
            Assert.IsTrue(legacyEncrypted.Length > 16 * 1024, "Expected encrypted payload to be larger than 16KB for this test.");

            (Stream decrypted, DecryptionContext ctx) = await EncryptionProcessor.DecryptAsync(legacyEncrypted, mockEncryptor.Object, CosmosDiagnosticsContext.Create(null), StreamOptIn(), CancellationToken.None);

            Assert.IsNotNull(ctx);
            decrypted.Position = 0;
            TestDoc result = TestCommon.FromStream<TestDoc>(decrypted);
            Assert.AreEqual(doc, result);
        }

        [TestMethod]
        public async Task LegacyDecrypt_StreamOptIn_Repeated_RoundTrips()
        {
            // Detect/route is called repeatedly to confirm the ArrayPool rental and detector state
            // are robust across multiple invocations on independent streams.
            for (int i = 0; i < 16; i++)
            {
                TestDoc doc = TestDoc.Create();
                Stream legacyEncrypted = await EncryptWithLegacy(doc);
                (Stream decrypted, DecryptionContext ctx) = await EncryptionProcessor.DecryptAsync(legacyEncrypted, mockEncryptor.Object, CosmosDiagnosticsContext.Create(null), StreamOptIn(), CancellationToken.None);
                Assert.IsNotNull(ctx, $"iteration {i}: missing decryption context");
                decrypted.Position = 0;
                TestDoc result = TestCommon.FromStream<TestDoc>(decrypted);
                Assert.AreEqual(doc, result, $"iteration {i}: round-trip mismatch");
            }
        }

        [TestMethod]
        public async Task MdeDecrypt_StreamOptIn_ProducesSameResultAsNewtonsoftPath()
        {
            // Golden parity for MDE: detector fast-path decrypt must equal the original Newtonsoft path.
            TestDoc doc = TestDoc.Create();
            Stream mdeEncrypted = await EncryptionProcessor.EncryptAsync(doc.ToStream(), mockEncryptor.Object, CreateMdeOptions(), JsonProcessor.Newtonsoft, CosmosDiagnosticsContext.Create(null), CancellationToken.None);
            mdeEncrypted.Position = 0;
            byte[] cipherBytes = new byte[mdeEncrypted.Length];
            int read = mdeEncrypted.Read(cipherBytes, 0, cipherBytes.Length);
            Assert.AreEqual(cipherBytes.Length, read);

            (Stream viaNewtonsoft, DecryptionContext ctxNewtonsoft) = await EncryptionProcessor.DecryptAsync(
                new MemoryStream(cipherBytes),
                mockEncryptor.Object,
                CosmosDiagnosticsContext.Create(null),
                requestOptions: null,
                CancellationToken.None);

            (Stream viaStreamOptIn, DecryptionContext ctxStreamOptIn) = await EncryptionProcessor.DecryptAsync(
                new MemoryStream(cipherBytes),
                mockEncryptor.Object,
                CosmosDiagnosticsContext.Create(null),
                StreamOptIn(),
                CancellationToken.None);

            viaNewtonsoft.Position = 0;
            viaStreamOptIn.Position = 0;
            TestDoc fromNewtonsoft = TestCommon.FromStream<TestDoc>(viaNewtonsoft);
            TestDoc fromStreamOptIn = TestCommon.FromStream<TestDoc>(viaStreamOptIn);
            Assert.AreEqual(fromNewtonsoft, fromStreamOptIn);
            CollectionAssert.AreEquivalent(
                ctxNewtonsoft.DecryptionInfoList.First().PathsDecrypted.ToList(),
                ctxStreamOptIn.DecryptionInfoList.First().PathsDecrypted.ToList());
        }

        [TestMethod]
        public async Task Decrypt_StreamOptIn_UnknownAlgorithm_RoutesThroughJObjectFallbackForParity()
        {
            // Detector contract: unknown algorithm strings (anything that is neither the well-known
            // legacy nor MDE name) are classified as Unknown so the document routes through the
            // JObject peek path. This is what a non-opt-in caller would do for the same payload,
            // and the Stream-opt-in path must behave identically — including for unknown algorithm
            // strings. Asserting parity rather than a hard exception type prevents regressions in
            // case the underlying MdeEncryptionProcessor exception layer changes in the future.
            string json = "{\"id\":\"x\",\"_ei\":{\"_ea\":\"SomeFutureAlgorithm\",\"_en\":\"dek1\",\"_ed\":\"AAEC\",\"_ef\":3,\"_ep\":[\"/x\"]}}";
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);

            (bool optInThrew, Exception optInEx) = await TryDecryptToCompletionAsync(new MemoryStream(bytes), StreamOptIn());
            (bool baselineThrew, Exception baselineEx) = await TryDecryptToCompletionAsync(new MemoryStream(bytes), requestOptions: null);

            Assert.AreEqual(baselineThrew, optInThrew,
                $"Stream-opt-in diverged from baseline on unknown algorithm. baseline={baselineEx?.GetType().Name ?? "<none>"}, opt-in={optInEx?.GetType().Name ?? "<none>"}");
            if (baselineThrew)
            {
                Assert.AreEqual(baselineEx.GetType(), optInEx.GetType());
            }
        }

        [TestMethod]
        public async Task Decrypt_StreamOptIn_NoEiProperty_DetectorFastPaths_NotEncrypted()
        {
            // Document without _ei must be returned as-is with a null DecryptionContext, matching
            // the pre-existing contract validated by Decrypt_StreamSelection_FallbackWhenUnencrypted.
            string json = "{\"id\":\"42\",\"pk\":\"p1\",\"NonSensitive\":\"v\",\"nested\":{\"a\":1}}";
            MemoryStream input = new(System.Text.Encoding.UTF8.GetBytes(json));
            (Stream result, DecryptionContext ctx) = await EncryptionProcessor.DecryptAsync(input, mockEncryptor.Object, CosmosDiagnosticsContext.Create(null), StreamOptIn(), CancellationToken.None);
            Assert.IsNull(ctx);
            Assert.AreEqual(0, result.Position);
            Assert.AreEqual(input.Length, result.Length);
        }

        private static async Task<Stream> EncryptWithLegacy(TestDoc doc)
        {
            EncryptionOptions legacy = new()
            {
                DataEncryptionKeyId = DekId,
#pragma warning disable CS0618
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
#pragma warning restore CS0618
                PathsToEncrypt = TestDoc.PathsToEncrypt,
            };
            Stream encrypted = await EncryptionProcessor.EncryptAsync(doc.ToStream(), mockEncryptor.Object, legacy, JsonProcessor.Newtonsoft, CosmosDiagnosticsContext.Create(null), CancellationToken.None);
            encrypted.Position = 0;
            return encrypted;
        }

        private static async Task<byte[]> EncryptWithLegacyToBytes(TestDoc doc)
        {
            Stream encrypted = await EncryptWithLegacy(doc);
            byte[] bytes = new byte[encrypted.Length];
            int read = encrypted.Read(bytes, 0, bytes.Length);
            Assert.AreEqual(bytes.Length, read);
            return bytes;
        }

        private static ItemRequestOptions StreamOptIn()
        {
            return new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream },
                },
            };
        }

        // Seekable Stream that is *not* a MemoryStream — forces TryDetectAlgorithm onto the
        // ArrayPool<byte> fallback path instead of TryGetBuffer.
        internal sealed class NonMemoryStreamWrapper : Stream
        {
            private readonly byte[] buffer;
            private long position;

            public NonMemoryStreamWrapper(byte[] buffer)
            {
                this.buffer = buffer;
            }

            public override bool CanRead => true;

            public override bool CanSeek => true;

            public override bool CanWrite => false;

            public override long Length => this.buffer.Length;

            public override long Position
            {
                get => this.position;
                set => this.position = value;
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
                this.position = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => this.position + offset,
                    SeekOrigin.End => this.buffer.Length + offset,
                    _ => throw new ArgumentOutOfRangeException(nameof(origin)),
                };
                return this.position;
            }

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] src, int offset, int count) => throw new NotSupportedException();
        }
#endif
    }
}

