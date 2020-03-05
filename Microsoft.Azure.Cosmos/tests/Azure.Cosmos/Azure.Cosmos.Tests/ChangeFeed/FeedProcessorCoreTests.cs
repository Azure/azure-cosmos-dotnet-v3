//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos.Serialization;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class FeedProcessorCoreTests
    {
        private static ProcessorOptions DefaultSettings = new ProcessorOptions() {
            FeedPollDelay = TimeSpan.FromSeconds(0)
        };

        [TestMethod]
        public async Task UsesCustomSerializer()
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(1000);

            Mock<ChangeFeedObserver<MyDocument>> mockObserver = new Mock<ChangeFeedObserver<MyDocument>>();
            mockObserver.Setup(o => o.ProcessChangesAsync(
                    It.IsAny<ChangeFeedObserverContext>(),
                    It.Is<IReadOnlyList<MyDocument>>(list => list[0].id.Equals("test")),
                    It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Mock<PartitionCheckpointer> mockCheckpointer = new Mock<PartitionCheckpointer>();
            Mock<FeedIterator> mockIterator = new Mock<FeedIterator>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(GetResponse(HttpStatusCode.OK, true));
            mockIterator.SetupSequence(i => i.HasMoreResults).Returns(true).Returns(false);

            CustomSerializer serializer = new CustomSerializer();
            FeedProcessorCore<MyDocument> processor = new FeedProcessorCore<MyDocument>(mockObserver.Object, mockIterator.Object, FeedProcessorCoreTests.DefaultSettings, mockCheckpointer.Object, serializer);

            try
            {
                await processor.RunAsync(cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                // Expected
            }

            Mock.Get(mockObserver.Object)
                .Verify(o => o.ProcessChangesAsync(
                    It.IsAny<ChangeFeedObserverContext>(), 
                    It.Is<IReadOnlyList<MyDocument>>(list => list[0].id.Equals("test")),
                    It.IsAny<CancellationToken>())
                    , Times.Once);

            Assert.AreEqual(1, serializer.FromStreamCalled, "Should have called FromStream on the custom serializer");
        }

        [TestMethod]
        public async Task ThrowsOnFailedCustomSerializer()
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(1000);

            Mock<ChangeFeedObserver<MyDocument>> mockObserver = new Mock<ChangeFeedObserver<MyDocument>>();
            Mock<PartitionCheckpointer> mockCheckpointer = new Mock<PartitionCheckpointer>();
            Mock<FeedIterator> mockIterator = new Mock<FeedIterator>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(GetResponse(HttpStatusCode.OK, true));
            mockIterator.SetupSequence(i => i.HasMoreResults).Returns(true).Returns(false);

            CustomSerializerFails serializer = new CustomSerializerFails();
            FeedProcessorCore<MyDocument> processor = new FeedProcessorCore<MyDocument>(mockObserver.Object, mockIterator.Object, FeedProcessorCoreTests.DefaultSettings, mockCheckpointer.Object, serializer);

            ObserverException caughtException = await Assert.ThrowsExceptionAsync<ObserverException>(() => processor.RunAsync(cancellationTokenSource.Token));
            Assert.IsInstanceOfType(caughtException.InnerException, typeof(CustomException));
        }

        [DataRow(HttpStatusCode.Gone, (int)Microsoft.Azure.Documents.SubStatusCodes.PartitionKeyRangeGone)]
        [DataRow(HttpStatusCode.Gone, (int)Microsoft.Azure.Documents.SubStatusCodes.CompletingSplit)]
        [DataTestMethod]
        public async Task ThrowOnPartitionSplit(HttpStatusCode statusCode, int subStatusCode)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(1000);

            Mock<ChangeFeedObserver<MyDocument>> mockObserver = new Mock<ChangeFeedObserver<MyDocument>>();

            Mock<PartitionCheckpointer> mockCheckpointer = new Mock<PartitionCheckpointer>();
            Mock<FeedIterator> mockIterator = new Mock<FeedIterator>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetResponse(statusCode, false, subStatusCode));

            FeedProcessorCore<MyDocument> processor = new FeedProcessorCore<MyDocument>(mockObserver.Object, mockIterator.Object, FeedProcessorCoreTests.DefaultSettings, mockCheckpointer.Object, CosmosTextJsonSerializer.CreateUserDefaultSerializer());

            await Assert.ThrowsExceptionAsync<FeedSplitException>(() => processor.RunAsync(cancellationTokenSource.Token));
        }

        private static ResponseMessage GetResponse(HttpStatusCode statusCode, bool includeItem, int subStatusCode = 0)
        {
            ResponseMessage message = new ResponseMessage(statusCode);
            message.CosmosHeaders.ContinuationToken = "someContinuation";
            if (subStatusCode > 0)
            {
                message.CosmosHeaders.Add(WFConstants.BackendHeaders.SubStatus, subStatusCode.ToString());
            }

            if (includeItem)
            {
                MyDocument document = new MyDocument();
                document.id = "test";
                CosmosFeedResponseUtil<MyDocument> cosmosFeedResponse = new CosmosFeedResponseUtil<MyDocument>();
                cosmosFeedResponse.Data = new System.Collections.ObjectModel.Collection<MyDocument>()
                {
                    document
                };

                message.Content = CosmosTextJsonSerializer.CreateUserDefaultSerializer().ToStream(cosmosFeedResponse);
            }

            return message;
        }

        public class MyDocument
        {
            public string id { get; set; }
        }

        private class CustomSerializer : CosmosSerializer
        {
            private CosmosSerializer cosmosSerializer = CosmosTextJsonSerializer.CreateUserDefaultSerializer();
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

        private class CustomSerializerFails: CosmosSerializer
        {
            private CosmosSerializer cosmosSerializer = CosmosTextJsonSerializer.CreateUserDefaultSerializer();
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
