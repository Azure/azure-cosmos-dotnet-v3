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

    [TestClass]
    public class InternalCapabilityTests
    {
        private const string DekId = "capabilityDek";
        private const string SensitiveValue = "secret value";
        private const string Document = "{\"id\":\"1\",\"Sensitive\":\"secret value\",\"Plain\":5}";

        [TestMethod]
        public void MdeAndLegacyAlgorithms_ExposeOnlyIntendedBufferCapabilities()
        {
            MdeEncryptionAlgorithm mdeAlgorithm = CreateMdeAlgorithm();
            Assert.IsTrue(mdeAlgorithm is IDataEncryptionKeyBuffer);

#pragma warning disable CS0618
            DataEncryptionKey legacyAlgorithm = DataEncryptionKey.Create(
                EnumerableBytes(32),
                CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized);
#pragma warning restore CS0618

            Assert.IsFalse(legacyAlgorithm is IDataEncryptionKeyBuffer);
        }

        [TestMethod]
        public void BufferCapability_UsesActualInitializedLengths()
        {
            OverpredictingBufferKey key = new ();
            MdeEncryptor encryptor = new ();
            byte[] plainText = Encoding.UTF8.GetBytes(SensitiveValue);

            byte[] encrypted = encryptor.Encrypt(key, TypeMarker.String, plainText, plainText.Length);
            Assert.AreEqual(plainText.Length + 1, encrypted.Length);

            using ArrayPoolManager pool = new ();
            (byte[] pooledEncrypted, int pooledEncryptedLength) = encryptor.Encrypt(
                key,
                TypeMarker.String,
                plainText,
                plainText.Length,
                pool);

            Assert.AreEqual(plainText.Length + 1, pooledEncryptedLength);

            (byte[] pooledPlainText, int pooledPlainTextLength) = encryptor.Decrypt(
                key,
                pooledEncrypted,
                pooledEncryptedLength,
                pool);

            Assert.AreEqual(plainText.Length, pooledPlainTextLength);
            CollectionAssert.AreEqual(plainText, pooledPlainText.AsSpan(0, pooledPlainTextLength).ToArray());
        }

        [TestMethod]
        public void BufferCapability_RejectsLengthBeyondDeclaredInitializedRange()
        {
            OverpredictingBufferKey key = new (underpredictDecrypt: true);
            MdeEncryptor encryptor = new ();
            byte[] cipherText = new byte[] { (byte)TypeMarker.String, 1, 2, 3, 4 };

            using ArrayPoolManager pool = new ();
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => encryptor.Decrypt(key, cipherText, cipherText.Length, pool));

            StringAssert.Contains(exception.Message, "wrote more plainText");
        }

        [TestMethod]
        [DynamicData(nameof(JsonProcessors), DynamicDataSourceType.Method)]
        public async Task EncryptorWithoutAccessor_UsesExactPublicArrays(int jsonProcessorValue)
        {
            JsonProcessor jsonProcessor = (JsonProcessor)jsonProcessorValue;
            PublicArrayEncryptor encryptor = new ();

            Stream encrypted = await EncryptAsync(jsonProcessor, encryptor, CancellationToken.None);
            Assert.AreEqual(1, encryptor.EncryptCalls);
            Assert.AreEqual(Encoding.UTF8.GetByteCount(SensitiveValue), encryptor.LastPlainTextLength);

            (Stream decrypted, DecryptionContext context) = await DecryptAsync(jsonProcessor, encrypted, encryptor);

            Assert.IsNotNull(context);
            Assert.AreEqual(1, encryptor.DecryptCalls);
            AssertRoundTrip(decrypted);
        }

        [TestMethod]
        [DynamicData(nameof(JsonProcessors), DynamicDataSourceType.Method)]
        public async Task DataEncryptionKeyWithoutBuffer_UsesExactPublicArrays(int jsonProcessorValue)
        {
            JsonProcessor jsonProcessor = (JsonProcessor)jsonProcessorValue;
            ArrayKeyAccessorEncryptor encryptor = new ();

            Stream encrypted = await EncryptAsync(jsonProcessor, encryptor, CancellationToken.None);
            Assert.AreEqual(1, encryptor.Key.EncryptCalls);
            Assert.AreEqual(Encoding.UTF8.GetByteCount(SensitiveValue), encryptor.Key.LastPlainTextLength);

            (Stream decrypted, DecryptionContext context) = await DecryptAsync(jsonProcessor, encrypted, encryptor);

            Assert.IsNotNull(context);
            Assert.AreEqual(1, encryptor.Key.DecryptCalls);
            AssertRoundTrip(decrypted);
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        public async Task EncryptorWithoutAccessor_Stream_PropagatesCancellation()
        {
            PublicArrayEncryptor encryptor = new ();
            using CancellationTokenSource cancellation = new ();
            cancellation.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => EncryptAsync(JsonProcessor.Stream, encryptor, cancellation.Token));

            Assert.AreEqual(1, encryptor.EncryptCalls);
        }

        [TestMethod]
        public async Task EncryptorWithoutAccessor_StreamDecryptableItem_UsesPublicArrays()
        {
            PublicArrayEncryptor encryptor = new ();
            Stream encrypted = await EncryptAsync(JsonProcessor.Stream, encryptor, CancellationToken.None);

            await using StreamDecryptableItem item = new (encrypted, encryptor, new JsonCosmosSerializer());
            (JsonElement result, DecryptionContext context) = await item.GetItemAsync<JsonElement>();

            Assert.IsNotNull(context);
            Assert.AreEqual(SensitiveValue, result.GetProperty("Sensitive").GetString());
            Assert.AreEqual(1, encryptor.DecryptCalls);
        }

        [TestMethod]
        public async Task EncryptorWithoutAccessor_FeedStream_UsesPublicArrays()
        {
            PublicArrayEncryptor encryptor = new ();
            using Stream encrypted = await EncryptAsync(JsonProcessor.Stream, encryptor, CancellationToken.None);
            using StreamReader reader = new (encrypted);
            string encryptedDocument = await reader.ReadToEndAsync();
            using MemoryStream feed = new (Encoding.UTF8.GetBytes(
                $"{{\"Documents\":[{encryptedDocument}],\"_count\":1}}"));

            await new StreamProcessor().DecryptJsonArrayStreamInPlaceAsync(
                feed,
                encryptor,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            using JsonDocument result = JsonDocument.Parse(feed);
            Assert.AreEqual(
                SensitiveValue,
                result.RootElement.GetProperty("Documents")[0].GetProperty("Sensitive").GetString());
            Assert.AreEqual(1, encryptor.DecryptCalls);
        }
#endif

        public static IEnumerable<object[]> JsonProcessors()
        {
            yield return new object[] { (int)JsonProcessor.Newtonsoft };
#if NET8_0_OR_GREATER
            yield return new object[] { (int)JsonProcessor.Stream };
#endif
        }

        private static Task<Stream> EncryptAsync(
            JsonProcessor jsonProcessor,
            Encryptor encryptor,
            CancellationToken cancellationToken)
        {
            EncryptionItemRequestOptions requestOptions = new ()
            {
                EncryptionOptions = new EncryptionOptions
                {
                    DataEncryptionKeyId = DekId,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                    PathsToEncrypt = new List<string> { "/Sensitive" },
                },
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
                new MemoryStream(Encoding.UTF8.GetBytes(Document)),
                encryptor,
                requestOptions,
                new CosmosDiagnosticsContext(),
                cancellationToken);
        }

        private static Task<(Stream, DecryptionContext)> DecryptAsync(
            JsonProcessor jsonProcessor,
            Stream encrypted,
            Encryptor encryptor)
        {
#if NET8_0_OR_GREATER
            if (jsonProcessor == JsonProcessor.Stream)
            {
                return EncryptionProcessor.DecryptAsync(
                    encrypted,
                    encryptor,
                    JsonProcessor.Stream,
                    legacyFallback: false,
                    new CosmosDiagnosticsContext(),
                    CancellationToken.None);
            }
#endif

            return EncryptionProcessor.DecryptAsync(
                encrypted,
                encryptor,
                new CosmosDiagnosticsContext(),
                requestOptions: null,
                CancellationToken.None);
        }

        private static void AssertRoundTrip(Stream decrypted)
        {
            using JsonDocument document = JsonDocument.Parse(decrypted);
            Assert.AreEqual(SensitiveValue, document.RootElement.GetProperty("Sensitive").GetString());
            Assert.AreEqual(5, document.RootElement.GetProperty("Plain").GetInt32());
            Assert.IsFalse(document.RootElement.TryGetProperty(Constants.EncryptedInfo, out _));
        }

        private static MdeEncryptionAlgorithm CreateMdeAlgorithm()
        {
            byte[] rawKey = EnumerableBytes(32);
            Microsoft.Data.Encryption.Cryptography.PlaintextDataEncryptionKey key = new (DekId, rawKey);
            return new MdeEncryptionAlgorithm(
                rawKey,
                key,
                Data.Encryption.Cryptography.EncryptionType.Randomized);
        }

        private static byte[] EnumerableBytes(int length)
        {
            byte[] bytes = new byte[length];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(i + 1);
            }

            return bytes;
        }

        private class PublicArrayEncryptor : Encryptor
        {
            public int EncryptCalls { get; private set; }

            public int DecryptCalls { get; private set; }

            public int LastPlainTextLength { get; private set; }

            public override Task<byte[]> EncryptAsync(
                byte[] plainText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                this.EncryptCalls++;
                this.LastPlainTextLength = plainText.Length;
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(TestCommon.EncryptData(plainText));
            }

            public override Task<byte[]> DecryptAsync(
                byte[] cipherText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                this.DecryptCalls++;
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(TestCommon.DecryptData(cipherText));
            }
        }

