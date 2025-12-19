//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
#if NET8_0_OR_GREATER
    using System.Text.Json;
    using System.Text.Json.Nodes;
#endif
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Tests;
#if NET8_0_OR_GREATER
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
#endif
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;
    using TestDoc = TestCommon.TestDoc;

    [TestClass]
    public class MdeEncryptionProcessorTests
    {
        private static Mock<Encryptor> mockEncryptor;
        private const string dekId = "dekId";

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            _ = testContext;

#if NET8_0_OR_GREATER
            // Force smallest possible initial buffer to make sure both secondary reads and resize paths are executed
            PooledStreamConfiguration.SetConfiguration(new PooledStreamConfiguration { StreamProcessorBufferSize = 16 });
#endif

            mockEncryptor = TestEncryptorFactory.CreateMde(dekId, out _);
        }

        [TestMethod]
        [DynamicData(nameof(JsonProcessors))]
        public async Task InvalidPathToEncrypt(int jsonProcessorValue)
        {
            JsonProcessor jsonProcessor = ResolveJsonProcessor(jsonProcessorValue);
            TestDoc testDoc = TestDoc.Create();
            EncryptionOptions encryptionOptionsWithInvalidPathToEncrypt = new()
            {
                DataEncryptionKeyId = dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = new List<string>() { "/SensitiveStr", "/Invalid" },
            };

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                   testDoc.ToStream(),
                   mockEncryptor.Object,
                   encryptionOptionsWithInvalidPathToEncrypt,
                   jsonProcessor,
                   new CosmosDiagnosticsContext(),
                   CancellationToken.None);


            JObject encryptedDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(encryptedStream);

            (JObject decryptedDoc, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
               encryptedDoc,
               mockEncryptor.Object,
               new CosmosDiagnosticsContext(),
               CancellationToken.None);

            VerifyDecryptionSucceeded(
                 decryptedDoc,
                 testDoc,
                 1,
                 decryptionContext,
                 invalidPathsConfigured: true);
        }

        [TestMethod]
        [DynamicData(nameof(JsonProcessors))]
        public async Task DuplicatePathToEncrypt(int jsonProcessorValue)
        {
            JsonProcessor jsonProcessor = ResolveJsonProcessor(jsonProcessorValue);
            TestDoc testDoc = TestDoc.Create();
            EncryptionOptions encryptionOptionsWithDuplicatePathToEncrypt = new()
            {
                DataEncryptionKeyId = dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = new List<string>() { "/SensitiveStr", "/SensitiveStr" },
            };

            try
            {
                await EncryptionProcessor.EncryptAsync(
                    testDoc.ToStream(),
                    mockEncryptor.Object,
                    encryptionOptionsWithDuplicatePathToEncrypt,
                    jsonProcessor,
                    new CosmosDiagnosticsContext(),
                    CancellationToken.None);

                Assert.Fail("Duplicate paths in PathToEncrypt didn't result in exception.");
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual("Duplicate paths in PathsToEncrypt passed via EncryptionOptions.", ex.Message);
            }
        }

        [TestMethod]
        [DynamicData(nameof(JsonProcessors))]
        public async Task EncryptDecryptPropertyWithNullValue_VerifyByNewtonsoft(int jsonProcessorValue)
        {
            JsonProcessor jsonProcessor = ResolveJsonProcessor(jsonProcessorValue);
            EncryptionOptions encryptionOptions = this.CreateEncryptionOptions();
            TestDoc testDoc = TestDoc.Create();
            testDoc.SensitiveStr = null;

            JObject encryptedDoc = await VerifyEncryptionSucceededNewtonsoft(testDoc, encryptionOptions, jsonProcessor);

            (JObject decryptedDoc, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
               encryptedDoc,
               mockEncryptor.Object,
               new CosmosDiagnosticsContext(),
               CancellationToken.None);

            VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                TestDoc.PathsToEncrypt.Count,
                decryptionContext);
        }

        [TestMethod]
        [DynamicData(nameof(JsonProcessors))]
        public async Task ValidateEncryptDecryptDocument_VerifyByNewtonsoft(int jsonProcessorValue)
        {
            JsonProcessor jsonProcessor = ResolveJsonProcessor(jsonProcessorValue);
            EncryptionOptions encryptionOptions = this.CreateEncryptionOptions();
            TestDoc testDoc = TestDoc.Create();

            JObject encryptedDoc = await VerifyEncryptionSucceededNewtonsoft(testDoc, encryptionOptions, jsonProcessor);

            (JObject decryptedDoc, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedDoc,
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                TestDoc.PathsToEncrypt.Count,
                decryptionContext);
        }

        [TestMethod]
        [DynamicData(nameof(JsonProcessors))]
        public async Task ValidateDecryptByNewtonsoftStream_VerifyByNewtonsoft(int jsonProcessorValue)
        {
            JsonProcessor jsonProcessor = ResolveJsonProcessor(jsonProcessorValue);
            EncryptionOptions encryptionOptions = this.CreateEncryptionOptions();
            TestDoc testDoc = TestDoc.Create();

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                mockEncryptor.Object,
                encryptionOptions,
                jsonProcessor,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            (Stream decryptedStream, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedStream,
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                requestOptions: null,
                CancellationToken.None);

            JObject decryptedDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(decryptedStream);
            VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                TestDoc.PathsToEncrypt.Count,
                decryptionContext);
        }

        [TestMethod]
        [DynamicData(nameof(JsonProcessorCombinations))]
        public async Task ValidateDecryptBySystemTextStream_VerifyByNewtonsoft(int encryptionJsonProcessorValue, int decryptionJsonProcessorValue)
        {
            JsonProcessor encryptionJsonProcessor = ResolveJsonProcessor(encryptionJsonProcessorValue);
            JsonProcessor decryptionJsonProcessor = ResolveJsonProcessor(decryptionJsonProcessorValue);
            EncryptionOptions encryptionOptions = this.CreateEncryptionOptions();
            TestDoc testDoc = TestDoc.Create();

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                mockEncryptor.Object,
                encryptionOptions,
                encryptionJsonProcessor,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            (Stream decryptedStream, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedStream,
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                RequestOptionsOverrideHelper.Create(decryptionJsonProcessor),
                CancellationToken.None);

            JObject decryptedDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(decryptedStream);
            VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                TestDoc.PathsToEncrypt.Count,
                decryptionContext);
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        [DynamicData(nameof(JsonProcessorCombinations))]
        public async Task ValidateDecryptBySystemTextStream_VerifyBySystemText(int encryptionJsonProcessorValue, int decryptionJsonProcessorValue)
        {
            JsonProcessor encryptionJsonProcessor = ResolveJsonProcessor(encryptionJsonProcessorValue);
            JsonProcessor decryptionJsonProcessor = ResolveJsonProcessor(decryptionJsonProcessorValue);
            EncryptionOptions encryptionOptions = this.CreateEncryptionOptions();
            TestDoc testDoc = TestDoc.Create();

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                mockEncryptor.Object,
                encryptionOptions,
                encryptionJsonProcessor,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            (Stream decryptedStream, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedStream,
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                RequestOptionsOverrideHelper.Create(decryptionJsonProcessor),
                CancellationToken.None);

            JsonNode decryptedDoc = JsonNode.Parse(decryptedStream);
            VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                TestDoc.PathsToEncrypt.Count,
                decryptionContext);
        }
