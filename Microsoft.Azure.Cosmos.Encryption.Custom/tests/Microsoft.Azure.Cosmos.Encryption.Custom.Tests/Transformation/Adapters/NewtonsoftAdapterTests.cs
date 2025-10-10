//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if ENCRYPTION_CUSTOM_PREVIEW
namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation.Adapters
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.Azure.Cosmos.Encryption.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class NewtonsoftAdapterTests
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
        public async Task EncryptAsync_AppendsEncryptionMetadata()
        {
            NewtonsoftAdapter adapter = new (new MdeJObjectEncryptionProcessor());
            using Stream input = TestCommon.ToStream(new { id = "1", Sensitive = "secret" });

            Stream encrypted = await adapter.EncryptAsync(input, mockEncryptor.Object, defaultOptions, CancellationToken.None);
            JObject result = Read(encrypted);

            Assert.IsTrue(result.TryGetValue(Constants.EncryptedInfo, out JToken ei), "Expected encrypted metadata");
            Assert.AreEqual(JTokenType.String, result["Sensitive"].Type);
            Assert.IsNotNull(ei);
        }

        [TestMethod]
        public async Task EncryptAsync_StreamOverload_Throws()
        {
            NewtonsoftAdapter adapter = new (new MdeJObjectEncryptionProcessor());
            using Stream input = TestCommon.ToStream(new { id = "1", Sensitive = "secret" });
            using MemoryStream output = new ();

            await Assert.ThrowsExceptionAsync<NotSupportedException>(
                () => adapter.EncryptAsync(input, output, mockEncryptor.Object, defaultOptions, CancellationToken.None));
        }

        [TestMethod]
        public async Task DecryptAsync_WhenNoMetadata_ReturnsOriginalStream()
        {
            NewtonsoftAdapter adapter = new (new MdeJObjectEncryptionProcessor());
            using MemoryStream input = new (Encoding.UTF8.GetBytes("{\"id\":\"1\"}"));
            CosmosDiagnosticsContext diagnostics = new CosmosDiagnosticsContext();

            (Stream result, DecryptionContext context) = await adapter.DecryptAsync(input, mockEncryptor.Object, diagnostics, CancellationToken.None);

            Assert.AreSame(input, result);
            Assert.IsNull(context);
            Assert.AreEqual(0, diagnostics.Scopes.Count);
            Assert.AreEqual(0, result.Position);
        }

        [TestMethod]
        public async Task DecryptAsync_WhenLegacyAlgorithm_ReturnsOriginalStream()
        {
            NewtonsoftAdapter adapter = new (new MdeJObjectEncryptionProcessor());

            #pragma warning disable CS0618
            EncryptionProperties legacyProps = new (
                encryptionFormatVersion: 2,
                encryptionAlgorithm: CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                dataEncryptionKeyId: "legacy-dek",
                encryptedData: null,
                encryptedPaths: new[] { "/Sensitive" });
            #pragma warning restore CS0618
            JObject legacyDoc = new ()
            {
                ["id"] = "1",
                [Constants.EncryptedInfo] = JObject.FromObject(legacyProps),
            };

            using MemoryStream input = new (Encoding.UTF8.GetBytes(legacyDoc.ToString(Formatting.None)));
            CosmosDiagnosticsContext diagnostics = new CosmosDiagnosticsContext();

            (Stream result, DecryptionContext context) = await adapter.DecryptAsync(input, mockEncryptor.Object, diagnostics, CancellationToken.None);

            Assert.AreSame(input, result);
            Assert.IsNull(context);
            Assert.AreEqual(0, diagnostics.Scopes.Count);
            Assert.AreEqual(0, result.Position);
        }

        [TestMethod]
        public async Task DecryptAsync_StreamOverload_WritesDecryptedPayload()
        {
            NewtonsoftAdapter adapter = new (new MdeJObjectEncryptionProcessor());
            Stream encrypted = await CreateEncryptedPayloadAsync(adapter);

            using MemoryStream output = new ();
            CosmosDiagnosticsContext diagnostics = new CosmosDiagnosticsContext();
            DecryptionContext context = await adapter.DecryptAsync(encrypted, output, mockEncryptor.Object, diagnostics, CancellationToken.None);

            Assert.IsNotNull(context);
            Assert.AreEqual(0, diagnostics.Scopes.Count);

            JObject roundTripped = Read(output);
            Assert.IsFalse(roundTripped.ContainsKey(Constants.EncryptedInfo));
            Assert.AreEqual("secret", roundTripped["Sensitive"].ToString());
        }

        [TestMethod]
        public async Task DecryptAsync_ReturnsDecryptedStream()
        {
            NewtonsoftAdapter adapter = new (new MdeJObjectEncryptionProcessor());
            Stream encrypted = await CreateEncryptedPayloadAsync(adapter);
            CosmosDiagnosticsContext diagnostics = new CosmosDiagnosticsContext();

            (Stream decryptedStream, DecryptionContext context) = await adapter.DecryptAsync(encrypted, mockEncryptor.Object, diagnostics, CancellationToken.None);

            Assert.IsNotNull(context);
            Assert.AreEqual(0, diagnostics.Scopes.Count);
            Assert.AreNotSame(encrypted, decryptedStream);

            JObject roundTripped = Read(decryptedStream);
            Assert.IsFalse(roundTripped.ContainsKey(Constants.EncryptedInfo));
            Assert.AreEqual("secret", roundTripped["Sensitive"].ToString());
            Assert.IsTrue(context.DecryptionInfoList[0].PathsDecrypted.Any(p => p == "/Sensitive"));
        }

        private static async Task<Stream> CreateEncryptedPayloadAsync(NewtonsoftAdapter adapter)
        {
            using Stream input = TestCommon.ToStream(new { id = "1", Sensitive = "secret" });
            Stream encrypted = await adapter.EncryptAsync(input, mockEncryptor.Object, defaultOptions, CancellationToken.None);
            encrypted.Position = 0;
            return encrypted;
        }

        private static JObject Read(Stream stream)
        {
            stream.Position = 0;
            using StreamReader reader = new (stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            using JsonTextReader jsonReader = new (reader);
            return JObject.Load(jsonReader);
        }
    }
}
#endif
