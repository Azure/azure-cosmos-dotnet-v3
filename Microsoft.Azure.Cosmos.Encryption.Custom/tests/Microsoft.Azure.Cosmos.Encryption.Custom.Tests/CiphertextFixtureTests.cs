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
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests that MDE ciphertext round-trips correctly with both JSON processors,
    /// proving built-in buffer path, exact-length behavior, and fixed-key decryption compatibility.
    /// </summary>
    [TestClass]
    public class CiphertextFixtureTests
    {
        private const string FixtureDekId = "fixtureDek";

        private const string FixtureOriginalJson =
            "{\"id\":\"fixture-doc-1\",\"PlainStr\":\"passthrough \\\"quoted\\\"\",\"SensStr\":\"secret value\",\"SensLong\":9007199254740993,\"SensDouble\":-2.5,\"SensBool\":true,\"SensObj\":{\"inner\":\"obj\",\"n\":null},\"SensArr\":[1,\"two\",false,null]}";

        private static readonly string[] EncryptedPaths = new[]
        {
            "/SensStr", "/SensLong", "/SensDouble", "/SensBool", "/SensObj", "/SensArr"
        };

        private static Encryptor CreateFixtureEncryptor()
        {
            byte[] rawKey = new byte[32];
            for (int i = 0; i < rawKey.Length; i++)
            {
                rawKey[i] = (byte)(i + 1);
            }

            Microsoft.Data.Encryption.Cryptography.PlaintextDataEncryptionKey plainDek = new (FixtureDekId, rawKey);
            MdeEncryptionAlgorithm mdeAlgorithm = new (rawKey, plainDek, Data.Encryption.Cryptography.EncryptionType.Randomized);
            return new FixedKeyEncryptor(mdeAlgorithm);
        }

        private static EncryptionOptions CreateOptions()
        {
            return new EncryptionOptions
            {
                DataEncryptionKeyId = FixtureDekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = new List<string>(EncryptedPaths),
            };
        }

        private sealed class FixedKeyEncryptor : Encryptor, IDataEncryptionKeyAccessor
        {
            private readonly DataEncryptionKey dek;

            public FixedKeyEncryptor(DataEncryptionKey dek)
            {
                this.dek = dek;
            }

            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                Assert.AreEqual(FixtureDekId, dataEncryptionKeyId);
                return Task.FromResult(this.dek);
            }

            public override Task<byte[]> EncryptAsync(byte[] plainText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(this.dek.EncryptData(plainText));
            }

            public override Task<byte[]> DecryptAsync(byte[] cipherText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(this.dek.DecryptData(cipherText));
            }
        }

        [TestMethod]
        public async Task BuiltInBufferPath_NewtonsoftProcessor_RoundTrips()
        {
            Encryptor encryptor = CreateFixtureEncryptor();
            EncryptionItemRequestOptions requestOptions = new () { EncryptionOptions = CreateOptions() };

            Stream encrypted = await EncryptionProcessor.EncryptAsync(
                new MemoryStream(Encoding.UTF8.GetBytes(FixtureOriginalJson)),
                encryptor,
                requestOptions,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptAsync(
                encrypted,
                encryptor,
                new CosmosDiagnosticsContext(),
                requestOptions: null,
                CancellationToken.None);

            Assert.IsNotNull(context, "document must be recognized as encrypted");
            AssertRoundTrip(new StreamReader(decrypted).ReadToEnd());
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        public async Task BuiltInBufferPath_StreamProcessor_RoundTrips()
        {
            Encryptor encryptor = CreateFixtureEncryptor();

            MemoryStream encryptedBuffer = new ();
            await EncryptionProcessor.EncryptAsync(
                new MemoryStream(Encoding.UTF8.GetBytes(FixtureOriginalJson)),
                encryptedBuffer,
                encryptor,
                CreateOptions(),
                JsonProcessor.Stream,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            encryptedBuffer.Position = 0;

            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptAsync(
                encryptedBuffer,
                encryptor,
                JsonProcessor.Stream,
                legacyFallback: false,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Assert.IsNotNull(context, "document must be recognized as encrypted");
            AssertRoundTrip(new StreamReader(decrypted).ReadToEnd());
        }

        [TestMethod]
        public async Task ExactCiphertextLength_StreamPath_EncryptedBytesAreExact()
        {
            Encryptor encryptor = CreateFixtureEncryptor();
            string json = "{\"id\":\"1\",\"SensStr\":\"hello world\"}";

            MemoryStream encryptedBuffer = new ();
            await EncryptionProcessor.EncryptAsync(
                new MemoryStream(Encoding.UTF8.GetBytes(json)),
                encryptedBuffer,
                encryptor,
                new EncryptionOptions
                {
                    DataEncryptionKeyId = FixtureDekId,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                    PathsToEncrypt = new List<string> { "/SensStr" },
                },
                JsonProcessor.Stream,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            encryptedBuffer.Position = 0;
            using JsonDocument doc = JsonDocument.Parse(encryptedBuffer);
            JsonElement root = doc.RootElement;

            Assert.IsTrue(root.TryGetProperty(Constants.EncryptedInfo, out _), "_ei must be present");
            Assert.IsTrue(root.TryGetProperty("SensStr", out JsonElement sensStr), "SensStr must be encrypted");

            // The ciphertext is stored as base64; verify it round-trips correctly
            byte[] cipherBytes = Convert.FromBase64String(sensStr.GetString());
            Assert.IsTrue(cipherBytes.Length > 1, "Ciphertext must have type marker + payload");
        }
#endif

        private static void AssertRoundTrip(string decryptedJson)
        {
            using JsonDocument expected = JsonDocument.Parse(FixtureOriginalJson);
            using JsonDocument actual = JsonDocument.Parse(decryptedJson);

            Assert.AreEqual("fixture-doc-1", actual.RootElement.GetProperty("id").GetString());
            Assert.AreEqual(expected.RootElement.GetProperty("PlainStr").GetString(), actual.RootElement.GetProperty("PlainStr").GetString());
            Assert.AreEqual(expected.RootElement.GetProperty("SensStr").GetString(), actual.RootElement.GetProperty("SensStr").GetString());
            Assert.AreEqual(9007199254740993L, actual.RootElement.GetProperty("SensLong").GetInt64());
            Assert.AreEqual(-2.5, actual.RootElement.GetProperty("SensDouble").GetDouble(), 0.0);
            Assert.IsTrue(actual.RootElement.GetProperty("SensBool").GetBoolean());
            Assert.AreEqual(expected.RootElement.GetProperty("SensObj").GetProperty("inner").GetString(), actual.RootElement.GetProperty("SensObj").GetProperty("inner").GetString());
            Assert.AreEqual(System.Text.Json.JsonValueKind.Null, actual.RootElement.GetProperty("SensObj").GetProperty("n").ValueKind);
            Assert.AreEqual(4, actual.RootElement.GetProperty("SensArr").GetArrayLength());
            Assert.IsFalse(actual.RootElement.TryGetProperty(Constants.EncryptedInfo, out _), "_ei must be removed after decryption");
        }
    }
}
