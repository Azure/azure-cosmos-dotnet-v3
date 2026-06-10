//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Binary cross-version ciphertext fixture (wire F-8 / H1).
    ///
    /// The fixture below is a complete encrypted document produced by THIS package's
    /// MDE pipeline (Newtonsoft processor, real AeadAes256CbcHmac256 from
    /// Microsoft.Data.Encryption.Cryptography 2.0.0-pre015, algorithm version byte 1,
    /// EncryptionType.Randomized) with the fixed 32-byte test root key {1,2,...,32}.
    /// It is stored as a constant and must remain decryptable by every future version of
    /// this package and of the MDE library.
    ///
    /// What this protects against:
    ///  - FUTURE regressions in the MDE AEAD implementation (key derivation, blob layout,
    ///    SQL serializers) breaking the ability to decrypt data at rest;
    ///  - regressions in this package's envelope parsing (_ei/_ep handling, TypeMarker
    ///    dispatch, base64 interop) on both JSON processors.
    ///
    /// What it does NOT protect against: it cannot retroactively prove that preview08 +
    /// MDE 1.2.0 ciphertext decrypts (that pipeline cannot run inside this repo). It
    /// pins the present, so any *future* divergence is caught. The AEAD blob layout is
    /// additionally pinned by asserting the version byte (0x01) of every property blob.
    /// </summary>
    [TestClass]
    public class CiphertextFixtureTests
    {
        private const string FixtureDekId = "fixtureDek";

        /// <summary>
        /// base64(UTF-8 encrypted document). Regenerate ONLY when intentionally breaking the
        /// wire format (which requires a format-version bump): encrypt FixtureOriginalJson
        /// with the fixed key via the Newtonsoft processor and re-embed.
        /// </summary>
        private const string FixtureEncryptedDocBase64 =
            "eyJpZCI6ImZpeHR1cmUtZG9jLTEiLCJQbGFpblN0ciI6InBhc3N0aHJvdWdoIFwicXVvdGVkXCIiLCJTZW5zU3RyIjoiQWdIeXd0M0RVOUlvMFpVdE91REM5d0hWbjI4dlB5R3hJWklTZGVtM2ZwalBObWdJY2lSN0trZi9OazFubzVOYUM1V2RUY0ZLWXZPTFNHUkh6YnBCZWlSbkpvQjd6bEZJelJoT2taL2FnMG5hbWc9PSIsIlNlbnNMb25nIjoiQkFGTnQyOHlxU09EbmxwUnZEQUE1d1psaDRuME8yR21ETVZ6RVhxTjZ2SmtOQ2JQem9VQ3JuUEVtOGhWNTdLVUNuTmV5ZGZlbGpPNEwzWHY4Qm5YOUk5MSIsIlNlbnNEb3VibGUiOiJBd0dhb25vMTA4T2xOaG9EQjNVa2ZUam1uK3RuREZzVW1vUjkzemRUK3B4Wk5ZVHltM3EyMHE5bjJHK0tveDBrcm94RjBkc2xVN1BOckVxWXJ2eVk1MzdoIiwiU2Vuc0Jvb2wiOiJCUUVKZzMzQUt5RFZUeU13azNwekhMVDRua1lCZFlGd3MwbzRHOFArUllxUGtmcU9ZNWpXZm0wVWFQU0JyU2NlVkNEaExpTDJEUWQ2NEdtbHlIcFovc25zIiwiU2Vuc09iaiI6IkJ3Rjg3VEVGTWZ3VFVUMGsxTjk3RW1mM0JCeEt2N3MvUndjb0dBQ2FDTzQwcDJ2Tk9LbXNLMGp1VlVEQUJlVmladStZelovUHJxSEdNaUJ4VC9sZml6ek9uZVJ2UXJBRlpTeHZPc0dmVlJtSkx3PT0iLCJTZW5zQXJyIjoiQmdHQnphTFA5dWtBUWY5VlI4WmhsNWNlOXMycXRNNVk0cDhYVDBsWHhPL0NZVXVXMCt5TlhwRldvbjZVbTkrbExVeGpWaDhWZ0lCM2svckhMZElEbGZrQk1RRWFKVkdhS1prb0E0WmUwL2ZMZHc9PSIsIl9laSI6eyJfZWYiOjMsIl9lbiI6ImZpeHR1cmVEZWsiLCJfZWEiOiJNZGVBZWFkQWVzMjU2Q2JjSG1hYzI1NlJhbmRvbWl6ZWQiLCJfZWQiOm51bGwsIl9lcCI6WyIvU2Vuc1N0ciIsIi9TZW5zTG9uZyIsIi9TZW5zRG91YmxlIiwiL1NlbnNCb29sIiwiL1NlbnNPYmoiLCIvU2Vuc0FyciJdfX0=";

        /// <summary>The exact plaintext document the fixture decrypts to (JSON-semantically).</summary>
        private const string FixtureOriginalJson =
            "{\"id\":\"fixture-doc-1\",\"PlainStr\":\"passthrough \\\"quoted\\\"\",\"SensStr\":\"secret \\u00e9\\ud83d\\ude00 value\",\"SensLong\":9007199254740993,\"SensDouble\":-2.5,\"SensBool\":true,\"SensObj\":{\"inner\":\"obj \\\"x\\\"\",\"n\":null},\"SensArr\":[1,\"two\",false,null]}";

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

        private sealed class FixedKeyEncryptor : Encryptor
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

            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptStreamAsync(
                encrypted,
                encryptor,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Assert.IsNotNull(context, "fixture document must be recognized as encrypted");
            AssertFixtureContent(new StreamReader(decrypted).ReadToEnd());
        }
#endif

        [TestMethod]
        public void Fixture_CipherBlobs_PinAlgorithmVersionByteAndTypeMarkers()
        {
            string encryptedJson = Encoding.UTF8.GetString(Convert.FromBase64String(FixtureEncryptedDocBase64));
            Newtonsoft.Json.Linq.JObject doc = Newtonsoft.Json.Linq.JObject.Parse(encryptedJson);

            (string name, byte marker)[] expectations =
            {
                ("SensStr", 2),
                ("SensDouble", 3),
                ("SensLong", 4),
                ("SensBool", 5),
                ("SensArr", 6),
                ("SensObj", 7),
            };

            foreach ((string name, byte marker) in expectations)
            {
                byte[] blob = Convert.FromBase64String(doc[name].ToObject<string>());
                Assert.AreEqual(marker, blob[0], $"TypeMarker byte for {name}");

                // Wire layout: TypeMarker(1) ‖ AEAD blob. The AEAD blob's first byte is the
                // algorithm version, pinned to 1 by the SDK (MdeEncryptionAlgorithm.Version).
                Assert.AreEqual(1, blob[1], $"AEAD algorithm version byte for {name}");
            }
        }

        [TestMethod]
        public async Task Fixture_FreshEncryptionWithFixedKey_RoundTripsAndKeepsVersionByte()
        {
            // AEAD is IV-randomized, so fresh ciphertext differs run to run; what must stay
            // stable is the deterministic layout (version byte) and decryptability.
            Encryptor encryptor = CreateFixtureEncryptor();

            EncryptionOptions options = new ()
            {
                DataEncryptionKeyId = FixtureDekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = new System.Collections.Generic.List<string> { "/SensStr" },
            };

            Stream encrypted = await EncryptionProcessor.EncryptAsync(
                new MemoryStream(Encoding.UTF8.GetBytes("{\"id\":\"1\",\"SensStr\":\"fresh value\"}")),
                encryptor,
                options,
                JsonProcessor.Newtonsoft,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            MemoryStream buffer = new ();
            await encrypted.CopyToAsync(buffer);

            Newtonsoft.Json.Linq.JObject doc = Newtonsoft.Json.Linq.JObject.Parse(Encoding.UTF8.GetString(buffer.ToArray()));
            byte[] blob = Convert.FromBase64String(doc["SensStr"].ToObject<string>());
            Assert.AreEqual(2, blob[0], "TypeMarker.String");
            Assert.AreEqual(1, blob[1], "AEAD algorithm version byte must stay 1");

            buffer.Position = 0;
            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptAsync(
                buffer,
                encryptor,
                new CosmosDiagnosticsContext(),
                requestOptions: null,
                CancellationToken.None);

            Assert.IsNotNull(context);
            Newtonsoft.Json.Linq.JObject decryptedDoc = Newtonsoft.Json.Linq.JObject.Parse(new StreamReader(decrypted).ReadToEnd());
            Assert.AreEqual("fresh value", decryptedDoc["SensStr"].ToObject<string>());
        }

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
