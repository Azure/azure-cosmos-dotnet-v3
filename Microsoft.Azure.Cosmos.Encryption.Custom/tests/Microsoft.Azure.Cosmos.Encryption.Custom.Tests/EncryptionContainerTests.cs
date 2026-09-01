//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
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
        private const string NewtonsoftProcessorName = "Newtonsoft";
    #if NET8_0_OR_GREATER
        private const string StreamProcessorName = "Stream";
    #endif

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
        public async Task GetItemQueryStreamIterator_ReturnsEncryptionFeedIteratorAsync(string jsonProcessor)
        {
            QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM c");
            QueryRequestOptions requestOptions = CreateRequestOptionsWithOverride(jsonProcessor);
            this.feedIteratorMock.SetupGet(f => f.HasMoreResults).Returns(true);
            ResponseMessage expectedResponse = new ResponseMessage(HttpStatusCode.TooManyRequests);
            this.feedIteratorMock
                .Setup(f => f.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);
            QueryRequestOptions forwardedOptions = null;
            this.innerContainerMock
                .Setup(c => c.GetItemQueryStreamIterator(
                    queryDefinition,
                    "token",
                    It.IsAny<QueryRequestOptions>()))
                .Callback<QueryDefinition, string, QueryRequestOptions>(
                    (_, _, options) => forwardedOptions = options)
                .Returns(this.feedIteratorMock.Object);

            FeedIterator iterator = this.encryptionContainer.GetItemQueryStreamIterator(
                queryDefinition,
                "token",
                requestOptions);

            Assert.IsInstanceOfType(iterator, typeof(EncryptionFeedIterator));
            Assert.IsTrue(iterator.HasMoreResults);

            ResponseMessage actualResponse = await iterator.ReadNextAsync();

            Assert.AreSame(expectedResponse, actualResponse);
            AssertSanitizedRequestOptions(requestOptions, forwardedOptions);

            this.innerContainerMock.Verify(
                c => c.GetItemQueryStreamIterator(
                    queryDefinition,
                    "token",
                    It.IsAny<QueryRequestOptions>()),
                Times.Once);
            this.feedIteratorMock.Verify(
                f => f.ReadNextAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetSupportedJsonProcessorsData), DynamicDataSourceType.Method)]
        public async Task GetItemQueryIterator_ReturnsTypedEncryptionFeedIteratorAsync(string jsonProcessor)
        {
#if NET8_0_OR_GREATER
            this.encryptionContainer.UseStreamingJsonProcessingByDefault();
#endif
            QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM c");
            QueryRequestOptions requestOptions = CreateRequestOptionsWithOverride(jsonProcessor);
            this.feedIteratorMock.SetupGet(f => f.HasMoreResults).Returns(true);
            ResponseMessage responseMessage = CreateOkResponse(CreateFeedPayload());
            this.feedIteratorMock
                .Setup(f => f.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseMessage);
            QueryRequestOptions forwardedOptions = null;
            this.innerContainerMock
                .Setup(c => c.GetItemQueryStreamIterator(
                    queryDefinition,
                    "token",
                    It.IsAny<QueryRequestOptions>()))
                .Callback<QueryDefinition, string, QueryRequestOptions>(
                    (_, _, options) => forwardedOptions = options)
                .Returns(this.feedIteratorMock.Object);

            FeedIterator<DecryptableItem> typedIterator = this.encryptionContainer.GetItemQueryIterator<DecryptableItem>(
                queryDefinition,
                "token",
                requestOptions);

            Assert.IsInstanceOfType(typedIterator, typeof(EncryptionFeedIterator<DecryptableItem>));
            Assert.IsTrue(typedIterator.HasMoreResults);

            FeedResponse<DecryptableItem> feedResponse = await typedIterator.ReadNextAsync();
            DecryptableItem decryptableItem = feedResponse.Resource.Single();

            if (string.Equals(jsonProcessor, NewtonsoftProcessorName, StringComparison.Ordinal))
            {
                Assert.IsInstanceOfType(decryptableItem, typeof(DecryptableItemCore));
            }
#if NET8_0_OR_GREATER
            else
            {
                Assert.IsInstanceOfType(decryptableItem, typeof(StreamDecryptableItem));
            }
#endif

            AssertSanitizedRequestOptions(requestOptions, forwardedOptions);
            this.innerContainerMock.Verify(
                c => c.GetItemQueryStreamIterator(
                    queryDefinition,
                    "token",
                    It.IsAny<QueryRequestOptions>()),
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
        public async Task GetChangeFeedIterator_ReturnsTypedEncryptionFeedIteratorAsync(string jsonProcessor)
        {
#if NET8_0_OR_GREATER
            this.encryptionContainer.UseStreamingJsonProcessingByDefault();
#endif
            ChangeFeedStartFrom startFrom = ChangeFeedStartFrom.Beginning();
            ChangeFeedMode mode = ChangeFeedMode.Incremental;
            ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, jsonProcessor },
                    { "unrelated", 42 },
                },
            };

            this.feedIteratorMock.SetupGet(f => f.HasMoreResults).Returns(true);
            ResponseMessage responseMessage = CreateOkResponse(CreateFeedPayload());
            this.feedIteratorMock
                .Setup(f => f.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseMessage);
            ChangeFeedRequestOptions forwardedOptions = null;
            this.innerContainerMock
                .Setup(c => c.GetChangeFeedStreamIterator(
                    startFrom,
                    mode,
                    It.IsAny<ChangeFeedRequestOptions>()))
                .Callback<ChangeFeedStartFrom, ChangeFeedMode, ChangeFeedRequestOptions>(
                    (_, _, options) => forwardedOptions = options)
                .Returns(this.feedIteratorMock.Object);

            FeedIterator<DecryptableItem> typedIterator = this.encryptionContainer.GetChangeFeedIterator<DecryptableItem>(
                startFrom,
                mode,
                requestOptions);

            Assert.IsInstanceOfType(typedIterator, typeof(EncryptionFeedIterator<DecryptableItem>));
            Assert.IsTrue(typedIterator.HasMoreResults);

            FeedResponse<DecryptableItem> feedResponse = await typedIterator.ReadNextAsync();
            DecryptableItem decryptableItem = feedResponse.Resource.Single();

            if (string.Equals(jsonProcessor, NewtonsoftProcessorName, StringComparison.Ordinal))
            {
                Assert.IsInstanceOfType(decryptableItem, typeof(DecryptableItemCore));
            }
