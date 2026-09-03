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
        private const string NumericFidelityDocument =
            "{\"id\":\"1\",\"Sensitive\":\"secret value\",\"HighPrecision\":1234567890.1234567890123456789,\"TrailingZero\":42.5000,\"Exponent\":6.022e+23}";

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
        public void BufferCapability_RejectsNegativeEncryptLength()
        {
            OverpredictingBufferKey key = new (negativeEncryptPrediction: true);
            MdeEncryptor encryptor = new ();
            byte[] plainText = Encoding.UTF8.GetBytes(SensitiveValue);

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => encryptor.Encrypt(key, TypeMarker.String, plainText, plainText.Length));

            StringAssert.Contains(exception.Message, nameof(IDataEncryptionKeyBuffer.GetEncryptByteCount));
        }

        [TestMethod]
        public void BufferCapability_RejectsCipherTextLengthBeyondDeclaredInitializedRange()
        {
            OverpredictingBufferKey key = new (underpredictEncrypt: true);
            MdeEncryptor encryptor = new ();
            byte[] plainText = Encoding.UTF8.GetBytes(SensitiveValue);

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => encryptor.Encrypt(key, TypeMarker.String, plainText, plainText.Length));

            StringAssert.Contains(exception.Message, "wrote more cipherText");
        }

        [TestMethod]
        public void BufferCapability_RejectsNegativeDecryptLength()
        {
            OverpredictingBufferKey key = new (negativeDecryptPrediction: true);
            MdeEncryptor encryptor = new ();
            byte[] cipherText = new byte[] { (byte)TypeMarker.String, 1, 2, 3, 4 };

            using ArrayPoolManager pool = new ();
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => encryptor.Decrypt(key, cipherText, cipherText.Length, pool));

            StringAssert.Contains(exception.Message, nameof(IDataEncryptionKeyBuffer.GetDecryptByteCount));
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
        [DataTestMethod]
        [DynamicData(nameof(NumericTokens), DynamicDataSourceType.Method)]
        public async Task EncryptorWithoutAccessor_StreamEncrypt_PreservesUntouchedNumericToken(
            string propertyName,
            string expectedRawText)
        {
            PublicArrayEncryptor encryptor = new ();

            using Stream encrypted = await EncryptAsync(
                JsonProcessor.Stream,
                encryptor,
                CancellationToken.None,
                NumericFidelityDocument);
            string encryptedJson = await ReadToEndAsync(encrypted);

            AssertUntouchedNumericToken(encryptedJson, propertyName, expectedRawText);
            AssertCiphertextFraming(encryptedJson);
        }

        [DataTestMethod]
        [DynamicData(nameof(NumericTokens), DynamicDataSourceType.Method)]
        public async Task AccessorEncrypt_PublicFallbackDecrypt_PreservesUntouchedNumericToken(
            string propertyName,
            string expectedRawText)
        {
            ArrayKeyAccessorEncryptor accessorEncryptor = new ();
            using Stream encrypted = await EncryptAsync(
                JsonProcessor.Stream,
                accessorEncryptor,
                CancellationToken.None,
                NumericFidelityDocument);
            string encryptedJson = await ReadToEndAsync(encrypted);
            AssertCiphertextFraming(encryptedJson);

            PublicArrayEncryptor publicDecryptor = new ();
            (Stream decrypted, DecryptionContext context) = await DecryptAsync(
                JsonProcessor.Stream,
                ToStream(encryptedJson),
                publicDecryptor);

            Assert.IsNotNull(context);
            Assert.AreEqual(1, publicDecryptor.DecryptCalls);
            using (decrypted)
            {
                AssertUntouchedNumericToken(
                    await ReadToEndAsync(decrypted),
                    propertyName,
                    expectedRawText);
            }
        }

        [TestMethod]
        public async Task PublicFallbackEncrypt_AccessorDecrypt_InteroperatesWithExactCiphertextFraming()
        {
            PublicArrayEncryptor publicEncryptor = new ();
            using Stream encrypted = await EncryptAsync(
                JsonProcessor.Stream,
                publicEncryptor,
                CancellationToken.None);
            string encryptedJson = await ReadToEndAsync(encrypted);
            AssertCiphertextFraming(encryptedJson);

            ArrayKeyAccessorEncryptor accessorDecryptor = new ();
            (Stream decrypted, DecryptionContext context) = await DecryptAsync(
                JsonProcessor.Stream,
                ToStream(encryptedJson),
                accessorDecryptor);

            Assert.IsNotNull(context);
            Assert.AreEqual(1, accessorDecryptor.Key.DecryptCalls);
            using (decrypted)
            {
                AssertRoundTrip(decrypted);
            }
        }

        [TestMethod]
        public async Task AccessorReturningNull_StreamEncrypt_ThrowsClearError()
        {
            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => EncryptAsync(
                    JsonProcessor.Stream,
                    new NullKeyAccessorEncryptor(),
                    CancellationToken.None));

            StringAssert.Contains(exception.Message, "returned null");
        }

        [TestMethod]
        public async Task AccessorReturningNull_StreamDecrypt_ThrowsClearError()
        {
            using Stream encrypted = await EncryptAsync(
                JsonProcessor.Stream,
                new ArrayKeyAccessorEncryptor(),
                CancellationToken.None);

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => DecryptAsync(
                    JsonProcessor.Stream,
                    encrypted,
                    new NullKeyAccessorEncryptor()));

            StringAssert.Contains(exception.Message, "returned null");
        }

        [TestMethod]
        public async Task EncryptorWithoutAccessor_StreamEncrypt_InputReadHonorsCancellation()
        {
            PublicArrayEncryptor encryptor = new ();
            using CancellationBlockingStream input = new (
                Array.Empty<byte>(),
                blockAfterPositionResetCount: 0);
            using CancellationTokenSource cancellation = new ();
            EncryptionItemRequestOptions requestOptions = RequestOptionsOverrideHelper.Create(
                new EncryptionOptions
                {
                    DataEncryptionKeyId = DekId,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                    PathsToEncrypt = new List<string> { "/Sensitive" },
                },
                JsonProcessor.Stream);

            Task operation = EncryptionProcessor.EncryptAsync(
                input,
                encryptor,
                requestOptions,
                new CosmosDiagnosticsContext(),
                cancellation.Token);

            await AssertPublicFallbackInputReadCancellationAsync(
                operation,
                input,
                cancellation,
                encryptor);
        }

        [TestMethod]
        public async Task EncryptorWithoutAccessor_StreamDecrypt_InputReadHonorsCancellation()
        {
            using Stream encrypted = await EncryptAsync(
                JsonProcessor.Stream,
                new ArrayKeyAccessorEncryptor(),
                CancellationToken.None);
            byte[] encryptedDocument = Encoding.UTF8.GetBytes(await ReadToEndAsync(encrypted));

            PublicArrayEncryptor encryptor = new ();
            using CancellationBlockingStream input = new (
                encryptedDocument,
                blockAfterPositionResetCount: 2);
            using CancellationTokenSource cancellation = new ();

            Task operation = EncryptionProcessor.DecryptAsync(
                input,
                encryptor,
                JsonProcessor.Stream,
                legacyFallback: false,
                new CosmosDiagnosticsContext(),
                cancellation.Token);

            await AssertPublicFallbackInputReadCancellationAsync(
                operation,
                input,
                cancellation,
                encryptor);
        }

        [TestMethod]
        public async Task EncryptorWithoutAccessor_Stream_PropagatesCancellation()
        {
            PublicArrayEncryptor encryptor = new ();
            using CancellationTokenSource cancellation = new ();
            cancellation.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => EncryptAsync(JsonProcessor.Stream, encryptor, cancellation.Token));

            Assert.AreEqual(0, encryptor.EncryptCalls);
        }

        [DataTestMethod]
        [DataRow("{\"id\":\"1\",\"Plain\":5}", DisplayName = "Encrypt configured path absent")]
        [DataRow("{\"id\":\"1\",\"Sensitive\":null,\"Plain\":5}", DisplayName = "Encrypt configured path null")]
        public async Task PublicFallbackStream_PreCanceledEncrypt_WithNoEncryptableValue_ThrowsWithoutCryptoOrOutput(
            string document)
        {
            PublicArrayEncryptor encryptor = new (ignoreCancellation: true);
            using MemoryStream output = new ();
            using CancellationTokenSource cancellation = new ();
            cancellation.Cancel();

            await AssertCanceledAsync(
                () => EncryptToOutputAsync(
                    document,
                    output,
                    encryptor,
                    cancellation.Token),
                cancellation.Token);

            Assert.AreEqual(0, encryptor.EncryptCalls);
            Assert.AreEqual(0, encryptor.DecryptCalls);
            Assert.AreEqual(0, output.Length);
        }

        [DataTestMethod]
        [DataRow("{\"id\":\"1\",\"Plain\":5}", DisplayName = "Decrypt configured path absent")]
        [DataRow("{\"id\":\"1\",\"Sensitive\":null,\"Plain\":5}", DisplayName = "Decrypt configured path null")]
        public async Task PublicFallbackStream_PreCanceledDecrypt_WithNoEncryptedValue_ThrowsWithoutCryptoOrOutput(
            string document)
        {
            byte[] encryptedDocument = await CreateEncryptedDocumentAsync(document);
            PublicArrayEncryptor encryptor = new (ignoreCancellation: true);
            using CancellationIgnoringMemoryStream input = new (encryptedDocument);
            Stream decrypted = null;
            using CancellationTokenSource cancellation = new ();
            cancellation.Cancel();

            try
            {
                await AssertCanceledAsync(
                    async () => (decrypted, _) = await DecryptFromStreamAsync(
                        input,
                        encryptor,
                        cancellation.Token),
                    cancellation.Token);
            }
            finally
            {
                decrypted?.Dispose();
            }

            Assert.AreEqual(0, encryptor.EncryptCalls);
            Assert.AreEqual(0, encryptor.DecryptCalls);
            Assert.IsNull(decrypted);
        }

        [TestMethod]
        public async Task PublicFallbackStream_PreCanceledEncrypt_WhenEncryptorIgnoresCancellation_ThrowsWithoutOutput()
        {
            PublicArrayEncryptor encryptor = new (ignoreCancellation: true);
            using MemoryStream output = new ();
            using CancellationTokenSource cancellation = new ();
            cancellation.Cancel();

            await AssertCanceledAsync(
                () => EncryptToOutputAsync(Document, output, encryptor, cancellation.Token),
                cancellation.Token);

            Assert.AreEqual(0, output.Length);
        }

        [TestMethod]
        public async Task PublicFallbackStream_CancellationDuringNonCooperatingEncrypt_StopsWaiting()
        {
            NonCooperatingBlockingEncryptor encryptor = new (blockEncrypt: true);
            using Stream input = ToStream(Document);
            using MemoryStream output = new ();
            using CancellationTokenSource cancellation = new ();

            Task operation = EncryptionProcessor.EncryptAsync(
                input,
                output,
                encryptor,
                new EncryptionOptions
                {
                    DataEncryptionKeyId = DekId,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                    PathsToEncrypt = new List<string> { "/Sensitive" },
                },
                JsonProcessor.Stream,
                new CosmosDiagnosticsContext(),
                cancellation.Token);

            await encryptor.OperationStarted.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();

            try
            {
                await AssertCanceledAsync(
                    async () => await operation.WaitAsync(TimeSpan.FromSeconds(5)),
                    cancellation.Token);
            }
            finally
            {
                encryptor.Release();
            }

            Assert.AreEqual(0, output.Length);
        }

        [TestMethod]
        public async Task PublicFallbackStream_CancellationDuringNonCooperatingDecrypt_StopsWaiting()
        {
            using Stream encrypted = await EncryptAsync(
                JsonProcessor.Stream,
                new ArrayKeyAccessorEncryptor(),
                CancellationToken.None);
            NonCooperatingBlockingEncryptor encryptor = new (blockEncrypt: false);
            using MemoryStream output = new ();
            using CancellationTokenSource cancellation = new ();

            Task operation = EncryptionProcessor.DecryptAsync(
                encrypted,
                output,
                encryptor,
                new CosmosDiagnosticsContext(),
                RequestOptionsOverrideHelper.Create(JsonProcessor.Stream),
                cancellation.Token);

            await encryptor.OperationStarted.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();

            try
            {
                await AssertCanceledAsync(
                    async () => await operation.WaitAsync(TimeSpan.FromSeconds(5)),
                    cancellation.Token);
            }
            finally
            {
                encryptor.Release();
            }

            Assert.AreEqual(0, output.Length);
        }

        [DataTestMethod]
        [DataRow(true, "null task")]
        [DataRow(false, "null cipherText")]
        public async Task PublicFallbackStream_NullEncryptResult_ThrowsClearErrorWithoutOutput(
            bool returnNullTask,
            string expectedMessage)
        {
            using Stream input = ToStream(Document);
            using MemoryStream output = new ();

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => EncryptionProcessor.EncryptAsync(
                    input,
                    output,
                    new NullReturningEncryptor(returnNullTask),
                    new EncryptionOptions
                    {
                        DataEncryptionKeyId = DekId,
                        EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                        PathsToEncrypt = new List<string> { "/Sensitive" },
                    },
                    JsonProcessor.Stream,
                    new CosmosDiagnosticsContext(),
                    CancellationToken.None));

            StringAssert.Contains(exception.Message, expectedMessage);
            Assert.AreEqual(0, output.Length);
        }

        [DataTestMethod]
        [DataRow(true, "null task")]
        [DataRow(false, "null plainText")]
        public async Task PublicFallbackStream_NullDecryptResult_ThrowsClearErrorWithoutOutput(
            bool returnNullTask,
            string expectedMessage)
        {
            using Stream encrypted = await EncryptAsync(
                JsonProcessor.Stream,
                new ArrayKeyAccessorEncryptor(),
                CancellationToken.None);
            using MemoryStream output = new ();

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => EncryptionProcessor.DecryptAsync(
                    encrypted,
                    output,
                    new NullReturningEncryptor(returnNullTask),
                    new CosmosDiagnosticsContext(),
                    RequestOptionsOverrideHelper.Create(JsonProcessor.Stream),
                    CancellationToken.None));

            StringAssert.Contains(exception.Message, expectedMessage);
            Assert.AreEqual(0, output.Length);
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

