namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class EncryptionFeedIteratorTTests
    {
        private const string JsonProcessorPropertyBagKey = "encryption-json-processor";

        [DataTestMethod]
        [DynamicData(nameof(GetJsonProcessorValues), DynamicDataSourceType.Method)]
        public void Ctor_Throws_OnNullParam(int jsonProcessorValue)
        {
            JsonProcessor jsonProcessor = (JsonProcessor)Enum.ToObject(typeof(JsonProcessor), jsonProcessorValue);
            Encryptor encryptor = Mock.Of<Encryptor>();
            EncryptionFeedIterator iterator = new (Mock.Of<FeedIterator>(), encryptor, jsonProcessor);
            CosmosResponseFactory responseFactory = Mock.Of<CosmosResponseFactory>();
            CosmosSerializer cosmosSerializer = Mock.Of<CosmosSerializer>();
            RequestOptions requestOptions = new ();

            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(null, responseFactory, encryptor, cosmosSerializer, requestOptions));
            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(iterator, null, encryptor, cosmosSerializer, requestOptions));
            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(iterator, responseFactory, null, cosmosSerializer, requestOptions));
            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(iterator, responseFactory, encryptor, null, requestOptions));
            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(null, responseFactory, encryptor, cosmosSerializer, jsonProcessor));
            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(iterator, null, encryptor, cosmosSerializer, jsonProcessor));
            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(iterator, responseFactory, null, cosmosSerializer, jsonProcessor));
            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(iterator, responseFactory, encryptor, null, jsonProcessor));
        }

        [DataTestMethod]
        [DynamicData(nameof(GetJsonProcessorValues), DynamicDataSourceType.Method)]
        public void Ctor_WithRequestOptions_Throws_OnNullParam(int jsonProcessorValue)
        {
            JsonProcessor jsonProcessor = (JsonProcessor)Enum.ToObject(typeof(JsonProcessor), jsonProcessorValue);
            FeedIterator mockFeedIterator = Mock.Of<FeedIterator>();
            Encryptor mockEncryptor = Mock.Of<Encryptor>();
            CosmosResponseFactory responseFactory = Mock.Of<CosmosResponseFactory>();
            CosmosSerializer cosmosSerializer = Mock.Of<CosmosSerializer>();

            RequestOptions requestOptions = new ItemRequestOptions
            {
                Properties = new System.Collections.Generic.Dictionary<string, object>
                {
                    { JsonProcessorPropertyBagKey, GetProcessorName(jsonProcessor) }
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

        [TestMethod]
        public void Ctor_AllowsNullRequestOptions_DefaultsToNewtonsoft()
        {
            FeedIterator mockFeedIterator = Mock.Of<FeedIterator>();
            Encryptor mockEncryptor = Mock.Of<Encryptor>();
            CosmosResponseFactory responseFactory = Mock.Of<CosmosResponseFactory>();
            CosmosSerializer cosmosSerializer = Mock.Of<CosmosSerializer>();

            EncryptionFeedIterator baseIterator = new EncryptionFeedIterator(mockFeedIterator, mockEncryptor, requestOptions: null);

            EncryptionFeedIterator<object> result = new EncryptionFeedIterator<object>(
                baseIterator,
                responseFactory,
                mockEncryptor,
                cosmosSerializer,
                requestOptions: null);

            Assert.IsNotNull(result);
        }
        private static string GetProcessorName(JsonProcessor jsonProcessor)
        {
            string processorName = jsonProcessor.ToString();

            return processorName;
        }

        public static IEnumerable<object[]> GetJsonProcessorValues()
        {
            yield return new object[] { (int)JsonProcessor.Newtonsoft };
#if NET8_0_OR_GREATER
            yield return new object[] { (int)JsonProcessor.Stream };
#endif
        }
    }
}
