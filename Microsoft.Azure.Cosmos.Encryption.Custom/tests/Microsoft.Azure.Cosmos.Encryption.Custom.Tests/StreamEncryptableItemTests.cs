// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.IO;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Moq.Protected;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class StreamEncryptableItemTests
    {
        [TestMethod]
        public void Ctor_WithNullStream_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(() => new StreamEncryptableItem(null));
            Assert.AreEqual("input", exception.ParamName);
        }

        [TestMethod]
        public void StreamPayload_ReturnsOriginalStream()
        {
            using MemoryStream payload = new();
            StreamEncryptableItem item = new(payload);

            Assert.AreSame(payload, item.StreamPayload);
        }

        [TestMethod]
        public void DecryptableItem_AccessBeforeInitialization_ThrowsInvalidOperationException()
        {
            StreamEncryptableItem item = new(new MemoryStream());

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => _ = item.DecryptableItem);
            Assert.AreEqual("Decryptable content is not initialized.", exception.Message);
        }

        [TestMethod]
        public void SetDecryptableItem_WhenDecryptableContentIsNull_ThrowsArgumentNullException()
        {
            StreamEncryptableItem item = new(new MemoryStream());
            Mock<Encryptor> encryptorMock = new(MockBehavior.Loose);
            CosmosSerializer serializer = CreateSerializerStub();

            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(() => item.SetDecryptableItem(null, encryptorMock.Object, serializer));
            Assert.AreEqual("decryptableContent", exception.ParamName);
        }

        [TestMethod]
        public void SetDecryptableItem_WhenEncryptorIsNull_ThrowsArgumentNullException()
        {
            StreamEncryptableItem item = new(new MemoryStream());
            CosmosSerializer serializer = CreateSerializerStub();
            JToken content = new JObject();

            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(() => item.SetDecryptableItem(content, null, serializer));
            Assert.AreEqual("encryptor", exception.ParamName);
        }

        [TestMethod]
        public void SetDecryptableItem_WhenSerializerIsNull_ThrowsArgumentNullException()
        {
            StreamEncryptableItem item = new(new MemoryStream());
            Mock<Encryptor> encryptorMock = new(MockBehavior.Loose);
            JToken content = new JObject();

            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(() => item.SetDecryptableItem(content, encryptorMock.Object, null));
            Assert.AreEqual("cosmosSerializer", exception.ParamName);
        }

        [TestMethod]
        public void SetDecryptableItem_WhenCalledTwice_ThrowsInvalidOperationException()
        {
            StreamEncryptableItem item = new(new MemoryStream());
            Mock<Encryptor> encryptorMock = new(MockBehavior.Loose);
            CosmosSerializer serializer = CreateSerializerStub();
            JToken content = new JObject();

            item.SetDecryptableItem(content, encryptorMock.Object, serializer);

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => item.SetDecryptableItem(new JObject(), encryptorMock.Object, serializer));
            Assert.AreEqual("Decryptable content is already initialized.", exception.Message);
        }

        [TestMethod]
        public void SetDecryptableItem_WithValidInputs_InitializesDecryptableItem()
        {
            StreamEncryptableItem item = new(new MemoryStream());
            Mock<Encryptor> encryptorMock = new(MockBehavior.Loose);
            CosmosSerializer serializer = CreateSerializerStub();
            JToken content = new JObject();

            item.SetDecryptableItem(content, encryptorMock.Object, serializer);

            DecryptableItem decryptableItem = item.DecryptableItem;
            Assert.IsNotNull(decryptableItem);
            Assert.IsInstanceOfType(decryptableItem, typeof(DecryptableItemCore));
        }

        [TestMethod]
        public void ToStream_ReturnsUnderlyingStream()
        {
            using MemoryStream payload = new();
            StreamEncryptableItem item = new(payload);
            CosmosSerializer serializer = CreateSerializerStub();

            Stream result = item.ToStream(serializer);

            Assert.AreSame(payload, result);
        }

        [TestMethod]
        public void Dispose_DisposesUnderlyingStream()
        {
            bool disposed = false;
            Mock<Stream> payloadMock = new(MockBehavior.Loose);
            payloadMock.Protected()
                .Setup("Dispose", ItExpr.IsAny<bool>())
                .Callback((bool _) => disposed = true);

            StreamEncryptableItem item = new(payloadMock.Object);

            item.Dispose();

            Assert.IsTrue(disposed);
            payloadMock.Protected().Verify("Dispose", Times.Once(), ItExpr.IsAny<bool>());
        }

        private static CosmosSerializer CreateSerializerStub()
        {
            return new Mock<CosmosSerializer>(MockBehavior.Strict).Object;
        }
    }
}
