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
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using static Microsoft.Azure.Cosmos.Container;

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
            ChangesEstimationHandler estimatorDispatcher = (long detectedEstimation, CancellationToken token) =>
            {
                detectedEstimationCorrectly = estimation == detectedEstimation;
                cancellationTokenSource.Cancel();
                return Task.CompletedTask;
            };

            Mock<FeedResponse<RemainingLeaseWork>> mockedResponse = new Mock<FeedResponse<RemainingLeaseWork>>();
            mockedResponse.Setup(r => r.Count).Returns(1);
            mockedResponse.Setup(r => r.GetEnumerator()).Returns(new List<RemainingLeaseWork>() { new RemainingLeaseWork(string.Empty, estimation, string.Empty) }.GetEnumerator());

            Mock<FeedIterator<RemainingLeaseWork>> mockedIterator = new Mock<FeedIterator<RemainingLeaseWork>>();
            mockedIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);

            Mock<ChangeFeedEstimator> mockedEstimator = new Mock<ChangeFeedEstimator>();
            mockedEstimator.Setup(e => e.GetRemainingLeaseWorkIterator(It.IsAny<ChangeFeedEstimatorRequestOptions>())).Returns(mockedIterator.Object);

            FeedEstimatorRunner estimatorCore = new FeedEstimatorRunner(estimatorDispatcher, mockedEstimator.Object, TimeSpan.FromMilliseconds(10));

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
        public async Task FeedEstimatorRunner_NoLeases()
        {
            const long estimation = 1; // When no leases the expected behavior is that the estimation is 1
            bool detectedEstimationCorrectly = false;
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(500);
            ChangesEstimationHandler estimatorDispatcher = (long detectedEstimation, CancellationToken token) =>
            {
                detectedEstimationCorrectly = estimation == detectedEstimation;
                cancellationTokenSource.Cancel();
                return Task.CompletedTask;
            };

            Mock<FeedResponse<RemainingLeaseWork>> mockedResponse = new Mock<FeedResponse<RemainingLeaseWork>>();
            mockedResponse.Setup(r => r.Count).Returns(0);

            Mock<FeedIterator<RemainingLeaseWork>> mockedIterator = new Mock<FeedIterator<RemainingLeaseWork>>();
            mockedIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);

            Mock<ChangeFeedEstimator> mockedEstimator = new Mock<ChangeFeedEstimator>();
            mockedEstimator.Setup(e => e.GetRemainingLeaseWorkIterator(It.IsAny<ChangeFeedEstimatorRequestOptions>())).Returns(mockedIterator.Object);

            FeedEstimatorRunner estimatorCore = new FeedEstimatorRunner(estimatorDispatcher, mockedEstimator.Object, TimeSpan.FromMilliseconds(10));

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
