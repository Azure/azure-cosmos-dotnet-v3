namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class EncryptionFeedIteratorTTests
    {
        [TestMethod]
        public void Ctor_Throws_OnNullParam()
        {
            JsonProcessor jsonProcessor = default;
            Encryptor encryptor = Mock.Of<Encryptor>();
            EncryptionFeedIterator iterator = new (Mock.Of<FeedIterator>(), encryptor, jsonProcessor);
            CosmosResponseFactory responseFactory = Mock.Of<CosmosResponseFactory>();
            CosmosSerializer cosmosSerializer = Mock.Of<CosmosSerializer>();
            RequestOptions requestOptions = new ();

            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(null, responseFactory, encryptor, cosmosSerializer, requestOptions));
            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(iterator, null, encryptor, cosmosSerializer, requestOptions));
            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(iterator, responseFactory, null, cosmosSerializer, requestOptions));
            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(iterator, responseFactory, encryptor, null, requestOptions));
            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(iterator, responseFactory, encryptor, cosmosSerializer, null));
            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(null, responseFactory, encryptor, cosmosSerializer, jsonProcessor));
            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(iterator, null, encryptor, cosmosSerializer, jsonProcessor));
            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(iterator, responseFactory, null, cosmosSerializer, jsonProcessor));
            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(iterator, responseFactory, encryptor, null, jsonProcessor));
        }
    }
}
