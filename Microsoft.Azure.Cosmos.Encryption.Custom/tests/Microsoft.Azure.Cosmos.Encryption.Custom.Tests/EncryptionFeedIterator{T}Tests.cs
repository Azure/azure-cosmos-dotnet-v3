namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class EncryptionFeedIteratorTTests
    {
        [DataTestMethod]
        [DataRow(JsonProcessor.Newtonsoft)]
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        [DataRow(JsonProcessor.Stream)]
#endif
        public void Ctor_Throws_OnNullParam(JsonProcessor jsonProcessor)
        {
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

        [DataTestMethod]
        [DataRow(JsonProcessor.Newtonsoft)]
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        [DataRow(JsonProcessor.Stream)]
#endif
        public void Ctor_WithRequestOptions_Throws_OnNullParam(JsonProcessor jsonProcessor)
        {
            FeedIterator mockFeedIterator = Mock.Of<FeedIterator>();
            Encryptor mockEncryptor = Mock.Of<Encryptor>();
            CosmosResponseFactory responseFactory = Mock.Of<CosmosResponseFactory>();
            CosmosSerializer cosmosSerializer = Mock.Of<CosmosSerializer>();

            RequestOptions requestOptions = new ItemRequestOptions
            {
                Properties = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "encryption-json-processor", jsonProcessor }
                }
            };

            EncryptionFeedIterator iterator = new EncryptionFeedIterator(mockFeedIterator, mockEncryptor, requestOptions);

            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(null, responseFactory, mockEncryptor, cosmosSerializer, requestOptions));
            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(iterator, null, mockEncryptor, cosmosSerializer, requestOptions));
        }

        [TestMethod]
        public void Ctor_WithRequestOptions_WithoutJsonProcessor_Succeeds()
        {
            FeedIterator mockFeedIterator = Mock.Of<FeedIterator>();
            Encryptor mockEncryptor = Mock.Of<Encryptor>();
            CosmosResponseFactory responseFactory = Mock.Of<CosmosResponseFactory>();
            CosmosSerializer cosmosSerializer = Mock.Of<CosmosSerializer>();
            
            RequestOptions requestOptions = new ItemRequestOptions();
            EncryptionFeedIterator baseIterator = new EncryptionFeedIterator(mockFeedIterator, mockEncryptor, requestOptions);

            EncryptionFeedIterator<Object> result = new EncryptionFeedIterator<Object>(baseIterator, responseFactory, mockEncryptor, cosmosSerializer, requestOptions);

            Assert.IsNotNull(result);
        }
    }
}
