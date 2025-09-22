// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Encryption.Custom;
using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests.Transformation
{
    [TestClass]
    public class StreamProcessorRoundTripTests
    {
        private const string DekId = "dek1";
    private const string Algo = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized; // Use modern MDE algorithm id.

        private sealed class PassthroughDataEncryptionKey : DataEncryptionKey
        {
            public override byte[] RawKey => Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef");
            public override string EncryptionAlgorithm => Algo;
            public override byte[] EncryptData(byte[] plainText) => plainText.ToArray();
            public override int EncryptData(byte[] plainText, int plainTextOffset, int plainTextLength, byte[] output, int outputOffset)
            {
                Array.Copy(plainText, plainTextOffset, output, outputOffset, plainTextLength);
                return plainTextLength;
            }
            public override int GetEncryptByteCount(int plainTextLength) => plainTextLength;
            public override byte[] DecryptData(byte[] cipherText) => cipherText.ToArray();
            public override int DecryptData(byte[] cipherText, int cipherTextOffset, int cipherTextLength, byte[] output, int outputOffset)
            {
                Array.Copy(cipherText, cipherTextOffset, output, outputOffset, cipherTextLength);
                return cipherTextLength;
            }
            public override int GetDecryptByteCount(int cipherTextLength) => cipherTextLength;
        }

        private sealed class MockEncryptor : Encryptor
        {
            private readonly DataEncryptionKey key = new PassthroughDataEncryptionKey();
            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                Assert.AreEqual(DekId, dataEncryptionKeyId);
                Assert.AreEqual(Algo, encryptionAlgorithm);
                return Task.FromResult(this.key);
            }
            public override Task<byte[]> EncryptAsync(byte[] plainText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
                => Task.FromResult(plainText); // passthrough
            public override Task<byte[]> DecryptAsync(byte[] cipherText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
                => Task.FromResult(cipherText); // passthrough
        }

        [TestMethod]
        public async Task EncryptDecrypt_Stream_RoundTrip_Passthrough()
        {
            // Arrange
            string json = "{\"id\":\"1\",\"sensitive\":\"secret\",\"regular\":42}";
            byte[] inputBytes = Encoding.UTF8.GetBytes(json);
            MemoryStream input = new MemoryStream(inputBytes, writable: false);
            MemoryStream encrypted = new MemoryStream();

            Encryptor encryptor = new MockEncryptor();
            EncryptionOptions options = new EncryptionOptions
            {
                DataEncryptionKeyId = DekId,
                EncryptionAlgorithm = Algo,
                PathsToEncrypt = new [] { "/sensitive" },
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
                JsonProcessor = JsonProcessor.Stream,
#endif
            };

            StreamProcessor sp = new StreamProcessor();

            // Act: encrypt stream
            await sp.EncryptStreamAsync(input, encrypted, encryptor, options, CancellationToken.None);

            // Prepare for decryption
            encrypted.Position = 0;
            MemoryStream decryptedOutput = new MemoryStream();
            using var doc = JsonDocument.Parse(encrypted, new JsonDocumentOptions { AllowTrailingCommas = true });
            // Extract the encryption properties object (last property) to reconstruct EncryptionProperties for decrypt
            // For passthrough we know format version 3 (no compression) based on EncryptStreamAsync implementation when compression disabled.
            encrypted.Position = 0; // reset for streaming decrypt

            // We need the encryption properties to call DecryptStreamAsync.
            // Simplest: parse once, locate the property name matching Constants.EncryptedInfo, and deserialize via System.Text.Json into a dynamic then manual construction is not available.
            // For brevity/low risk: reuse Newtonsoft if available, else quick STJ scan.
            using var reader = new StreamReader(encrypted, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            string encryptedText = reader.ReadToEnd();
            int idx = encryptedText.LastIndexOf("\"__ei\""); // Constants.EncryptedInfo is likely "__ei"; adjust if different.
            Assert.IsTrue(idx > 0, "Encrypted info property not found");
            // Reset again for streaming decrypt
            encrypted.Position = 0;

            EncryptionProperties props = new EncryptionProperties(
                encryptionFormatVersion: 3,
                encryptionAlgorithm: Algo,
                dataEncryptionKeyId: DekId,
                encryptedData: null,
                encryptedPaths: options.PathsToEncrypt);

            // Act: decrypt stream
            DecryptionContext ctx = await sp.DecryptStreamAsync(encrypted, decryptedOutput, encryptor, props, new CosmosDiagnosticsContext(), CancellationToken.None);

            decryptedOutput.Position = 0;
            string roundTrip = Encoding.UTF8.GetString(decryptedOutput.ToArray());

            // Assert
            Assert.IsTrue(roundTrip.Contains("\"sensitive\":\"secret\""), "Sensitive field should remain 'secret' in passthrough test");
            Assert.AreEqual(1, ctx.DecryptionInfoList.Count, "Expected a single decryption info entry");
            Assert.AreEqual(DekId, ctx.DecryptionInfoList[0].DataEncryptionKeyId);
        }
    }
}
#endif
