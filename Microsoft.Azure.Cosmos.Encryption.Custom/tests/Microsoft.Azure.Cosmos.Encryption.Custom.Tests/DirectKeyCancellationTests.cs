//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using H = EncryptionScenarioTestHelpers;

    [TestClass]
    public class DirectKeyCancellationTests
    {
#if NET8_0_OR_GREATER
        [TestMethod]
        public async Task StreamEncryption_CancellationDuringMetadataWrite_PreservesInputOwnership()
        {
            using CancellationTokenSource cancellation = new();
            CancelingKey key = new(cancellation, 99);
            DirectEncryptor encryptor = new(key, cancellation, cancelOnKey: false);
            using MemoryStream input = H.Stream(H.PlainItem().ToString(Formatting.None));
            using MetadataCancelingStream output = new(cancellation);

            OperationCanceledException failure = await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
                EncryptionProcessor.EncryptAsync(input, output, encryptor,
                    H.Options((int)JsonProcessor.Stream).EncryptionOptions,
                    JsonProcessor.Stream, new CosmosDiagnosticsContext(), cancellation.Token));

            Assert.IsTrue(output.MetadataWritten, "Cancellation must occur during the final metadata write.");
            Assert.AreEqual(cancellation.Token, failure.CancellationToken);
            Assert.IsTrue(input.CanRead, "Final-publication cancellation must not dispose unwrapped batch input.");
        }

        private sealed class MetadataCancelingStream : MemoryStream
        {
            private readonly CancellationTokenSource cancellation;

            internal MetadataCancelingStream(CancellationTokenSource cancellation)
            {
                this.cancellation = cancellation;
            }

            internal bool MetadataWritten { get; private set; }

            public override void Write(byte[] buffer, int offset, int count)
            {
                base.Write(buffer, offset, count);
                this.CancelOnMetadata(buffer.AsSpan(offset, count));
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                base.Write(buffer);
                this.CancelOnMetadata(buffer);
            }

            private void CancelOnMetadata(ReadOnlySpan<byte> buffer)
            {
                if (Encoding.UTF8.GetString(buffer).Contains("\"_ei\"", StringComparison.Ordinal))
                {
                    this.MetadataWritten = true;
                    this.cancellation.Cancel();
                }
            }
        }
