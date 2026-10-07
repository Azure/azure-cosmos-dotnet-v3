//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using H = EncryptionScenarioTestHelpers;

    [TestClass]
    public class EncryptionScenarioContractRegressionTests
    {
        [DataTestMethod]
        [DataRow(H.Mde, "{}")]
        [DataRow(H.Mde, "[]")]
        [DataRow(H.Legacy, "{}")]
        [DataRow(H.Legacy, "[]")]
        public async Task LazyMalformedKey_RetainsPrimaryMetadataException(string algorithm, string key)
        {
            JObject document = algorithm == H.Legacy ? H.LegacyItem() : H.MdeItem();
            document["_ei"]["_en"] = JToken.Parse(key);
            string original = document.ToString();
            H.PublicEncryptor encryptor = new();
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            inner.Setup(c => c.ReadItemStreamAsync((string)document["id"], new PartitionKey("p"),
                    It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => H.Response(HttpStatusCode.OK, document.ToString(Formatting.None)));
            ItemResponse<DecryptableItem> response = await container.ReadItemAsync<DecryptableItem>((string)document["id"], new PartitionKey("p"));

            for (int attempt = 0; attempt < 2; attempt++)
            {
                EncryptionException failure = await Assert.ThrowsExceptionAsync<EncryptionException>(
                    () => response.Resource.GetItemAsync<JObject>());
                Assert.IsInstanceOfType(failure.InnerException, typeof(InvalidOperationException));
                Assert.AreEqual(EncryptionMetadataClassifier.InvalidMetadataMessage, failure.InnerException.Message);
                Assert.IsTrue(JToken.DeepEquals(document, JObject.Parse(failure.EncryptedContent)));
            }

            Assert.AreEqual(original, document.ToString());
            Assert.AreEqual(0, encryptor.DecryptCalls);
            Assert.AreEqual(0, encryptor.KeyCalls);
            await response.Resource.DisposeAsync();
        }

        [DataTestMethod]
        [DataRow("fault")]
        [DataRow("null")]
        [DataRow("cancel")]
        public async Task LegacyWriteFailure_DoesNotDisposeBorrowedCallerInput(string fault)
        {
            using CancellationTokenSource cancellation = new();
            InvalidOperationException expected = new("custom Legacy encryption failed");
            H.PublicEncryptor encryptor = new()
            {
                OnEncrypt = (bytes, algorithm, token) =>
                {
                    if (fault == "fault")
                    {
                        return Task.FromException<byte[]>(expected);
                    }

                    if (fault == "cancel")
                    {
                        cancellation.Cancel();
                        return Task.FromCanceled<byte[]>(cancellation.Token);
                    }

                    return Task.FromResult<byte[]>(null);
                },
            };
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            using MemoryStream input = H.Stream(H.PlainItem().ToString(Formatting.None));
            byte[] before = input.ToArray();
            Exception failure = await H.CaptureExceptionAsync(async () =>
            {
                using ResponseMessage response = await container.CreateItemStreamAsync(input, new PartitionKey("p"), H.Options(0, H.Legacy), cancellation.Token);
            });

            if (fault == "fault")
            {
                Assert.AreSame(expected, failure);
            }
            else if (fault == "cancel")
            {
                Assert.IsInstanceOfType(failure, typeof(OperationCanceledException));
                Assert.AreEqual(cancellation.Token, ((OperationCanceledException)failure).CancellationToken);
            }
            else
            {
                Assert.IsInstanceOfType(failure, typeof(InvalidOperationException));
                StringAssert.Contains(failure.Message, "null cipherText");
            }

            inner.Verify(c => c.CreateItemStreamAsync(It.IsAny<Stream>(), It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
            CollectionAssert.AreEqual(before, input.ToArray());
            // EncryptionProcessor's EncryptAsync remarks promise that failure does not dispose input.
            Assert.IsTrue(input.CanRead, "A failed Legacy encryption must leave borrowed caller input open.");
        }

        [DataTestMethod]
        [DynamicData(nameof(DateStringRows), DynamicDataSourceType.Method)]
        public async Task EncryptDecrypt_DateLookingStrings_RetainExactStringValue(string algorithm, bool nested, int processor)
        {
            const string date = "2024-01-01T10:00:00.000Z";
            JObject original = H.PlainItem();
            original["Sensitive"] = nested
                ? new JObject { ["when"] = JValue.CreateString(date), ["values"] = new JArray(JValue.CreateString(date)) }
                : JValue.CreateString(date);
            H.PublicEncryptor encryptor = new();
            using MemoryStream input = H.Stream(original.ToString(Formatting.None));
            using Stream encrypted = await EncryptionProcessor.EncryptAsync(input, encryptor, H.Options(0, algorithm),
                new CosmosDiagnosticsContext(), CancellationToken.None);
            using MemoryStream readInput = new();
            await encrypted.CopyToAsync(readInput);
            readInput.Position = 0;
            (Stream decrypted, _) = await EncryptionProcessor.DecryptAsync(readInput, encryptor, new CosmosDiagnosticsContext(),
                RequestOptionsOverrideHelper.Create((JsonProcessor)processor), CancellationToken.None);
            using (decrypted)
            {
                JObject actual = EncryptionProcessor.BaseSerializer.FromStream<JObject>(decrypted);
                Assert.IsTrue(JToken.DeepEquals(original, actual),
                    $"String JSON values must retain their exact contents. Expected {original}; actual {actual}.");
                Assert.AreEqual(JTokenType.String, nested ? actual["Sensitive"]["when"].Type : actual["Sensitive"].Type);
            }
        }

        public static IEnumerable<object[]> DateStringRows()
        {
            foreach (int processor in H.Processors)
            {
                foreach (string algorithm in new[] { H.Legacy, H.Mde })
                {
                    yield return new object[] { algorithm, false, processor };
                    yield return new object[] { algorithm, true, processor };
                }
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(ReadManyErrorRows), DynamicDataSourceType.Method)]
        public async Task ReadManyStream_ServiceErrorRemainsInspectable(int status, bool nullContent, int processor)
        {
            const string errorBody = "{\"code\":\"TooManyRequests\",\"message\":\"service sentinel\"}";
            using ResponseMessage raw = H.Response((HttpStatusCode)status, nullContent ? null : errorBody);
            raw.Headers["x-ms-activity-id"] = "activity-sentinel";
            raw.Headers["x-ms-request-charge"] = "12.5";
            raw.Headers["x-ms-retry-after-ms"] = "23";
            raw.Headers["x-scenario-sentinel"] = "retained-header";
            H.PublicEncryptor encryptor = new();
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            IReadOnlyList<(string id, PartitionKey partitionKey)> items = new[] { ("i", new PartitionKey("p")) };
            ReadManyRequestOptions options = new()
            {
                Properties = new Dictionary<string, object> { [JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey] = (JsonProcessor)processor },
            };
            inner.Setup(c => c.ReadManyItemsStreamAsync(items, options, It.IsAny<CancellationToken>())).ReturnsAsync(raw);

            using ResponseMessage response = await container.ReadManyItemsStreamAsync(items, options);

            Assert.AreEqual((HttpStatusCode)status, response.StatusCode);
            foreach (string header in raw.Headers)
            {
                Assert.AreEqual(raw.Headers[header], response.Headers[header], header);
            }

            Assert.IsFalse(response.IsSuccessStatusCode);
            if (nullContent)
            {
                Assert.IsNull(response.Content);
            }
            else
            {
                using StreamReader reader = new(response.Content, Encoding.UTF8, false, 1024, leaveOpen: true);
                Assert.AreEqual(errorBody, await reader.ReadToEndAsync());
            }

            Assert.AreEqual(0, encryptor.KeyCalls);
            Assert.AreEqual(0, encryptor.DecryptCalls);
        }

        public static IEnumerable<object[]> ReadManyErrorRows()
        {
            foreach (int processor in H.Processors)
            {
                foreach (int status in new[] { 404, 429 })
                {
                    yield return new object[] { status, false, processor };
                    yield return new object[] { status, true, processor };
                }
            }
        }

#if NET8_0_OR_GREATER
        [DataTestMethod]
        [DataRow(null)]
        [DataRow("Stream")]
        [DataRow("Newtonsoft")]
        public async Task PointRead_ContainerStreamDefaultAndExplicitOverride_SelectDocumentedReader(string processorOverride)
        {
            List<string> scopes = new();
            using ActivityListener listener = new()
            {
                ShouldListenTo = source => source.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
                ActivityStarted = activity => scopes.Add(activity.OperationName),
            };
            ActivitySource.AddActivityListener(listener);
            H.PublicEncryptor encryptor = new();
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            container.UseStreamingJsonProcessingByDefault();
            ItemRequestOptions options = processorOverride == null ? null : new()
            {
                Properties = new Dictionary<string, object> { [JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey] = processorOverride },
            };
            inner.Setup(c => c.ReadItemStreamAsync("mde", new PartitionKey("p"), options, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => H.Response(HttpStatusCode.OK, H.MdeItem().ToString(Formatting.None)));
            using ResponseMessage response = await container.ReadItemStreamAsync("mde", new PartitionKey("p"), options);
            JObject actual = EncryptionProcessor.BaseSerializer.FromStream<JObject>(response.Content);
            Assert.AreEqual("secret!", (string)actual["Sensitive"]);
            string expected = processorOverride ?? "Stream";
            CollectionAssert.Contains(scopes, CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + expected,
                "The configured container read default applies unless a per-call override is present.");
        }
#endif
    }
}
