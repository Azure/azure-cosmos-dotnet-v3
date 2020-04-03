//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.DataEncryptionKeyProvider.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class EncryptionUnitTests
    {
        private const string DatabaseId = "mockDatabase";
        private const string ContainerId = "mockContainer";
        private const string DekId = "mockDek";
        private const CosmosEncryptionAlgorithm Algo = CosmosEncryptionAlgorithm.AE_AES_256_CBC_HMAC_SHA_256_RANDOMIZED;
        private const double requestCharge = 0.6;

        private Mock<DataEncryptionKey> mockDataEncryptionKey;
        private Mock<Cosmos.DataEncryptionKeyProvider> mockDataEncryptionKeyProvider;
        private EncryptionTestHandler testHandler;

        [TestMethod]
        public async Task EncryptionUTCreateItemWithUnknownDek()
        {
            Container container = this.GetContainerWithMockSetup();
            MyItem item = EncryptionUnitTests.GetNewItem();

            try
            {
                await container.CreateItemAsync(
                    item,
                    new Cosmos.PartitionKey(item.PK),
                    new ItemRequestOptions
                    {
                        EncryptionOptions = new EncryptionOptions
                        {
                            DataEncryptionKeyId = "random",
                            EncryptionAlgorithm = CosmosEncryptionAlgorithm.AE_AES_256_CBC_HMAC_SHA_256_RANDOMIZED,
                            PathsToEncrypt = MyItem.PathsToEncrypt
                        }
                    });

                Assert.Fail("Expected CreateItemAsync with unknown data encryption key to fail");
            }
            catch(Exception)
            {
                // todo: Should we expose a exception class in the contract too
            }
        }

        [TestMethod]
        public async Task EncryptionUTCreateItem()
        {
            Container container = this.GetContainerWithMockSetup();
            MyItem item = await EncryptionUnitTests.CreateItemAsync(container, EncryptionUnitTests.DekId, MyItem.PathsToEncrypt);

            // Validate server state
            Assert.IsTrue(this.testHandler.Items.TryGetValue(item.Id, out JObject serverItem));
            Assert.IsNotNull(serverItem);
            Assert.AreEqual(item.Id, serverItem.Property(Constants.Properties.Id).Value.Value<string>());
            Assert.AreEqual(item.PK, serverItem.Property(nameof(MyItem.PK)).Value.Value<string>());
            Assert.IsNull(serverItem.Property(nameof(MyItem.EncStr1)));
            Assert.IsNull(serverItem.Property(nameof(MyItem.EncInt)));

            JProperty eiJProp = serverItem.Property(Constants.Properties.EncryptedInfo);
            Assert.IsNotNull(eiJProp);
            Assert.IsNotNull(eiJProp.Value);
            Assert.AreEqual(JTokenType.Object, eiJProp.Value.Type);
            EncryptionProperties encryptionPropertiesAtServer = ((JObject)eiJProp.Value).ToObject<EncryptionProperties>();

            Assert.IsNotNull(encryptionPropertiesAtServer);
            Assert.AreEqual(EncryptionUnitTests.DekId, encryptionPropertiesAtServer.DataEncryptionKeyId);
            Assert.AreEqual(2, encryptionPropertiesAtServer.EncryptionFormatVersion);
            Assert.IsNotNull(encryptionPropertiesAtServer.EncryptedData);

            JObject decryptedJObj = EncryptionUnitTests.ParseStream(new MemoryStream(EncryptionUnitTests.DecryptData(encryptionPropertiesAtServer.EncryptedData)));
            Assert.AreEqual(2, decryptedJObj.Properties().Count());
            Assert.AreEqual(item.EncStr1, decryptedJObj.Property(nameof(MyItem.EncStr1)).Value.Value<string>());
            Assert.AreEqual(item.EncInt, decryptedJObj.Property(nameof(MyItem.EncInt)).Value.Value<int>());
        }

        [TestMethod]
        public async Task EncryptionUTReadItem()
        {
            Container container = this.GetContainerWithMockSetup();
            MyItem item = await EncryptionUnitTests.CreateItemAsync(container, EncryptionUnitTests.DekId, MyItem.PathsToEncrypt);

            ItemResponse<MyItem> readResponse = await container.ReadItemAsync<MyItem>(item.Id, new Cosmos.PartitionKey(item.PK));
            Assert.AreEqual(item, readResponse.Resource);
        }

        private static async Task<MyItem> CreateItemAsync(Container container, string dekId, List<string> pathsToEncrypt)
        {
            MyItem item = EncryptionUnitTests.GetNewItem();
            ItemResponse<MyItem> response = await container.CreateItemAsync<MyItem>(
                item,
                requestOptions: new ItemRequestOptions
                {
                    EncryptionOptions = new EncryptionOptions
                    {
                        DataEncryptionKeyId = dekId,
                        EncryptionAlgorithm = EncryptionUnitTests.Algo,
                        PathsToEncrypt = pathsToEncrypt
                    }
                });

            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.AreEqual(item, response.Resource);
            return item;
        }

        private static MyItem GetNewItem()
        {
            return new MyItem()
            {
                Id = Guid.NewGuid().ToString(),
                PK = "pk",
                EncStr1 = "sensitive",
                EncInt = 10000
            };
        }

        private Container GetContainerWithMockSetup(EncryptionTestHandler encryptionTestHandler = null)
        {
            this.testHandler = encryptionTestHandler ?? new EncryptionTestHandler();

            this.mockDataEncryptionKeyProvider = new Mock<Cosmos.DataEncryptionKeyProvider>();

            this.mockDataEncryptionKey = new Mock<DataEncryptionKey>();
            this.mockDataEncryptionKey.Setup(m => m.EncryptData(It.IsAny<byte[]>()))
                .Returns((byte[] plainText) => EncryptionUnitTests.EncryptData(plainText));
            this.mockDataEncryptionKey.Setup(m => m.DecryptData(It.IsAny<byte[]>()))
                .Returns((byte[] plainText) => EncryptionUnitTests.DecryptData(plainText));
            this.mockDataEncryptionKey.SetupGet(p => p.EncryptionAlgorithm).Returns(EncryptionUnitTests.Algo);
            this.mockDataEncryptionKey.SetupGet(p => p.RawKey).Returns(new byte[1] { 42 });

            this.mockDataEncryptionKeyProvider.Setup(m => m.FetchDataEncryptionKeyAsync(
                EncryptionUnitTests.DekId,
                EncryptionUnitTests.Algo,
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockDataEncryptionKey.Object);

            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient((builder) => builder
                .AddCustomHandlers(this.testHandler)
                .WithDataEncryptionKeyProvider(this.mockDataEncryptionKeyProvider.Object));

            DatabaseCore database = new DatabaseCore(client.ClientContext, EncryptionUnitTests.DatabaseId);
            return new ContainerInlineCore(new ContainerCore(client.ClientContext, database, EncryptionUnitTests.ContainerId));
        }

        private static JObject ParseStream(Stream stream)
        {
            return JObject.Load(new JsonTextReader(new StreamReader(stream)));
        }

        private static byte[] EncryptData(byte[] plainText)
        {
            return plainText.Select(b => (byte)(b + 1)).ToArray();
        }

        private static byte[] DecryptData(byte[] plainText)
        {
            return plainText.Select(b => (byte)(b - 1)).ToArray();
        }

        private class MyItem
        {
            public static List<string> PathsToEncrypt { get; } = new List<string>() { "/EncStr1", "/EncInt" };

            [JsonProperty(PropertyName = Constants.Properties.Id, NullValueHandling = NullValueHandling.Ignore)]
            public string Id { get; set; }

            public string PK { get; set; }

            public string EncStr1 { get; set; }

            public int EncInt { get; set; }

            // todo: byte array, parts of objects, structures, enum

            public override bool Equals(object obj)
            {
                MyItem item = obj as MyItem;
                return item != null &&
                       this.Id == item.Id &&
                       this.PK == item.PK &&
                       this.EncStr1 == item.EncStr1 &&
                       this.EncInt == item.EncInt;
            }

            public override int GetHashCode()
            {
                int hashCode = -307924070;
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Id);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.PK);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.EncStr1);
                hashCode = (hashCode * -1521134295) + this.EncInt.GetHashCode();
                return hashCode;
            }
        }

        private class EncryptionPropertiesComparer : IEqualityComparer<EncryptionProperties>
        {
            public bool Equals(EncryptionProperties x, EncryptionProperties y)
            {
                if (x == null && y == null) return true;
                if (x == null || y == null) return false;
                return x.EncryptionFormatVersion == y.EncryptionFormatVersion
                    && x.DataEncryptionKeyId == y.DataEncryptionKeyId
                    && x.EncryptedData.SequenceEqual(y.EncryptedData);
            }

            public int GetHashCode(EncryptionProperties obj)
            {
                // sufficient for test impl.
                return 0;
            }
        }

        private class EncryptionTestHandler : TestHandler
        {
            private readonly Func<RequestMessage, Task<ResponseMessage>> func;

            private readonly CosmosJsonDotNetSerializer serializer = new CosmosJsonDotNetSerializer();


            public EncryptionTestHandler(Func<RequestMessage, Task<ResponseMessage>> func = null)
            {
                this.func = func;
            }

            public ConcurrentDictionary<string, JObject> Items { get; } = new ConcurrentDictionary<string, JObject>();

            public List<RequestMessage> Received { get; } = new List<RequestMessage>();

            public override async Task<ResponseMessage> SendAsync(
                RequestMessage request,
                CancellationToken cancellationToken)
            {
                // We clone the request message as the Content is disposed before we can use it in the test assertions later.
                this.Received.Add(EncryptionTestHandler.CloneRequestMessage(request));

                if (this.func != null)
                {
                    return await this.func(request);
                }

                HttpStatusCode httpStatusCode = HttpStatusCode.InternalServerError;

                if(request.ResourceType == ResourceType.Document)
                {
                    JObject item = null;
                    if (request.OperationType == OperationType.Create)
                    {
                        item = EncryptionUnitTests.ParseStream(request.Content);
                        string itemId = item.Property("id").Value.Value<string>();

                        httpStatusCode = HttpStatusCode.Created;
                        if (!this.Items.TryAdd(itemId, item))
                        {
                            httpStatusCode = HttpStatusCode.Conflict;
                        }
                    }
                    else if (request.OperationType == OperationType.Read)
                    {
                        string itemId = EncryptionTestHandler.ParseItemUri(request.RequestUri);
                        httpStatusCode = HttpStatusCode.OK;
                        if (!this.Items.TryGetValue(itemId, out item))
                        {
                            httpStatusCode = HttpStatusCode.NotFound;
                        }
                    }
                    else if (request.OperationType == OperationType.Replace)
                    {
                        string itemId = EncryptionTestHandler.ParseItemUri(request.RequestUri);
                        item = EncryptionUnitTests.ParseStream(request.Content);

                        httpStatusCode = HttpStatusCode.OK;
                        if (!this.Items.TryGetValue(itemId, out JObject existingItem))
                        {
                            httpStatusCode = HttpStatusCode.NotFound;
                        }

                        if (!this.Items.TryUpdate(itemId, item, existingItem)) { throw new InvalidOperationException("Concurrency not handled in tests."); }
                    }
                    else if (request.OperationType == OperationType.Delete)
                    {
                        string itemId = EncryptionTestHandler.ParseItemUri(request.RequestUri);
                        httpStatusCode = HttpStatusCode.NoContent;
                        if (!this.Items.TryRemove(itemId, out _))
                        {
                            httpStatusCode = HttpStatusCode.NotFound;
                        }
                    }

                    ResponseMessage responseMessage = new ResponseMessage(httpStatusCode, request)
                    {
                        Content = item != null ? this.serializer.ToStream(item) : null,
                    };

                    responseMessage.Headers.RequestCharge = EncryptionUnitTests.requestCharge;
                    return responseMessage;

                }

                return new ResponseMessage(httpStatusCode, request);
            }

            private static RequestMessage CloneRequestMessage(RequestMessage request)
            {
                MemoryStream contentClone = null;
                if (request.Content != null)
                {
                    // assuming seekable Stream
                    contentClone = new MemoryStream((int)request.Content.Length);
                    request.Content.CopyTo(contentClone);
                    request.Content.Position = 0;
                }

                RequestMessage clone = new RequestMessage(request.Method, request.RequestUri)
                {
                    OperationType = request.OperationType,
                    ResourceType = request.ResourceType,
                    RequestOptions = request.RequestOptions,
                    Content = contentClone
                };

                foreach (string headerName in request.Headers)
                {
                    clone.Headers.Set(headerName, request.Headers[headerName]);
                }

                return clone;
            }

            private static string ParseItemUri(Uri requestUri)
            {
                string[] segments = requestUri.OriginalString.Split("/", StringSplitOptions.RemoveEmptyEntries);
                Assert.AreEqual(6, segments.Length);
                Assert.AreEqual(Paths.DatabasesPathSegment, segments[0]);
                Assert.AreEqual(EncryptionUnitTests.DatabaseId, segments[1]);
                Assert.AreEqual(Paths.CollectionsPathSegment, segments[2]);
                Assert.AreEqual(EncryptionUnitTests.ContainerId, segments[3]);
                Assert.AreEqual(Paths.DocumentsPathSegment, segments[4]);
                return segments[5];
            }
        }
    }
}