#if NET8_0_OR_GREATER
            else
            {
                Assert.IsInstanceOfType(decryptableItem, typeof(StreamDecryptableItem));
            }
#endif

            AssertSanitizedRequestOptions(requestOptions, forwardedOptions);
            this.innerContainerMock.Verify(
                c => c.GetChangeFeedStreamIterator(
                    startFrom,
                    mode,
                    It.IsAny<ChangeFeedRequestOptions>()),
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
        public async Task GetItemQueryIterator_ForNonDecryptableType_UsesResponseFactoryAsync(string jsonProcessor)
        {
            QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM c");
            QueryRequestOptions requestOptions = CreateRequestOptionsWithOverride(jsonProcessor);

            ResponseMessage expectedResponse = new ResponseMessage(HttpStatusCode.NotFound);
            this.feedIteratorMock
                .Setup(f => f.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            QueryRequestOptions forwardedOptions = null;
            this.innerContainerMock
                .Setup(c => c.GetItemQueryStreamIterator(
                    queryDefinition,
                    "token",
                    It.IsAny<QueryRequestOptions>()))
                .Callback<QueryDefinition, string, QueryRequestOptions>(
                    (_, _, options) => forwardedOptions = options)
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
            AssertSanitizedRequestOptions(requestOptions, forwardedOptions);

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

#if NET8_0_OR_GREATER
        [TestMethod]
        public void UseStreamingJsonProcessingByDefault_SetsDefaultJsonProcessor()
        {
            Assert.AreEqual(JsonProcessor.Newtonsoft, this.encryptionContainer.DefaultJsonProcessor);

            this.encryptionContainer.UseStreamingJsonProcessingByDefault();

            Assert.AreEqual(JsonProcessor.Stream, this.encryptionContainer.DefaultJsonProcessor);
        }

        [TestMethod]
        public async Task GetItemQueryIterator_WithNullRequestOptions_UsesDefaultJsonProcessor()
        {
            this.encryptionContainer.UseStreamingJsonProcessingByDefault();

            this.feedIteratorMock.SetupGet(f => f.HasMoreResults).Returns(true);
            ResponseMessage responseMessage = CreateOkResponse(CreateFeedPayload());
            this.feedIteratorMock
                .Setup(f => f.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseMessage);
            this.innerContainerMock
                .Setup(c => c.GetItemQueryStreamIterator(It.IsAny<QueryDefinition>(), It.IsAny<string>(), (QueryRequestOptions)null))
                .Returns(this.feedIteratorMock.Object);

            FeedIterator<DecryptableItem> typedIterator = this.encryptionContainer.GetItemQueryIterator<DecryptableItem>(
                new QueryDefinition("SELECT * FROM c"),
                continuationToken: null,
                requestOptions: null);

            FeedResponse<DecryptableItem> feedResponse = await typedIterator.ReadNextAsync();
            DecryptableItem decryptableItem = feedResponse.Resource.Single();

            Assert.IsInstanceOfType(decryptableItem, typeof(StreamDecryptableItem),
                "With UseStreamingJsonProcessingByDefault and null requestOptions, iterator should use Stream processor.");
        }

        [TestMethod]
        public async Task GetChangeFeedIterator_WithNullRequestOptions_UsesDefaultJsonProcessor()
        {
            this.encryptionContainer.UseStreamingJsonProcessingByDefault();

            ChangeFeedStartFrom startFrom = ChangeFeedStartFrom.Beginning();
            ChangeFeedMode mode = ChangeFeedMode.Incremental;

            this.feedIteratorMock.SetupGet(f => f.HasMoreResults).Returns(true);
            ResponseMessage responseMessage = CreateOkResponse(CreateFeedPayload());
            this.feedIteratorMock
                .Setup(f => f.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseMessage);
            this.innerContainerMock
                .Setup(c => c.GetChangeFeedStreamIterator(startFrom, mode, (ChangeFeedRequestOptions)null))
                .Returns(this.feedIteratorMock.Object);

            FeedIterator<DecryptableItem> typedIterator = this.encryptionContainer.GetChangeFeedIterator<DecryptableItem>(
                startFrom,
                mode,
                changeFeedRequestOptions: null);

            FeedResponse<DecryptableItem> feedResponse = await typedIterator.ReadNextAsync();
            DecryptableItem decryptableItem = feedResponse.Resource.Single();

            Assert.IsInstanceOfType(decryptableItem, typeof(StreamDecryptableItem),
                "With UseStreamingJsonProcessingByDefault and null requestOptions, change feed iterator should use Stream processor.");
        }
#endif

        [DataTestMethod]
        [DataRow("Create")]
        [DataRow("Replace")]
        [DataRow("Upsert")]
        [DataRow("Read")]
        [DataRow("Delete")]
        public async Task PointStreamOperations_SanitizeRequestOptionsWithoutMutatingCaller(string operation)
        {
            ItemRequestOptions requestOptions = new ()
            {
                IfMatchEtag = "etag",
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, NewtonsoftProcessorName },
                    { "unrelated", 42 },
                },
            };
            ItemRequestOptions forwardedOptions = null;
            ResponseMessage innerResponse = new (operation == "Create" ? HttpStatusCode.Created : HttpStatusCode.OK);
            this.innerContainerMock
                .Setup(c => c.CreateItemStreamAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<PartitionKey>(),
                    It.IsAny<ItemRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Stream, PartitionKey, ItemRequestOptions, CancellationToken>(
                    (_, _, options, _) => forwardedOptions = options)
                .ReturnsAsync(innerResponse);
            this.innerContainerMock
                .Setup(c => c.ReplaceItemStreamAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<PartitionKey>(),
                    It.IsAny<ItemRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Stream, string, PartitionKey, ItemRequestOptions, CancellationToken>(
                    (_, _, _, options, _) => forwardedOptions = options)
                .ReturnsAsync(innerResponse);
            this.innerContainerMock
                .Setup(c => c.UpsertItemStreamAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<PartitionKey>(),
                    It.IsAny<ItemRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Stream, PartitionKey, ItemRequestOptions, CancellationToken>(
                    (_, _, options, _) => forwardedOptions = options)
                .ReturnsAsync(innerResponse);
            this.innerContainerMock
                .Setup(c => c.ReadItemStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<PartitionKey>(),
                    It.IsAny<ItemRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, PartitionKey, ItemRequestOptions, CancellationToken>(
                    (_, _, options, _) => forwardedOptions = options)
                .ReturnsAsync(innerResponse);
            this.innerContainerMock
                .Setup(c => c.DeleteItemStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<PartitionKey>(),
                    It.IsAny<ItemRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, PartitionKey, ItemRequestOptions, CancellationToken>(
                    (_, _, options, _) => forwardedOptions = options)
                .ReturnsAsync(innerResponse);

            using ResponseMessage response = operation switch
            {
                "Create" => await this.encryptionContainer.CreateItemStreamAsync(
                    CreateItemStream(),
                    new PartitionKey("pk1"),
                    requestOptions),
                "Replace" => await this.encryptionContainer.ReplaceItemStreamAsync(
                    CreateItemStream(),
                    "doc1",
                    new PartitionKey("pk1"),
                    requestOptions),
                "Upsert" => await this.encryptionContainer.UpsertItemStreamAsync(
                    CreateItemStream(),
                    new PartitionKey("pk1"),
                    requestOptions),
                "Read" => await this.encryptionContainer.ReadItemStreamAsync(
                    "doc1",
                    new PartitionKey("pk1"),
                    requestOptions),
                "Delete" => await this.encryptionContainer.DeleteItemStreamAsync(
                    "doc1",
                    new PartitionKey("pk1"),
                    requestOptions),
                _ => throw new AssertFailedException($"Unknown operation: {operation}"),
            };

            Assert.AreSame(innerResponse, response);
            Assert.AreEqual("etag", forwardedOptions.IfMatchEtag);
            AssertSanitizedRequestOptions(requestOptions, forwardedOptions);
        }

        [DataTestMethod]
        [DataRow("Create")]
        [DataRow("Replace")]
        [DataRow("Upsert")]
        [DataRow("Delete")]
        public async Task UnencryptedTypedOperations_SanitizeRequestOptionsWithoutChangingDispatch(string operation)
        {
            JObject item = JObject.Parse("{\"id\":\"doc1\"}");
            ItemRequestOptions requestOptions = new ()
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, NewtonsoftProcessorName },
                    { "unrelated", 42 },
                },
            };
            ItemRequestOptions forwardedOptions = null;
            ItemResponse<JObject> innerResponse = Mock.Of<ItemResponse<JObject>>();
            this.innerContainerMock
                .Setup(c => c.CreateItemAsync(
                    item,
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<ItemRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns<JObject, PartitionKey?, ItemRequestOptions, CancellationToken>(
                    (_, _, options, _) =>
                    {
                        forwardedOptions = options;
                        return Task.FromResult(innerResponse);
                    });
            this.innerContainerMock
                .Setup(c => c.ReplaceItemAsync(
                    item,
                    "doc1",
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<ItemRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns<JObject, string, PartitionKey?, ItemRequestOptions, CancellationToken>(
                    (_, _, _, options, _) =>
                    {
                        forwardedOptions = options;
                        return Task.FromResult(innerResponse);
                    });
            this.innerContainerMock
                .Setup(c => c.UpsertItemAsync(
                    item,
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<ItemRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns<JObject, PartitionKey?, ItemRequestOptions, CancellationToken>(
                    (_, _, options, _) =>
                    {
                        forwardedOptions = options;
                        return Task.FromResult(innerResponse);
                    });
            this.innerContainerMock
                .Setup(c => c.DeleteItemAsync<JObject>(
                    "doc1",
                    It.IsAny<PartitionKey>(),
                    It.IsAny<ItemRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, PartitionKey, ItemRequestOptions, CancellationToken>(
                    (_, _, options, _) =>
                    {
                        forwardedOptions = options;
                        return Task.FromResult(innerResponse);
                    });

            ItemResponse<JObject> response = operation switch
            {
                "Create" => await this.encryptionContainer.CreateItemAsync(
                    item,
                    new PartitionKey("pk1"),
                    requestOptions),
                "Replace" => await this.encryptionContainer.ReplaceItemAsync(
                    item,
                    "doc1",
                    new PartitionKey("pk1"),
                    requestOptions),
                "Upsert" => await this.encryptionContainer.UpsertItemAsync(
                    item,
                    new PartitionKey("pk1"),
                    requestOptions),
                "Delete" => await this.encryptionContainer.DeleteItemAsync<JObject>(
                    "doc1",
                    new PartitionKey("pk1"),
                    requestOptions),
                _ => throw new AssertFailedException($"Unknown operation: {operation}"),
            };

            Assert.AreSame(innerResponse, response);
            AssertSanitizedRequestOptions(requestOptions, forwardedOptions);
        }

#if NET8_0_OR_GREATER
        [DataTestMethod]
        [DataRow("Create", false, "Newtonsoft")]
        [DataRow("Replace", false, "Newtonsoft")]
        [DataRow("Upsert", false, "Newtonsoft")]
        [DataRow("Create", true, "Stream")]
        [DataRow("Replace", true, "Stream")]
        [DataRow("Upsert", true, "Stream")]
        public async Task EncryptedWrites_DefaultToNewtonsoft_AndHonorPerRequestOverride(
            string operation,
            bool useStreamOverride,
            string expectedProcessor)
        {
            Mock<Encryptor> mdeEncryptor = TestEncryptorFactory.CreateMde("dekId", out _);
            EncryptionContainer container = CreateEncryptionContainer(
                this.innerContainerMock,
                mdeEncryptor,
                this.responseFactoryMock,
                this.serializerMock);
            container.UseStreamingJsonProcessingByDefault();

            EncryptionItemRequestOptions requestOptions = CreateEncryptedWriteOptions();
            if (useStreamOverride)
            {
                requestOptions.Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, StreamProcessorName },
                    { "unrelated", 42 },
                };
            }

            ItemRequestOptions forwardedOptions = null;
            this.SetupNullContentWriteResponses(options => forwardedOptions = options);
            List<Activity> activities = new ();
            using ActivityListener listener = CreateActivityListener(activities);

            using ResponseMessage response = await ExecuteWriteAsync(
                container,
                operation,
                requestOptions);

            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.IsTrue(activities.Any(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeEncryptModeSelectionPrefix + expectedProcessor));
            if (useStreamOverride)
            {
                AssertSanitizedRequestOptions(requestOptions, forwardedOptions);
            }
            else
            {
                Assert.AreSame(requestOptions, forwardedOptions);
            }
        }

        [DataTestMethod]
        [DataRow(null, "Stream")]
        [DataRow("Newtonsoft", "Newtonsoft")]
        public async Task ReadItemStreamAsync_UsesConfiguredDefaultUnlessRequestOverrides(
            string requestProcessor,
            string expectedProcessor)
        {
            this.encryptionContainer.UseStreamingJsonProcessingByDefault();
            ItemRequestOptions requestOptions = requestProcessor == null
                ? null
                : new ItemRequestOptions
                {
                    Properties = new Dictionary<string, object>
                    {
                        { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, requestProcessor },
                        { "unrelated", 42 },
                    },
                };
            ItemRequestOptions forwardedOptions = null;
            this.innerContainerMock
                .Setup(c => c.ReadItemStreamAsync(
                    "doc1",
                    new PartitionKey("pk1"),
                    It.IsAny<ItemRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, PartitionKey, ItemRequestOptions, CancellationToken>(
                    (_, _, options, _) => forwardedOptions = options)
                .ReturnsAsync(CreateOkResponse("{\"id\":\"doc1\"}"));
            List<Activity> activities = new ();
            using ActivityListener listener = CreateActivityListener(activities);

            using ResponseMessage response = await this.encryptionContainer.ReadItemStreamAsync(
                "doc1",
                new PartitionKey("pk1"),
                requestOptions);

            Assert.IsTrue(activities.Any(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + expectedProcessor));
            if (requestOptions == null)
            {
                Assert.IsNull(forwardedOptions);
            }
            else
            {
                AssertSanitizedRequestOptions(requestOptions, forwardedOptions);
            }
        }
#endif

        [TestMethod]
        public async Task ReadManyItemsStreamAsync_SanitizesOptionsAndPreservesErrorResponses()
        {
            IReadOnlyList<(string id, PartitionKey partitionKey)> items = new[]
            {
                ("doc1", new PartitionKey("pk1")),
            };
            ReadManyRequestOptions requestOptions = new ()
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, NewtonsoftProcessorName },
                    { "unrelated", 42 },
                },
            };
            ReadManyRequestOptions forwardedOptions = null;
            ResponseMessage innerResponse = new (HttpStatusCode.TooManyRequests)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes("{\"message\":\"throttled\"}")),
            };
            this.innerContainerMock
                .Setup(c => c.ReadManyItemsStreamAsync(
                    items,
                    It.IsAny<ReadManyRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<IReadOnlyList<(string id, PartitionKey partitionKey)>, ReadManyRequestOptions, CancellationToken>(
                    (_, options, _) => forwardedOptions = options)
                .ReturnsAsync(innerResponse);

            ResponseMessage response = await this.encryptionContainer.ReadManyItemsStreamAsync(
                items,
                requestOptions);

            Assert.AreSame(innerResponse, response);
            Assert.AreEqual("{\"message\":\"throttled\"}", TestCommon.FromStream<JToken>(response.Content).ToString(Newtonsoft.Json.Formatting.None));
            AssertSanitizedRequestOptions(requestOptions, forwardedOptions);
        }

        [TestMethod]
        public async Task ReadManyItemsStreamAsync_NullContent_ReturnsOriginalResponse()
        {
            IReadOnlyList<(string id, PartitionKey partitionKey)> items = new[]
            {
                ("doc1", new PartitionKey("pk1")),
            };
            ResponseMessage innerResponse = new (HttpStatusCode.OK);
            this.innerContainerMock
                .Setup(c => c.ReadManyItemsStreamAsync(
                    items,
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(innerResponse);

            ResponseMessage response = await this.encryptionContainer.ReadManyItemsStreamAsync(items);

            Assert.AreSame(innerResponse, response);
            Assert.IsNull(response.Content);
        }

        [DataTestMethod]
        [DataRow("StreamDefinition")]
        [DataRow("StreamText")]
        [DataRow("StreamFeedRange")]
        [DataRow("TypedDefinition")]
        [DataRow("TypedText")]
        [DataRow("TypedFeedRange")]
        public void QueryIteratorOverloads_SanitizeRequestOptionsWithoutMutatingCaller(string operation)
        {
            QueryDefinition queryDefinition = new ("SELECT * FROM c");
            FeedRange feedRange = Mock.Of<FeedRange>();
            QueryRequestOptions requestOptions = CreateRequestOptionsWithOverride(NewtonsoftProcessorName);
            QueryRequestOptions forwardedOptions = null;
            this.innerContainerMock
                .Setup(c => c.GetItemQueryStreamIterator(
                    queryDefinition,
                    "token",
                    It.IsAny<QueryRequestOptions>()))
                .Callback<QueryDefinition, string, QueryRequestOptions>(
                    (_, _, options) => forwardedOptions = options)
                .Returns(this.feedIteratorMock.Object);
            this.innerContainerMock
                .Setup(c => c.GetItemQueryStreamIterator(
                    "SELECT * FROM c",
                    "token",
                    It.IsAny<QueryRequestOptions>()))
                .Callback<string, string, QueryRequestOptions>(
                    (_, _, options) => forwardedOptions = options)
                .Returns(this.feedIteratorMock.Object);
            this.innerContainerMock
                .Setup(c => c.GetItemQueryStreamIterator(
                    feedRange,
                    queryDefinition,
                    "token",
                    It.IsAny<QueryRequestOptions>()))
                .Callback<FeedRange, QueryDefinition, string, QueryRequestOptions>(
                    (_, _, _, options) => forwardedOptions = options)
                .Returns(this.feedIteratorMock.Object);

            object iterator = operation switch
            {
                "StreamDefinition" => this.encryptionContainer.GetItemQueryStreamIterator(
                    queryDefinition,
                    "token",
                    requestOptions),
                "StreamText" => this.encryptionContainer.GetItemQueryStreamIterator(
                    "SELECT * FROM c",
                    "token",
                    requestOptions),
                "StreamFeedRange" => this.encryptionContainer.GetItemQueryStreamIterator(
                    feedRange,
                    queryDefinition,
                    "token",
                    requestOptions),
                "TypedDefinition" => this.encryptionContainer.GetItemQueryIterator<JObject>(
                    queryDefinition,
                    "token",
                    requestOptions),
                "TypedText" => this.encryptionContainer.GetItemQueryIterator<JObject>(
                    "SELECT * FROM c",
                    "token",
                    requestOptions),
                "TypedFeedRange" => this.encryptionContainer.GetItemQueryIterator<JObject>(
                    feedRange,
                    queryDefinition,
                    "token",
                    requestOptions),
                _ => throw new AssertFailedException($"Unknown operation: {operation}"),
            };

            Assert.IsNotNull(iterator);
            AssertSanitizedRequestOptions(requestOptions, forwardedOptions);
        }

        [TestMethod]
        public void GetChangeFeedStreamIterator_SanitizesRequestOptionsWithoutMutatingCaller()
        {
            ChangeFeedStartFrom startFrom = ChangeFeedStartFrom.Beginning();
            ChangeFeedMode mode = ChangeFeedMode.Incremental;
            ChangeFeedRequestOptions requestOptions = new ()
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, NewtonsoftProcessorName },
                    { "unrelated", 42 },
                },
            };
            ChangeFeedRequestOptions forwardedOptions = null;
            this.innerContainerMock
                .Setup(c => c.GetChangeFeedStreamIterator(
                    startFrom,
                    mode,
                    It.IsAny<ChangeFeedRequestOptions>()))
                .Callback<ChangeFeedStartFrom, ChangeFeedMode, ChangeFeedRequestOptions>(
                    (_, _, options) => forwardedOptions = options)
                .Returns(this.feedIteratorMock.Object);

            FeedIterator iterator = this.encryptionContainer.GetChangeFeedStreamIterator(
                startFrom,
                mode,
                requestOptions);

            Assert.IsInstanceOfType(iterator, typeof(EncryptionFeedIterator));
            AssertSanitizedRequestOptions(requestOptions, forwardedOptions);
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        public void GetItemLinqQueryable_SanitizesAndCarriesOverrideThroughDerivedQueries()
        {
            this.encryptionContainer.UseStreamingJsonProcessingByDefault();
            QueryRequestOptions requestOptions = CreateRequestOptionsWithOverride(NewtonsoftProcessorName);
            QueryRequestOptions forwardedOptions = null;
            IOrderedQueryable<int> innerQuery = new[] { 1, 2 }.AsQueryable().OrderBy(value => value);
            this.innerContainerMock
                .Setup(c => c.GetItemLinqQueryable<int>(
                    false,
                    "token",
                    It.IsAny<QueryRequestOptions>(),
                    null))
                .Returns<bool, string, QueryRequestOptions, CosmosLinqSerializerOptions>(
                    (_, _, options, _) =>
                    {
                        forwardedOptions = options;
                        return innerQuery;
                    });

            IOrderedQueryable<int> query = this.encryptionContainer.GetItemLinqQueryable<int>(
                allowSynchronousQueryExecution: false,
                continuationToken: "token",
                requestOptions: requestOptions);
            IQueryable<int> filteredQuery = query.Where(value => value > 1);
            Assert.AreSame(innerQuery, query);
            AssertSanitizedRequestOptions(requestOptions, forwardedOptions);

            requestOptions.Properties = CreateJsonProcessorPropertyBag(StreamProcessorName);

            Assert.AreEqual(JsonProcessor.Newtonsoft, this.encryptionContainer.ResolveLinqJsonProcessor(query));
            Assert.AreEqual(JsonProcessor.Newtonsoft, this.encryptionContainer.ResolveLinqJsonProcessor(filteredQuery));
        }

        [TestMethod]
        public async Task ChangeFeedStreamProcessor_UsesConfiguredDefault()
        {
            this.encryptionContainer.UseStreamingJsonProcessingByDefault();
            Container.ChangeFeedStreamHandler capturedHandler = null;
            this.innerContainerMock
                .Setup(c => c.GetChangeFeedProcessorBuilder(
                    "processor",
                    It.IsAny<Container.ChangeFeedStreamHandler>()))
                .Callback<string, Container.ChangeFeedStreamHandler>(
                    (_, handler) => capturedHandler = handler)
                .Returns((ChangeFeedProcessorBuilder)null);
            Stream deliveredChanges = null;
            Container.ChangeFeedStreamHandler handler = (_, changes, _) =>
            {
                deliveredChanges = changes;
                return Task.CompletedTask;
            };
            this.encryptionContainer.GetChangeFeedProcessorBuilder("processor", handler);
            using Stream changes = new MemoryStream(Encoding.UTF8.GetBytes(CreateFeedPayload()));

            await capturedHandler(
                Mock.Of<ChangeFeedProcessorContext>(),
                changes,
                CancellationToken.None);

            Assert.AreSame(changes, deliveredChanges);
        }

#endif

        [DataTestMethod]
        [DataRow("Create")]
        [DataRow("Replace")]
        [DataRow("Upsert")]
        public async Task EncryptableItem_ContentResponseDisabled_PreservesSuccessfulWrite(string operation)
        {
            EncryptionItemRequestOptions requestOptions = new ()
            {
                EnableContentResponseOnWrite = false,
                EncryptionOptions = new EncryptionOptions
                {
                    DataEncryptionKeyId = "dekId",
#pragma warning disable CS0618
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
#pragma warning restore CS0618
                    PathsToEncrypt = Array.Empty<string>(),
                },
            };
            HttpStatusCode statusCode = operation == "Create"
                ? HttpStatusCode.Created
                : HttpStatusCode.OK;
            ResponseMessage innerResponse = new (statusCode);
            this.innerContainerMock
                .Setup(c => c.CreateItemStreamAsync(
                    It.IsAny<Stream>(),
                    new PartitionKey("pk1"),
                    requestOptions,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(innerResponse);
            this.innerContainerMock
                .Setup(c => c.ReplaceItemStreamAsync(
                    It.IsAny<Stream>(),
                    "doc1",
                    new PartitionKey("pk1"),
                    requestOptions,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(innerResponse);
            this.innerContainerMock
                .Setup(c => c.UpsertItemStreamAsync(
                    It.IsAny<Stream>(),
                    new PartitionKey("pk1"),
                    requestOptions,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(innerResponse);
            EncryptableItemStream item = new (
                new MemoryStream(Encoding.UTF8.GetBytes("{\"id\":\"doc1\",\"pk\":\"pk1\"}")));

            ItemResponse<EncryptableItemStream> response = operation switch
            {
                "Create" => await this.encryptionContainer.CreateItemAsync(
                    item,
                    new PartitionKey("pk1"),
                    requestOptions),
                "Replace" => await this.encryptionContainer.ReplaceItemAsync(
                    item,
                    "doc1",
                    new PartitionKey("pk1"),
                    requestOptions),
                "Upsert" => await this.encryptionContainer.UpsertItemAsync(
                    item,
                    new PartitionKey("pk1"),
                    requestOptions),
                _ => throw new AssertFailedException($"Unknown operation: {operation}"),
            };

            Assert.AreEqual(statusCode, response.StatusCode);
            Assert.AreSame(item, response.Resource);
            Assert.ThrowsException<InvalidOperationException>(() => _ = item.DecryptableItem);
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

        private static QueryRequestOptions CreateRequestOptionsWithOverride(string jsonProcessor)
        {
            return new QueryRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, jsonProcessor },
                    { "unrelated", 42 },
                },
            };
        }

        private static Dictionary<string, object> CreateJsonProcessorPropertyBag(string jsonProcessor)
        {
            return new Dictionary<string, object>
            {
                { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, jsonProcessor }
            };
        }

        private static void AssertSanitizedRequestOptions(
            RequestOptions callerOptions,
            RequestOptions forwardedOptions)
        {
            Assert.IsNotNull(forwardedOptions);
            Assert.AreNotSame(callerOptions, forwardedOptions);
            Assert.IsTrue(callerOptions.Properties.ContainsKey(
                JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey));
            Assert.IsFalse(forwardedOptions.Properties.ContainsKey(
                JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey));
            Assert.AreEqual(42, forwardedOptions.Properties["unrelated"]);
        }

        public static IEnumerable<object[]> GetSupportedJsonProcessorsData()
        {
#if NET8_0_OR_GREATER
            yield return new object[] { StreamProcessorName };
#endif
            yield return new object[] { NewtonsoftProcessorName };
        }

        private static string CreateFeedPayload()
        {
            return "{\"Documents\":[{\"id\":\"doc1\"}]}";
        }

        private static MemoryStream CreateItemStream()
        {
            return new MemoryStream(Encoding.UTF8.GetBytes("{\"id\":\"doc1\",\"Sensitive\":\"secret\"}"));
        }

        private static EncryptionItemRequestOptions CreateEncryptedWriteOptions()
        {
            return new EncryptionItemRequestOptions
            {
                EncryptionOptions = new EncryptionOptions
                {
                    DataEncryptionKeyId = "dekId",
#pragma warning disable CS0618
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
#pragma warning restore CS0618
                    PathsToEncrypt = new[] { "/Sensitive" },
                },
            };
        }

        private static Task<ResponseMessage> ExecuteWriteAsync(
            EncryptionContainer container,
            string operation,
            EncryptionItemRequestOptions requestOptions)
        {
            return operation switch
            {
                "Create" => container.CreateItemStreamAsync(
                    CreateItemStream(),
                    new PartitionKey("pk1"),
                    requestOptions),
                "Replace" => container.ReplaceItemStreamAsync(
                    CreateItemStream(),
                    "doc1",
                    new PartitionKey("pk1"),
                    requestOptions),
                "Upsert" => container.UpsertItemStreamAsync(
                    CreateItemStream(),
                    new PartitionKey("pk1"),
                    requestOptions),
                _ => throw new AssertFailedException($"Unknown operation: {operation}"),
            };
        }

        private void SetupNullContentWriteResponses(Action<ItemRequestOptions> captureOptions)
        {
            this.innerContainerMock
                .Setup(inner => inner.CreateItemStreamAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<PartitionKey>(),
                    It.IsAny<ItemRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Stream, PartitionKey, ItemRequestOptions, CancellationToken>(
                    (_, _, options, _) => captureOptions(options))
                .ReturnsAsync(new ResponseMessage(HttpStatusCode.Created));
            this.innerContainerMock
                .Setup(inner => inner.ReplaceItemStreamAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<PartitionKey>(),
                    It.IsAny<ItemRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Stream, string, PartitionKey, ItemRequestOptions, CancellationToken>(
                    (_, _, _, options, _) => captureOptions(options))
                .ReturnsAsync(new ResponseMessage(HttpStatusCode.OK));
            this.innerContainerMock
                .Setup(inner => inner.UpsertItemStreamAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<PartitionKey>(),
                    It.IsAny<ItemRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Stream, PartitionKey, ItemRequestOptions, CancellationToken>(
                    (_, _, options, _) => captureOptions(options))
                .ReturnsAsync(new ResponseMessage(HttpStatusCode.OK));
        }

#if NET8_0_OR_GREATER
        private static ActivityListener CreateActivityListener(List<Activity> activities)
        {
            ActivityListener listener = new ()
            {
                ShouldListenTo = source => source.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => activities.Add(activity),
            };
            ActivitySource.AddActivityListener(listener);
            return listener;
        }
#endif

    }
}