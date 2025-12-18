//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Tests; // TestEncryptorFactory & TestCommon
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Additional coverage for scenarios explicitly requested: large payloads, corrupted payload (assert existing coverage), concurrency and cancellation.
    /// Corrupted payload scenarios are already extensively covered in StreamProcessorDecryptorTests; here we focus on large payload and multi-task safety plus cancellation.
    /// </summary>
    [TestClass]
    public class StreamProcessorConcurrencyAndCancellationTests
    {
        private const string DekId = "dekId";
        private static Mock<Encryptor> mockEncryptor;

        [ClassInitialize]
        public static void Init(TestContext ctx)
        {
            _ = ctx;
            mockEncryptor = TestEncryptorFactory.CreateMde(DekId, out _);
        }

        private static EncryptionOptions CreateEncryptionOptions(IEnumerable<string> paths)
        {
            return new EncryptionOptions
            {
                DataEncryptionKeyId = DekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = paths.ToList()
            };
        }

        [TestMethod]
        public async Task EncryptDecrypt_LargePayload_StreamProcessor()
        {
            // Force small initial buffer to exercise multiple resizes while handling a large single property.
            int original = Transformation.StreamProcessor.InitialBufferSize;
            Transformation.StreamProcessor.InitialBufferSize = 32; // tiny to trigger many growths
            try
            {
                string largeValue = new string('x', 250_000); // ~250 KB
                var doc = new
                {
                    id = Guid.NewGuid().ToString(),
                    Large = largeValue,
                    P1 = new string('a', 1024),
                    P2 = "b",
                };
                EncryptionOptions options = CreateEncryptionOptions(new[] { "/Large", "/P1", "/P2" });

                Stream encrypted = await EncryptionProcessor.EncryptAsync(
                    TestCommon.ToStream(doc),
                    mockEncryptor.Object,
                    options,
                    JsonProcessor.Stream,
                    new CosmosDiagnosticsContext(),
                    CancellationToken.None);

                // Decrypt using streaming path explicitly.
                (Stream decryptedStream, DecryptionContext ctx) = await EncryptionProcessor.DecryptAsync(
                    encrypted,
                    mockEncryptor.Object,
                    new CosmosDiagnosticsContext(),
                    RequestOptionsOverrideHelper.Create(JsonProcessor.Stream),
                    CancellationToken.None);

                Assert.IsNotNull(ctx);
                Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Contains("/Large"));

                decryptedStream.Position = 0;
                using JsonDocument jd = JsonDocument.Parse(decryptedStream);
                JsonElement root = jd.RootElement;
                Assert.AreEqual(largeValue.Length, root.GetProperty("Large").GetString().Length);
                Assert.AreEqual(1024, root.GetProperty("P1").GetString().Length);
                Assert.AreEqual("b", root.GetProperty("P2").GetString());
            }
            finally
            {
                Transformation.StreamProcessor.InitialBufferSize = original;
            }
        }

        [TestMethod]
        public async Task Concurrent_EncryptDecrypt_StreamProcessor()
        {
            int parallelism = Math.Min(8, Environment.ProcessorCount);
            var tasks = new List<Task>();
            for (int i = 0; i < parallelism; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    string large = new string((char)('a' + (i % 26)), 50_000);
                    var doc = new { id = Guid.NewGuid().ToString(), Large = large, P1 = i.ToString(), P2 = (i * 2).ToString() };
                    EncryptionOptions options = CreateEncryptionOptions(new[] { "/Large", "/P1", "/P2" });
                    Stream encrypted = await EncryptionProcessor.EncryptAsync(TestCommon.ToStream(doc), mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);
                    (Stream decrypted, DecryptionContext ctx) = await EncryptionProcessor.DecryptAsync(encrypted, mockEncryptor.Object, new CosmosDiagnosticsContext(), RequestOptionsOverrideHelper.Create(JsonProcessor.Stream), CancellationToken.None);
                    decrypted.Position = 0;
                    using JsonDocument jd = JsonDocument.Parse(decrypted);
                    JsonElement root = jd.RootElement;
                    Assert.AreEqual(large.Length, root.GetProperty("Large").GetString().Length);
                    Assert.AreEqual(doc.P1, root.GetProperty("P1").GetString());
                    Assert.AreEqual(doc.P2, root.GetProperty("P2").GetString());
                    Assert.IsTrue(ctx.DecryptionInfoList[0].PathsDecrypted.Count >= 3);
                }));
            }

            await Task.WhenAll(tasks);
        }

        [TestMethod]
        public async Task Encrypt_Cancellation_Aborts()
        {
            string large = new string('z', 50_000); // forces multiple reads with small chunk size
            var doc = new { id = Guid.NewGuid().ToString(), Large = large };

            // Use slow stream to create a window for cancellation.
            byte[] payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(doc));
            using SlowCancelableStream slow = new(payload, chunkSize: 64, perReadDelayMs: 1);
            using CancellationTokenSource cts = new();
            EncryptionOptions options = CreateEncryptionOptions(new[] { "/Large" });
            Task encryptTask = EncryptionProcessor.EncryptAsync(slow, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), cts.Token);
            cts.CancelAfter(5); // cancel shortly after start

            try
            {
                await encryptTask;
                Assert.Fail("Expected cancellation");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex is OperationCanceledException, $"Expected OperationCanceledException but got {ex.GetType()}");
            }
        }

        [TestMethod]
        public async Task Decrypt_Cancellation_Aborts()
        {
            // First create a valid encrypted payload
            string large = new string('y', 50_000);
            var doc = new { id = Guid.NewGuid().ToString(), Large = large };
            EncryptionOptions options = CreateEncryptionOptions(new[] { "/Large" });
            Stream encrypted = await EncryptionProcessor.EncryptAsync(TestCommon.ToStream(doc), mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);

            // Wrap encrypted stream in slow stream (must be seekable; we copy bytes)
            byte[] bytes;
            if (encrypted is MemoryStream ms)
            {
                bytes = ms.ToArray();
            }
            else if (encrypted is PooledMemoryStream pms)
            {
                bytes = pms.ToArray();
            }
            else
            {
                // Fallback for other stream types
                using var memStream = new MemoryStream();
                await encrypted.CopyToAsync(memStream);
                bytes = memStream.ToArray();
            }
            SlowCancelableStream slow = new(bytes, chunkSize: 64, perReadDelayMs: 1);
            using CancellationTokenSource cts = new();
            Task<(Stream, DecryptionContext)> decryptTask = EncryptionProcessor.DecryptAsync(
                slow,
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                RequestOptionsOverrideHelper.Create(JsonProcessor.Stream),
                cts.Token);

            cts.CancelAfter(5);
            try
            {
                await decryptTask;
                Assert.Fail("Expected cancellation");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex is OperationCanceledException, $"Expected OperationCanceledException but got {ex.GetType()}");
            }
        }

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
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Synchronous Read not used");
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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
    }
}
#endif