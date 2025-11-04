//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation.Adapters
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.Azure.Cosmos.Encryption.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class SystemTextJsonSystemTextJsonStreamAdapterTests
    {
        private const string DekId = "dek-id";
        private static Mock<Encryptor> mockEncryptor = null!;
        private static EncryptionOptions defaultOptions = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _ = context;
            mockEncryptor = TestEncryptorFactory.CreateMde(DekId, out _);
            defaultOptions = new EncryptionOptions
            {
                DataEncryptionKeyId = DekId,
#pragma warning disable CS0618
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
#pragma warning restore CS0618
                PathsToEncrypt = new[] { "/Sensitive" },
                JsonProcessor = JsonProcessor.Stream,
            };
        }

        [TestMethod]
        public async Task EncryptAsync_ReturnsEncryptedStream()
        {
            SystemTextJsonStreamAdapter adapter = new (new StreamProcessor());
            using Stream input = TestCommon.ToStream(new { id = "1", Sensitive = "secret" });

            Stream encrypted = await adapter.EncryptAsync(input, mockEncryptor.Object, defaultOptions, CancellationToken.None);
            using JsonDocument doc = JsonDocument.Parse(encrypted, new JsonDocumentOptions { AllowTrailingCommas = true });

            Assert.IsTrue(doc.RootElement.TryGetProperty(Constants.EncryptedInfo, out JsonElement ei));
            Assert.AreEqual(JsonValueKind.Object, ei.ValueKind);
        }

        [TestMethod]
        public async Task EncryptAsync_StreamOverload_WritesToOutput()
        {
            SystemTextJsonStreamAdapter adapter = new (new StreamProcessor());
            using Stream input = TestCommon.ToStream(new { id = "1", Sensitive = "secret" });
            using MemoryStream output = new ();

            await adapter.EncryptAsync(input, output, mockEncryptor.Object, defaultOptions, CancellationToken.None);

            output.Position = 0;
            using JsonDocument doc = JsonDocument.Parse(output);
            Assert.IsTrue(doc.RootElement.TryGetProperty(Constants.EncryptedInfo, out _));
        }

        [TestMethod]
        public async Task EncryptAsync_StreamOverload_WithNonStreamProcessor_Throws()
        {
            SystemTextJsonStreamAdapter adapter = new (new StreamProcessor());
            using Stream input = TestCommon.ToStream(new { id = "1" });
            using MemoryStream output = new ();

            EncryptionOptions wrongOptions = new EncryptionOptions
            {
                DataEncryptionKeyId = DekId,
#pragma warning disable CS0618
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
#pragma warning restore CS0618
                PathsToEncrypt = new[] { "/Sensitive" },
                JsonProcessor = JsonProcessor.Newtonsoft,
            };

            await Assert.ThrowsExceptionAsync<NotSupportedException>(
                () => adapter.EncryptAsync(input, output, mockEncryptor.Object, wrongOptions, CancellationToken.None));
        }

        [TestMethod]
        public async Task DecryptAsync_WhenNoMetadata_ReturnsOriginalStream()
        {
            SystemTextJsonStreamAdapter adapter = new (new StreamProcessor());
            using MemoryStream input = new (Encoding.UTF8.GetBytes("{\"id\":\"1\"}"));
            CosmosDiagnosticsContext diagnostics = new CosmosDiagnosticsContext();

            (Stream result, DecryptionContext context) = await adapter.DecryptAsync(input, mockEncryptor.Object, diagnostics, CancellationToken.None);

            Assert.AreSame(input, result);
            Assert.IsNull(context);
            Assert.AreEqual(0, result.Position);
        }

        [TestMethod]
        public async Task DecryptAsync_ReturnsDecryptedStream()
        {
            SystemTextJsonStreamAdapter adapter = new (new StreamProcessor());
            Stream encrypted = await CreateEncryptedPayloadAsync(adapter);
            CosmosDiagnosticsContext diagnostics = new CosmosDiagnosticsContext();

            (Stream decrypted, DecryptionContext context) = await adapter.DecryptAsync(encrypted, mockEncryptor.Object, diagnostics, CancellationToken.None);

            Assert.IsNotNull(context);
            Assert.AreNotSame(encrypted, decrypted);

            using JsonDocument doc = JsonDocument.Parse(decrypted);
            Assert.AreEqual("secret", doc.RootElement.GetProperty("Sensitive").GetString());
        }

        [TestMethod]
        public async Task DecryptAsync_OutputStream_WritesDecryptedPayload()
        {
            SystemTextJsonStreamAdapter adapter = new (new StreamProcessor());
            Stream encrypted = await CreateEncryptedPayloadAsync(adapter);
            CosmosDiagnosticsContext diagnostics = new CosmosDiagnosticsContext();
            using MemoryStream output = new ();

            DecryptionContext context = await adapter.DecryptAsync(encrypted, output, mockEncryptor.Object, diagnostics, CancellationToken.None);

            Assert.IsNotNull(context);
            output.Position = 0;
            using JsonDocument doc = JsonDocument.Parse(output);
            Assert.AreEqual("secret", doc.RootElement.GetProperty("Sensitive").GetString());
        }

        [TestMethod]
        public async Task DecryptAsync_OutputStream_WithNonEncryptedPayload_ReturnsNull()
        {
            SystemTextJsonStreamAdapter adapter = new (new StreamProcessor());
            using MemoryStream input = new (Encoding.UTF8.GetBytes("{\"id\":1}"));
            using MemoryStream output = new ();
            CosmosDiagnosticsContext diagnostics = new CosmosDiagnosticsContext();

            DecryptionContext context = await adapter.DecryptAsync(input, output, mockEncryptor.Object, diagnostics, CancellationToken.None);

            Assert.IsNull(context);
            Assert.AreEqual(0, input.Position);
            Assert.AreEqual(0, output.Length);
        }

        [TestMethod]
        public async Task DecryptAsync_WithLegacyAlgorithm_Throws()
        {
            SystemTextJsonStreamAdapter adapter = new (new StreamProcessor());
            EncryptionProperties legacyProps = CreateLegacyEncryptionProperties();
            EncryptionPropertiesWrapper wrapper = new (legacyProps);
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(wrapper);
            using MemoryStream input = new (payload);
            CosmosDiagnosticsContext diagnostics = new CosmosDiagnosticsContext();

            NotSupportedException exception = await Assert.ThrowsExceptionAsync<NotSupportedException>(async () =>
            {
                await adapter.DecryptAsync(input, mockEncryptor.Object, diagnostics, CancellationToken.None);
            });

            Assert.IsTrue(exception.Message.Contains("not supported"), $"Unexpected exception message: {exception.Message}");
#pragma warning disable CS0618
            Assert.IsTrue(exception.Message.Contains(CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized), $"Exception should mention the unsupported algorithm");
#pragma warning restore CS0618
        }

        private static async Task<Stream> CreateEncryptedPayloadAsync(SystemTextJsonStreamAdapter adapter)
        {
            using Stream input = TestCommon.ToStream(new { id = "1", Sensitive = "secret" });
            Stream encrypted = await adapter.EncryptAsync(input, mockEncryptor.Object, defaultOptions, CancellationToken.None);
            encrypted.Position = 0;
            return encrypted;
        }

        private static EncryptionProperties CreateLegacyEncryptionProperties()
        {
#pragma warning disable CS0618
            return new EncryptionProperties(
                encryptionFormatVersion: 2,
                encryptionAlgorithm: CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                dataEncryptionKeyId: "legacy-dek",
                encryptedData: null,
                encryptedPaths: new[] { "/Sensitive" });
#pragma warning restore CS0618
        }
    }
}
#endif
