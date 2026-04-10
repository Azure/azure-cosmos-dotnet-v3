//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class FeedEstimatorRunnerTests
    {
        [TestMethod]
        public async Task FeedEstimatorRunner_ReceivesEstimation()
        {
            const long estimation = 10;
            bool detectedEstimationCorrectly = false;
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(500);
            Task estimatorDispatcher(long detectedEstimation, CancellationToken token)
            {
                detectedEstimationCorrectly = estimation == detectedEstimation;
                cancellationTokenSource.Cancel();
                return Task.CompletedTask;
            }

            Mock<FeedResponse<ChangeFeedProcessorState>> mockedResponse = new Mock<FeedResponse<ChangeFeedProcessorState>>();
            mockedResponse.Setup(r => r.Count).Returns(1);
            mockedResponse.Setup(r => r.GetEnumerator()).Returns(new List<ChangeFeedProcessorState>() { new ChangeFeedProcessorState(string.Empty, estimation, string.Empty) }.GetEnumerator());

            Mock<FeedIterator<ChangeFeedProcessorState>> mockedIterator = new Mock<FeedIterator<ChangeFeedProcessorState>>();
            mockedIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);

            Mock<ChangeFeedEstimator> mockedEstimator = new Mock<ChangeFeedEstimator>();
            mockedEstimator.Setup(e => e.GetCurrentStateIterator(It.IsAny<ChangeFeedEstimatorRequestOptions>())).Returns(mockedIterator.Object);

            FeedEstimatorRunner estimatorCore = new FeedEstimatorRunner(estimatorDispatcher, mockedEstimator.Object, Mock.Of<ChangeFeedProcessorHealthMonitor>(), TimeSpan.FromMilliseconds(10));

            try
            {
                await estimatorCore.RunAsync(cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                // expected
            }

            Assert.IsTrue(detectedEstimationCorrectly);
        }

        [TestMethod]
        public async Task FeedEstimatorRunner_TransientErrorsShouldContinue()
        {
            const long estimation = 10;
            bool detectedEstimationCorrectly = false;
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(500);
            Task estimatorDispatcher(long detectedEstimation, CancellationToken token)
            {
                detectedEstimationCorrectly = estimation == detectedEstimation;
                cancellationTokenSource.Cancel();
                return Task.CompletedTask;
            }

            Mock<FeedResponse<ChangeFeedProcessorState>> mockedResponse = new Mock<FeedResponse<ChangeFeedProcessorState>>();
            mockedResponse.Setup(r => r.Count).Returns(1);
            mockedResponse.Setup(r => r.GetEnumerator()).Returns(new List<ChangeFeedProcessorState>() { new ChangeFeedProcessorState(string.Empty, estimation, string.Empty) }.GetEnumerator());

            CosmosException exception = CosmosExceptionFactory.CreateThrottledException("throttled", new Headers());
            Mock<FeedIterator<ChangeFeedProcessorState>> mockedIterator = new Mock<FeedIterator<ChangeFeedProcessorState>>();
            mockedIterator.SetupSequence(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception)
                .ReturnsAsync(mockedResponse.Object);

            Mock<ChangeFeedEstimator> mockedEstimator = new Mock<ChangeFeedEstimator>();
            mockedEstimator.Setup(e => e.GetCurrentStateIterator(It.IsAny<ChangeFeedEstimatorRequestOptions>())).Returns(mockedIterator.Object);

            Mock<ChangeFeedProcessorHealthMonitor> healthMonitor = new Mock<ChangeFeedProcessorHealthMonitor>();

            FeedEstimatorRunner estimatorCore = new FeedEstimatorRunner(estimatorDispatcher, mockedEstimator.Object, healthMonitor.Object, TimeSpan.FromMilliseconds(10));

            try
            {
                await estimatorCore.RunAsync(cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                // expected
            }

            Assert.IsTrue(detectedEstimationCorrectly);
            mockedIterator.Verify(i => i.ReadNextAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));

            healthMonitor
                .Verify(m => m.NotifyErrorAsync(It.IsAny<string>(), exception), Times.Once);
        }

        [TestMethod]
        public async Task FeedEstimatorRunner_NoLeases()
        {
            const long estimation = 1; // When no leases the expected behavior is that the estimation is 1
            bool detectedEstimationCorrectly = false;
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(500);
            Task estimatorDispatcher(long detectedEstimation, CancellationToken token)
            {
                detectedEstimationCorrectly = estimation == detectedEstimation;
                cancellationTokenSource.Cancel();
                return Task.CompletedTask;
            }

            Mock<FeedResponse<ChangeFeedProcessorState>> mockedResponse = new Mock<FeedResponse<ChangeFeedProcessorState>>();
            mockedResponse.Setup(r => r.Count).Returns(0);

            Mock<FeedIterator<ChangeFeedProcessorState>> mockedIterator = new Mock<FeedIterator<ChangeFeedProcessorState>>();
            mockedIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);

            Mock<ChangeFeedEstimator> mockedEstimator = new Mock<ChangeFeedEstimator>();
            mockedEstimator.Setup(e => e.GetCurrentStateIterator(It.IsAny<ChangeFeedEstimatorRequestOptions>())).Returns(mockedIterator.Object);

            FeedEstimatorRunner estimatorCore = new FeedEstimatorRunner(estimatorDispatcher, mockedEstimator.Object, Mock.Of<ChangeFeedProcessorHealthMonitor>(), TimeSpan.FromMilliseconds(10));

            try
            {
                await estimatorCore.RunAsync(cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                // expected
            }

            Assert.IsTrue(detectedEstimationCorrectly);
        }
    }
}