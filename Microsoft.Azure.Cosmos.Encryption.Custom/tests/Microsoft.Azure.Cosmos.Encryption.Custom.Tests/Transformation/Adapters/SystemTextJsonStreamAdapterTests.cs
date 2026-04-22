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

            await adapter.EncryptAsync(input, output, mockEncryptor.Object, defaultOptions, JsonProcessor.Stream, CancellationToken.None);

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
            };

            await Assert.ThrowsExceptionAsync<NotSupportedException>(
                () => adapter.EncryptAsync(input, output, mockEncryptor.Object, wrongOptions, JsonProcessor.Newtonsoft, CancellationToken.None));
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
        [TestCategory("Integration")]
        [TestCategory("MemoryLeak")]
        public async Task DecryptAsync_OutputStream_WithNonEncryptedPayload_ReturnsNull()
        {
            // CRITICAL: Tests SystemTextJsonStreamAdapter.cs:52 disposal fix
            // When DecryptStreamAsync returns null context, the PooledMemoryStream
            // created at line 48 MUST be disposed to prevent memory leak

            SystemTextJsonStreamAdapter adapter = new (new StreamProcessor());
            using MemoryStream input = new (Encoding.UTF8.GetBytes("{\"id\":1}"));
            using MemoryStream output = new ();
            CosmosDiagnosticsContext diagnostics = new CosmosDiagnosticsContext();

            DecryptionContext context = await adapter.DecryptAsync(input, output, mockEncryptor.Object, diagnostics, CancellationToken.None);

            Assert.IsNull(context, "Context should be null for non-encrypted payload");
            Assert.AreEqual(0, input.Position, "Input should be reset to position 0");
            Assert.AreEqual(0, output.Length, "Output should be empty for non-encrypted payload");
        }

        [TestMethod]
        [TestCategory("Stress")]
        [TestCategory("MemoryLeak")]
        public async Task DecryptAsync_OutputOverload_NoEncryption_RepeatedCalls_NoMemoryLeak()
        {
            // Stress test to verify line 52 disposal fix prevents memory leaks
            // Run 1000 times - if PooledMemoryStream not disposed, ArrayPool will exhaust
            SystemTextJsonStreamAdapter adapter = new (new StreamProcessor());
            string plainJson = "{\"id\":\"test\"}";

            for (int i = 0; i < 1000; i++)
            {
                using MemoryStream input = new (Encoding.UTF8.GetBytes(plainJson));
                using MemoryStream output = new ();
                CosmosDiagnosticsContext diagnostics = new CosmosDiagnosticsContext();

                DecryptionContext context = await adapter.DecryptAsync(
                    input, output, mockEncryptor.Object, diagnostics, CancellationToken.None);

                Assert.IsNull(context, $"Iteration {i}: Context should be null");
            }

            // If we got here without OutOfMemoryException, disposal is working
            Assert.IsTrue(true, "1000 iterations completed without memory leak");
        }

        [TestMethod]
        public async Task DecryptAsync_WhenStreamProcessorThrows_DisposesBufferWriterAndRethrows()
        {
            // Covers the catch-path in SystemTextJsonStreamAdapter.DecryptAsync(Stream input, ...)
            // where the RentArrayBufferWriter must be disposed before re-throwing so the
            // rented ArrayPool buffer doesn't leak.
            SystemTextJsonStreamAdapter adapter = new (new StreamProcessor());
            Stream encrypted = await CreateEncryptedPayloadAsync(adapter);

            Mock<Encryptor> failingEncryptor = new ();
            failingEncryptor
                .Setup(e => e.GetEncryptionKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("key-unwrap failure"));

            CosmosDiagnosticsContext diagnostics = new CosmosDiagnosticsContext();

            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await adapter.DecryptAsync(encrypted, failingEncryptor.Object, diagnostics, CancellationToken.None));
            Assert.AreEqual("key-unwrap failure", ex.Message);
        }

        [TestMethod]
        public async Task DecryptAsync_OutputStream_WhenStreamProcessorThrows_Rethrows()
        {
            // Covers the caller-provided-output overload's exception path. The underlying
            // RentArrayBufferWriter in the Stream-overload-of-StreamProcessor is scoped via
            // `using` so it must be disposed even when decrypt throws.
            SystemTextJsonStreamAdapter adapter = new (new StreamProcessor());
            Stream encrypted = await CreateEncryptedPayloadAsync(adapter);

            Mock<Encryptor> failingEncryptor = new ();
            failingEncryptor
                .Setup(e => e.GetEncryptionKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("key-unwrap failure"));

            using MemoryStream output = new ();
            CosmosDiagnosticsContext diagnostics = new CosmosDiagnosticsContext();

            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await adapter.DecryptAsync(encrypted, output, failingEncryptor.Object, diagnostics, CancellationToken.None));
            Assert.AreEqual("key-unwrap failure", ex.Message);
        }

        [TestMethod]
        [TestCategory("Stress")]
        [TestCategory("MemoryLeak")]
        public async Task DecryptAsync_NewOutputOverload_RepeatedCalls_NoMemoryLeak()
        {
            // Stress test for the new (Stream, DecryptionContext)-returning overload. The
            // returned Stream is a ReadOnlyBufferWriterStream that owns a RentArrayBufferWriter
            // rented from ArrayPool<byte>.Shared. If disposal of the returned Stream fails
            // to return the rented buffer, ArrayPool would exhaust after a few hundred
            // iterations and the Rent call would allocate fresh arrays. We mirror the
            // existing leak-stress test for the provided-output overload.
            SystemTextJsonStreamAdapter adapter = new (new StreamProcessor());

            for (int i = 0; i < 1000; i++)
            {
                using Stream encrypted = await CreateEncryptedPayloadAsync(adapter);
                CosmosDiagnosticsContext diagnostics = new CosmosDiagnosticsContext();

                (Stream decrypted, DecryptionContext context) = await adapter.DecryptAsync(
                    encrypted, mockEncryptor.Object, diagnostics, CancellationToken.None);

                Assert.IsNotNull(context, $"Iteration {i}: Context should not be null");
                using (decrypted)
                {
                    // Touch the result so reads are actually exercised across iterations.
                    byte[] scratch = new byte[1];
                    Assert.AreNotEqual(0, decrypted.Length, $"Iteration {i}: decrypted length");
                    decrypted.Position = 0;
                    int n = await decrypted.ReadAsync(scratch.AsMemory(0, 1), CancellationToken.None);
                    Assert.AreEqual(1, n);
                }
            }
        }

        [TestMethod]
        public async Task DecryptAsync_NewOutput_ReturnedStream_ReadAsync_ProducesSameBytesAsEncrypted()
        {
            // End-to-end round-trip asserting that the new (Stream, DecryptionContext)-returning
            // adapter path produces a stream whose bytes decrypt back to the original plaintext.
            SystemTextJsonStreamAdapter adapter = new (new StreamProcessor());

            object original = new { id = "1", Sensitive = "secret-value", NonSensitive = 42 };
            using Stream plaintextIn = TestCommon.ToStream(original);
            using Stream encrypted = await adapter.EncryptAsync(plaintextIn, mockEncryptor.Object, defaultOptions, CancellationToken.None);
            encrypted.Position = 0;

            CosmosDiagnosticsContext diagnostics = new CosmosDiagnosticsContext();
            (Stream decrypted, DecryptionContext context) = await adapter.DecryptAsync(encrypted, mockEncryptor.Object, diagnostics, CancellationToken.None);
            using (decrypted)
            {
                Assert.IsNotNull(context);
                using JsonDocument doc = JsonDocument.Parse(decrypted);
                Assert.AreEqual("secret-value", doc.RootElement.GetProperty("Sensitive").GetString());
                Assert.AreEqual(42, doc.RootElement.GetProperty("NonSensitive").GetInt32());
                Assert.IsFalse(doc.RootElement.TryGetProperty(Constants.EncryptedInfo, out _));
            }
        }

        [TestMethod]
        public async Task DecryptAsync_NewOutput_OnNonSeekableStream_ThrowsArgumentException()
        {
            // The decrypt path reads the input forward to locate _ei, then restarts reading
            // from byte 0 for the decrypt loop. A non-seekable stream cannot satisfy that
            // contract, so the _ei reader fails fast. This preserves the pre-change
            // behaviour of throwing on non-seekable input (the old DeserializeAsync call
            // threw NotSupportedException from input.Position = 0).
            SystemTextJsonStreamAdapter adapter = new (new StreamProcessor());
            using Stream input = new NonSeekableReadOnlyStream(Encoding.UTF8.GetBytes("{\"id\":\"1\"}"));
            CosmosDiagnosticsContext diagnostics = new CosmosDiagnosticsContext();

            await Assert.ThrowsExceptionAsync<ArgumentException>(
                async () => await adapter.DecryptAsync(input, mockEncryptor.Object, diagnostics, CancellationToken.None));
        }

        private sealed class NonSeekableReadOnlyStream : Stream
        {
            private readonly byte[] data;
            private int position;

            public NonSeekableReadOnlyStream(byte[] data) { this.data = data; }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (this.position >= this.data.Length)
                {
                    return 0;
                }

                int n = Math.Min(count, this.data.Length - this.position);
                Array.Copy(this.data, this.position, buffer, offset, n);
                this.position += n;
                return n;
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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
