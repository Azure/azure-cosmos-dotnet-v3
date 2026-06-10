//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Regression tests for the preview08-compatibility findings that are not covered by
    /// <see cref="CrossProcessorCompatibilityTests"/>:
    ///  - C4: custom <see cref="Encryptor"/>/<see cref="DataEncryptionKey"/> subclasses written
    ///    against preview08 (no GetEncryptionKeyAsync / no buffer-based members) must keep
    ///    working and must NOT be silently bypassed on the MDE path.
    ///  - M1: integral literals beyond Int64 and non-finite doubles are rejected by both paths.
    ///  - M2: integral doubles decrypt with Newtonsoft-compatible text ("5.0", not "5") so the
    ///    TypeMarker does not flap between Double and Long across re-encrypt cycles.
    ///  - F-9: legacy-algorithm documents produce a clear error through the Stream decrypt path.
    ///  - F-10: a non-object _ei value is passed through unchanged (not an error).
    /// </summary>
    [TestClass]
    public class CompatibilityRegressionTests
    {
        private const string DekId = "legacyCompatDek";

        /// <summary>
        /// An Encryptor written against the preview08 surface: overrides ONLY
        /// EncryptAsync/DecryptAsync. GetEncryptionKeyAsync is intentionally NOT overridden.
        /// </summary>
        private sealed class Preview08StyleEncryptor : Encryptor
        {
            public int EncryptCalls;
            public int DecryptCalls;

            public override Task<byte[]> EncryptAsync(byte[] plainText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref this.EncryptCalls);
                Assert.AreEqual(DekId, dataEncryptionKeyId);
                return Task.FromResult(TestCommon.EncryptData(plainText));
            }

            public override Task<byte[]> DecryptAsync(byte[] cipherText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref this.DecryptCalls);
                Assert.AreEqual(DekId, dataEncryptionKeyId);
                return Task.FromResult(TestCommon.DecryptData(cipherText));
            }
        }

        /// <summary>
        /// A DataEncryptionKey written against the preview08 surface: overrides ONLY the
        /// array-based EncryptData/DecryptData. None of the buffer-based members added later
        /// are overridden.
        /// </summary>
        private sealed class Preview08StyleDataEncryptionKey : DataEncryptionKey
        {
            public int ArrayEncryptCalls;
            public int ArrayDecryptCalls;

            public override byte[] RawKey => null;

            public override string EncryptionAlgorithm => CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized;

            public override byte[] EncryptData(byte[] plainText)
            {
                Interlocked.Increment(ref this.ArrayEncryptCalls);
                return TestCommon.EncryptData(plainText);
            }

            public override byte[] DecryptData(byte[] cipherText)
            {
                Interlocked.Increment(ref this.ArrayDecryptCalls);
                return TestCommon.DecryptData(cipherText);
            }
        }

        /// <summary>
        /// An Encryptor that DOES override GetEncryptionKeyAsync, handing out a
        /// preview08-style DataEncryptionKey (array-based members only).
        /// </summary>
        private sealed class KeyAccessEncryptor : Encryptor
        {
            public readonly Preview08StyleDataEncryptionKey Dek = new ();

            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                Assert.AreEqual(DekId, dataEncryptionKeyId);
                return Task.FromResult<DataEncryptionKey>(this.Dek);
            }

            public override Task<byte[]> EncryptAsync(byte[] plainText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException("must not be called when GetEncryptionKeyAsync is available");
            }

            public override Task<byte[]> DecryptAsync(byte[] cipherText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException("must not be called when GetEncryptionKeyAsync is available");
            }
        }

        private static EncryptionOptions CreateOptions(IEnumerable<string> paths)
        {
            return new EncryptionOptions
            {
                DataEncryptionKeyId = DekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = new List<string>(paths),
            };
        }

        private static MemoryStream ToStream(string json)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(json));
        }

        // ---- C4: custom Encryptor dispatch ---------------------------------------------------

        [TestMethod]
        public async Task CustomEncryptor_WithoutKeyAccess_NewtonsoftPath_DispatchesThroughEncryptAsync()
        {
            Preview08StyleEncryptor encryptor = new ();
            string json = "{\"id\":\"1\",\"Sensitive\":\"secret value\",\"Plain\":5}";

            Stream encrypted = await EncryptionProcessor.EncryptAsync(
                ToStream(json),
                encryptor,
                CreateOptions(new[] { "/Sensitive" }),
                JsonProcessor.Newtonsoft,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Assert.AreEqual(1, encryptor.EncryptCalls, "custom EncryptAsync override must be invoked per encrypted property");

            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptAsync(
                encrypted,
                encryptor,
                new CosmosDiagnosticsContext(),
                requestOptions: null,
                CancellationToken.None);

            Assert.IsNotNull(context);
            Assert.AreEqual(1, encryptor.DecryptCalls, "custom DecryptAsync override must be invoked per encrypted property");

            using JsonDocument doc = JsonDocument.Parse(decrypted);
            Assert.AreEqual("secret value", doc.RootElement.GetProperty("Sensitive").GetString());
            Assert.AreEqual(5, doc.RootElement.GetProperty("Plain").GetInt32());
        }

        [TestMethod]
        public async Task CustomDataEncryptionKey_ArrayBasedOnly_NewtonsoftPath_RoundTrips()
        {
            KeyAccessEncryptor encryptor = new ();
            string json = "{\"id\":\"1\",\"Sensitive\":\"secret value\"}";

            Stream encrypted = await EncryptionProcessor.EncryptAsync(
                ToStream(json),
                encryptor,
                CreateOptions(new[] { "/Sensitive" }),
                JsonProcessor.Newtonsoft,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Assert.AreEqual(1, encryptor.Dek.ArrayEncryptCalls, "array-based EncryptData must be used for legacy DataEncryptionKey implementations");

            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptAsync(
                encrypted,
                encryptor,
                new CosmosDiagnosticsContext(),
                requestOptions: null,
                CancellationToken.None);

            Assert.IsNotNull(context);
            Assert.AreEqual(1, encryptor.Dek.ArrayDecryptCalls, "array-based DecryptData must be used (default buffer-based shim)");

            using JsonDocument doc = JsonDocument.Parse(decrypted);
            Assert.AreEqual("secret value", doc.RootElement.GetProperty("Sensitive").GetString());
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        public async Task CustomEncryptor_WithoutKeyAccess_StreamPath_ThrowsClearError()
        {
            Preview08StyleEncryptor encryptor = new ();
            string json = "{\"id\":\"1\",\"Sensitive\":\"secret value\"}";

            NotSupportedException ex = await Assert.ThrowsExceptionAsync<NotSupportedException>(() => EncryptionProcessor.EncryptAsync(
                ToStream(json),
                encryptor,
                CreateOptions(new[] { "/Sensitive" }),
                JsonProcessor.Stream,
                new CosmosDiagnosticsContext(),
                CancellationToken.None));

            StringAssert.Contains(ex.Message, nameof(Encryptor.GetEncryptionKeyAsync));
            Assert.AreEqual(0, encryptor.EncryptCalls, "Stream path must fail fast, not silently bypass the custom Encryptor");
        }

        [TestMethod]
        public async Task CustomEncryptor_WithoutKeyAccess_StreamDecrypt_ThrowsClearError()
        {
            // Encrypt with the supported (Newtonsoft) path first.
            Preview08StyleEncryptor encryptor = new ();
            string json = "{\"id\":\"1\",\"Sensitive\":\"secret value\"}";

            Stream encrypted = await EncryptionProcessor.EncryptAsync(
                ToStream(json),
                encryptor,
                CreateOptions(new[] { "/Sensitive" }),
                JsonProcessor.Newtonsoft,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            NotSupportedException ex = await Assert.ThrowsExceptionAsync<NotSupportedException>(() => EncryptionProcessor.DecryptStreamAsync(
                encrypted,
                encryptor,
                new CosmosDiagnosticsContext(),
                CancellationToken.None));

            StringAssert.Contains(ex.Message, nameof(Encryptor.GetEncryptionKeyAsync));
        }

        [TestMethod]
        public async Task CustomDataEncryptionKey_ArrayBasedOnly_StreamPath_RoundTrips()
        {
            // A legacy DataEncryptionKey works on the Stream path too, provided the Encryptor
            // exposes it via GetEncryptionKeyAsync: the buffer-based calls fall back to the
            // array-based implementation.
            KeyAccessEncryptor encryptor = new ();
            string json = "{\"id\":\"1\",\"Sensitive\":\"secret value\"}";

            Stream encrypted = await EncryptionProcessor.EncryptAsync(
                ToStream(json),
                encryptor,
                CreateOptions(new[] { "/Sensitive" }),
                JsonProcessor.Stream,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Assert.IsTrue(encryptor.Dek.ArrayEncryptCalls >= 1, "array-based EncryptData fallback must be used");

            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptStreamAsync(
                encrypted,
                encryptor,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Assert.IsNotNull(context);
            Assert.IsTrue(encryptor.Dek.ArrayDecryptCalls >= 1, "array-based DecryptData fallback must be used");

            using JsonDocument doc = JsonDocument.Parse(decrypted);
            Assert.AreEqual("secret value", doc.RootElement.GetProperty("Sensitive").GetString());
        }
#endif

        // ---- M1: non-finite doubles rejected on the Newtonsoft path ---------------------------

        [TestMethod]
        public async Task NewtonsoftEncrypt_InfinityProducingLiteral_Throws()
        {
            // 1e309 overflows double range; modern runtimes parse it as +Infinity which is not
            // representable in JSON. Must be rejected, matching the Stream path.
            Preview08StyleEncryptor encryptor = new ();
            string json = "{\"id\":\"1\",\"Big\":1e309}";

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => EncryptionProcessor.EncryptAsync(
                ToStream(json),
                encryptor,
                CreateOptions(new[] { "/Big" }),
                JsonProcessor.Newtonsoft,
                new CosmosDiagnosticsContext(),
                CancellationToken.None));
        }

#if NET8_0_OR_GREATER
        // ---- M2: integral double text parity ---------------------------------------------------

        [TestMethod]
        public async Task IntegralDouble_RoundTrip_PreservesDoubleTypeMarkerAcrossProcessors()
        {
            Moq.Mock<Encryptor> mockEncryptor = TestEncryptorFactory.CreateMde(DekId, out _);
            string json = "{\"id\":\"1\",\"D\":5.0}";

            // Encrypt (Newtonsoft) -> decrypt (Stream): text must stay an explicit double.
            Stream encrypted = await EncryptionProcessor.EncryptAsync(
                ToStream(json),
                mockEncryptor.Object,
                CreateOptions(new[] { "/D" }),
                JsonProcessor.Newtonsoft,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            byte[] cipher;
            using (JsonDocument encDoc = JsonDocument.Parse(CopyToArray(encrypted)))
            {
                cipher = Convert.FromBase64String(encDoc.RootElement.GetProperty("D").GetString());
                Assert.AreEqual((byte)3, cipher[0], "must be TypeMarker.Double after first encrypt");
            }

            encrypted.Position = 0;
            (Stream decrypted, _) = await EncryptionProcessor.DecryptStreamAsync(
                encrypted,
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            string decryptedJson = new StreamReader(decrypted).ReadToEnd();
            StringAssert.Contains(decryptedJson, "\"D\":5.0", "Stream decrypt must format integral doubles with an explicit decimal point (Newtonsoft parity)");

            // Re-encrypt the Stream-decrypted text (Newtonsoft) -> the value must classify as
            // Double again (no Long/Double TypeMarker flapping).
            Stream reEncrypted = await EncryptionProcessor.EncryptAsync(
                ToStream(decryptedJson),
                mockEncryptor.Object,
                CreateOptions(new[] { "/D" }),
                JsonProcessor.Newtonsoft,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            using (JsonDocument reDoc = JsonDocument.Parse(CopyToArray(reEncrypted)))
            {
                byte[] reCipher = Convert.FromBase64String(reDoc.RootElement.GetProperty("D").GetString());
                Assert.AreEqual((byte)3, reCipher[0], "TypeMarker must remain Double after a Stream-decrypt/re-encrypt cycle");
            }

            reEncrypted.Position = 0;
            (Stream decrypted2, _) = await EncryptionProcessor.DecryptStreamAsync(
                reEncrypted,
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            using JsonDocument finalDoc = JsonDocument.Parse(decrypted2);
            Assert.AreEqual(5.0, finalDoc.RootElement.GetProperty("D").GetDouble(), 0.0);
        }

        // ---- F-9: clear error for legacy-algorithm documents via the Stream decrypt API --------

        [TestMethod]
        public async Task StreamDecrypt_LegacyFormatVersionDocument_ThrowsActionableError()
        {
            Moq.Mock<Encryptor> mockEncryptor = TestEncryptorFactory.CreateMde(DekId, out _);

            // Hand-crafted legacy (_ef=2) envelope; MDE algorithm string so the adapter's
            // algorithm gate does not fire first and the format gate is exercised.
            string json = "{\"id\":\"1\",\"_ei\":{\"_ef\":2,\"_en\":\"" + DekId + "\",\"_ea\":\"" + CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized + "\",\"_ed\":\"AAEC\",\"_ep\":[]}}";

            NotSupportedException ex = await Assert.ThrowsExceptionAsync<NotSupportedException>(() => EncryptionProcessor.DecryptStreamAsync(
                ToStream(json),
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None));

            StringAssert.Contains(ex.Message, "legacy", "error must state the document uses the legacy algorithm");
            StringAssert.Contains(ex.Message, "Newtonsoft", "error must point the user to the Newtonsoft processor");
        }

        // ---- F-10: non-object _ei value passes through ------------------------------------------

        [TestMethod]
        public async Task StreamDecrypt_NonObjectEiValue_PassesDocumentThrough()
        {
            Moq.Mock<Encryptor> mockEncryptor = TestEncryptorFactory.CreateMde(DekId, out _);
            string json = "{\"id\":\"1\",\"_ei\":\"not an envelope\",\"Data\":\"plain\"}";

            (Stream output, DecryptionContext context) = await EncryptionProcessor.DecryptStreamAsync(
                ToStream(json),
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Assert.IsNull(context, "non-envelope _ei means the document is not encrypted");

            using JsonDocument doc = JsonDocument.Parse(output);
            Assert.AreEqual("not an envelope", doc.RootElement.GetProperty(Constants.EncryptedInfo).GetString(), "_ei user data must be preserved");
            Assert.AreEqual("plain", doc.RootElement.GetProperty("Data").GetString());
        }

        private static byte[] CopyToArray(Stream stream)
        {
            using MemoryStream ms = new ();
            stream.Position = 0;
            stream.CopyTo(ms);
            stream.Position = 0;
            return ms.ToArray();
        }
#endif
    }
}