#if NET8_0_OR_GREATER
        private sealed class JsonCosmosSerializer : CosmosSerializer
        {
            public override T FromStream<T>(Stream stream)
            {
                return JsonSerializer.Deserialize<T>(stream);
            }

            public override Stream ToStream<T>(T input)
            {
                MemoryStream stream = new ();
                JsonSerializer.Serialize(stream, input);
                stream.Position = 0;
                return stream;
            }
        }
#endif

        private sealed class ArrayKeyAccessorEncryptor : PublicArrayEncryptor, IDataEncryptionKeyAccessor
        {
            public ArrayOnlyKey Key { get; } = new ();

            public Task<DataEncryptionKey> GetEncryptionKeyAsync(
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<DataEncryptionKey>(this.Key);
            }
        }

        private sealed class ArrayOnlyKey : DataEncryptionKey
        {
            public int EncryptCalls { get; private set; }

            public int DecryptCalls { get; private set; }

            public int LastPlainTextLength { get; private set; }

            public override byte[] RawKey => null;

            public override string EncryptionAlgorithm => CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized;

            public override byte[] EncryptData(byte[] plainText)
            {
                this.EncryptCalls++;
                this.LastPlainTextLength = plainText.Length;
                return TestCommon.EncryptData(plainText);
            }

            public override byte[] DecryptData(byte[] cipherText)
            {
                this.DecryptCalls++;
                return TestCommon.DecryptData(cipherText);
            }

        }

        private sealed class OverpredictingBufferKey : DataEncryptionKey, IDataEncryptionKeyBuffer
        {
            private readonly bool underpredictDecrypt;

            public OverpredictingBufferKey(bool underpredictDecrypt = false)
            {
                this.underpredictDecrypt = underpredictDecrypt;
            }

            public override byte[] RawKey => null;

            public override string EncryptionAlgorithm => CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized;

            public override byte[] EncryptData(byte[] plainText)
            {
                throw new AssertFailedException("The buffer capability should be preferred.");
            }

            public override byte[] DecryptData(byte[] cipherText)
            {
                throw new AssertFailedException("The buffer capability should be preferred.");
            }

            public int EncryptData(
                byte[] plainText,
                int plainTextOffset,
                int plainTextLength,
                byte[] output,
                int outputOffset)
            {
                Buffer.BlockCopy(plainText, plainTextOffset, output, outputOffset, plainTextLength);
                return plainTextLength;
            }

            public int GetEncryptByteCount(int plainTextLength)
            {
                return plainTextLength + 8;
            }

            public int DecryptData(
                byte[] cipherText,
                int cipherTextOffset,
                int cipherTextLength,
                byte[] output,
                int outputOffset)
            {
                Buffer.BlockCopy(cipherText, cipherTextOffset, output, outputOffset, cipherTextLength);
                return cipherTextLength;
            }

            public int GetDecryptByteCount(int cipherTextLength)
            {
                return this.underpredictDecrypt ? cipherTextLength - 1 : cipherTextLength + 8;
            }
        }
    }
}
