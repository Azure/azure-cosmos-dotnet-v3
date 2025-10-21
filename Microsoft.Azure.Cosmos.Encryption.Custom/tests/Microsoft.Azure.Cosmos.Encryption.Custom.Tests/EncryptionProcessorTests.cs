//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
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
        private static EncryptionOptions CreateMdeOptions(JsonProcessor processor) => new EncryptionOptions
        {
            DataEncryptionKeyId = DekId,
#pragma warning disable CS0618
            EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
#pragma warning restore CS0618
            PathsToEncrypt = TestDoc.PathsToEncrypt,
            JsonProcessor = processor,
        };

        [TestMethod]
        public async Task EncryptDecrypt_StreamProcessor_WithProvidedOutput()
        {
            TestDoc doc = TestDoc.Create();
            EncryptionOptions opts = CreateMdeOptions(JsonProcessor.Stream);
            CosmosDiagnosticsContext diagEncrypt = CosmosDiagnosticsContext.Create(null);
            MemoryStream encrypted = new();
            await EncryptionProcessor.EncryptAsync(doc.ToStream(), encrypted, mockEncryptor.Object, opts, diagEncrypt, CancellationToken.None);
            encrypted.Position = 0;
            Assert.AreEqual(1, diagEncrypt.Scopes.Count(s => s.StartsWith(EncryptionDiagnostics.ScopeEncryptModeSelectionPrefix + JsonProcessor.Stream)), "Expected a single Stream selection scope for encrypt");
            Assert.IsFalse(diagEncrypt.Scopes.Any(s => s.StartsWith(EncryptionDiagnostics.ScopeEncryptModeSelectionPrefix + JsonProcessor.Newtonsoft)));

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
            Assert.AreEqual(1, diagDecrypt.Scopes.Count(s => s.StartsWith(EncryptionDiagnostics.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream)), "Expected a single Stream selection scope for decrypt");
            Assert.IsFalse(diagDecrypt.Scopes.Any(s => s.StartsWith(EncryptionDiagnostics.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft)));
        }

    [TestMethod]
    public async Task Encrypt_NewtonsoftProcessor_TracksScope()
    {
        TestDoc doc = TestDoc.Create();
        EncryptionOptions opts = CreateMdeOptions(JsonProcessor.Newtonsoft);
        CosmosDiagnosticsContext diagEncrypt = CosmosDiagnosticsContext.Create(null);
        Stream encrypted = await EncryptionProcessor.EncryptAsync(doc.ToStream(), mockEncryptor.Object, opts, diagEncrypt, CancellationToken.None);

    Assert.AreEqual(1, diagEncrypt.Scopes.Count(s => s.StartsWith(EncryptionDiagnostics.ScopeEncryptModeSelectionPrefix + JsonProcessor.Newtonsoft)), "Expected a single Newtonsoft selection scope for encrypt");
    Assert.IsFalse(diagEncrypt.Scopes.Any(s => s.StartsWith(EncryptionDiagnostics.ScopeEncryptModeSelectionPrefix + JsonProcessor.Stream)));

        encrypted.Dispose();
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
            Assert.AreEqual(1, ctxDiag.Scopes.Count(s => s.StartsWith(EncryptionDiagnostics.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream)), "Expected a single Stream selection scope when falling back for unencrypted payload");
            Assert.IsFalse(ctxDiag.Scopes.Any(s => s.StartsWith(EncryptionDiagnostics.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft)));
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
            Stream legacyEncrypted = await EncryptionProcessor.EncryptAsync(doc.ToStream(), mockEncryptor.Object, legacy, CosmosDiagnosticsContext.Create(null), CancellationToken.None);
            legacyEncrypted.Position = 0;

            ItemRequestOptions opts = new() { Properties = new Dictionary<string, object> { { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream } } };
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
            Stream legacyEncrypted = await EncryptionProcessor.EncryptAsync(doc.ToStream(), mockEncryptor.Object, legacy, CosmosDiagnosticsContext.Create(null), CancellationToken.None);
            legacyEncrypted.Position = 0;

            ItemRequestOptions opts = new() { Properties = new Dictionary<string, object> { { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream } } };
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
        public async Task Encrypt_LegacyAlgorithm_StreamProcessorOverride_Throws()
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
            ItemRequestOptions ro = new() { Properties = new Dictionary<string, object> { { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream } } };
            CosmosDiagnosticsContext diag = CosmosDiagnosticsContext.Create(null);
            try
            {
                await EncryptionProcessor.EncryptAsync(doc.ToStream(), mockEncryptor.Object, legacy, ro, diag, CancellationToken.None);
                Assert.Fail("Expected NotSupportedException for legacy algorithm with Stream processor override.");
            }
            catch (NotSupportedException ex)
            {
                Assert.IsTrue(ex.Message.IndexOf("not supported", StringComparison.OrdinalIgnoreCase) >= 0, $"Unexpected message: {ex.Message}");
            }
        }
#endif
    }
}