#if NET8_0_OR_GREATER
        public static IEnumerable<object[]> NumericTokens()
        {
            yield return new object[] { "HighPrecision", "1234567890.1234567890123456789" };
            yield return new object[] { "TrailingZero", "42.5000" };
            yield return new object[] { "Exponent", "6.022e+23" };
        }
#endif

        private static Task<Stream> EncryptAsync(
            JsonProcessor jsonProcessor,
            Encryptor encryptor,
            CancellationToken cancellationToken,
            string document = Document)
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
                ToStream(document),
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

        private static void AssertUntouchedNumericToken(
            string json,
            string propertyName,
            string expectedRawText)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            Assert.AreEqual(
                expectedRawText,
                document.RootElement.GetProperty(propertyName).GetRawText());
        }

        private static void AssertCiphertextFraming(string encryptedJson)
        {
            using JsonDocument document = JsonDocument.Parse(encryptedJson);
            byte[] cipherText = document.RootElement.GetProperty("Sensitive").GetBytesFromBase64();
            Assert.AreEqual((byte)TypeMarker.String, cipherText[0]);
            Assert.AreEqual(
                "{\"_ef\":3,\"_en\":\"capabilityDek\",\"_ea\":\"MdeAeadAes256CbcHmac256Randomized\",\"_ed\":null,\"_ep\":[\"/Sensitive\"]}",
                document.RootElement.GetProperty(Constants.EncryptedInfo).GetRawText());
        }

        private static MemoryStream ToStream(string json)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(json));
        }

        private static async Task<string> ReadToEndAsync(Stream stream)
        {
            stream.Position = 0;
            using StreamReader reader = new (stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }

#if NET8_0_OR_GREATER
        private static async Task<byte[]> CreateEncryptedDocumentAsync(string document)
        {
            using Stream encrypted = await EncryptAsync(
                JsonProcessor.Stream,
                new ArrayKeyAccessorEncryptor(),
                CancellationToken.None,
                document);
            return Encoding.UTF8.GetBytes(await ReadToEndAsync(encrypted));
        }

        private static async Task AssertCanceledAsync(
            Func<Task> operation,
            CancellationToken expectedCancellationToken)
        {
            try
            {
                await operation();
                Assert.Fail("Expected the operation to observe the caller's cancellation token.");
            }
            catch (OperationCanceledException exception)
            {
                Assert.AreEqual(expectedCancellationToken, exception.CancellationToken);
            }
        }

        private static async Task EncryptToOutputAsync(
            string document,
            Stream output,
            Encryptor encryptor,
            CancellationToken cancellationToken)
        {
            using Stream input = ToStream(document);
            await EncryptionProcessor.EncryptAsync(
                input,
                output,
                encryptor,
                new EncryptionOptions
                {
                    DataEncryptionKeyId = DekId,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                    PathsToEncrypt = new List<string> { "/Sensitive" },
                },
                JsonProcessor.Stream,
                new CosmosDiagnosticsContext(),
                cancellationToken);
        }

        private static Task<(Stream, DecryptionContext)> DecryptFromStreamAsync(
            Stream input,
            Encryptor encryptor,
            CancellationToken cancellationToken)
        {
            return EncryptionProcessor.DecryptAsync(
                input,
                encryptor,
                JsonProcessor.Stream,
                legacyFallback: false,
                new CosmosDiagnosticsContext(),
                cancellationToken);
        }

        private static async Task AssertPublicFallbackInputReadCancellationAsync(
            Task operation,
            CancellationBlockingStream input,
            CancellationTokenSource cancellation,
            PublicArrayEncryptor encryptor)
        {
            await input.BlockingReadStarted.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();

            try
            {
                await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                    async () => await operation.WaitAsync(TimeSpan.FromSeconds(5)));
            }
            finally
            {
                input.ReleaseBlockedRead();
                try
                {
                    await operation.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch
                {
                }
            }

            Assert.AreEqual(0, encryptor.EncryptCalls);
            Assert.AreEqual(0, encryptor.DecryptCalls);
        }
#endif

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
            private readonly bool ignoreCancellation;

            public PublicArrayEncryptor(bool ignoreCancellation = false)
            {
                this.ignoreCancellation = ignoreCancellation;
            }

            public int EncryptCalls { get; private set; }

            public int DecryptCalls { get; private set; }

            public int LastPlainTextLength { get; private set; }

            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException("Direct key access is not supported.");
            }

            public override Task<byte[]> EncryptAsync(
                byte[] plainText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                this.EncryptCalls++;
                this.LastPlainTextLength = plainText.Length;
                if (!this.ignoreCancellation)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                return Task.FromResult(TestCommon.EncryptData(plainText));
            }

            public override Task<byte[]> DecryptAsync(
                byte[] cipherText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                this.DecryptCalls++;
                if (!this.ignoreCancellation)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                return Task.FromResult(TestCommon.DecryptData(cipherText));
            }
        }

