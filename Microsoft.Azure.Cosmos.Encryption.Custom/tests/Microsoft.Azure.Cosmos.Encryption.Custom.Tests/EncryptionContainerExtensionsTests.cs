//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class EncryptionContainerExtensionsTests
    {
        [TestMethod]
        public void WithEncryptor_ReturnsEncryptionContainer()
        {
            Mock<Container> innerContainerMock = new ();
            Mock<Encryptor> encryptorMock = new ();
            Mock<CosmosResponseFactory> responseFactoryMock = new ();
            Mock<CosmosSerializer> serializerMock = new ();

            CosmosClientOptions clientOptions = new ()
            {
                Serializer = serializerMock.Object
            };

            Mock<CosmosClient> clientMock = new ();
            clientMock.SetupGet(c => c.ResponseFactory).Returns(responseFactoryMock.Object);
            clientMock.SetupGet(c => c.ClientOptions).Returns(clientOptions);

            Mock<Database> databaseMock = new ();
            databaseMock.SetupGet(d => d.Client).Returns(clientMock.Object);
            databaseMock.SetupGet(d => d.Id).Returns("test-database");

            innerContainerMock.SetupGet(c => c.Database).Returns(databaseMock.Object);
            innerContainerMock.SetupGet(c => c.Id).Returns("test-container");

            Container wrapped = innerContainerMock.Object.WithEncryptor(encryptorMock.Object);

            Assert.IsInstanceOfType(wrapped, typeof(EncryptionContainer));

            EncryptionContainer encryptionContainer = (EncryptionContainer)wrapped;
            Assert.AreSame(encryptorMock.Object, encryptionContainer.Encryptor);
            Assert.AreSame(databaseMock.Object, encryptionContainer.Database);
            Assert.AreSame(responseFactoryMock.Object, encryptionContainer.ResponseFactory);
            Assert.AreSame(serializerMock.Object, encryptionContainer.CosmosSerializer);
            Assert.AreEqual(JsonProcessor.Newtonsoft, encryptionContainer.DefaultJsonProcessor);
        }

        [TestMethod]
        public void WithEncryptor_ThrowsWhenEncryptorMissing()
        {
            Mock<Container> innerContainerMock = new ();

            Assert.ThrowsException<ArgumentNullException>(() => innerContainerMock.Object.WithEncryptor(encryptor: null));
        }

        [TestMethod]
        public void ToEncryptionStreamIterator_ThrowsForNonEncryptionContainer()
        {
            Container regularContainer = new Mock<Container>().Object;
            IQueryable<int> queryable = Enumerable.Empty<int>().AsQueryable();

            ArgumentOutOfRangeException exception = Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => regularContainer.ToEncryptionStreamIterator(queryable));

            StringAssert.Contains(exception.Message, nameof(EncryptionContainer));
            StringAssert.Contains(exception.Message, nameof(EncryptionContainerExtensions.ToEncryptionStreamIterator));
        }

        [TestMethod]
        public void ToEncryptionFeedIterator_ThrowsForNonEncryptionContainer()
        {
            Container regularContainer = new Mock<Container>().Object;
            IQueryable<int> queryable = Enumerable.Empty<int>().AsQueryable();

            ArgumentOutOfRangeException exception = Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => regularContainer.ToEncryptionFeedIterator(queryable));

            StringAssert.Contains(exception.Message, nameof(EncryptionContainer));
            StringAssert.Contains(exception.Message, nameof(EncryptionContainerExtensions.ToEncryptionFeedIterator));
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        public void UseStreamingJsonProcessingByDefault_SetsDefaultJsonProcessor()
        {
            Mock<CosmosClient> clientMock = new();
            clientMock.SetupGet(c => c.ClientOptions).Returns(new CosmosClientOptions()
            {
                Serializer = Mock.Of<CosmosSerializer>()
            });

            Mock<Database> databaseMock = new();
            databaseMock.SetupGet(d => d.Client).Returns(clientMock.Object);

            Mock<Container> innerContainerMock = new();
            innerContainerMock.SetupGet(c => c.Database).Returns(databaseMock.Object);

            EncryptionContainer encryptionContainer = new(innerContainerMock.Object, Mock.Of<Encryptor>());

            Assert.AreEqual(JsonProcessor.Newtonsoft, encryptionContainer.DefaultJsonProcessor);

            Container result = EncryptionContainerExtensions.UseStreamingJsonProcessingByDefault(encryptionContainer);

            Assert.AreSame(encryptionContainer, result);
            Assert.AreEqual(JsonProcessor.Stream, encryptionContainer.DefaultJsonProcessor);
        }

        [TestMethod]
        public void UseStreamingJsonProcessingByDefault_ThrowsForNonEncryptionContainer()
        {
            Container nonEncryptionContainer = Mock.Of<Container>();

            Assert.ThrowsException<NotSupportedException>(() => nonEncryptionContainer.UseStreamingJsonProcessingByDefault());
        }
#endif
    }
}
