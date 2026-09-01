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
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Regression tests for released subclass compatibility and JSON processor parity.
    /// </summary>
    [TestClass]
    public class CompatibilityRegressionTests
    {
        private const string DekId = "legacyCompatDek";

        private sealed class Preview08StyleEncryptor : Encryptor
        {
            public int EncryptCalls;
            public int DecryptCalls;

            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException("This legacy encryptor does not support direct key access.");
            }

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

        private class ReleasedStyleDataEncryptionKey : DataEncryptionKey
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

            public override int EncryptData(byte[] plainText, int plainTextOffset, int plainTextLength, byte[] output, int outputOffset)
            {
                Buffer.BlockCopy(plainText, plainTextOffset, output, outputOffset, plainTextLength);
                return plainTextLength;
            }

            public override int DecryptData(byte[] cipherText, int cipherTextOffset, int cipherTextLength, byte[] output, int outputOffset)
            {
                Buffer.BlockCopy(cipherText, cipherTextOffset, output, outputOffset, cipherTextLength);
                return cipherTextLength;
            }

            public override int GetEncryptByteCount(int plainTextLength) => plainTextLength;

            public override int GetDecryptByteCount(int cipherTextLength) => cipherTextLength;
        }

        private sealed class KeyAccessEncryptor : Encryptor, IDataEncryptionKeyAccessor
        {
            public readonly ReleasedStyleDataEncryptionKey Dek = new ();

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

        private sealed class OverpredictingDataEncryptionKey : DataEncryptionKey, IDataEncryptionKeyBuffer
        {
            public override byte[] RawKey => null;

            public override string EncryptionAlgorithm => CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized;

            public override byte[] EncryptData(byte[] plainText)
            {
                return (byte[])plainText.Clone();
            }

            public override int EncryptData(
                byte[] plainText,
                int plainTextOffset,
                int plainTextLength,
                byte[] output,
                int outputOffset)
            {
                Buffer.BlockCopy(plainText, plainTextOffset, output, outputOffset, plainTextLength);
                return plainTextLength;
            }

            public override int GetEncryptByteCount(int plainTextLength)
            {
                return plainTextLength + 8;
            }

            public override byte[] DecryptData(byte[] cipherText)
            {
                return (byte[])cipherText.Clone();
            }

            public override int DecryptData(
                byte[] cipherText,
                int cipherTextOffset,
                int cipherTextLength,
                byte[] output,
                int outputOffset)
            {
                Buffer.BlockCopy(cipherText, cipherTextOffset, output, outputOffset, cipherTextLength);
                return cipherTextLength;
            }

            public override int GetDecryptByteCount(int cipherTextLength)
            {
                return cipherTextLength;
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

        private static Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            JsonProcessor jsonProcessor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            EncryptionItemRequestOptions requestOptions = new ()
            {
                EncryptionOptions = encryptionOptions,
            };
#if NET8_0_OR_GREATER
            requestOptions.Properties = new Dictionary<string, object>
            {
                { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, jsonProcessor.ToString() },
            };
#else
            Assert.AreEqual(JsonProcessor.Newtonsoft, jsonProcessor);
#endif
            return EncryptionProcessor.EncryptAsync(
                input,
                encryptor,
                requestOptions,
                diagnosticsContext,
                cancellationToken);
        }

#if NET8_0_OR_GREATER
        private static Task<(Stream, DecryptionContext)> DecryptStreamAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return EncryptionProcessor.DecryptAsync(
                input,
                encryptor,
                JsonProcessor.Stream,
                legacyFallback: false,
                diagnosticsContext,
                cancellationToken);
        }
#endif

        [TestMethod]
        public async Task CustomEncryptor_WithoutKeyAccess_NewtonsoftPath_DispatchesThroughEncryptAsync()
        {
            Preview08StyleEncryptor encryptor = new ();
            string json = "{\"id\":\"1\",\"Sensitive\":\"secret value\",\"Plain\":5}";

            Stream encrypted = await EncryptAsync(
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

            Stream encrypted = await EncryptAsync(
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
        public void CustomDataEncryptionKey_OverpredictedCiphertextLength_ReturnsActualLength()
        {
            byte[] plainText = Encoding.UTF8.GetBytes("secret");
            MdeEncryptor mdeEncryptor = new ();
            OverpredictingDataEncryptionKey encryptionKey = new ();

            byte[] unpooled = mdeEncryptor.Encrypt(
                encryptionKey,
                TypeMarker.String,
                plainText,
                plainText.Length);

            Assert.AreEqual(plainText.Length + 1, unpooled.Length);

            using ArrayPoolManager pool = new ();
            (byte[] pooled, int pooledLength) = mdeEncryptor.Encrypt(
                encryptionKey,
                TypeMarker.String,
                plainText,
                plainText.Length,
                pool);

            Assert.AreEqual(plainText.Length + 1, pooledLength);
            Assert.AreEqual((byte)TypeMarker.String, pooled[0]);
        }

        [TestMethod]
        public async Task CustomEncryptor_WithoutKeyAccess_StreamPath_ThrowsClearError()
        {
            Preview08StyleEncryptor encryptor = new ();
            string json = "{\"id\":\"1\",\"Sensitive\":\"secret value\"}";

            NotSupportedException ex = await Assert.ThrowsExceptionAsync<NotSupportedException>(() => EncryptAsync(
                ToStream(json),
                encryptor,
                CreateOptions(new[] { "/Sensitive" }),
                JsonProcessor.Stream,
                new CosmosDiagnosticsContext(),
                CancellationToken.None));

            StringAssert.Contains(ex.Message, "built-in encryption key accessor");
            Assert.AreEqual(0, encryptor.EncryptCalls, "Stream path must fail fast, not silently bypass the custom Encryptor");
        }

        [TestMethod]
        public async Task CustomEncryptor_WithoutKeyAccess_StreamDecrypt_ThrowsClearError()
        {
            Preview08StyleEncryptor encryptor = new ();
            string json = "{\"id\":\"1\",\"Sensitive\":\"secret value\"}";

            Stream encrypted = await EncryptAsync(
                ToStream(json),
                encryptor,
                CreateOptions(new[] { "/Sensitive" }),
                JsonProcessor.Newtonsoft,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            NotSupportedException ex = await Assert.ThrowsExceptionAsync<NotSupportedException>(() => DecryptStreamAsync(
                encrypted,
                encryptor,
                new CosmosDiagnosticsContext(),
                CancellationToken.None));

            StringAssert.Contains(ex.Message, "built-in encryption key accessor");
        }

        [TestMethod]
        public async Task CustomDataEncryptionKey_ArrayBasedOnly_StreamPath_RoundTrips()
        {
            KeyAccessEncryptor encryptor = new ();
            string json = "{\"id\":\"1\",\"Sensitive\":\"secret value\"}";

            Stream encrypted = await EncryptAsync(
                ToStream(json),
                encryptor,
                CreateOptions(new[] { "/Sensitive" }),
                JsonProcessor.Stream,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Assert.IsTrue(encryptor.Dek.ArrayEncryptCalls >= 1, "array-based EncryptData fallback must be used");

            (Stream decrypted, DecryptionContext context) = await DecryptStreamAsync(
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
    }
}