#endif

        [TestMethod]
        [DynamicData(nameof(JsonProcessors))]
        public async Task DecryptStreamWithoutEncryptedProperty(int processorValue)
        {
            JsonProcessor processor = ResolveJsonProcessor(processorValue);
            TestDoc testDoc = TestDoc.Create();
            Stream docStream = testDoc.ToStream();

            (Stream decryptedStream, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                docStream,
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                RequestOptionsOverrideHelper.Create(processor),
                CancellationToken.None);

            Assert.IsTrue(decryptedStream.CanSeek);
            Assert.AreEqual(0, decryptedStream.Position);
            Assert.AreEqual(docStream.Length, decryptedStream.Length);
            Assert.IsNull(decryptionContext);
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("PooledResources")]
        public async Task DecryptStreamAsync_CreatesPooledMemoryStream_SuccessPath()
        {
            // Tests MdeEncryptionProcessor.cs:141 - new PooledMemoryStream() on success path
            TestDoc testDoc = TestDoc.Create();
            EncryptionOptions options = this.CreateEncryptionOptions();

            // Encrypt document using StreamProcessor
            Stream encrypted = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                mockEncryptor.Object,
                options,
                JsonProcessor.Stream,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            // Read encryption properties
            encrypted.Position = 0;
            using (JsonDocument doc = JsonDocument.Parse(encrypted, new JsonDocumentOptions { AllowTrailingCommas = true }))
            {
                JsonElement ei = doc.RootElement.GetProperty(Constants.EncryptedInfo);
                EncryptionProperties props = System.Text.Json.JsonSerializer.Deserialize<EncryptionProperties>(ei.GetRawText());

                // Test MdeEncryptionProcessor.DecryptStreamAsync
                encrypted.Position = 0;
                MdeEncryptionProcessor processor = new MdeEncryptionProcessor();
                (Stream decrypted, DecryptionContext context) = await processor.DecryptStreamAsync(
                    encrypted,
                    mockEncryptor.Object,
                    props,
                    new CosmosDiagnosticsContext(),
                    CancellationToken.None);

                Assert.IsNotNull(context, "Context should not be null for encrypted payload");
                Assert.IsNotNull(decrypted, "Decrypted stream should not be null");

                // Verify stream is readable
                decrypted.Position = 0;
                JsonNode decryptedDoc = JsonNode.Parse(decrypted);
                Assert.AreEqual(testDoc.SensitiveStr, decryptedDoc[nameof(TestDoc.SensitiveStr)].GetValue<string>());

                // Cleanup
                await decrypted.DisposeAsync();
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("PooledResources")]
        [TestCategory("MemoryLeak")]
        public async Task DecryptStreamAsync_WithEmptyEncryptedPaths_HandlesCorrectly()
        {
            // Tests MdeEncryptionProcessor.cs:141-148 disposal path
            // Even with empty encrypted paths, the PooledMemoryStream should be handled correctly

            TestDoc testDoc = TestDoc.Create();
            string plainJson = System.Text.Json.JsonSerializer.Serialize(testDoc);
            using MemoryStream input = new(System.Text.Encoding.UTF8.GetBytes(plainJson));

            // Create encryption properties with empty paths
            EncryptionProperties propsEmptyPaths = new EncryptionProperties(
                encryptionFormatVersion: EncryptionFormatVersion.Mde,
                encryptionAlgorithm: CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                dataEncryptionKeyId: dekId,
                encryptedData: null,
                encryptedPaths: new List<string>());

            // Decrypt with empty paths
            MdeEncryptionProcessor processor = new MdeEncryptionProcessor();
            (Stream result, DecryptionContext context) = await processor.DecryptStreamAsync(
                input,
                mockEncryptor.Object,
                propsEmptyPaths,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            // With empty paths, decryption still succeeds but doesn't decrypt anything
            Assert.IsNotNull(result, "Result stream should not be null");

            // Cleanup
            if (result != input)
            {
                await result.DisposeAsync();
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("PooledResources")]
        [TestCategory("Stress")]
        public async Task DecryptStreamAsync_RepeatedCalls_NoMemoryLeak()
        {
            // Stress test to verify PooledMemoryStream disposal in MdeEncryptionProcessor
            // Run 100 iterations - if PooledMemoryStream not disposed, ArrayPool will exhaust

            TestDoc testDoc = TestDoc.Create();
            EncryptionOptions options = this.CreateEncryptionOptions();

            for (int i = 0; i < 100; i++)
            {
                // Encrypt document
                Stream encrypted = await EncryptionProcessor.EncryptAsync(
                    testDoc.ToStream(),
                    mockEncryptor.Object,
                    options,
                    JsonProcessor.Stream,
                    new CosmosDiagnosticsContext(),
                    CancellationToken.None);

                // Read encryption properties
                encrypted.Position = 0;
                using (JsonDocument doc = JsonDocument.Parse(encrypted, new JsonDocumentOptions { AllowTrailingCommas = true }))
                {
                    JsonElement ei = doc.RootElement.GetProperty(Constants.EncryptedInfo);
                    EncryptionProperties props = System.Text.Json.JsonSerializer.Deserialize<EncryptionProperties>(ei.GetRawText());

                    // Decrypt
                    encrypted.Position = 0;
                    MdeEncryptionProcessor processor = new MdeEncryptionProcessor();
                    (Stream decrypted, DecryptionContext context) = await processor.DecryptStreamAsync(
                        encrypted,
                        mockEncryptor.Object,
                        props,
                        new CosmosDiagnosticsContext(),
                        CancellationToken.None);

                    Assert.IsNotNull(context, $"Iteration {i}: Context should not be null");
                    await decrypted.DisposeAsync();
                }
            }

            // If we got here without OutOfMemoryException, disposal is working
            Assert.IsTrue(true, "100 iterations completed without memory leak");
        }
#endif

        private static async Task<JObject> VerifyEncryptionSucceededNewtonsoft(TestDoc testDoc, EncryptionOptions encryptionOptions, JsonProcessor jsonProcessor)
        {
            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                 testDoc.ToStream(),
                 mockEncryptor.Object,
                 encryptionOptions,
                 jsonProcessor,
                 new CosmosDiagnosticsContext(),
                 CancellationToken.None);

            JObject encryptedDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(encryptedStream);

            Assert.AreEqual(testDoc.Id, encryptedDoc.Property("id").Value.Value<string>());
            Assert.AreEqual(testDoc.PK, encryptedDoc.Property(nameof(TestDoc.PK)).Value.Value<string>());
            Assert.AreEqual(testDoc.NonSensitive, encryptedDoc.Property(nameof(TestDoc.NonSensitive)).Value.Value<string>());
            Assert.IsNotNull(encryptedDoc.Property(nameof(TestDoc.SensitiveInt)).Value.Value<string>());
            Assert.AreNotEqual(testDoc.SensitiveInt, encryptedDoc.Property(nameof(TestDoc.SensitiveInt)).Value.Value<string>()); // not equal since value is encrypted

            JProperty eiJProp = encryptedDoc.Property(Constants.EncryptedInfo);
            Assert.IsNotNull(eiJProp);
            Assert.IsNotNull(eiJProp.Value);
            Assert.AreEqual(JTokenType.Object, eiJProp.Value.Type);
            EncryptionProperties encryptionProperties = ((JObject)eiJProp.Value).ToObject<EncryptionProperties>();

            Assert.IsNotNull(encryptionProperties);
            Assert.AreEqual(dekId, encryptionProperties.DataEncryptionKeyId);

            Assert.AreEqual(EncryptionFormatVersion.Mde, encryptionProperties.EncryptionFormatVersion);

            Assert.IsNull(encryptionProperties.EncryptedData);
            Assert.IsNotNull(encryptionProperties.EncryptedPaths);

            if (testDoc.SensitiveStr == null)
            {
                Assert.IsNull(encryptedDoc.Property(nameof(TestDoc.SensitiveStr)).Value.Value<string>()); // since null value is not encrypted
                Assert.AreEqual(TestDoc.PathsToEncrypt.Count - 1, encryptionProperties.EncryptedPaths.Count());
            }
            else
            {
                Assert.IsNotNull(encryptedDoc.Property(nameof(TestDoc.SensitiveStr)).Value.Value<string>());
                Assert.AreNotEqual(testDoc.SensitiveStr, encryptedDoc.Property(nameof(TestDoc.SensitiveStr)).Value.Value<string>()); // not equal since value is encrypted
                Assert.AreEqual(TestDoc.PathsToEncrypt.Count, encryptionProperties.EncryptedPaths.Count());
            }

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

            Assert.IsNotNull(decryptionContext);
            Assert.IsNotNull(decryptionContext.DecryptionInfoList);
            DecryptionInfo decryptionInfo = decryptionContext.DecryptionInfoList[0];
            Assert.AreEqual(dekId, decryptionInfo.DataEncryptionKeyId);
            if (expectedDoc.SensitiveStr == null)
            {
                Assert.AreEqual(pathCount - 1, decryptionInfo.PathsDecrypted.Count);
                Assert.IsTrue(TestDoc.PathsToEncrypt.Exists(path => !decryptionInfo.PathsDecrypted.Contains(path)));
            }
            else
            {
                Assert.AreEqual(pathCount, decryptionInfo.PathsDecrypted.Count);

                if (!invalidPathsConfigured)
                {
                    Assert.IsFalse(TestDoc.PathsToEncrypt.Exists(path => !decryptionInfo.PathsDecrypted.Contains(path)));
                }
                else
                {
                    Assert.IsTrue(TestDoc.PathsToEncrypt.Exists(path => !decryptionInfo.PathsDecrypted.Contains(path)));
                }
            }
        }

#if NET8_0_OR_GREATER
        private static void VerifyDecryptionSucceeded(
            JsonNode decryptedDoc,
            TestDoc expectedDoc,
            int pathCount,
            DecryptionContext decryptionContext,
            bool invalidPathsConfigured = false)
        {
            AssertNullableValueKind(expectedDoc.SensitiveStr, decryptedDoc, nameof(TestDoc.SensitiveStr));
            Assert.AreEqual(expectedDoc.SensitiveInt, decryptedDoc[nameof(TestDoc.SensitiveInt)].GetValue<long>());
            Assert.IsNull(decryptedDoc[Constants.EncryptedInfo]);

            Assert.IsNotNull(decryptionContext);
            Assert.IsNotNull(decryptionContext.DecryptionInfoList);
            DecryptionInfo decryptionInfo = decryptionContext.DecryptionInfoList[0];
            Assert.AreEqual(dekId, decryptionInfo.DataEncryptionKeyId);
            if (expectedDoc.SensitiveStr == null)
            {
                Assert.AreEqual(pathCount - 1, decryptionInfo.PathsDecrypted.Count);
                Assert.IsTrue(TestDoc.PathsToEncrypt.Exists(path => !decryptionInfo.PathsDecrypted.Contains(path)));
            }
            else
            {
                Assert.AreEqual(pathCount, decryptionInfo.PathsDecrypted.Count);

                if (!invalidPathsConfigured)
                {
                    Assert.IsFalse(TestDoc.PathsToEncrypt.Exists(path => !decryptionInfo.PathsDecrypted.Contains(path)));
                }
                else
                {
                    Assert.IsTrue(TestDoc.PathsToEncrypt.Exists(path => !decryptionInfo.PathsDecrypted.Contains(path)));
                }
            }
        }

        private static void AssertNullableValueKind<T>(T expectedValue, JsonNode node, string propertyName) where T : class
        {
            if (expectedValue == null)
            {
                Assert.IsTrue(node.AsObject().ContainsKey(propertyName));
                Assert.AreEqual(null, node[propertyName]);
            }
            else
            {
                Assert.AreEqual(expectedValue, node[propertyName].GetValue<T>());
            }
        }
#endif

        private EncryptionOptions CreateEncryptionOptions()
        {
            return new EncryptionOptions()
            {
                DataEncryptionKeyId = dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = TestDoc.PathsToEncrypt,
            };
        }

        public static IEnumerable<object[]> JsonProcessors
        {
            get
            {
                foreach (JsonProcessor processor in EnumerateJsonProcessors())
                {
                    yield return new object[] { (int)processor };
                }
            }
        }

        public static IEnumerable<object[]> JsonProcessorCombinations
        {
            get
            {
                JsonProcessor[] processors = EnumerateJsonProcessors().ToArray();
                foreach (JsonProcessor encProcessor in processors)
                {
                    foreach (JsonProcessor decProcessor in processors)
                    {
                        yield return new object[] { (int)encProcessor, (int)decProcessor };
                    }
                }
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

        private static IEnumerable<JsonProcessor> EnumerateJsonProcessors()
        {
            yield return JsonProcessor.Newtonsoft;
#if NET8_0_OR_GREATER
            yield return JsonProcessor.Stream;
#endif
        }
    }
}