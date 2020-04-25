//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
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
        private const string Algo = "testAlgo";
        private const double requestCharge = 0.6;

        private Mock<Encryptor> mockEncryptor;
        private EncryptionTestHandler testHandler;

        [TestMethod]
        public async Task EncryptionUTCreateItemWithUnknownDek()
        {
            Container container = this.GetContainerWithMockSetup();
            MyItem item = MyItem.GetNew();

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
                            EncryptionAlgorithm = EncryptionUnitTests.Algo,
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
            Assert.IsNull(serverItem.Property(nameof(MyItem.Str)));
            Assert.IsNull(serverItem.Property(nameof(MyItem.Int)));

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
            Assert.AreEqual(item.Str, decryptedJObj.Property(nameof(MyItem.Str)).Value.Value<string>());
            Assert.AreEqual(item.Int, decryptedJObj.Property(nameof(MyItem.Int)).Value.Value<int>());
        }


        [TestMethod]
        public async Task EncryptionUTPathHandling()
        {
            Container container = this.GetContainerWithMockSetup();

            // Invalid path to encrypt
            try
            {
                await EncryptionUnitTests.CreateItemAsync(container, EncryptionUnitTests.DekId, new List<string> { "Int" });
                Assert.Fail("Expected encryption with invalid path to fail");
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual(nameof(EncryptionOptions.PathsToEncrypt), ex.ParamName);
            }

            // Duplicate paths to encrypt
            try
            {
                await EncryptionUnitTests.CreateItemAsync(container, EncryptionUnitTests.DekId, new List<string> { "/Child/MyChars", "/Int", "/Child/MyChars", "/Str" });
                Assert.Fail("Expected encryption with duplicate path to fail");
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual(nameof(EncryptionOptions.PathsToEncrypt), ex.ParamName);
            }

            // Overlapping (parent and child) paths to encrypt
            try
            {
                await EncryptionUnitTests.CreateItemAsync(container, EncryptionUnitTests.DekId, new List<string> { "/Child/MyChars", "/Int", "/Child", "/Str" });
                Assert.Fail("Expected encryption with overlapping path to fail");
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual(nameof(EncryptionOptions.PathsToEncrypt), ex.ParamName);
            }

            // No properties to encrypt
            MyItem item1 = await EncryptionUnitTests.CreateAndQueryItemAsync(container, EncryptionUnitTests.DekId, new List<string> { });
            JObject serverItem1JObj = this.testHandler.Items[item1.Id];
            JProperty eiJProp1 = serverItem1JObj.Property(Constants.Properties.EncryptedInfo);
            Assert.IsNull(eiJProp1);
            MyItem serverItem1 = serverItem1JObj.ToObject<MyItem>();
            Assert.AreEqual(item1, serverItem1);

            // Property to encrypt not in serialized item, or path intending to reference array indexes (are ignored)
            MyItem item2 = await EncryptionUnitTests.CreateAndQueryItemAsync(container, EncryptionUnitTests.DekId, new List<string> { "/unknown", "/ByteArr[2]", "/ByteArr/2" });
            JObject serverItem2JObj = this.testHandler.Items[item2.Id];
            JProperty eiJProp2 = serverItem2JObj.Property(Constants.Properties.EncryptedInfo);
            Assert.IsNull(eiJProp2);
            MyItem serverItem2 = serverItem2JObj.ToObject<MyItem>();
            Assert.AreEqual(item2, serverItem2);

            // Encrypt various types
            MyItem item3 = await EncryptionUnitTests.CreateAndQueryItemAsync(container, EncryptionUnitTests.DekId, new List<string> { "/Struct", "/Enum", "/ByteArr", "/Child" });
            MyItem serverItem3 = this.testHandler.Items[item3.Id].ToObject<MyItem>();
            item3.Struct = null;
            item3.Enum = null;
            item3.ByteArr = null;
            item3.Child = null;
            Assert.AreEqual(item3, serverItem3);

            // Encrypt part of child class
            MyItem item4 = await EncryptionUnitTests.CreateAndQueryItemAsync(container, EncryptionUnitTests.DekId, new List<string> { "/Child/MyStr", "/ByteArr", "/Child/MyChars" });
            MyItem serverItem4 = this.testHandler.Items[item4.Id].ToObject<MyItem>();
            item4.Child.MyStr = null;
            item4.Child.MyChars = null;
            item4.ByteArr = null;
            Assert.AreEqual(item4, serverItem4);
        }

        [TestMethod]
        public async Task EncryptionUTReadItem()
        {
            Container container = this.GetContainerWithMockSetup();
            MyItem item = await EncryptionUnitTests.CreateItemAsync(container, EncryptionUnitTests.DekId, MyItem.PathsToEncrypt);

            ItemResponse<MyItem> readResponse = await container.ReadItemAsync<MyItem>(item.Id, new Cosmos.PartitionKey(item.PK));
            Assert.AreEqual(item, readResponse.Resource);
        }

        private static async Task<MyItem> CreateAndQueryItemAsync(Container container, string dekId, List<string> pathsToEncrypt)
        {
            MyItem item = await EncryptionUnitTests.CreateItemAsync(container, dekId, pathsToEncrypt);
            FeedIterator<MyItem> feedIterator = container.GetItemQueryIterator<MyItem>(
                $"SELECT * FROM c where c.id = '{item.Id}'");
            FeedResponse<MyItem> queryResultItems = await feedIterator.ReadNextAsync();
            Assert.AreEqual(1, queryResultItems.Count);
            Assert.AreEqual(item, queryResultItems.First());
            return item;
        }

        private static async Task<MyItem> CreateItemAsync(Container container, string dekId, List<string> pathsToEncrypt)
        {
            MyItem item = MyItem.GetNew();
            ItemResponse<MyItem> response = await container.CreateItemAsync<MyItem>(
                item,
                requestOptions: new ItemRequestOptions
                {
                    EncryptionOptions = new EncryptionOptions
                    {
                        DataEncryptionKeyId = EncryptionUnitTests.DekId,
                        EncryptionAlgorithm = EncryptionUnitTests.Algo,
                        PathsToEncrypt = pathsToEncrypt
                    }
                });

            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.AreEqual(item, response.Resource);
            return item;
        }

        private Container GetContainerWithMockSetup(EncryptionTestHandler encryptionTestHandler = null)
        {
            this.testHandler = encryptionTestHandler ?? new EncryptionTestHandler();

            this.mockEncryptor = new Mock<Encryptor>();

            this.mockEncryptor.Setup(m => m.EncryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] plainText, string dekId, string algo, CancellationToken t) => EncryptionUnitTests.EncryptData(plainText));
            this.mockEncryptor.Setup(m => m.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] cipherText, string dekId, string algo, CancellationToken t) => EncryptionUnitTests.DecryptData(cipherText));
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient((builder) => builder
                .AddCustomHandlers(this.testHandler)
                .WithEncryptor(this.mockEncryptor.Object));

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
            public static List<string> PathsToEncrypt { get; } = new List<string>() { "/Str", "/Int" };

            [JsonProperty(PropertyName = Constants.Properties.Id, NullValueHandling = NullValueHandling.Ignore)]
            public string Id { get; set; }

            public string PK { get; set; }

            public string Str { get; set; }

            public int? Int { get; set; }

            public byte[] ByteArr { get; set; }

            public MyItemChild Child { get; set; }

            public MyStruct? Struct { get; set; }

            public MyEnum? Enum { get; set; }

            public static MyItem GetNew()
            {
                MyStruct myStruct = default;
                myStruct.Prop1 = 42;
                myStruct.Prop2 = new byte[] { 1, 2 };

                return new MyItem()
                {
                    Id = Guid.NewGuid().ToString(),
                    PK = "pk",
                    Str = "sensitive",
                    Int = 10000,
                    Struct = myStruct,
                    Enum = MyEnum.Bar,
                    ByteArr = new byte[] { 3, 4 },
                    Child = new MyItemChild()
                    {
                        MyInt = 57,
                        MyStr = "childStr",
                        MyChars = new char[] { 'e', 'f' }
                    }
                };
            }

            public override bool Equals(object obj)
            {
                MyItem item = obj as MyItem;
                return item != null &&
                       this.Id == item.Id &&
                       this.PK == item.PK &&
                       this.Str == item.Str &&
                       this.Int == item.Int &&
                       MyItem.Equals(this.ByteArr, item.ByteArr) &&
                       MyItem.Equals(this.Child, item.Child) &&
                       this.Struct.Equals(item.Struct) &&
                       this.Enum == item.Enum;
            }

            public override int GetHashCode()
            {
                int hashCode = -307924070;
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Id);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.PK);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Str);
                hashCode = (hashCode * -1521134295) + this.Int.GetHashCode();
                return hashCode;
            }

            public static bool Equals<T>(T[] x, T[] y)
            {
                return (x == null && y == null)
                    || (x != null && y != null && x.SequenceEqual(y));
            }

            private static bool Equals<T>(T x, T y) where T : class
            {
                return (x == null && y == null)
                    || (x != null && x.Equals(y));
            }
        }

        private class MyItemChild : IEquatable<MyItemChild>
        {
            public string MyStr { get; set; }

            public int? MyInt { get; set; }

            public char[] MyChars { get; set; }

            public override bool Equals(object obj)
            {
                return this.Equals(obj as MyItemChild);
            }

            public bool Equals(MyItemChild other)
            {
                return other != null &&
                       this.MyStr == other.MyStr &&
                       this.MyInt == other.MyInt &&
                       MyItem.Equals(this.MyChars, other.MyChars);
            }

            public override int GetHashCode()
            {
                int hashCode = -1230299138;
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.MyStr);
                hashCode = (hashCode * -1521134295) + this.MyInt.GetHashCode();
                return hashCode;
            }
        }

        private struct MyStruct : IEquatable<MyStruct>
        {
            public int Prop1 { get; set; }
            public byte[] Prop2 { get; set; }

            public override bool Equals(object obj)
            {
                return obj is MyStruct @struct && this.Equals(@struct);
            }

            public bool Equals(MyStruct other)
            {
                return this.Prop1 == other.Prop1 &&
                       MyItem.Equals(this.Prop2, other.Prop2);
            }

            public override int GetHashCode()
            {
                int hashCode = 1850903905;
                hashCode = (hashCode * -1521134295) + this.Prop1.GetHashCode();
                return hashCode;
            }
        }

        private enum MyEnum
        {
            Foo = 20,
            Bar = 21
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
                    else if(request.OperationType == OperationType.Query)
                    {
                        string query = EncryptionUnitTests.ParseStream(request.Content).Property("query").Value.Value<string>();
                        string itemId = query.Split('\'')[1]; // expecting specific query in unit test
                        httpStatusCode = HttpStatusCode.OK;
                        JObject queryResponse = new JObject();
                        queryResponse.Add("_rid", "containerRid");

                        if (this.Items.TryGetValue(itemId, out item))
                        {
                            queryResponse.Add("_count", "1");
                            queryResponse.Add("Documents", new JArray(new JObject(item)));
                        }
                        else
                        {
                            queryResponse.Add("_count", "0");
                            queryResponse.Add("Documents", new JArray());
                        }

                        ResponseMessage queryResponseMessage = new ResponseMessage(httpStatusCode, request)
                        {
                            Content = this.serializer.ToStream(queryResponse)
                        };

                        queryResponseMessage.Headers.ActivityId = request.Headers.ActivityId ?? Guid.Empty.ToString();
                        queryResponseMessage.Headers.RequestCharge = EncryptionUnitTests.requestCharge;
                        return queryResponseMessage;
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
