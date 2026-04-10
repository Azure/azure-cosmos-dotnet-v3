//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using static Microsoft.Azure.Cosmos.Container;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class FeedProcessorCoreTests
    {
        private static readonly ProcessorOptions DefaultSettings = new ProcessorOptions()
        {
            FeedPollDelay = TimeSpan.FromSeconds(0)
        };

        [TestMethod]
        public async Task UsesCustomSerializer()
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(1000);

            ChangesHandler<MyDocument> handler = (changes, cancelationToken) =>
            {
                IReadOnlyList<MyDocument> list = changes as IReadOnlyList<MyDocument>;
                Assert.IsNotNull(list);
                Assert.AreEqual("test", list[0].id);
                return Task.CompletedTask;
            };
            Mock<PartitionCheckpointer> mockCheckpointer = new Mock<PartitionCheckpointer>();
            Mock<FeedIterator> mockIterator = new Mock<FeedIterator>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(GetResponse(HttpStatusCode.OK, true));
            mockIterator.SetupSequence(i => i.HasMoreResults).Returns(true).Returns(false);

            CustomSerializer serializer = new CustomSerializer();
            ChangeFeedObserverFactoryCore<MyDocument> factory = new ChangeFeedObserverFactoryCore<MyDocument>(handler, new CosmosSerializerCore(serializer));
            FeedProcessorCore processor = new FeedProcessorCore(factory.CreateObserver(), mockIterator.Object, FeedProcessorCoreTests.DefaultSettings, mockCheckpointer.Object);

            try
            {
                await processor.RunAsync(cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                // Expected
            }

            Assert.AreEqual(1, serializer.FromStreamCalled, "Should have called FromStream on the custom serializer");
        }

        [TestMethod]
        public async Task ReadAsyncExpectedTimeoutTest()
        {
            ChangesHandler<MyDocument> handler = (changes, cancelationToken) =>
            {
                IReadOnlyList<MyDocument> list = changes as IReadOnlyList<MyDocument>;
                Assert.IsNotNull(list);
                Assert.AreEqual("test", list[0].id);
                return Task.CompletedTask;
            };
            Mock<PartitionCheckpointer> mockCheckpointer = new Mock<PartitionCheckpointer>();
            Mock<FeedIterator> mockIterator = new Mock<FeedIterator>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    await Task.Delay(200);
                    return GetResponse(HttpStatusCode.OK, true);
                });
            mockIterator.SetupSequence(i => i.HasMoreResults).Returns(true).Returns(false);

            CustomSerializer serializer = new CustomSerializer();
            ChangeFeedObserverFactoryCore<MyDocument> factory = new ChangeFeedObserverFactoryCore<MyDocument>(handler, new CosmosSerializerCore(serializer));
            ProcessorOptions options = new ProcessorOptions()
            {
                RequestTimeout = TimeSpan.FromMilliseconds(100)
            };
            FeedProcessorCore processor = new FeedProcessorCore(factory.CreateObserver(), mockIterator.Object, options, mockCheckpointer.Object);

            try
            {
                Task runTask = processor.RunAsync(default);
                if (!ReferenceEquals(await Task.WhenAny(runTask, Task.Delay(1000)), runTask))
                {
                    Assert.Fail("Test timed out without throwing expected CosmosException.");
                }
            }
            catch (CosmosException timeoutException)
            {
                Assert.AreEqual(timeoutException.StatusCode, HttpStatusCode.RequestTimeout);
            }

        }

        [TestMethod]
        public async Task ThrowsOnFailedCustomSerializer()
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(1000);

            Mock<PartitionCheckpointer> mockCheckpointer = new Mock<PartitionCheckpointer>();
            Mock<FeedIterator> mockIterator = new Mock<FeedIterator>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(GetResponse(HttpStatusCode.OK, true));
            mockIterator.SetupSequence(i => i.HasMoreResults).Returns(true).Returns(false);

            CustomSerializerFails serializer = new CustomSerializerFails();
            ChangesHandler<dynamic> handler = (changes, cancelationToken) => Task.CompletedTask;
            ChangeFeedObserverFactoryCore<dynamic> factory = new ChangeFeedObserverFactoryCore<dynamic>(handler, new CosmosSerializerCore(serializer));
            FeedProcessorCore processor = new FeedProcessorCore(factory.CreateObserver(), mockIterator.Object, FeedProcessorCoreTests.DefaultSettings, mockCheckpointer.Object);

            ChangeFeedProcessorUserException caughtException = await Assert.ThrowsExceptionAsync<ChangeFeedProcessorUserException>(() => processor.RunAsync(cancellationTokenSource.Token));
            Assert.IsInstanceOfType(caughtException.InnerException, typeof(CustomException));
        }

        [DataRow(HttpStatusCode.Gone, (int)Documents.SubStatusCodes.PartitionKeyRangeGone)]
        [DataRow(HttpStatusCode.Gone, (int)Documents.SubStatusCodes.CompletingSplit)]
        [DataTestMethod]
        public async Task ThrowOnPartitionSplit(HttpStatusCode statusCode, int subStatusCode)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(1000);

            Mock<ChangeFeedObserver> mockObserver = new Mock<ChangeFeedObserver>();

            Mock<PartitionCheckpointer> mockCheckpointer = new Mock<PartitionCheckpointer>();
            Mock<FeedIterator> mockIterator = new Mock<FeedIterator>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetResponse(statusCode, false, subStatusCode));

            FeedProcessorCore processor = new FeedProcessorCore(mockObserver.Object, mockIterator.Object, FeedProcessorCoreTests.DefaultSettings, mockCheckpointer.Object);

            await Assert.ThrowsExceptionAsync<FeedRangeGoneException>(() => processor.RunAsync(cancellationTokenSource.Token));
        }

        [DataRow(HttpStatusCode.NotFound, (int)Documents.SubStatusCodes.Unknown)]
        [DataTestMethod]
        public async Task ThrowOnPartitionGone(HttpStatusCode statusCode, int subStatusCode)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(1000);

            Mock<ChangeFeedObserver> mockObserver = new Mock<ChangeFeedObserver>();

            Mock<PartitionCheckpointer> mockCheckpointer = new Mock<PartitionCheckpointer>();
            Mock<FeedIterator> mockIterator = new Mock<FeedIterator>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetResponse(statusCode, false, subStatusCode));

            FeedProcessorCore processor = new FeedProcessorCore(mockObserver.Object, mockIterator.Object, FeedProcessorCoreTests.DefaultSettings, mockCheckpointer.Object);

            CosmosException cosmosException = await Assert.ThrowsExceptionAsync<CosmosException>(() => processor.RunAsync(cancellationTokenSource.Token));
            Assert.AreEqual(statusCode, cosmosException.StatusCode);
            Assert.AreEqual(subStatusCode, (int)cosmosException.Headers.SubStatusCode);
        }

        [DataRow(HttpStatusCode.NotFound, (int)Documents.SubStatusCodes.ReadSessionNotAvailable)]
        [DataTestMethod]
        public async Task ThrowOnReadSessionNotAvailable(HttpStatusCode statusCode, int subStatusCode)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(1000);

            Mock<ChangeFeedObserver> mockObserver = new Mock<ChangeFeedObserver>();

            Mock<PartitionCheckpointer> mockCheckpointer = new Mock<PartitionCheckpointer>();
            Mock<FeedIterator> mockIterator = new Mock<FeedIterator>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetResponse(statusCode, false, subStatusCode));

            FeedProcessorCore processor = new FeedProcessorCore(
                mockObserver.Object,
                mockIterator.Object,
                FeedProcessorCoreTests.DefaultSettings,
                mockCheckpointer.Object);

            CosmosException cosmosException = await Assert.ThrowsExceptionAsync<CosmosException>(() => processor.RunAsync(cancellationTokenSource.Token));
            Assert.AreEqual(statusCode, cosmosException.StatusCode);
            Assert.AreEqual(subStatusCode, (int)cosmosException.Headers.SubStatusCode);
        }

        private static ResponseMessage GetResponse(HttpStatusCode statusCode, bool includeItem, int subStatusCode = 0)
        {
            ResponseMessage message = new ResponseMessage(statusCode);
            message.Headers.ContinuationToken = "someContinuation";
            if (subStatusCode > 0)
            {
                message.Headers.SubStatusCode = (Documents.SubStatusCodes)subStatusCode;
            }

            if (includeItem)
            {
                MyDocument document = new MyDocument
                {
                    id = "test"
                };

                message.Content = new CosmosJsonDotNetSerializer().ToStream(new { Documents = new List<MyDocument>() { document } });
            }

            return message;
        }

        public class MyDocument
        {
            public string id { get; set; }
        }

        private class CustomSerializer : CosmosSerializer
        {
            private readonly CosmosSerializer cosmosSerializer = new CosmosJsonDotNetSerializer();
            public int FromStreamCalled = 0;
            public int ToStreamCalled = 0;

            public override T FromStream<T>(Stream stream)
            {
                this.FromStreamCalled++;
                return this.cosmosSerializer.FromStream<T>(stream);
            }

            public override Stream ToStream<T>(T input)
            {
                this.ToStreamCalled++;
                return this.cosmosSerializer.ToStream<T>(input);
            }
        }

        private class CustomSerializerFails : CosmosSerializer
        {
            private readonly CosmosSerializer cosmosSerializer = new CosmosJsonDotNetSerializer();
            public override T FromStream<T>(Stream stream)
            {
                throw new CustomException();
            }

            public override Stream ToStream<T>(T input)
            {
                return this.cosmosSerializer.ToStream<T>(input);
            }
        }


        private class CustomException : Exception
        {

        }
    }
}