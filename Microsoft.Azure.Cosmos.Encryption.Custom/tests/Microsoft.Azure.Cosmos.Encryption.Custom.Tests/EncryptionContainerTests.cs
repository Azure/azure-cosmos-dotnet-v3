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
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class EncryptionContainerTests
    {
        private Mock<Container> innerContainerMock;
        private Mock<Encryptor> encryptorMock;
        private Mock<CosmosResponseFactory> responseFactoryMock;
        private Mock<CosmosSerializer> serializerMock;
        private Mock<FeedIterator> feedIteratorMock;
        private EncryptionContainer encryptionContainer;

        [TestInitialize]
        public void TestInitialize()
        {
            this.encryptionContainer = CreateEncryptionContainer(
                out this.innerContainerMock,
                out this.encryptorMock,
                out this.responseFactoryMock,
                out this.serializerMock);
            this.feedIteratorMock = new Mock<FeedIterator>();
        }

        [DataTestMethod]
        [DynamicData(nameof(GetSupportedJsonProcessorsData), DynamicDataSourceType.Method)]
        public async Task GetItemQueryStreamIterator_ReturnsEncryptionFeedIteratorAsync(JsonProcessor jsonProcessor)
        {
            QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM c");
            QueryRequestOptions requestOptions = CreateRequestOptionsWithOverride(jsonProcessor);
            this.feedIteratorMock.SetupGet(f => f.HasMoreResults).Returns(true);
            ResponseMessage expectedResponse = new ResponseMessage(HttpStatusCode.TooManyRequests);
            this.feedIteratorMock
                .Setup(f => f.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);
            this.innerContainerMock
                .Setup(c => c.GetItemQueryStreamIterator(queryDefinition, "token", requestOptions))
                .Returns(this.feedIteratorMock.Object);

            FeedIterator iterator = this.encryptionContainer.GetItemQueryStreamIterator(
                queryDefinition,
                "token",
                requestOptions);

            Assert.IsInstanceOfType(iterator, typeof(EncryptionFeedIterator));
            Assert.IsTrue(iterator.HasMoreResults);

            ResponseMessage actualResponse = await iterator.ReadNextAsync();

            Assert.AreSame(expectedResponse, actualResponse);

            this.innerContainerMock.Verify(
                c => c.GetItemQueryStreamIterator(queryDefinition, "token", requestOptions),
                Times.Once);
            this.feedIteratorMock.Verify(
                f => f.ReadNextAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetSupportedJsonProcessorsData), DynamicDataSourceType.Method)]
        public async Task GetItemQueryIterator_ReturnsTypedEncryptionFeedIteratorAsync(JsonProcessor jsonProcessor)
        {
            QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM c");
            QueryRequestOptions requestOptions = CreateRequestOptionsWithOverride(jsonProcessor);
            this.feedIteratorMock.SetupGet(f => f.HasMoreResults).Returns(true);
            ResponseMessage responseMessage = CreateOkResponse(CreateFeedPayload());
            this.feedIteratorMock
                .Setup(f => f.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseMessage);
            this.innerContainerMock
                .Setup(c => c.GetItemQueryStreamIterator(queryDefinition, "token", requestOptions))
                .Returns(this.feedIteratorMock.Object);

            FeedIterator<DecryptableItem> typedIterator = this.encryptionContainer.GetItemQueryIterator<DecryptableItem>(
                queryDefinition,
                "token",
                requestOptions);

            Assert.IsInstanceOfType(typedIterator, typeof(EncryptionFeedIterator<DecryptableItem>));
            Assert.IsTrue(typedIterator.HasMoreResults);

            FeedResponse<DecryptableItem> feedResponse = await typedIterator.ReadNextAsync();
            DecryptableItem decryptableItem = feedResponse.Resource.Single();

            if (jsonProcessor == JsonProcessor.Newtonsoft)
            {
                Assert.IsInstanceOfType(decryptableItem, typeof(DecryptableItemCore));
            }
#if NET8_0_OR_GREATER
            else
            {
                Assert.IsInstanceOfType(decryptableItem, typeof(StreamDecryptableItem));
            }
#endif

            this.innerContainerMock.Verify(
                c => c.GetItemQueryStreamIterator(queryDefinition, "token", requestOptions),
                Times.Once);
            this.feedIteratorMock.Verify(
                f => f.ReadNextAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task GetItemQueryIterator_DefaultsToNewtonsoftJsonProcessorWhenOverrideMissingAsync()
        {
            QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM c");
            QueryRequestOptions requestOptions = new QueryRequestOptions();

            string payload = "{\"Documents\":[{\"id\":\"doc1\"}]}";
            ResponseMessage responseMessage = CreateOkResponse(payload);
            this.feedIteratorMock
                .Setup(f => f.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseMessage);
            this.innerContainerMock
                .Setup(c => c.GetItemQueryStreamIterator(queryDefinition, null, requestOptions))
                .Returns(this.feedIteratorMock.Object);

            FeedIterator<DecryptableItem> typedIterator = this.encryptionContainer.GetItemQueryIterator<DecryptableItem>(
                queryDefinition,
                continuationToken: null,
                requestOptions: requestOptions);

            FeedResponse<DecryptableItem> feedResponse = await typedIterator.ReadNextAsync();
            DecryptableItem decryptableItem = feedResponse.Resource.Single();

            Assert.IsInstanceOfType(decryptableItem, typeof(DecryptableItemCore));
        }

        [DataTestMethod]
        [DynamicData(nameof(GetSupportedJsonProcessorsData), DynamicDataSourceType.Method)]
        public async Task GetChangeFeedIterator_ReturnsTypedEncryptionFeedIteratorAsync(JsonProcessor jsonProcessor)
        {
            ChangeFeedStartFrom startFrom = ChangeFeedStartFrom.Beginning();
            ChangeFeedMode mode = ChangeFeedMode.Incremental;
            ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions
            {
                Properties = CreateJsonProcessorPropertyBag(jsonProcessor)
            };

            this.feedIteratorMock.SetupGet(f => f.HasMoreResults).Returns(true);
            ResponseMessage responseMessage = CreateOkResponse(CreateFeedPayload());
            this.feedIteratorMock
                .Setup(f => f.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseMessage);
            this.innerContainerMock
                .Setup(c => c.GetChangeFeedStreamIterator(startFrom, mode, requestOptions))
                .Returns(this.feedIteratorMock.Object);

            FeedIterator<DecryptableItem> typedIterator = this.encryptionContainer.GetChangeFeedIterator<DecryptableItem>(
                startFrom,
                mode,
                requestOptions);

            Assert.IsInstanceOfType(typedIterator, typeof(EncryptionFeedIterator<DecryptableItem>));
            Assert.IsTrue(typedIterator.HasMoreResults);

            FeedResponse<DecryptableItem> feedResponse = await typedIterator.ReadNextAsync();
            DecryptableItem decryptableItem = feedResponse.Resource.Single();

            if (jsonProcessor == JsonProcessor.Newtonsoft)
            {
                Assert.IsInstanceOfType(decryptableItem, typeof(DecryptableItemCore));
            }
#if NET8_0_OR_GREATER
            else
            {
                Assert.IsInstanceOfType(decryptableItem, typeof(StreamDecryptableItem));
            }
#endif

            this.innerContainerMock.Verify(
                c => c.GetChangeFeedStreamIterator(startFrom, mode, requestOptions),
                Times.Once);
            this.feedIteratorMock.Verify(
                f => f.ReadNextAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task GetChangeFeedProcessorBuilder_WrapsDecryptableDelegateAsync()
        {
            Container.ChangesHandler<JObject> capturedHandler = null;

            this.innerContainerMock
                .Setup(c => c.GetChangeFeedProcessorBuilder(
                    "processor",
                    It.IsAny<Container.ChangesHandler<JObject>>()))
                .Callback<string, Container.ChangesHandler<JObject>>((_, handler) => capturedHandler = handler)
                .Returns((ChangeFeedProcessorBuilder)null);

            Mock<Container.ChangesHandler<DecryptableItem>> handlerMock = new Mock<Container.ChangesHandler<DecryptableItem>>();
            handlerMock
                .Setup(h => h(It.IsAny<IReadOnlyCollection<DecryptableItem>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            ChangeFeedProcessorBuilder builder = this.encryptionContainer.GetChangeFeedProcessorBuilder(
                "processor",
                handlerMock.Object);

            Assert.IsNull(builder);
            Assert.IsNotNull(capturedHandler);

            IReadOnlyCollection<JObject> documents = new List<JObject>
            {
                JObject.Parse("{\"id\":\"doc1\"}")
            };

            await capturedHandler(documents, CancellationToken.None);

            handlerMock.Verify(
                h => h(
                    It.Is<IReadOnlyCollection<DecryptableItem>>(items => items.Count == 1 && items.All(item => item is DecryptableItem)),
                    It.Is<CancellationToken>(ct => ct == CancellationToken.None)),
                Times.Once);
        }

        [TestMethod]
        public async Task GetChangeFeedProcessorBuilderWithManualCheckpoint_WrapsDelegateAsync()
        {
            Container.ChangeFeedHandlerWithManualCheckpoint<JObject> capturedHandler = null;

            this.innerContainerMock
                .Setup(c => c.GetChangeFeedProcessorBuilderWithManualCheckpoint(
                    "processor",
                    It.IsAny<Container.ChangeFeedHandlerWithManualCheckpoint<JObject>>()))
                .Callback<string, Container.ChangeFeedHandlerWithManualCheckpoint<JObject>>((_, handler) => capturedHandler = handler)
                .Returns((ChangeFeedProcessorBuilder)null);

            Mock<Container.ChangeFeedHandlerWithManualCheckpoint<DecryptableItem>> handlerMock = new Mock<Container.ChangeFeedHandlerWithManualCheckpoint<DecryptableItem>>();
            handlerMock
                .Setup(h => h(
                    It.IsAny<ChangeFeedProcessorContext>(),
                    It.IsAny<IReadOnlyCollection<DecryptableItem>>(),
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            ChangeFeedProcessorBuilder builder = this.encryptionContainer.GetChangeFeedProcessorBuilderWithManualCheckpoint(
                "processor",
                handlerMock.Object);

            Assert.IsNull(builder);
            Assert.IsNotNull(capturedHandler);

            Mock<ChangeFeedProcessorContext> contextMock = new Mock<ChangeFeedProcessorContext>();
            IReadOnlyCollection<JObject> documents = new List<JObject>
            {
                JObject.Parse("{\"id\":\"doc1\"}")
            };
            Func<Task> checkpoint = () => Task.CompletedTask;

            await capturedHandler(
                contextMock.Object,
                documents,
                checkpoint,
                CancellationToken.None);

            handlerMock.Verify(
                h => h(
                    contextMock.Object,
                    It.Is<IReadOnlyCollection<DecryptableItem>>(items => items.Count == 1 && items.All(item => item is DecryptableItem)),
                    It.Is<Func<Task>>(f => ReferenceEquals(f, checkpoint)),
                    It.Is<CancellationToken>(ct => ct == CancellationToken.None)),
                Times.Once);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetSupportedJsonProcessorsData), DynamicDataSourceType.Method)]
        public async Task GetItemQueryIterator_ForNonDecryptableType_UsesResponseFactoryAsync(JsonProcessor jsonProcessor)
        {
            QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM c");
            QueryRequestOptions requestOptions = CreateRequestOptionsWithOverride(jsonProcessor);

            ResponseMessage expectedResponse = new ResponseMessage(HttpStatusCode.NotFound);
            this.feedIteratorMock
                .Setup(f => f.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            this.innerContainerMock
                .Setup(c => c.GetItemQueryStreamIterator(queryDefinition, "token", requestOptions))
                .Returns(this.feedIteratorMock.Object);

            Mock<FeedResponse<JObject>> feedResponseMock = new Mock<FeedResponse<JObject>>();
            this.responseFactoryMock
                .Setup(f => f.CreateItemFeedResponse<JObject>(expectedResponse))
                .Returns(feedResponseMock.Object);

            FeedIterator<JObject> typedIterator = this.encryptionContainer.GetItemQueryIterator<JObject>(
                queryDefinition,
                "token",
                requestOptions);

            FeedResponse<JObject> actualResponse = await typedIterator.ReadNextAsync();

            Assert.AreSame(feedResponseMock.Object, actualResponse);

            this.responseFactoryMock.Verify(
                f => f.CreateItemFeedResponse<JObject>(expectedResponse),
                Times.Once);
            this.feedIteratorMock.Verify(
                f => f.ReadNextAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public void GetChangeFeedProcessorBuilder_PropagatesInnerException()
        {
            InvalidOperationException expected = new InvalidOperationException("boom");
            this.innerContainerMock
                .Setup(c => c.GetChangeFeedProcessorBuilder(
                    "processor",
                    It.IsAny<Container.ChangesHandler<JObject>>()))
                .Throws(expected);

            InvalidOperationException actual = Assert.ThrowsException<InvalidOperationException>(() =>
                this.encryptionContainer.GetChangeFeedProcessorBuilder(
                    "processor",
                    (IReadOnlyCollection<DecryptableItem> _, CancellationToken __) => Task.CompletedTask));

            Assert.AreSame(expected, actual);
        }

        [TestMethod]
        public async Task ReadManyItemsStreamAsync_UsesDefaultJsonProcessorWhenOptionsMissing()
        {
            IReadOnlyList<(string id, PartitionKey partitionKey)> items = new List<(string, PartitionKey)>
            {
                ("doc1", new PartitionKey("pk1"))
            };

            string payload = "{\"Documents\":[{\"id\":\"doc1\",\"pk\":\"pk1\"}]}";
            ResponseMessage innerResponse = new ResponseMessage(HttpStatusCode.OK)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(payload))
            };

            this.innerContainerMock
                .Setup(c => c.ReadManyItemsStreamAsync(items, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(innerResponse);
            this.encryptionContainer.DefaultJsonProcessor = JsonProcessor.Newtonsoft;

            ResponseMessage decryptedResponse = await this.encryptionContainer.ReadManyItemsStreamAsync(
                items,
                readManyRequestOptions: null,
                cancellationToken: default);

            Assert.IsInstanceOfType(decryptedResponse, typeof(DecryptedResponseMessage));

            using Stream content = decryptedResponse.Content;
            JObject result = EncryptionProcessor.BaseSerializer.FromStream<JObject>(content);
            Assert.AreEqual("doc1", result[Constants.DocumentsResourcePropertyName]?[0]?["id"]?.Value<string>());
            Assert.AreEqual("pk1", result[Constants.DocumentsResourcePropertyName]?[0]?["pk"]?.Value<string>());

            this.innerContainerMock.Verify(
                c => c.ReadManyItemsStreamAsync(items, null, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private static EncryptionContainer CreateEncryptionContainer(
            out Mock<Container> innerContainerMock,
            out Mock<Encryptor> encryptorMock,
            out Mock<CosmosResponseFactory> responseFactoryMock,
            out Mock<CosmosSerializer> serializerMock)
        {
            innerContainerMock = new Mock<Container>();
            encryptorMock = new Mock<Encryptor>();
            responseFactoryMock = new Mock<CosmosResponseFactory>();
            serializerMock = new Mock<CosmosSerializer>();

            return CreateEncryptionContainer(innerContainerMock, encryptorMock, responseFactoryMock, serializerMock);
        }

        private static EncryptionContainer CreateEncryptionContainer(
            Mock<Container> innerContainerMock,
            Mock<Encryptor> encryptorMock,
            Mock<CosmosResponseFactory> responseFactoryMock,
            Mock<CosmosSerializer> serializerMock)
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                Serializer = serializerMock.Object
            };

            Mock<CosmosClient> clientMock = new Mock<CosmosClient>();
            clientMock.SetupGet(c => c.ResponseFactory).Returns(responseFactoryMock.Object);
            clientMock.SetupGet(c => c.ClientOptions).Returns(clientOptions);

            Mock<Database> databaseMock = new Mock<Database>();
            databaseMock.SetupGet(d => d.Client).Returns(clientMock.Object);
            databaseMock.SetupGet(d => d.Id).Returns("test-database");

            innerContainerMock.SetupGet(c => c.Database).Returns(databaseMock.Object);
            innerContainerMock.SetupGet(c => c.Id).Returns("test-container");

            return new EncryptionContainer(innerContainerMock.Object, encryptorMock.Object);
        }

        private static ResponseMessage CreateOkResponse(string payload)
        {
            ResponseMessage response = new ResponseMessage(HttpStatusCode.OK)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(payload))
            };

            return response;
        }

        private static QueryRequestOptions CreateRequestOptionsWithOverride(JsonProcessor jsonProcessor)
        {
            return new QueryRequestOptions
            {
                Properties = CreateJsonProcessorPropertyBag(jsonProcessor)
            };
        }

        private static Dictionary<string, object> CreateJsonProcessorPropertyBag(JsonProcessor jsonProcessor)
        {
            return new Dictionary<string, object>
            {
                { RequestOptionsPropertiesExtensions.JsonProcessorPropertyBagKey, jsonProcessor }
            };
        }

        public static IEnumerable<object[]> GetSupportedJsonProcessorsData()
        {
#if NET8_0_OR_GREATER
            yield return new object[] { JsonProcessor.Stream };
#endif
            yield return new object[] { JsonProcessor.Newtonsoft };
        }

        private static string CreateFeedPayload()
        {
            return "{\"Documents\":[{\"id\":\"doc1\"}]}";
        }
    }
}