#endif

        [DataTestMethod]
        [DynamicData(nameof(CancellationRows), DynamicDataSourceType.Method)]
        public async Task DirectKey_Cancellation_IsObservedAtCallerBoundaries(int processor, bool encrypt, int cancelAt)
        {
            const int propertyCount = 3;
            using CancellationTokenSource cancellation = new();
            CancelingKey key = new(cancellation, cancelAt);
            DirectEncryptor encryptor = new(key, cancellation, cancelAt == 0);
            string[] paths = Enumerable.Range(0, propertyCount).Select(i => "/Sensitive" + i).ToArray();
            JObject document = new() { ["id"] = "cancellation" };
            for (int i = 0; i < propertyCount; i++)
            {
                document["Sensitive" + i] = encrypt
                    ? "value" + i
                    : Convert.ToBase64String(new byte[] { 2 }.Concat(Encoding.UTF8.GetBytes("value" + i)).ToArray());
            }

            if (!encrypt)
            {
                document["_ei"] = H.Metadata(H.Mde, paths);
            }

            using MemoryStream input = H.Stream(document.ToString(Formatting.None));
            Stream result = null;
            if (cancelAt == -1)
            {
                cancellation.Cancel();
            }

            try
            {
                Exception failure = await H.CaptureExceptionAsync(async () =>
                {
                    if (encrypt)
                    {
                        result = await EncryptionProcessor.EncryptAsync(input, encryptor,
                            H.Options(processor, H.Mde, paths), new CosmosDiagnosticsContext(), cancellation.Token);
                    }
                    else
                    {
                        (result, _) = await EncryptionProcessor.DecryptAsync(input, encryptor,
                            new CosmosDiagnosticsContext(),
                            RequestOptionsOverrideHelper.Create((JsonProcessor)processor), cancellation.Token);
                    }
                });

                if (cancelAt == 99)
                {
                    Assert.IsNull(failure);
                    Assert.IsNotNull(result);
                    Assert.AreEqual(propertyCount, key.Calls);
                    using StreamReader reader = new(result, Encoding.UTF8, false, 1024, leaveOpen: true);
                    JObject actual = JObject.Parse(await reader.ReadToEndAsync());
                    Assert.AreEqual(!encrypt, actual["_ei"] == null);
                    if (!encrypt)
                    {
                        Assert.AreEqual("value2", actual["Sensitive2"].Value<string>());
                    }
                }
                else
                {
                    Assert.IsInstanceOfType(failure, typeof(OperationCanceledException));
                    Assert.AreEqual(cancellation.Token, ((OperationCanceledException)failure).CancellationToken);
                    Assert.IsNull(result, "A canceled operation must not publish a successful transformed result.");
                    Assert.AreEqual(Math.Max(0, cancelAt), key.Calls, "No further property may reach crypto after cancellation.");
                    Assert.IsTrue(input.CanRead, "Canceled processing must not consume input ownership.");
                }
            }
            finally
            {
                result?.Dispose();
            }
        }

        public static IEnumerable<object[]> CancellationRows()
        {
            foreach (int processor in H.Processors)
            {
                foreach (bool encrypt in new[] { false, true })
                {
                    foreach (int cancelAt in new[] { -1, 0, 1, 3, 99 })
                    {
                        yield return new object[] { processor, encrypt, cancelAt };
                    }
                }
            }
        }

        private sealed class DirectEncryptor : Encryptor, IDataEncryptionKeyAccessor
        {
            private readonly DataEncryptionKey key;
            private readonly CancellationTokenSource cancellation;
            private readonly bool cancelOnKey;

            internal DirectEncryptor(DataEncryptionKey key, CancellationTokenSource cancellation, bool cancelOnKey)
            {
                this.key = key;
                this.cancellation = cancellation;
                this.cancelOnKey = cancelOnKey;
            }

            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                Assert.AreEqual(this.cancellation.Token, cancellationToken);
                if (this.cancelOnKey)
                {
                    this.cancellation.Cancel();
                }

                return Task.FromResult(this.key);
            }

            public override Task<byte[]> EncryptAsync(byte[] plainText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("The direct-key path must not use the public encryptor fallback.");

            public override Task<byte[]> DecryptAsync(byte[] cipherText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("The direct-key path must not use the public encryptor fallback.");
        }

        private sealed class CancelingKey : DataEncryptionKey
        {
            private readonly CancellationTokenSource cancellation;
            private readonly int cancelAt;

            internal CancelingKey(CancellationTokenSource cancellation, int cancelAt)
            {
                this.cancellation = cancellation;
                this.cancelAt = cancelAt;
            }

            internal int Calls { get; private set; }

            public override byte[] RawKey => null;

            public override string EncryptionAlgorithm => H.Mde;

            public override byte[] EncryptData(byte[] plainText) => this.Transform(plainText);

            public override byte[] DecryptData(byte[] cipherText) => this.Transform(cipherText);

            public override int GetEncryptByteCount(int plainTextLength) => plainTextLength;

            public override int GetDecryptByteCount(int cipherTextLength) => cipherTextLength;

            public override int EncryptData(byte[] plainText, int plainTextOffset, int plainTextLength, byte[] output, int outputOffset)
                => throw new InvalidOperationException("This test key exposes only the public-array capability.");

            public override int DecryptData(byte[] cipherText, int cipherTextOffset, int cipherTextLength, byte[] output, int outputOffset)
                => throw new InvalidOperationException("This test key exposes only the public-array capability.");

            private byte[] Transform(byte[] input)
            {
                this.Calls++;
                if (this.Calls == this.cancelAt)
                {
                    this.cancellation.Cancel();
                }

                return input.ToArray();
            }
        }
    }
}
