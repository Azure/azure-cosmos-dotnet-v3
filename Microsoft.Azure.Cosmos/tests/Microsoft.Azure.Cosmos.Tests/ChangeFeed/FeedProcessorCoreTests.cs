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
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Tests;
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
            FeedProcessorCore<MyDocument> processor = new FeedProcessorCore<MyDocument>(
                mockObserver.Object,
                mockIterator.Object,
                FeedProcessorCoreTests.DefaultSettings,
                mockCheckpointer.Object,
                new CosmosSerializerCore(serializer));

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
            FeedProcessorCore<MyDocument> processor = new FeedProcessorCore<MyDocument>(
                mockObserver.Object,
                mockIterator.Object,
                FeedProcessorCoreTests.DefaultSettings,
                mockCheckpointer.Object,
                new CosmosSerializerCore(serializer));

            ObserverException caughtException = await Assert.ThrowsExceptionAsync<ObserverException>(() => processor.RunAsync(cancellationTokenSource.Token));
            Assert.IsInstanceOfType(caughtException.InnerException, typeof(CustomException));
        }

        [DataRow(HttpStatusCode.Gone, (int)Documents.SubStatusCodes.PartitionKeyRangeGone)]
        [DataRow(HttpStatusCode.Gone, (int)Documents.SubStatusCodes.CompletingSplit)]
        [DataTestMethod]
        public async Task ThrowOnPartitionSplit(HttpStatusCode statusCode, int subStatusCode)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(1000);

            Mock<ChangeFeedObserver<MyDocument>> mockObserver = new Mock<ChangeFeedObserver<MyDocument>>();

            Mock<PartitionCheckpointer> mockCheckpointer = new Mock<PartitionCheckpointer>();
            Mock<FeedIterator> mockIterator = new Mock<FeedIterator>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetResponse(statusCode, false, subStatusCode));

            FeedProcessorCore<MyDocument> processor = new FeedProcessorCore<MyDocument>(
                mockObserver.Object,
                mockIterator.Object,
                FeedProcessorCoreTests.DefaultSettings,
                mockCheckpointer.Object,
                MockCosmosUtil.Serializer);

            await Assert.ThrowsExceptionAsync<FeedRangeGoneException>(() => processor.RunAsync(cancellationTokenSource.Token));
        }

        [DataRow(HttpStatusCode.NotFound, (int)Documents.SubStatusCodes.Unknown)]
        [DataTestMethod]
        public async Task ThrowOnPartitionGone(HttpStatusCode statusCode, int subStatusCode)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(1000);

            Mock<ChangeFeedObserver<MyDocument>> mockObserver = new Mock<ChangeFeedObserver<MyDocument>>();

            Mock<PartitionCheckpointer> mockCheckpointer = new Mock<PartitionCheckpointer>();
            Mock<FeedIterator> mockIterator = new Mock<FeedIterator>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetResponse(statusCode, false, subStatusCode));

            FeedProcessorCore<MyDocument> processor = new FeedProcessorCore<MyDocument>(
                mockObserver.Object,
                mockIterator.Object,
                FeedProcessorCoreTests.DefaultSettings,
                mockCheckpointer.Object,
                MockCosmosUtil.Serializer);

            await Assert.ThrowsExceptionAsync<FeedNotFoundException>(() => processor.RunAsync(cancellationTokenSource.Token));
        }

        [DataRow(HttpStatusCode.NotFound, (int)Documents.SubStatusCodes.ReadSessionNotAvailable)]
        [DataTestMethod]
        public async Task ThrowOnReadSessionNotAvailable(HttpStatusCode statusCode, int subStatusCode)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(1000);

            Mock<ChangeFeedObserver<MyDocument>> mockObserver = new Mock<ChangeFeedObserver<MyDocument>>();

            Mock<PartitionCheckpointer> mockCheckpointer = new Mock<PartitionCheckpointer>();
            Mock<FeedIterator> mockIterator = new Mock<FeedIterator>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetResponse(statusCode, false, subStatusCode));

            FeedProcessorCore<MyDocument> processor = new FeedProcessorCore<MyDocument>(
                mockObserver.Object,
                mockIterator.Object,
                FeedProcessorCoreTests.DefaultSettings,
                mockCheckpointer.Object,
                MockCosmosUtil.Serializer);

            await Assert.ThrowsExceptionAsync<FeedReadSessionNotAvailableException>(() => processor.RunAsync(cancellationTokenSource.Token));
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
                MyDocument document = new MyDocument();
                document.id = "test";

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
            private CosmosSerializer cosmosSerializer = new CosmosJsonDotNetSerializer();
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
            private CosmosSerializer cosmosSerializer = new CosmosJsonDotNetSerializer();
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
