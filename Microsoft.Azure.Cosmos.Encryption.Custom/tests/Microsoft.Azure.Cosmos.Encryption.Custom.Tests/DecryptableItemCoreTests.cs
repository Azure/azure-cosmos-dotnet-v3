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
    using Microsoft.Azure.Cosmos.Encryption.Custom.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;

#pragma warning disable CS0618 // Exercise the supported legacy read path.

    [TestClass]
    public class DecryptableItemCoreTests
    {
        private const string DekId = "synthetic-diagnostic-dek";
        private const string Secret = "synthetic-local-plaintext-sentinel";

        [DataTestMethod]
        [DataRow(CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized)]
        [DataRow(CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized)]
        public async Task SerializerFailure_PreservesCiphertextAndAllowsRepeatedReads(string algorithm)
        {
            Mock<Encryptor> encryptor = CreateEncryptor(algorithm);
            JObject document = await CreateEncryptedDocumentAsync(encryptor.Object, algorithm);
            string originalCiphertext = document.ToString();
            Assert.IsFalse(originalCiphertext.Contains(Secret, StringComparison.Ordinal));
            Assert.IsInstanceOfType(document[Constants.EncryptedInfo], typeof(JObject));

            InvalidOperationException serializerFailure = new ("Synthetic application deserialization failure.");
            int serializerCalls = 0;
            Mock<CosmosSerializer> serializer = new (MockBehavior.Strict);
            serializer.Setup(s => s.FromStream<JObject>(It.IsAny<Stream>()))
                .Returns((Stream stream) =>
                {
                    JObject plaintext = EncryptionProcessor.BaseSerializer.FromStream<JObject>(stream);
                    Assert.AreEqual(Secret, (string)plaintext["secret"], "Decryption must finish before the serializer fails.");
                    Assert.IsNull(plaintext[Constants.EncryptedInfo], "Successful decryption must remove encryption metadata.");
                    if (++serializerCalls <= 2)
                    {
                        throw serializerFailure;
                    }

                    return plaintext;
                });
            DecryptableItemCore item = new (document, encryptor.Object, serializer.Object);

            for (int attempt = 0; attempt < 2; attempt++)
            {
                EncryptionException exception = await Assert.ThrowsExceptionAsync<EncryptionException>(
                    () => item.GetItemAsync<JObject>());
                Assert.AreEqual(attempt + 1, serializerCalls);
                Assert.AreSame(serializerFailure, exception.InnerException);
                Assert.IsFalse(exception.EncryptedContent.Contains(Secret, StringComparison.Ordinal),
                    "EncryptedContent must not expose successfully decrypted plaintext.");
                Assert.AreEqual(originalCiphertext, exception.EncryptedContent);
                Assert.AreEqual(DekId, exception.DataEncryptionKeyId);
                Assert.AreEqual(originalCiphertext, document.ToString(), "The caller's encrypted document must remain unchanged.");
            }

            for (int attempt = 0; attempt < 2; attempt++)
            {
                (JObject plaintext, DecryptionContext context) = await item.GetItemAsync<JObject>();
                Assert.AreEqual(Secret, (string)plaintext["secret"]);
                Assert.IsNotNull(context, "Each repeated lazy read must retain decryption metadata.");
                Assert.AreEqual(DekId, context.DecryptionInfoList[0].DataEncryptionKeyId);
                Assert.AreEqual("/secret", context.DecryptionInfoList[0].PathsDecrypted[0]);
                Assert.AreEqual(originalCiphertext, document.ToString());
                plaintext["secret"] = "application mutation";
            }

            Assert.AreEqual(4, serializerCalls);
        }

        [DataTestMethod]
        [DataRow(CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized, false)]
        [DataRow(CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized, true)]
        [DataRow(CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized, false)]
        [DataRow(CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized, true)]
        public async Task MissingOrNullDekId_PreservesOriginalEncryptedFailure(string algorithm, bool explicitNull)
        {
            Mock<Encryptor> encryptor = CreateEncryptor(algorithm);
            JObject document = await CreateEncryptedDocumentAsync(encryptor.Object, algorithm);
            JObject metadata = (JObject)document[Constants.EncryptedInfo];
            if (explicitNull)
            {
                metadata[Constants.EncryptionDekId] = JValue.CreateNull();
            }
            else
            {
                metadata.Remove(Constants.EncryptionDekId);
            }

            string originalCiphertext = document.ToString();
            DecryptableItemCore item = new (document, encryptor.Object, new Mock<CosmosSerializer>(MockBehavior.Strict).Object);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                EncryptionException exception = await Assert.ThrowsExceptionAsync<EncryptionException>(
                    () => item.GetItemAsync<JObject>());
                Assert.AreEqual(string.Empty, exception.DataEncryptionKeyId);
                Assert.AreEqual(originalCiphertext, exception.EncryptedContent);
                Assert.IsInstanceOfType(exception.InnerException, typeof(InvalidOperationException));
                Assert.AreEqual(originalCiphertext, document.ToString());
            }
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public async Task PlaintextSerializerFailure_DoesNotReportEncryptedContent(int metadataShape)
        {
            JObject document = CreatePlaintext();
            if (metadataShape == 1)
            {
                document[Constants.EncryptedInfo] = JValue.CreateNull();
            }
            else if (metadataShape == 2)
            {
                document[Constants.EncryptedInfo] = "not an encryption envelope";
            }

            string original = document.ToString();
            InvalidOperationException serializerFailure = new ("Synthetic plaintext deserialization failure.");
            int serializerCalls = 0;
            Mock<CosmosSerializer> serializer = new (MockBehavior.Strict);
            serializer.Setup(s => s.FromStream<JObject>(It.IsAny<Stream>()))
                .Returns((Stream stream) =>
                {
                    JObject plaintext = EncryptionProcessor.BaseSerializer.FromStream<JObject>(stream);
                    Assert.IsTrue(JToken.DeepEquals(document, plaintext));
                    if (++serializerCalls == 1)
                    {
                        throw serializerFailure;
                    }

                    return plaintext;
                });
            DecryptableItemCore item = new (document, new Mock<Encryptor>(MockBehavior.Strict).Object, serializer.Object);
            EncryptionException exception = await Assert.ThrowsExceptionAsync<EncryptionException>(
                () => item.GetItemAsync<JObject>());
            Assert.AreSame(serializerFailure, exception.InnerException);
            Assert.AreEqual(string.Empty, exception.EncryptedContent);
            Assert.AreEqual(string.Empty, exception.DataEncryptionKeyId);

            (JObject result, DecryptionContext context) = await item.GetItemAsync<JObject>();
            Assert.IsNull(context);
            Assert.IsTrue(JToken.DeepEquals(document, result));
            Assert.AreEqual(original, document.ToString());
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task MissingOrNullAlgorithm_PreservesUnsupportedFailureWithoutPlaintext(bool explicitNull)
        {
            JObject document = CreatePlaintext();
            JObject metadata = new () { [Constants.EncryptionDekId] = DekId };
            if (explicitNull)
            {
                metadata[Constants.EncryptionAlgorithm] = JValue.CreateNull();
            }

            document[Constants.EncryptedInfo] = metadata;
            string original = document.ToString();
            DecryptableItemCore item = new (
                document,
                new Mock<Encryptor>(MockBehavior.Strict).Object,
                new Mock<CosmosSerializer>(MockBehavior.Strict).Object);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                EncryptionException exception = await Assert.ThrowsExceptionAsync<EncryptionException>(
                    () => item.GetItemAsync<JObject>());
                Assert.IsInstanceOfType(exception.InnerException, typeof(NotSupportedException));
                Assert.AreEqual(string.Empty, exception.EncryptedContent);
                Assert.AreEqual(DekId, exception.DataEncryptionKeyId);
                Assert.AreEqual(original, document.ToString());
            }
        }

        private static Mock<Encryptor> CreateEncryptor(string algorithm)
        {
            return algorithm == CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized
                ? TestEncryptorFactory.CreateMde(DekId, out _)
                : TestEncryptorFactory.CreateLegacy(DekId);
        }

        private static JObject CreatePlaintext()
        {
            return new JObject { ["id"] = "synthetic-item", ["secret"] = Secret };
        }

        private static async Task<JObject> CreateEncryptedDocumentAsync(Encryptor encryptor, string algorithm)
        {
            EncryptionItemRequestOptions options = RequestOptionsOverrideHelper.Create(
                new EncryptionOptions
                {
                    DataEncryptionKeyId = DekId,
                    EncryptionAlgorithm = algorithm,
                    PathsToEncrypt = new[] { "/secret" },
                },
                JsonProcessor.Newtonsoft);
            using Stream input = EncryptionProcessor.BaseSerializer.ToStream(CreatePlaintext());
            using Stream encrypted = await EncryptionProcessor.EncryptAsync(
                input, encryptor, options, new CosmosDiagnosticsContext(), CancellationToken.None);
            return EncryptionProcessor.BaseSerializer.FromStream<JObject>(encrypted);
        }
    }

#pragma warning restore CS0618
}