#if NET8_0_OR_GREATER
        private sealed class NullReturningEncryptor : Encryptor
        {
            private readonly bool returnNullTask;

            public NullReturningEncryptor(bool returnNullTask)
            {
                this.returnNullTask = returnNullTask;
            }

            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException("Direct key access is not supported.");
            }

            public override Task<byte[]> EncryptAsync(
                byte[] plainText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                return this.returnNullTask
                    ? null
                    : Task.FromResult<byte[]>(null);
            }

            public override Task<byte[]> DecryptAsync(
                byte[] cipherText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                return this.returnNullTask
                    ? null
                    : Task.FromResult<byte[]>(null);
            }
        }

        private sealed class NonCooperatingBlockingEncryptor : Encryptor
        {
            private readonly bool blockEncrypt;
            private readonly TaskCompletionSource<bool> operationStarted = new (
                TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> release = new (
                TaskCreationOptions.RunContinuationsAsynchronously);

            public NonCooperatingBlockingEncryptor(bool blockEncrypt)
            {
                this.blockEncrypt = blockEncrypt;
            }

            public Task OperationStarted => this.operationStarted.Task;

            public void Release()
            {
                this.release.TrySetResult(true);
            }

            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException("Direct key access is not supported.");
            }

            public override async Task<byte[]> EncryptAsync(
                byte[] plainText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                if (this.blockEncrypt)
                {
                    this.operationStarted.TrySetResult(true);
                    await this.release.Task.ConfigureAwait(false);
                }

                return TestCommon.EncryptData(plainText);
            }

            public override async Task<byte[]> DecryptAsync(
                byte[] cipherText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                if (!this.blockEncrypt)
                {
                    this.operationStarted.TrySetResult(true);
                    await this.release.Task.ConfigureAwait(false);
                }

                return TestCommon.DecryptData(cipherText);
            }
        }

        private sealed class CancellationIgnoringMemoryStream : MemoryStream
        {
            public CancellationIgnoringMemoryStream(byte[] buffer)
                : base(buffer, writable: false)
            {
            }

            public override ValueTask<int> ReadAsync(
                Memory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                return base.ReadAsync(buffer, CancellationToken.None);
            }
        }

        private sealed class CancellationBlockingStream : MemoryStream
        {
            private readonly int blockAfterPositionResetCount;
            private readonly CancellationTokenSource release = new ();
            private readonly TaskCompletionSource<bool> blockingReadStarted = new (
                TaskCreationOptions.RunContinuationsAsynchronously);
            private int positionResetCount;

            public CancellationBlockingStream(
                byte[] initialContent,
                int blockAfterPositionResetCount)
                : base(initialContent, writable: false)
            {
                this.blockAfterPositionResetCount = blockAfterPositionResetCount;
            }

            public Task BlockingReadStarted => this.blockingReadStarted.Task;

            public override long Position
            {
                get => base.Position;
                set
                {
                    base.Position = value;
                    if (value == 0)
                    {
                        this.positionResetCount++;
                    }
                }
            }

            public void ReleaseBlockedRead()
            {
                this.release.Cancel();
            }

            public override ValueTask<int> ReadAsync(
                Memory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                if (this.positionResetCount >= this.blockAfterPositionResetCount)
                {
                    this.blockingReadStarted.TrySetResult(true);
                    return new ValueTask<int>(this.WaitForCancellationAsync(cancellationToken));
                }

                return base.ReadAsync(buffer, cancellationToken);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.release.Cancel();
                    this.release.Dispose();
                }

                base.Dispose(disposing);
            }

            private async Task<int> WaitForCancellationAsync(CancellationToken cancellationToken)
            {
                using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    this.release.Token);

                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, linkedCancellation.Token);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                throw new InvalidOperationException("The test released an input read that ignored cancellation.");
            }
        }

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

            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<DataEncryptionKey>(this.Key);
            }
        }

        private sealed class NullKeyAccessorEncryptor : PublicArrayEncryptor, IDataEncryptionKeyAccessor
        {
            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<DataEncryptionKey>(null);
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

            public override int EncryptData(
                byte[] plainText,
                int plainTextOffset,
                int plainTextLength,
                byte[] output,
                int outputOffset)
            {
                throw new AssertFailedException("The optional buffer capability is not implemented.");
            }

            public override int GetEncryptByteCount(int plainTextLength)
            {
                throw new AssertFailedException("The optional buffer capability is not implemented.");
            }

            public override int DecryptData(
                byte[] cipherText,
                int cipherTextOffset,
                int cipherTextLength,
                byte[] output,
                int outputOffset)
            {
                throw new AssertFailedException("The optional buffer capability is not implemented.");
            }

            public override int GetDecryptByteCount(int cipherTextLength)
            {
                throw new AssertFailedException("The optional buffer capability is not implemented.");
            }
        }

        private sealed class OverpredictingBufferKey : DataEncryptionKey, IDataEncryptionKeyBuffer
        {
            private readonly bool underpredictEncrypt;
            private readonly bool underpredictDecrypt;
            private readonly bool negativeEncryptPrediction;
            private readonly bool negativeDecryptPrediction;

            public OverpredictingBufferKey(
                bool underpredictEncrypt = false,
                bool underpredictDecrypt = false,
                bool negativeEncryptPrediction = false,
                bool negativeDecryptPrediction = false)
            {
                this.underpredictEncrypt = underpredictEncrypt;
                this.underpredictDecrypt = underpredictDecrypt;
                this.negativeEncryptPrediction = negativeEncryptPrediction;
                this.negativeDecryptPrediction = negativeDecryptPrediction;
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

            public override int EncryptData(
                byte[] plainText,
                int plainTextOffset,
                int plainTextLength,
                byte[] output,
                int outputOffset)
            {
                int bytesToCopy = this.underpredictEncrypt ? plainTextLength - 1 : plainTextLength;
                Buffer.BlockCopy(plainText, plainTextOffset, output, outputOffset, bytesToCopy);
                return plainTextLength;
            }

            public override int GetEncryptByteCount(int plainTextLength)
            {
                if (this.negativeEncryptPrediction)
                {
                    return -1;
                }

                if (this.underpredictEncrypt)
                {
                    return plainTextLength - 1;
                }

                return plainTextLength + 8;
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
                if (this.negativeDecryptPrediction)
                {
                    return -1;
                }

                return this.underpredictDecrypt ? cipherTextLength - 1 : cipherTextLength + 8;
            }
        }
    }
}