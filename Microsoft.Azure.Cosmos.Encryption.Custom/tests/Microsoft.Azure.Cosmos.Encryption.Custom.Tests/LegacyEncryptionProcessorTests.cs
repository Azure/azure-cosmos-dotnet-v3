//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Tests;
    using TestDoc = TestCommon.TestDoc;

#pragma warning disable CS0618 // Type or member is obsolete

    [TestClass]
    public class LegacyEncryptionProcessorTests
    {
        private static Mock<Encryptor> mockEncryptor;
        private static EncryptionOptions encryptionOptions;
        private const string dekId = "dekId";

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            _ = testContext;
            LegacyEncryptionProcessorTests.encryptionOptions = new EncryptionOptions()
            {
                DataEncryptionKeyId = LegacyEncryptionProcessorTests.dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                PathsToEncrypt = TestDoc.PathsToEncrypt
            };
            LegacyEncryptionProcessorTests.mockEncryptor = TestEncryptorFactory.CreateLegacy(dekId);
        }

        [TestMethod]
        [DynamicData(nameof(JsonProcessors))]
        public async Task InvalidPathToEncrypt(int jsonProcessorValue)
        {
            JsonProcessor jsonProcessor = ResolveJsonProcessor(jsonProcessorValue);
            TestDoc testDoc = TestDoc.Create();
            EncryptionOptions encryptionOptionsWithInvalidPathToEncrypt = new ()
            {
                DataEncryptionKeyId = LegacyEncryptionProcessorTests.dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                PathsToEncrypt = new List<string>() { "/SensitiveStr", "/Invalid" }
            };

            if (jsonProcessor != JsonProcessor.Newtonsoft)
            {
                await VerifyUnsupportedProcessorAsync(testDoc, jsonProcessor, encryptionOptionsWithInvalidPathToEncrypt);
                return;
            }

            JObject encryptedDoc = await VerifyEncryptionSucceeded(testDoc, jsonProcessor, encryptionOptionsWithInvalidPathToEncrypt);
            Assert.IsNull(encryptedDoc.Property("Invalid"));

            (JObject decryptedDoc, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedDoc,
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            LegacyEncryptionProcessorTests.VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                1,
                decryptionContext,
                invalidPathsConfigured: true);
        }

        [TestMethod]
        [DynamicData(nameof(JsonProcessors))]
        public async Task EncryptDecryptPropertyWithNullValue(int jsonProcessorValue)
        {
            JsonProcessor jsonProcessor = ResolveJsonProcessor(jsonProcessorValue);
            TestDoc testDoc = TestDoc.Create();
            testDoc.SensitiveStr = null;

            if (jsonProcessor != JsonProcessor.Newtonsoft)
            {
                await VerifyUnsupportedProcessorAsync(testDoc, jsonProcessor);
                return;
            }

            JObject encryptedDoc = await LegacyEncryptionProcessorTests.VerifyEncryptionSucceeded(testDoc, jsonProcessor);

            (JObject decryptedDoc, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedDoc,
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            LegacyEncryptionProcessorTests.VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                TestDoc.PathsToEncrypt.Count,
                decryptionContext);
        }

        [TestMethod]
        [DynamicData(nameof(JsonProcessors))]
        public async Task ValidateEncryptDecryptDocument(int jsonProcessorValue)
        {
            JsonProcessor jsonProcessor = ResolveJsonProcessor(jsonProcessorValue);
            TestDoc testDoc = TestDoc.Create();

            if (jsonProcessor != JsonProcessor.Newtonsoft)
            {
                await VerifyUnsupportedProcessorAsync(testDoc, jsonProcessor);
                return;
            }

            JObject encryptedDoc = await LegacyEncryptionProcessorTests.VerifyEncryptionSucceeded(testDoc, jsonProcessor);

            (JObject decryptedDoc, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedDoc,
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            LegacyEncryptionProcessorTests.VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                TestDoc.PathsToEncrypt.Count,
                decryptionContext);
        }

        [TestMethod]
        [DynamicData(nameof(JsonProcessors))]
        public async Task ValidateDecryptStream(int jsonProcessorValue)
        {
            JsonProcessor jsonProcessor = ResolveJsonProcessor(jsonProcessorValue);
            TestDoc testDoc = TestDoc.Create();

            if (jsonProcessor != JsonProcessor.Newtonsoft)
            {
                await VerifyUnsupportedProcessorAsync(testDoc, jsonProcessor);
                return;
            }

            EncryptionItemRequestOptions requestOptions = RequestOptionsOverrideHelper.Create(LegacyEncryptionProcessorTests.encryptionOptions, jsonProcessor);

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                requestOptions,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            (Stream decryptedStream, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedStream,
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Assert.IsTrue(decryptedStream.CanSeek);
            Assert.AreEqual(0L, decryptedStream.Position);
            Assert.IsTrue(decryptedStream.Length > 0);
            JObject decryptedDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(decryptedStream);
            LegacyEncryptionProcessorTests.VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                TestDoc.PathsToEncrypt.Count,
                decryptionContext);
        }

        [TestMethod]
        public async Task DecryptStreamWithoutEncryptedProperty()
        {
            TestDoc testDoc = TestDoc.Create();
            Stream docStream = testDoc.ToStream();

            (Stream decryptedStream, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                docStream,
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Assert.IsTrue(decryptedStream.CanSeek);
            Assert.AreEqual(0, decryptedStream.Position);
            Assert.AreEqual(docStream.Length, decryptedStream.Length);
            Assert.IsNull(decryptionContext);
            Assert.AreSame(docStream, decryptedStream);
            Assert.IsTrue(JToken.DeepEquals(
                JObject.FromObject(testDoc),
                EncryptionProcessor.BaseSerializer.FromStream<JObject>(decryptedStream)));
        }

        private static async Task VerifyUnsupportedProcessorAsync(
            TestDoc testDoc,
            JsonProcessor jsonProcessor,
            EncryptionOptions options = null)
        {
            Mock<Encryptor> encryptor = TestEncryptorFactory.CreateLegacy(dekId);
            using Stream input = testDoc.ToStream();
            long originalLength = input.Length;
            EncryptionItemRequestOptions requestOptions = RequestOptionsOverrideHelper.Create(
                options ?? LegacyEncryptionProcessorTests.encryptionOptions,
                jsonProcessor);

            NotSupportedException exception = await Assert.ThrowsExceptionAsync<NotSupportedException>(() =>
                EncryptionProcessor.EncryptAsync(
                    input,
                    encryptor.Object,
                    requestOptions,
                    new CosmosDiagnosticsContext(),
                    CancellationToken.None));

            StringAssert.Contains(exception.Message, "JsonProcessor.Stream");
            encryptor.Verify(value => value.EncryptAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            encryptor.Verify(value => value.DecryptAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.IsTrue(input.CanRead);
            Assert.AreEqual(0L, input.Position);
            Assert.AreEqual(originalLength, input.Length);
            Assert.IsTrue(JToken.DeepEquals(
                JObject.FromObject(testDoc),
                EncryptionProcessor.BaseSerializer.FromStream<JObject>(input)));
        }

        private static async Task<JObject> VerifyEncryptionSucceeded(
            TestDoc testDoc,
            JsonProcessor jsonProcessor,
            EncryptionOptions options = null)
        {
            options ??= LegacyEncryptionProcessorTests.encryptionOptions;
            EncryptionItemRequestOptions requestOptions = RequestOptionsOverrideHelper.Create(options, jsonProcessor);

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                requestOptions,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            JObject encryptedDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(encryptedStream);
            
            Assert.AreEqual(testDoc.Id, encryptedDoc.Property("id").Value.Value<string>());
            Assert.AreEqual(testDoc.PK, encryptedDoc.Property(nameof(TestDoc.PK)).Value.Value<string>());
            Assert.AreEqual(testDoc.NonSensitive, encryptedDoc.Property(nameof(TestDoc.NonSensitive)).Value.Value<string>());
            foreach (JProperty property in JObject.FromObject(testDoc).Properties())
            {
                if (options.PathsToEncrypt.Contains("/" + property.Name))
                {
                    Assert.IsNull(encryptedDoc.Property(property.Name), $"Encrypted property {property.Name} must not remain in plaintext.");
                }
                else
                {
                    Assert.IsTrue(JToken.DeepEquals(property.Value, encryptedDoc[property.Name]), $"Untargeted property {property.Name} must be unchanged.");
                }
            }

            JProperty eiJProp = encryptedDoc.Property(Constants.EncryptedInfo);
            Assert.IsNotNull(eiJProp);
            Assert.IsNotNull(eiJProp.Value);
            Assert.AreEqual(JTokenType.Object, eiJProp.Value.Type);
            EncryptionProperties encryptionProperties = ((JObject)eiJProp.Value).ToObject<EncryptionProperties>();

            Assert.IsNotNull(encryptionProperties);
            Assert.AreEqual(LegacyEncryptionProcessorTests.dekId, encryptionProperties.DataEncryptionKeyId);
            Assert.AreEqual(CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized, encryptionProperties.EncryptionAlgorithm);
            Assert.AreEqual(2, encryptionProperties.EncryptionFormatVersion);
            Assert.IsNotNull(encryptionProperties.EncryptedData);
            Assert.IsTrue(encryptionProperties.EncryptedData.Length > 0);
            CollectionAssert.AreEquivalent(options.PathsToEncrypt.ToArray(), encryptionProperties.EncryptedPaths.ToArray());

            return encryptedDoc;
        }

        private static void VerifyDecryptionSucceeded(
            JObject decryptedDoc,
            TestDoc expectedDoc,
            int pathCount,
            DecryptionContext decryptionContext,
            bool invalidPathsConfigured = false)
        {
            Assert.AreEqual(expectedDoc.SensitiveStr, decryptedDoc.Property(nameof(TestDoc.SensitiveStr)).Value.Value<string>());
            Assert.AreEqual(expectedDoc.SensitiveInt, decryptedDoc.Property(nameof(TestDoc.SensitiveInt)).Value.Value<int>());
            Assert.IsNull(decryptedDoc.Property(Constants.EncryptedInfo));
            Assert.IsNull(decryptedDoc.Property("Invalid"));
            Assert.IsTrue(JToken.DeepEquals(JObject.FromObject(expectedDoc), decryptedDoc), "Every original field and JSON type must round-trip.");
            if (expectedDoc.SensitiveStr == null)
            {
                Assert.AreEqual(JTokenType.Null, decryptedDoc.Property(nameof(TestDoc.SensitiveStr)).Value.Type);
            }
            
            Assert.IsNotNull(decryptionContext);
            Assert.IsNotNull(decryptionContext.DecryptionInfoList);
            Assert.AreEqual(1, decryptionContext.DecryptionInfoList.Count);
            DecryptionInfo decryptionInfo = decryptionContext.DecryptionInfoList[0];
            Assert.AreEqual(LegacyEncryptionProcessorTests.dekId, decryptionInfo.DataEncryptionKeyId);
            Assert.AreEqual(pathCount, decryptionInfo.PathsDecrypted.Count);
            CollectionAssert.AreEquivalent(
                invalidPathsConfigured ? new[] { "/SensitiveStr" } : TestDoc.PathsToEncrypt.ToArray(),
                decryptionInfo.PathsDecrypted.ToArray());
        }

        public static IEnumerable<object[]> JsonProcessors
        {
            get
            {
                yield return new object[] { (int)JsonProcessor.Newtonsoft };
#if NET8_0_OR_GREATER
                yield return new object[] { (int)JsonProcessor.Stream };
#endif
            }
        }

        private static JsonProcessor ResolveJsonProcessor(int value)
        {
            if (!Enum.IsDefined(typeof(JsonProcessor), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Invalid JsonProcessor value supplied to test.");
            }

            return (JsonProcessor)value;
        }
    }

#pragma warning restore CS0618 // Type or member is obsolete
}
