//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Tests;
    using Moq;
    using Newtonsoft.Json.Linq;

    internal static class EncryptionScenarioTestHelpers
    {
        internal const string KeyId = "scenario-dek";
        internal const string Mde = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized;
#pragma warning disable CS0618
        internal const string Legacy = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized;
#pragma warning restore CS0618

        internal static IEnumerable<int> Processors
        {
            get
            {
                yield return (int)JsonProcessor.Newtonsoft;
#if NET8_0_OR_GREATER
                yield return (int)JsonProcessor.Stream;
#endif
            }
        }

        internal static MemoryStream Stream(string json) => new(Encoding.UTF8.GetBytes(json));

        internal static MemoryStream Output()
        {
            MemoryStream output = new();
            output.Write(new byte[] { 11, 12, 13, 14, 15 }, 0, 5);
            output.Position = 2;
            return output;
        }

        internal static EncryptionItemRequestOptions Options(int processor, string algorithm = Mde, params string[] paths)
            => RequestOptionsOverrideHelper.Create(new EncryptionOptions
            {
                DataEncryptionKeyId = KeyId,
                EncryptionAlgorithm = algorithm,
                PathsToEncrypt = paths.Length == 0 ? new[] { "/Sensitive" } : paths,
            }, (JsonProcessor)processor);

        internal static JObject Metadata(string algorithm, params string[] paths) => new()
        {
            ["_ef"] = algorithm == Legacy ? 2 : 3,
            ["_ea"] = algorithm,
            ["_en"] = KeyId,
            ["_ed"] = algorithm == Legacy ? JValue.CreateString("9wEC") : JValue.CreateNull(),
            ["_ep"] = new JArray(paths),
        };

        internal static JObject PlainItem(string id = "i") => new()
        {
            ["id"] = id,
            ["PK"] = "p",
            ["Plain"] = "preserved",
            ["Sensitive"] = "secret!",
        };

        internal static JObject MdeItem(string id = "mde")
        {
            JObject item = PlainItem(id);
            item["Sensitive"] = Convert.ToBase64String(new byte[] { 2 }.Concat(Encoding.UTF8.GetBytes("secret!")).ToArray());
            item["_ei"] = Metadata(Mde, "/Sensitive");
            return item;
        }

        internal static JObject LegacyItem(string id = "legacy")
        {
            JObject item = PlainItem(id);
            item.Remove("Sensitive");
            item["_ei"] = Metadata(Legacy, "/Sensitive");
            item["_ei"]["_ed"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"Sensitive\":\"secret!\"}"));
            return item;
        }

        internal static JObject Feed(params JToken[] items) => new()
        {
            ["_rid"] = "scenario-feed",
            ["Documents"] = new JArray(items),
            ["_count"] = items.Length,
        };

        internal static ResponseMessage Response(HttpStatusCode status, string json) => new(status)
        {
            Content = json == null ? null : Stream(json),
        };

        internal static async Task<Exception> CaptureExceptionAsync(Func<Task> operation)
        {
            try
            {
                await operation();
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        internal static Container Container(
            Encryptor encryptor,
            out Mock<Container> inner,
            out Mock<CosmosResponseFactory> factory)
        {
            Mock<CosmosSerializer> serializer = new(MockBehavior.Strict);
            serializer.Setup(s => s.ToStream(It.IsAny<JObject>()))
                .Returns((JObject item) => EncryptionProcessor.BaseSerializer.ToStream(item));
            serializer.Setup(s => s.FromStream<JObject>(It.IsAny<Stream>()))
                .Returns((Stream input) => EncryptionProcessor.BaseSerializer.FromStream<JObject>(input));
            factory = new Mock<CosmosResponseFactory>();
            factory.Setup(f => f.CreateItemResponse<JObject>(It.IsAny<ResponseMessage>()))
                .Returns((ResponseMessage response) =>
                {
                    JObject item = EncryptionProcessor.BaseSerializer.FromStream<JObject>(response.Content);
                    Mock<ItemResponse<JObject>> result = new();
                    result.SetupGet(r => r.Resource).Returns(item);
                    result.SetupGet(r => r.StatusCode).Returns(response.StatusCode);
                    response.Dispose();
                    return result.Object;
                });
            Mock<CosmosClient> client = new();
            client.SetupGet(c => c.ClientOptions).Returns(new CosmosClientOptions { Serializer = serializer.Object });
            client.SetupGet(c => c.ResponseFactory).Returns(factory.Object);
            Mock<Database> database = new();
            database.SetupGet(d => d.Client).Returns(client.Object);
            inner = new Mock<Container>();
            inner.SetupGet(c => c.Database).Returns(database.Object);
            return inner.Object.WithEncryptor(encryptor);
        }

        internal sealed class PublicEncryptor : Encryptor
        {
            internal Func<byte[], string, CancellationToken, Task<byte[]>> OnEncrypt { get; set; }

            internal Func<byte[], string, CancellationToken, Task<byte[]>> OnDecrypt { get; set; }

            internal int EncryptCalls { get; private set; }

            internal int DecryptCalls { get; private set; }

            internal int KeyCalls { get; private set; }

            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                this.KeyCalls++;
                throw new InvalidOperationException("The public-array scenario encryptor has no key-access capability.");
            }

            public override Task<byte[]> EncryptAsync(byte[] plainText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                this.EncryptCalls++;
                return this.OnEncrypt == null ? Task.FromResult(plainText.ToArray()) : this.OnEncrypt(plainText, encryptionAlgorithm, cancellationToken);
            }

            public override Task<byte[]> DecryptAsync(byte[] cipherText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                this.DecryptCalls++;
                return this.OnDecrypt == null ? Task.FromResult(cipherText.ToArray()) : this.OnDecrypt(cipherText, encryptionAlgorithm, cancellationToken);
            }
        }
    }
}
