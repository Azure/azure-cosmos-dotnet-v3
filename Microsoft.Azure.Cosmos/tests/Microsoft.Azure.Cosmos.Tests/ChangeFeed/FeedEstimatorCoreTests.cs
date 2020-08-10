//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using static Microsoft.Azure.Cosmos.Container;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class FeedEstimatorCoreTests
    {
        [TestMethod]
        public async Task FeedEstimatorCore_ReceivesEstimation()
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

            Mock<RemainingWorkEstimator> mockedEstimator = new Mock<RemainingWorkEstimator>();
            mockedEstimator.Setup(e => e.GetEstimatedRemainingWorkAsync(It.IsAny<CancellationToken>())).ReturnsAsync(estimation);

            FeedEstimatorCore estimatorCore = new FeedEstimatorCore(estimatorDispatcher, mockedEstimator.Object, TimeSpan.FromMilliseconds(10));

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
        public async Task FeedEstimatorCore_ReceivesEstimation_List()
        {
            IReadOnlyList<RemainingLeaseWork> estimation = new List<RemainingLeaseWork>()
            {
                new RemainingLeaseWork(Guid.NewGuid().ToString(), 5, Guid.NewGuid().ToString()),
                new RemainingLeaseWork(Guid.NewGuid().ToString(), 10, Guid.NewGuid().ToString()),
            };
            bool detectedEstimationCorrectly = false;
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(500);
            ChangesEstimationDetailedHandler estimatorDispatcher = (IReadOnlyList<RemainingLeaseWork> detectedEstimation, CancellationToken token) =>
            {
                detectedEstimationCorrectly = detectedEstimation.Count == estimation.Count
                                            && detectedEstimation[1].RemainingWork == estimation[1].RemainingWork
                                            && detectedEstimation[0].RemainingWork == estimation[0].RemainingWork;

                cancellationTokenSource.Cancel();
                return Task.CompletedTask;
            };

            Mock<RemainingWorkEstimator> mockedEstimator = new Mock<RemainingWorkEstimator>();
            mockedEstimator.Setup(e => e.GetEstimatedRemainingWorkPerLeaseTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(estimation);

            FeedEstimatorCore estimatorCore = new FeedEstimatorCore(estimatorDispatcher, mockedEstimator.Object, TimeSpan.FromMilliseconds(10));

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
