//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Pins an MDE ciphertext fixture so future versions continue to decrypt the current
    /// envelope, type markers, and versioned AEAD payload with both JSON processors.
    /// </summary>
    [TestClass]
    public class CiphertextFixtureTests
    {
        private const string FixtureDekId = "fixtureDek";

        /// <summary>Base64-encoded UTF-8 encrypted document produced with the fixed test key.</summary>
        private const string FixtureEncryptedDocBase64 =
            "eyJpZCI6ImZpeHR1cmUtZG9jLTEiLCJQbGFpblN0ciI6InBhc3N0aHJvdWdoIFwicXVvdGVkXCIiLCJTZW5zU3RyIjoiQWdIeXd0M0RVOUlvMFpVdE91REM5d0hWbjI4dlB5R3hJWklTZGVtM2ZwalBObWdJY2lSN0trZi9OazFubzVOYUM1V2RUY0ZLWXZPTFNHUkh6YnBCZWlSbkpvQjd6bEZJelJoT2taL2FnMG5hbWc9PSIsIlNlbnNMb25nIjoiQkFGTnQyOHlxU09EbmxwUnZEQUE1d1psaDRuME8yR21ETVZ6RVhxTjZ2SmtOQ2JQem9VQ3JuUEVtOGhWNTdLVUNuTmV5ZGZlbGpPNEwzWHY4Qm5YOUk5MSIsIlNlbnNEb3VibGUiOiJBd0dhb25vMTA4T2xOaG9EQjNVa2ZUam1uK3RuREZzVW1vUjkzemRUK3B4Wk5ZVHltM3EyMHE5bjJHK0tveDBrcm94RjBkc2xVN1BOckVxWXJ2eVk1MzdoIiwiU2Vuc0Jvb2wiOiJCUUVKZzMzQUt5RFZUeU13azNwekhMVDRua1lCZFlGd3MwbzRHOFArUllxUGtmcU9ZNWpXZm0wVWFQU0JyU2NlVkNEaExpTDJEUWQ2NEdtbHlIcFovc25zIiwiU2Vuc09iaiI6IkJ3Rjg3VEVGTWZ3VFVUMGsxTjk3RW1mM0JCeEt2N3MvUndjb0dBQ2FDTzQwcDJ2Tk9LbXNLMGp1VlVEQUJlVmladStZelovUHJxSEdNaUJ4VC9sZml6ek9uZVJ2UXJBRlpTeHZPc0dmVlJtSkx3PT0iLCJTZW5zQXJyIjoiQmdHQnphTFA5dWtBUWY5VlI4WmhsNWNlOXMycXRNNVk0cDhYVDBsWHhPL0NZVXVXMCt5TlhwRldvbjZVbTkrbExVeGpWaDhWZ0lCM2svckhMZElEbGZrQk1RRWFKVkdhS1prb0E0WmUwL2ZMZHc9PSIsIl9laSI6eyJfZWYiOjMsIl9lbiI6ImZpeHR1cmVEZWsiLCJfZWEiOiJNZGVBZWFkQWVzMjU2Q2JjSG1hYzI1NlJhbmRvbWl6ZWQiLCJfZWQiOm51bGwsIl9lcCI6WyIvU2Vuc1N0ciIsIi9TZW5zTG9uZyIsIi9TZW5zRG91YmxlIiwiL1NlbnNCb29sIiwiL1NlbnNPYmoiLCIvU2Vuc0FyciJdfX0=";

        /// <summary>The exact plaintext document the fixture decrypts to (JSON-semantically).</summary>
        private const string FixtureOriginalJson =
            "{\"id\":\"fixture-doc-1\",\"PlainStr\":\"passthrough \\\"quoted\\\"\",\"SensStr\":\"secret \\u00e9\\ud83d\\ude00 value\",\"SensLong\":9007199254740993,\"SensDouble\":-2.5,\"SensBool\":true,\"SensObj\":{\"inner\":\"obj \\\"x\\\"\",\"n\":null},\"SensArr\":[1,\"two\",false,null]}";

        private static Encryptor CreateFixtureEncryptor()
        {
            return new FixedKeyEncryptor(CreateFixtureDataEncryptionKey());
        }

        private static Encryptor CreatePublicFixtureEncryptor()
        {
            return new PublicFixedKeyEncryptor(CreateFixtureDataEncryptionKey());
        }

        private static DataEncryptionKey CreateFixtureDataEncryptionKey()
        {
            byte[] rawKey = new byte[32];
            for (int i = 0; i < rawKey.Length; i++)
            {
                rawKey[i] = (byte)(i + 1);
            }

            Microsoft.Data.Encryption.Cryptography.PlaintextDataEncryptionKey plainDek = new (FixtureDekId, rawKey);
            return new MdeEncryptionAlgorithm(rawKey, plainDek, Data.Encryption.Cryptography.EncryptionType.Randomized);
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

        private sealed class PublicFixedKeyEncryptor : Encryptor
        {
            private readonly DataEncryptionKey dek;

            public PublicFixedKeyEncryptor(DataEncryptionKey dek)
            {
                this.dek = dek;
            }

            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException("Direct key access is not supported.");
            }

            public override Task<byte[]> EncryptAsync(byte[] plainText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(this.dek.EncryptData(plainText));
            }

            public override Task<byte[]> DecryptAsync(byte[] cipherText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                Assert.AreEqual(FixtureDekId, dataEncryptionKeyId);
                return Task.FromResult(this.dek.DecryptData(cipherText));
            }
        }

        [TestMethod]
        public async Task Fixture_DecryptsWithNewtonsoftProcessor()
        {
            Encryptor encryptor = CreateFixtureEncryptor();
            MemoryStream encrypted = new (Convert.FromBase64String(FixtureEncryptedDocBase64));

            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptAsync(
                encrypted,
                encryptor,
                new CosmosDiagnosticsContext(),
                requestOptions: null,
                CancellationToken.None);

            Assert.IsNotNull(context, "fixture document must be recognized as encrypted");
            AssertFixtureContent(new StreamReader(decrypted).ReadToEnd());
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        public async Task Fixture_DecryptsWithStreamProcessor()
        {
            Encryptor encryptor = CreateFixtureEncryptor();
            MemoryStream encrypted = new (Convert.FromBase64String(FixtureEncryptedDocBase64));

            (Stream decrypted, DecryptionContext context) = await DecryptStreamAsync(
                encrypted,
                encryptor,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Assert.IsNotNull(context, "fixture document must be recognized as encrypted");
            AssertFixtureContent(new StreamReader(decrypted).ReadToEnd());
        }

        [TestMethod]
        public async Task Fixture_DecryptsWithStreamProcessorPublicFallback()
        {
            Encryptor encryptor = CreatePublicFixtureEncryptor();
            MemoryStream encrypted = new (Convert.FromBase64String(FixtureEncryptedDocBase64));

            (Stream decrypted, DecryptionContext context) = await DecryptStreamAsync(
                encrypted,
                encryptor,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Assert.IsNotNull(context, "fixture document must be recognized as encrypted");
            AssertFixtureContent(new StreamReader(decrypted).ReadToEnd());
        }
#endif

        private static void AssertFixtureContent(string decryptedJson)
        {
            using System.Text.Json.JsonDocument expected = System.Text.Json.JsonDocument.Parse(FixtureOriginalJson);
            using System.Text.Json.JsonDocument actual = System.Text.Json.JsonDocument.Parse(decryptedJson);

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
