//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
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
        private const double requestCharge = 0.6;
        private const CosmosEncryptionAlgorithm Algo = CosmosEncryptionAlgorithm.AE_AES_256_CBC_HMAC_SHA_256_RANDOMIZED;

        private TimeSpan cacheTTL = TimeSpan.FromDays(1);
        private byte[] dek = new byte[] { 1, 2, 3, 4 };
        private EncryptionKeyWrapMetadata metadata1 = new EncryptionKeyWrapMetadata("metadata1");
        private EncryptionKeyWrapMetadata metadata2 = new EncryptionKeyWrapMetadata("metadata2");
        private string metadataUpdateSuffix = "updated";

        private EncryptionTestHandler testHandler;
        private Mock<EncryptionKeyWrapProvider> mockKeyWrapProvider;
        private Mock<EncryptionAlgorithm> mockEncryptionAlgorithm;
        private Mock<DatabaseCore> mockDatabaseCore;

        [TestMethod]
        public async Task EncryptionUTCreateDekWithoutEncryptionSerializer()
        {
            DatabaseCore database = (DatabaseCore)((ContainerCore)(ContainerInlineCore)this.GetContainer()).Database;

            try
            {
                await database.CreateDataEncryptionKeyAsync("mydek", EncryptionUnitTests.Algo, this.metadata1);
                Assert.Fail();
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual(ClientResources.EncryptionKeyWrapProviderNotConfigured, ex.Message);
            }

        }

        [TestMethod]
        public async Task EncryptionUTRewrapDekWithoutEncryptionSerializer()
        {
            string dekId = "mydek";
            EncryptionTestHandler testHandler = new EncryptionTestHandler();

            // Create a DEK using a properly setup client first
            Container container = this.GetContainerWithMockSetup(testHandler);
            DatabaseCore databaseWithSerializer = (DatabaseCore)((ContainerCore)(ContainerInlineCore)container).Database;

            DataEncryptionKeyResponse dekResponse = await databaseWithSerializer.CreateDataEncryptionKeyAsync(dekId, EncryptionUnitTests.Algo, this.metadata1);
            Assert.AreEqual(HttpStatusCode.Created, dekResponse.StatusCode);

            // Clear the handler pipeline that would have got setup
            testHandler.InnerHandler = null;

            // Ensure rewrap for this key fails on improperly configured client
            try
            {
                DatabaseCore database = (DatabaseCore)((ContainerCore)(ContainerInlineCore)this.GetContainer(testHandler)).Database;
                DataEncryptionKey dek = database.GetDataEncryptionKey(dekId);
                await dek.RewrapAsync(this.metadata2);
                Assert.Fail();
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual(ClientResources.EncryptionKeyWrapProviderNotConfigured, ex.Message);
            }
        }

        [TestMethod]
        public async Task EncryptionUTCreateDek()
        {
            Container container = this.GetContainerWithMockSetup();
            DatabaseCore database = (DatabaseCore)((ContainerCore)(ContainerInlineCore)container).Database;

            string dekId = "mydek";
            DataEncryptionKeyResponse dekResponse = await database.CreateDataEncryptionKeyAsync(dekId, EncryptionUnitTests.Algo, this.metadata1);
            Assert.AreEqual(HttpStatusCode.Created, dekResponse.StatusCode);
            Assert.AreEqual(requestCharge, dekResponse.RequestCharge);
            Assert.IsNotNull(dekResponse.ETag);

            DataEncryptionKeyProperties dekProperties = dekResponse.Resource;
            Assert.IsNotNull(dekProperties);
            Assert.AreEqual(dekResponse.ETag, dekProperties.ETag);
            Assert.AreEqual(dekId, dekProperties.Id);

            Assert.AreEqual(1, this.testHandler.Received.Count);
            RequestMessage createDekRequestMessage = this.testHandler.Received[0];
            Assert.AreEqual(ResourceType.ClientEncryptionKey, createDekRequestMessage.ResourceType);
            Assert.AreEqual(OperationType.Create, createDekRequestMessage.OperationType);

            Assert.IsTrue(this.testHandler.Deks.ContainsKey(dekId));
            DataEncryptionKeyProperties serverDekProperties = this.testHandler.Deks[dekId];
            Assert.IsTrue(serverDekProperties.Equals(dekProperties));

            // Make sure we didn't push anything else in the JSON (such as raw DEK) by comparing JSON properties
            // to properties exposed in DataEncryptionKeyProperties.
            createDekRequestMessage.Content.Position = 0; // it is a test assumption that the client uses MemoryStream
            JObject jObj = JObject.Parse(await new StreamReader(createDekRequestMessage.Content).ReadToEndAsync());
            IEnumerable<string> dekPropertiesPropertyNames = GetJsonPropertyNamesForType(typeof(DataEncryptionKeyProperties));

            foreach (JProperty property in jObj.Properties())
            {
                Assert.IsTrue(dekPropertiesPropertyNames.Contains(property.Name));
            }

            // Key wrap metadata should be the only "object" child in the JSON (given current properties in DataEncryptionKeyProperties)
            IEnumerable<JToken> objectChildren = jObj.PropertyValues().Where(v => v.Type == JTokenType.Object);
            Assert.AreEqual(1, objectChildren.Count());
            JObject keyWrapMetadataJObj = (JObject)objectChildren.First();
            Assert.AreEqual(Constants.Properties.KeyWrapMetadata, ((JProperty)keyWrapMetadataJObj.Parent).Name);

            IEnumerable<string> keyWrapMetadataPropertyNames = GetJsonPropertyNamesForType(typeof(EncryptionKeyWrapMetadata));
            foreach (JProperty property in keyWrapMetadataJObj.Properties())
            {
                Assert.IsTrue(keyWrapMetadataPropertyNames.Contains(property.Name));
            }

            IEnumerable<byte> expectedWrappedKey = this.VerifyWrap(this.dek, this.metadata1);
            this.mockKeyWrapProvider.VerifyNoOtherCalls();

            Assert.IsTrue(expectedWrappedKey.SequenceEqual(dekProperties.WrappedDataEncryptionKey));
        }

        [TestMethod]
        public async Task EncryptionUTRewrapDek()
        {
            Container container = this.GetContainerWithMockSetup();
            DatabaseCore database = (DatabaseCore)((ContainerCore)(ContainerInlineCore)container).Database;

            string dekId = "mydek";
            DataEncryptionKeyResponse createResponse = await database.CreateDataEncryptionKeyAsync(dekId, EncryptionUnitTests.Algo, this.metadata1);
            DataEncryptionKeyProperties createdProperties = createResponse.Resource;
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            this.VerifyWrap(this.dek, this.metadata1);

            DataEncryptionKey dek = database.GetDataEncryptionKey(dekId);
            DataEncryptionKeyResponse rewrapResponse = await dek.RewrapAsync(this.metadata2);
            DataEncryptionKeyProperties rewrappedProperties = rewrapResponse.Resource;
            Assert.IsNotNull(rewrappedProperties);

            Assert.AreEqual(dekId, rewrappedProperties.Id);
            Assert.AreEqual(createdProperties.CreatedTime, rewrappedProperties.CreatedTime);
            Assert.IsNotNull(rewrappedProperties.LastModified);
            Assert.AreEqual(createdProperties.ResourceId, rewrappedProperties.ResourceId);
            Assert.AreEqual(createdProperties.SelfLink, rewrappedProperties.SelfLink);

            IEnumerable<byte> expectedRewrappedKey = this.dek.Select(b => (byte)(b + 2));
            Assert.IsTrue(expectedRewrappedKey.SequenceEqual(rewrappedProperties.WrappedDataEncryptionKey));

            Assert.AreEqual(new EncryptionKeyWrapMetadata(this.metadata2.Value + this.metadataUpdateSuffix), rewrappedProperties.EncryptionKeyWrapMetadata);

            Assert.AreEqual(2, this.testHandler.Received.Count);
            RequestMessage rewrapRequestMessage = this.testHandler.Received[1];
            Assert.AreEqual(ResourceType.ClientEncryptionKey, rewrapRequestMessage.ResourceType);
            Assert.AreEqual(OperationType.Replace, rewrapRequestMessage.OperationType);
            Assert.AreEqual(createResponse.ETag, rewrapRequestMessage.Headers[HttpConstants.HttpHeaders.IfMatch]);

            Assert.IsTrue(this.testHandler.Deks.ContainsKey(dekId));
            DataEncryptionKeyProperties serverDekProperties = this.testHandler.Deks[dekId];
            Assert.IsTrue(serverDekProperties.Equals(rewrappedProperties));

            this.VerifyWrap(this.dek, this.metadata2);
            this.mockKeyWrapProvider.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task EncryptionUTCreateItemWithUnknownDek()
        {
            Container container = this.GetContainerWithMockSetup();
            DatabaseCore database = (DatabaseCore)((ContainerCore)(ContainerInlineCore)container).Database;

            MyItem item = EncryptionUnitTests.GetNewItem();
            try
            {
                await container.CreateItemAsync(
                    item,
                    new PartitionKey(item.PK),
                    new ItemRequestOptions
                    {
                        EncryptionOptions = new EncryptionOptions
                        {
                            DataEncryptionKey = database.GetDataEncryptionKey("random"),
                            PathsToEncrypt = MyItem.PathsToEncrypt
                        }
                    });

                Assert.Fail();
            }
            catch(CosmosException ex)
            {
                Assert.IsTrue(ex.Message.Contains(ClientResources.DataEncryptionKeyNotFound));
            }
        }

        [TestMethod]
        public async Task EncryptionUTCreateItem()
        {
            Container container = this.GetContainerWithMockSetup();
            DatabaseCore database = (DatabaseCore)((ContainerCore)(ContainerInlineCore)container).Database;

            string dekId = "mydek";
            DataEncryptionKeyResponse dekResponse = await database.CreateDataEncryptionKeyAsync(dekId, EncryptionUnitTests.Algo, this.metadata1);
            Assert.AreEqual(HttpStatusCode.Created, dekResponse.StatusCode);
            MyItem item = await EncryptionUnitTests.CreateItemAsync(container, dekId, MyItem.PathsToEncrypt);

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
            Assert.AreEqual(dekResponse.Resource.ResourceId, encryptionPropertiesAtServer.DataEncryptionKeyRid);
            Assert.AreEqual(1, encryptionPropertiesAtServer.EncryptionFormatVersion);
            Assert.IsNotNull(encryptionPropertiesAtServer.EncryptedData);

            JObject decryptedJObj = EncryptionUnitTests.ParseStream(new MemoryStream(encryptionPropertiesAtServer.EncryptedData.Reverse().ToArray()));
            Assert.AreEqual(2, decryptedJObj.Properties().Count());
            Assert.AreEqual(item.Str, decryptedJObj.Property(nameof(MyItem.Str)).Value.Value<string>());
            Assert.AreEqual(item.Int, decryptedJObj.Property(nameof(MyItem.Int)).Value.Value<int>());
        }

        [TestMethod]
        public async Task EncryptionUTPathHandling()
        {
            Container container = this.GetContainerWithMockSetup();
            DatabaseCore database = (DatabaseCore)((ContainerCore)(ContainerInlineCore)container).Database;

            string dekId = "mydek";
            DataEncryptionKeyResponse dekResponse = await database.CreateDataEncryptionKeyAsync(dekId, EncryptionUnitTests.Algo, this.metadata1);
            Assert.AreEqual(HttpStatusCode.Created, dekResponse.StatusCode);


            // Invalid path to encrypt
            try
            {
                await EncryptionUnitTests.CreateItemAsync(container, dekId, new List<string> { "Int" });
                Assert.Fail("Expected encryption with invalid path to fail");
            }
            catch(ArgumentException ex)
            {
                Assert.AreEqual(nameof(EncryptionOptions.PathsToEncrypt), ex.ParamName);
            }

            // Overlapping (parent and child) paths to encrypt
            try
            {
                await EncryptionUnitTests.CreateItemAsync(container, dekId, new List<string> { "/Child/MyChars", "/Int", "/Child", "/Str" });
                Assert.Fail("Expected encryption with overlapping path to fail");
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual(nameof(EncryptionOptions.PathsToEncrypt), ex.ParamName);
            }

            // No properties to encrypt
            MyItem item1 = await EncryptionUnitTests.CreateItemAsync(container, dekId, new List<string> { });
            JObject serverItem1JObj = this.testHandler.Items[item1.Id];
            JProperty eiJProp1 = serverItem1JObj.Property(Constants.Properties.EncryptedInfo);
            Assert.IsNull(eiJProp1);
            MyItem serverItem1 = serverItem1JObj.ToObject<MyItem>();
            Assert.AreEqual(item1, serverItem1);

            // Property to encrypt not in serialized item, or path intending to reference array indexes (are ignored)
            MyItem item2 = await EncryptionUnitTests.CreateItemAsync(container, dekId, new List<string> { "/unknown", "/ByteArr[2]", "/ByteArr/2" });
            JObject serverItem2JObj = this.testHandler.Items[item2.Id];
            JProperty eiJProp2 = serverItem2JObj.Property(Constants.Properties.EncryptedInfo);
            Assert.IsNull(eiJProp2);
            MyItem serverItem2 = serverItem2JObj.ToObject<MyItem>();
            Assert.AreEqual(item2, serverItem2);

            // Encrypt various types
            MyItem item3 = await EncryptionUnitTests.CreateItemAsync(container, dekId, new List<string> { "/Struct", "/Enum", "/ByteArr", "/Child" });
            MyItem serverItem3 = this.testHandler.Items[item3.Id].ToObject<MyItem>();
            item3.Struct = null;
            item3.Enum = null;
            item3.ByteArr = null;
            item3.Child = null;
            Assert.AreEqual(item3, serverItem3);

            // Encrypt part of child class
            MyItem item4 = await EncryptionUnitTests.CreateItemAsync(container, dekId, new List<string> { "/Child/MyStr", "/ByteArr", "/Child/MyChars" });
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
            DatabaseCore database = (DatabaseCore)((ContainerCore)(ContainerInlineCore)container).Database;

            string dekId = "mydek";
            DataEncryptionKeyResponse dekResponse = await database.CreateDataEncryptionKeyAsync(dekId, EncryptionUnitTests.Algo, this.metadata1);
            Assert.AreEqual(HttpStatusCode.Created, dekResponse.StatusCode);
            MyItem item = await EncryptionUnitTests.CreateItemAsync(container, dekId, MyItem.PathsToEncrypt);

            ItemResponse<MyItem> readResponse = await container.ReadItemAsync<MyItem>(item.Id, new PartitionKey(item.PK));
            Assert.AreEqual(item, readResponse.Resource);
        }

        private static async Task<MyItem> CreateItemAsync(Container container, string dekId, List<string> pathsToEncrypt)
        {
            DatabaseCore database = (DatabaseCore)((ContainerCore)(ContainerInlineCore)container).Database;

            MyItem item = EncryptionUnitTests.GetNewItem();

            ItemResponse<MyItem> response = await container.CreateItemAsync<MyItem>(
                item,
                requestOptions: new ItemRequestOptions
                {
                    EncryptionOptions = new EncryptionOptions
                    {
                        DataEncryptionKey = database.GetDataEncryptionKey(dekId),
                        PathsToEncrypt = pathsToEncrypt
                    }
                });

            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.AreEqual(item, response.Resource);
            return item;
        }

        private static MyItem GetNewItem()
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
                    MyChars = new char[] { 'e', 'f'}
                }
            };
        }

        private static IEnumerable<string> GetJsonPropertyNamesForType(Type type)
        {
            return type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(p => Attribute.GetCustomAttribute(p, typeof(JsonPropertyAttribute)) != null)
                .Select(p => ((JsonPropertyAttribute)Attribute.GetCustomAttribute(p, typeof(JsonPropertyAttribute))).PropertyName);
        }

        private IEnumerable<byte> VerifyWrap(IEnumerable<byte> dek, EncryptionKeyWrapMetadata inputMetadata)
        {
            this.mockKeyWrapProvider.Verify(m => m.WrapKeyAsync(
                It.Is<byte[]>(key => key.SequenceEqual(dek)),
                inputMetadata,
                It.IsAny<CancellationToken>()),
            Times.Exactly(1));

            IEnumerable<byte> expectedWrappedKey = null;
            if (inputMetadata == this.metadata1)
            {

                expectedWrappedKey = dek.Select(b => (byte)(b + 1));
            }
            else if (inputMetadata == this.metadata2)
            {
                expectedWrappedKey = dek.Select(b => (byte)(b + 2));
            }
            else
            {
                Assert.Fail();
            }

            // Verify we did unwrap to check on the wrapping
            EncryptionKeyWrapMetadata expectedUpdatedMetadata = new EncryptionKeyWrapMetadata(inputMetadata.Value + this.metadataUpdateSuffix);
            this.VerifyUnwrap(expectedWrappedKey, expectedUpdatedMetadata);

            return expectedWrappedKey;
        }

        private void VerifyUnwrap(IEnumerable<byte> wrappedDek, EncryptionKeyWrapMetadata inputMetadata)
        {
            this.mockKeyWrapProvider.Verify(m => m.UnwrapKeyAsync(
                It.Is<byte[]>(wrappedKey => wrappedKey.SequenceEqual(wrappedDek)),
                inputMetadata,
                It.IsAny<CancellationToken>()),
            Times.Exactly(1));
        }

        private Container GetContainer(EncryptionTestHandler encryptionTestHandler = null)
        {
            this.testHandler = encryptionTestHandler ?? new EncryptionTestHandler();
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient((builder) => builder.AddCustomHandlers(this.testHandler));
            DatabaseCore database = new DatabaseCore(client.ClientContext, EncryptionUnitTests.DatabaseId);
            return new ContainerInlineCore(new ContainerCore(client.ClientContext, database, EncryptionUnitTests.ContainerId));
        }

        private Container GetContainerWithMockSetup(EncryptionTestHandler encryptionTestHandler = null)
        {
            this.testHandler = encryptionTestHandler ?? new EncryptionTestHandler();

            this.mockKeyWrapProvider = new Mock<EncryptionKeyWrapProvider>();
            this.mockKeyWrapProvider.Setup(m => m.WrapKeyAsync(It.IsAny<byte[]>(), It.IsAny<EncryptionKeyWrapMetadata>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] key, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken) =>
                {
                    EncryptionKeyWrapMetadata responseMetadata = new EncryptionKeyWrapMetadata(metadata.Value + this.metadataUpdateSuffix);
                    int moveBy = metadata.Value == this.metadata1.Value ? 1 : 2;
                    return new EncryptionKeyWrapResult(key.Select(b => (byte)(b + moveBy)).ToArray(), responseMetadata);
                });
            this.mockKeyWrapProvider.Setup(m => m.UnwrapKeyAsync(It.IsAny<byte[]>(), It.IsAny<EncryptionKeyWrapMetadata>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] wrappedKey, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken) =>
                {
                    int moveBy = metadata.Value == this.metadata1.Value + this.metadataUpdateSuffix ? 1 : 2;
                    return new EncryptionKeyUnwrapResult(wrappedKey.Select(b => (byte)(b - moveBy)).ToArray(), this.cacheTTL);
                });

            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient((builder) => builder
                .AddCustomHandlers(this.testHandler)
                .WithEncryptionKeyWrapProvider(this.mockKeyWrapProvider.Object));

            this.mockEncryptionAlgorithm = new Mock<EncryptionAlgorithm>();
            this.mockEncryptionAlgorithm.Setup(m => m.EncryptData(It.IsAny<byte[]>()))
                .Returns((byte[] plainText) => plainText.Reverse().ToArray());
            this.mockEncryptionAlgorithm.Setup(m => m.DecryptData(It.IsAny<byte[]>()))
                .Returns((byte[] cipherText) => cipherText.Reverse().ToArray());
            this.mockEncryptionAlgorithm.SetupGet(m => m.AlgorithmName).Returns(AeadAes256CbcHmac256Algorithm.AlgorithmNameConstant);

            this.mockDatabaseCore = new Mock<DatabaseCore>(client.ClientContext, EncryptionUnitTests.DatabaseId);
            this.mockDatabaseCore.CallBase = true;
            this.mockDatabaseCore.Setup(m => m.GetDataEncryptionKey(It.IsAny<string>()))
             .Returns((string id) =>
             {
                 Mock<DataEncryptionKeyCore> mockDekCore = new Mock<DataEncryptionKeyCore>(client.ClientContext, this.mockDatabaseCore.Object, id);
                 mockDekCore.CallBase = true;
                 mockDekCore.Setup(m => m.GenerateKey(EncryptionUnitTests.Algo)).Returns(this.dek);
                 mockDekCore.Setup(m => m.GetEncryptionAlgorithm(It.IsAny<byte[]>(), EncryptionUnitTests.Algo))
                    .Returns(this.mockEncryptionAlgorithm.Object);
                 return new DataEncryptionKeyInlineCore(mockDekCore.Object);
             });

            return new ContainerInlineCore(new ContainerCore(client.ClientContext, this.mockDatabaseCore.Object, EncryptionUnitTests.ContainerId));
        }

        private static JObject ParseStream(Stream stream)
        {
            return JObject.Load(new JsonTextReader(new StreamReader(stream)));
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
                    && x.DataEncryptionKeyRid == y.DataEncryptionKeyRid
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

            public ConcurrentDictionary<string, DataEncryptionKeyProperties> Deks { get; } = new ConcurrentDictionary<string, DataEncryptionKeyProperties>();


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

                if (request.ResourceType == ResourceType.ClientEncryptionKey)
                {
                    DataEncryptionKeyProperties dekProperties = null;
                    if (request.OperationType == OperationType.Create)
                    {
                        dekProperties = this.serializer.FromStream<DataEncryptionKeyProperties>(request.Content);
                        string databaseRid = ResourceId.NewDatabaseId(1).ToString();
                        dekProperties.ResourceId = ResourceId.NewClientEncryptionKeyId(databaseRid, (uint)this.Received.Count).ToString();
                        dekProperties.CreatedTime = EncryptionTestHandler.ReducePrecisionToSeconds(DateTime.UtcNow);
                        dekProperties.LastModified = dekProperties.CreatedTime;
                        dekProperties.ETag = Guid.NewGuid().ToString();
                        dekProperties.SelfLink = string.Format(
                            "dbs/{0}/{1}/{2}/",
                           databaseRid,
                            Paths.ClientEncryptionKeysPathSegment,
                            dekProperties.ResourceId);

                        httpStatusCode = HttpStatusCode.Created;
                        if (!this.Deks.TryAdd(dekProperties.Id, dekProperties))
                        {
                            httpStatusCode = HttpStatusCode.Conflict;
                        }
                    }
                    else if (request.OperationType == OperationType.Read)
                    {
                        string dekId = EncryptionTestHandler.ParseDekUri(request.RequestUri);
                        httpStatusCode = HttpStatusCode.OK;
                        if (!this.Deks.TryGetValue(dekId, out dekProperties))
                        {
                            httpStatusCode = HttpStatusCode.NotFound;
                        }
                    }
                    else if(request.OperationType == OperationType.Replace)
                    {
                        string dekId = EncryptionTestHandler.ParseDekUri(request.RequestUri);
                        dekProperties = this.serializer.FromStream<DataEncryptionKeyProperties>(request.Content);
                        dekProperties.LastModified = EncryptionTestHandler.ReducePrecisionToSeconds(DateTime.UtcNow);
                        dekProperties.ETag = Guid.NewGuid().ToString();

                        httpStatusCode = HttpStatusCode.OK;
                        if (!this.Deks.TryGetValue(dekId, out DataEncryptionKeyProperties existingDekProperties))
                        {
                            httpStatusCode = HttpStatusCode.NotFound;
                        }

                        if(!this.Deks.TryUpdate(dekId, dekProperties, existingDekProperties)) { throw new InvalidOperationException("Concurrency not handled in tests."); }
                    }
                    else if (request.OperationType == OperationType.Delete)
                    {
                        string dekId = EncryptionTestHandler.ParseDekUri(request.RequestUri);
                        httpStatusCode = HttpStatusCode.NoContent;
                        if (!this.Deks.TryRemove(dekId, out _))
                        {
                            httpStatusCode = HttpStatusCode.NotFound;
                        }
                    }

                    ResponseMessage responseMessage = new ResponseMessage(httpStatusCode, request)
                    {
                        Content = dekProperties != null ? this.serializer.ToStream(dekProperties) : null,
                    };

                    responseMessage.Headers.RequestCharge = EncryptionUnitTests.requestCharge;
                    responseMessage.Headers.ETag = dekProperties?.ETag;
                    return responseMessage;
                }
                else if(request.ResourceType == ResourceType.Document)
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

                foreach (string x in request.Headers)
                {
                    clone.Headers.Set(x, request.Headers[x]);
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

            private static string ParseDekUri(Uri requestUri)
            {
                string[] segments = requestUri.OriginalString.Split("/", StringSplitOptions.RemoveEmptyEntries);
                Assert.AreEqual(4, segments.Length);
                Assert.AreEqual(Paths.DatabasesPathSegment, segments[0]);
                Assert.AreEqual(EncryptionUnitTests.DatabaseId, segments[1]);
                Assert.AreEqual(Paths.ClientEncryptionKeysPathSegment, segments[2]);
                return segments[3];
            }

            private static DateTime ReducePrecisionToSeconds(DateTime input)
            {
                return new DateTime(input.Year, input.Month, input.Day, input.Hour, input.Minute, input.Second, DateTimeKind.Utc);
            }
        }
    }
}
