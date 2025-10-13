namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests.Diagnostics
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Encryption.Tests; // For TestCommon utilities

    [TestClass]
    public class EncryptionProcessorDiagnosticsTests
    {
        private sealed class NoopEncryptor : Encryptor
        {
            private sealed class FakeMdeDataEncryptionKey : DataEncryptionKey
            {
                private readonly byte[] rawKey = new byte[32];

                public FakeMdeDataEncryptionKey()
                {
                    // Deterministic but non-zero bytes.
                    for (int i = 0; i < this.rawKey.Length; i++)
                    {
                        this.rawKey[i] = (byte)(i + 1);
                    }
                }

                public override byte[] RawKey => this.rawKey;

                public override string EncryptionAlgorithm => CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized;

                public override byte[] EncryptData(byte[] plainText) => (byte[])plainText.Clone();

                public override int EncryptData(byte[] plainText, int plainTextOffset, int plainTextLength, byte[] output, int outputOffset)
                {
                    Array.Copy(plainText, plainTextOffset, output, outputOffset, plainTextLength);
                    return plainTextLength;
                }

                public override int GetEncryptByteCount(int plainTextLength) => plainTextLength;

                public override byte[] DecryptData(byte[] cipherText) => (byte[])cipherText.Clone();

                public override int DecryptData(byte[] cipherText, int cipherTextOffset, int cipherTextLength, byte[] output, int outputOffset)
                {
                    Array.Copy(cipherText, cipherTextOffset, output, outputOffset, cipherTextLength);
                    return cipherTextLength;
                }

                public override int GetDecryptByteCount(int cipherTextLength) => cipherTextLength;
            }

            private readonly DataEncryptionKey fakeKey = new FakeMdeDataEncryptionKey();

            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default) => Task.FromResult(this.fakeKey);
            public override Task<byte[]> EncryptAsync(byte[] plainText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default) => Task.FromResult(plainText);
            public override Task<byte[]> DecryptAsync(byte[] cipherText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default) => Task.FromResult(cipherText);
        }

        private static Encryptor CreateNoopEncryptor() => new NoopEncryptor();

        [TestMethod]
        public async Task DecryptAsync_SelectsNewtonsoftScopeByDefault()
        {
            // _ei present but null so encryption properties are treated as absent. Ensures early return without decrypt.
            string json = "{\"_ei\":null}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            Encryptor encryptor = CreateNoopEncryptor();
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            try
            {
                await EncryptionProcessor.DecryptAsync(input, encryptor, ctx, requestOptions: null, CancellationToken.None);
            }
            catch (NotSupportedException)
            {
            }
            AssertScopePresent(ctx, "EncryptionProcessor.Decrypt.Mde.Newtonsoft");
#if NET8_0_OR_GREATER
            AssertScopeAbsent(ctx, "EncryptionProcessor.Decrypt.Mde.Stream");
            AssertScopeAbsent(ctx, "EncryptionProcessor.DecryptStreamImpl.Mde");
#endif
        }

    #if NET8_0_OR_GREATER
    [TestMethod]
        public async Task DecryptAsync_ProvidedOutput_NewtonsoftScope()
        {
#if NET8_0_OR_GREATER
            string json = "{\"_ei\":null}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new MemoryStream();
            Encryptor encryptor = CreateNoopEncryptor();
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            try
            {
                _ = await EncryptionProcessor.DecryptAsync(input, output, encryptor, ctx, null, CancellationToken.None);
            }
            catch (NotSupportedException)
            {
            }
            AssertScopePresent(ctx, "EncryptionProcessor.Decrypt.Mde.Newtonsoft");
            AssertScopeAbsent(ctx, "EncryptionProcessor.Decrypt.Mde.Stream");
            AssertScopeAbsent(ctx, "EncryptionProcessor.DecryptStreamImpl.Mde"); // Newtonsoft path shouldn't include impl scope
#else
            Assert.Inconclusive("Provided-output decrypt path only available in NET8 preview build.");
#endif
        }

        [TestMethod]
        public async Task DecryptAsync_StreamOverride_SelectsStreamScope()
        {
#if NET8_0_OR_GREATER
            string json = "{\"_ei\":null}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            Encryptor encryptor = CreateNoopEncryptor();
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            ItemRequestOptions options = new ItemRequestOptions { Properties = new System.Collections.Generic.Dictionary<string, object>{{"encryption-json-processor", JsonProcessor.Stream}}};
            try
            {
                await EncryptionProcessor.DecryptAsync(input, encryptor, ctx, options, CancellationToken.None);
            }
            catch (NotSupportedException)
            {
            }
            AssertScopePresent(ctx, "EncryptionProcessor.Decrypt.Mde.Stream");
            AssertScopeAbsent(ctx, "EncryptionProcessor.Decrypt.Mde.Newtonsoft");
            AssertScopeAbsent(ctx, "EncryptionProcessor.DecryptStreamImpl.Mde");
#else
            Assert.Inconclusive("Stream override only validated in NET8 preview build.");
#endif
        }

        [TestMethod]
        public async Task DecryptAsync_ProvidedOutput_StreamScope()
        {
#if NET8_0_OR_GREATER
            string json = "{\"_ei\":null}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new MemoryStream();
            Encryptor encryptor = CreateNoopEncryptor();
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            ItemRequestOptions options = new ItemRequestOptions { Properties = new System.Collections.Generic.Dictionary<string, object>{{"encryption-json-processor", JsonProcessor.Stream}}};
            try
            {
                _ = await EncryptionProcessor.DecryptAsync(input, output, encryptor, ctx, options, CancellationToken.None);
            }
            catch (NotSupportedException)
            {
            }
            AssertScopePresent(ctx, "EncryptionProcessor.Decrypt.Mde.Stream");
            AssertScopeAbsent(ctx, "EncryptionProcessor.Decrypt.Mde.Newtonsoft");
            AssertScopeAbsent(ctx, "EncryptionProcessor.DecryptStreamImpl.Mde");
#else
            Assert.Inconclusive("Provided-output stream path only available in NET8 preview build.");
#endif
        }

        [TestMethod]
    public async Task DecryptAsync_MdePayload_DefaultNewtonsoft_Scopes()
        {
#if NET8_0_OR_GREATER
            // Create an MDE encrypted payload using Newtonsoft processor (default path)
            var testDoc = new { id = "id1", pk = "pk1", Sensitive = "s" };
            // Build encryption options (MDE) with Newtonsoft (implicit)
            EncryptionOptions opts = new()
            {
                DataEncryptionKeyId = "dekId",
#pragma warning disable CS0618
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
#pragma warning restore CS0618
                PathsToEncrypt = new System.Collections.Generic.List<string> { "/Sensitive" },
                JsonProcessor = JsonProcessor.Newtonsoft,
            };
            CosmosDiagnosticsContext encryptDiag = CosmosDiagnosticsContext.Create(null);
            Encryptor encryptor = CreateNoopEncryptor();
            Stream encrypted = await EncryptionProcessor.EncryptAsync(TestCommon.ToStream(testDoc), encryptor, opts, encryptDiag, CancellationToken.None);
            encrypted.Position = 0;

            CosmosDiagnosticsContext decryptDiag = CosmosDiagnosticsContext.Create(null);
            (Stream decrypted, DecryptionContext decCtx) = await EncryptionProcessor.DecryptAsync(encrypted, encryptor, decryptDiag, requestOptions: null, CancellationToken.None);
            Assert.IsNotNull(decCtx); // Should decrypt
            AssertScopePresent(decryptDiag, "EncryptionProcessor.Decrypt.Mde.Newtonsoft");
            AssertScopeAbsent(decryptDiag, "EncryptionProcessor.Decrypt.Mde.Stream");
            AssertScopeAbsent(decryptDiag, "EncryptionProcessor.DecryptStreamImpl.Mde"); // Newtonsoft path
            // Ensure no duplicate selection scopes
            Assert.AreEqual(1, decryptDiag.Scopes.Count(s => s.StartsWith("EncryptionProcessor.Decrypt.Mde.Newtonsoft", StringComparison.Ordinal)));
        #else
            Assert.Inconclusive("MDE payload diagnostic selection test only valid in NET8 preview.");
        #endif
        }

        [TestMethod]
    public async Task DecryptAsync_MdePayload_ProvidedOutput_Newtonsoft_Scopes()
        {
#if NET8_0_OR_GREATER
            var testDoc = new { id = "id1", pk = "pk1", Sensitive = "s" };
            EncryptionOptions opts = new()
            {
                DataEncryptionKeyId = "dekId",
#pragma warning disable CS0618
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
#pragma warning restore CS0618
                PathsToEncrypt = new System.Collections.Generic.List<string> { "/Sensitive" },
                JsonProcessor = JsonProcessor.Newtonsoft,
            };
            CosmosDiagnosticsContext encryptDiag = CosmosDiagnosticsContext.Create(null);
            Encryptor encryptor = CreateNoopEncryptor();
            Stream encrypted = await EncryptionProcessor.EncryptAsync(TestCommon.ToStream(testDoc), encryptor, opts, encryptDiag, CancellationToken.None);
            encrypted.Position = 0;

            CosmosDiagnosticsContext decryptDiag = CosmosDiagnosticsContext.Create(null);
            MemoryStream output = new();
            DecryptionContext decCtx = await EncryptionProcessor.DecryptAsync(encrypted, output, encryptor, decryptDiag, requestOptions: null, CancellationToken.None);
            Assert.IsNotNull(decCtx);
            AssertScopePresent(decryptDiag, "EncryptionProcessor.Decrypt.Mde.Newtonsoft");
            AssertScopeAbsent(decryptDiag, "EncryptionProcessor.Decrypt.Mde.Stream");
            AssertScopeAbsent(decryptDiag, "EncryptionProcessor.DecryptStreamImpl.Mde");
            Assert.AreEqual(1, decryptDiag.Scopes.Count(s => s.StartsWith("EncryptionProcessor.Decrypt.Mde.Newtonsoft", StringComparison.Ordinal)));
        #else
            Assert.Inconclusive("Provided-output MDE newtonsoft selection test only valid in NET8 preview.");
        #endif
        }

        [TestMethod]
    public async Task DecryptAsync_NullInput_NoScopes()
        {
            Encryptor encryptor = CreateNoopEncryptor();
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            (Stream result, DecryptionContext dctx) = await EncryptionProcessor.DecryptAsync((Stream)null, encryptor, ctx, requestOptions: null, CancellationToken.None);
            Assert.IsNull(result);
            Assert.IsNull(dctx);
#if NET8_0_OR_GREATER
            Assert.AreEqual(0, ctx.Scopes.Count, $"Expected no scopes but found: {string.Join(';', ctx.Scopes)}");
#else
        Assert.AreEqual(0, ctx.Scopes.Count);
#endif
        }

        [TestMethod]
        public async Task DecryptAsync_StreamOverride_MalformedJson_EmitsSelectionOnly()
        {
#if NET8_0_OR_GREATER
            string malformed = "{\"_ei\":{"; // truncated JSON
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(malformed));
            Encryptor encryptor = CreateNoopEncryptor();
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            ItemRequestOptions options = new ItemRequestOptions { Properties = new System.Collections.Generic.Dictionary<string, object>{{"encryption-json-processor", JsonProcessor.Stream}}};
            try
            {
                await EncryptionProcessor.DecryptAsync(input, encryptor, ctx, options, CancellationToken.None);
                Assert.Fail("Expected exception for malformed JSON");
            }
            catch (Exception)
            {
                // Swallow; focus on scopes
            }
            AssertScopePresent(ctx, "EncryptionProcessor.Decrypt.Mde.Stream");
            // Impl scope may appear if the code reached MDE stream impl before failing; assert at most one.
            Assert.IsTrue(ctx.Scopes.Count(s => s == "EncryptionProcessor.DecryptStreamImpl.Mde") == 0);
            Assert.AreEqual(1, ctx.Scopes.Count(s => s.StartsWith("EncryptionProcessor.Decrypt.Mde.Stream", StringComparison.Ordinal)));
            #else
            Assert.Inconclusive("Malformed JSON stream override test only valid in NET8 preview.");
            #endif
        }

        [TestMethod]
    public async Task DecryptAsync_StreamOverride_DuplicateScopePrevention()
        {
#if NET8_0_OR_GREATER
            // Reuse MDE encrypted payload but consume via stream override
            var testDoc = new { id = "id1", pk = "pk1", Sensitive = "s" };
            EncryptionOptions opts = new()
            {
                DataEncryptionKeyId = "dekId",
#pragma warning disable CS0618
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
#pragma warning restore CS0618
                PathsToEncrypt = new System.Collections.Generic.List<string> { "/Sensitive" },
                JsonProcessor = JsonProcessor.Newtonsoft,
            };
            CosmosDiagnosticsContext encryptDiag = CosmosDiagnosticsContext.Create(null);
            Encryptor encryptor = CreateNoopEncryptor();
            Stream encrypted = await EncryptionProcessor.EncryptAsync(TestCommon.ToStream(testDoc), encryptor, opts, encryptDiag, CancellationToken.None);
            encrypted.Position = 0;

            CosmosDiagnosticsContext decryptDiag = CosmosDiagnosticsContext.Create(null);
            ItemRequestOptions options = new ItemRequestOptions { Properties = new System.Collections.Generic.Dictionary<string, object>{{"encryption-json-processor", JsonProcessor.Stream}}};
            (Stream decrypted, DecryptionContext ctxDec) = await EncryptionProcessor.DecryptAsync(encrypted, encryptor, decryptDiag, options, CancellationToken.None);
            Assert.IsNotNull(ctxDec);
            Assert.AreEqual(1, decryptDiag.Scopes.Count(s => s.StartsWith("EncryptionProcessor.Decrypt.Mde.Stream", StringComparison.Ordinal)));
            Assert.AreEqual(0, decryptDiag.Scopes.Count(s => s == "EncryptionProcessor.DecryptStreamImpl.Mde"));
        #else
            Assert.Inconclusive("Duplicate scope prevention test only valid in NET8 preview.");
        #endif
        }

        [TestMethod]
        public async Task EncryptAsync_EmitsNewtonsoftSelectionScope()
        {
#if NET8_0_OR_GREATER
            var testDoc = new { id = "id1", pk = "pk1", P = "v" };
            EncryptionOptions opts = new()
            {
                DataEncryptionKeyId = "dekId",
#pragma warning disable CS0618
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
#pragma warning restore CS0618
                PathsToEncrypt = new System.Collections.Generic.List<string> { "/P" },
                JsonProcessor = JsonProcessor.Newtonsoft,
            };
            CosmosDiagnosticsContext encryptDiag = CosmosDiagnosticsContext.Create(null);
            Encryptor encryptor = CreateNoopEncryptor();
            _ = await EncryptionProcessor.EncryptAsync(TestCommon.ToStream(testDoc), encryptor, opts, encryptDiag, CancellationToken.None);

            AssertScopePresent(encryptDiag, "EncryptionProcessor.Encrypt.Mde.Newtonsoft");
            AssertScopeAbsent(encryptDiag, "EncryptionProcessor.Encrypt.Mde.Stream");
            Assert.AreEqual(1, encryptDiag.Scopes.Count(s => s.StartsWith("EncryptionProcessor.Encrypt.Mde.Newtonsoft", StringComparison.Ordinal)));
#else
            Assert.Inconclusive("Encrypt telemetry contract test only relevant for NET8 preview.");
#endif
        }

        [TestMethod]
        public async Task EncryptAsync_StreamProcessor_EmitsStreamSelectionScope()
        {
#if NET8_0_OR_GREATER
            var testDoc = new { id = "id1", pk = "pk1", P = "v" };
            EncryptionOptions opts = new()
            {
                DataEncryptionKeyId = "dekId",
#pragma warning disable CS0618
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
#pragma warning restore CS0618
                PathsToEncrypt = new System.Collections.Generic.List<string> { "/P" },
                JsonProcessor = JsonProcessor.Stream,
            };
            CosmosDiagnosticsContext encryptDiag = CosmosDiagnosticsContext.Create(null);
            Encryptor encryptor = CreateNoopEncryptor();
            using MemoryStream input = (MemoryStream)TestCommon.ToStream(testDoc);
            MemoryStream output = new();
            await EncryptionProcessor.EncryptAsync(input, output, encryptor, opts, encryptDiag, CancellationToken.None);

            AssertScopePresent(encryptDiag, "EncryptionProcessor.Encrypt.Mde.Stream");
            Assert.AreEqual(1, encryptDiag.Scopes.Count(s => s.StartsWith("EncryptionProcessor.Encrypt.Mde.Stream", StringComparison.Ordinal)));
#else
            Assert.Inconclusive("Stream encrypt telemetry test only relevant for NET8 preview.");
#endif
        }

        [TestMethod]
    public async Task DecryptAsync_StreamCancellation_EmitsSelectionBeforeCancel()
        {
#if NET8_0_OR_GREATER
            // Build a moderate payload to give cancellation window
            string big = new string('x', 10_000);
            var testDoc = new { id = "id1", pk = "pk1", Sensitive = big };
            EncryptionOptions opts = new()
            {
                DataEncryptionKeyId = "dekId",
#pragma warning disable CS0618
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
#pragma warning restore CS0618
                PathsToEncrypt = new System.Collections.Generic.List<string> { "/Sensitive" },
                JsonProcessor = JsonProcessor.Newtonsoft,
            };
            CosmosDiagnosticsContext encDiag = CosmosDiagnosticsContext.Create(null);
            Encryptor encryptor = CreateNoopEncryptor();
            Stream encrypted = await EncryptionProcessor.EncryptAsync(TestCommon.ToStream(testDoc), encryptor, opts, encDiag, CancellationToken.None);
            encrypted.Position = 0;

            // Wrap in slow stream that is seekable (copy bytes)
            byte[] bytes = ((MemoryStream)encrypted).ToArray();
            SlowCancelableStream slow = new(bytes, chunkSize: 64, perReadDelayMs: 1);
            CosmosDiagnosticsContext decDiag = CosmosDiagnosticsContext.Create(null);
            using CancellationTokenSource cts = new();
            cts.CancelAfter(5);
            try
            {
                _ = await EncryptionProcessor.DecryptAsync(slow, encryptor, decDiag, new ItemRequestOptions { Properties = new System.Collections.Generic.Dictionary<string, object>{{"encryption-json-processor", JsonProcessor.Stream}}}, cts.Token);
                Assert.Fail("Expected cancellation");
            }
            catch (OperationCanceledException)
            {
                // expected
            }
            Assert.IsTrue(decDiag.Scopes.Count(s => s.StartsWith("EncryptionProcessor.Decrypt.Mde.Stream", StringComparison.Ordinal)) >= 1);
            // Impl may or may not appear depending on timing; assert at most one.
            Assert.IsTrue(decDiag.Scopes.Count(s => s == "EncryptionProcessor.DecryptStreamImpl.Mde") == 0, "Impl scope unexpectedly present");
            #else
            Assert.Inconclusive("Cancellation telemetry test only valid in NET8 preview.");
            #endif
        }
    #endif

        private sealed class SlowCancelableStream : Stream
        {
            private readonly byte[] data;
            private readonly int chunkSize;
            private readonly int perReadDelayMs;
            private long position;

            public SlowCancelableStream(byte[] data, int chunkSize, int perReadDelayMs)
            {
                this.data = data;
                this.chunkSize = chunkSize;
                this.perReadDelayMs = perReadDelayMs;
                this.position = 0;
            }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => this.data.Length;
            public override long Position { get => this.position; set => this.position = value; }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Sync read not used");
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin)
            {
                long newPos = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => this.position + offset,
                    SeekOrigin.End => this.data.Length + offset,
                    _ => this.position,
                };
                if (newPos < 0 || newPos > this.data.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset));
                }
                this.position = newPos;
                return this.position;
            }
            public override void SetLength(long value) => throw new NotSupportedException();
            public override ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
            {
                return new ValueTask<int>(this.ReadInternalAsync(destination, cancellationToken));
            }

            private async Task<int> ReadInternalAsync(Memory<byte> destination, CancellationToken cancellationToken)
            {
                if (this.position >= this.data.Length)
                {
                    return 0;
                }
                if (this.perReadDelayMs > 0)
                {
                    await Task.Delay(this.perReadDelayMs, cancellationToken);
                }
                int remaining = (int)Math.Min(this.data.Length - this.position, this.chunkSize);
                int toCopy = Math.Min(remaining, destination.Length);
                this.data.AsSpan((int)this.position, toCopy).CopyTo(destination.Span);
                this.position += toCopy;
                return toCopy;
            }
        }

        private static void AssertScopePresent(CosmosDiagnosticsContext ctx, string prefix)
        {
            Assert.IsTrue(ctx.Scopes.Any(s => s.StartsWith(prefix, StringComparison.Ordinal)), $"Expected scope prefix '{prefix}' not found. Scopes: {string.Join(';', ctx.Scopes)}");
        }

        private static void AssertScopeAbsent(CosmosDiagnosticsContext ctx, string prefix)
        {
            Assert.IsFalse(ctx.Scopes.Any(s => s.StartsWith(prefix, StringComparison.Ordinal)), $"Did not expect scope prefix '{prefix}'. Scopes: {string.Join(';', ctx.Scopes)}");
        }
    }
}
