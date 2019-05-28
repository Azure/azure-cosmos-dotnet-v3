//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class FeedEstimatorCoreTests
    {
        [TestMethod]
        public async Task FeedEstimatorCore_ReceivesEstimation()
        {
            const long estimation = 10;
            IReadOnlyList<RemainingLeaseTokenWork> estimationList = new List<RemainingLeaseTokenWork>()
            {
                new RemainingLeaseTokenWork(Guid.NewGuid().ToString(), estimation / 2),
                new RemainingLeaseTokenWork(Guid.NewGuid().ToString(), estimation / 2)
            };
            bool detectedEstimationCorrectly = false;
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(5000);
            ChangeFeedEstimatorDispatcher estimatorDispatcher = new ChangeFeedEstimatorDispatcher((IReadOnlyList<RemainingLeaseTokenWork> detectedEstimation, CancellationToken token) =>
            {
                detectedEstimationCorrectly = estimation == detectedEstimation.Sum(e => e.RemainingWork);

                return Task.CompletedTask;
            },  TimeSpan.FromSeconds(1));

            Mock<RemainingWorkEstimator> mockedEstimator = new Mock<RemainingWorkEstimator>();
            mockedEstimator.Setup(e => e.GetEstimatedRemainingWorkPerLeaseTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(estimationList);

            FeedEstimatorCore estimatorCore = new FeedEstimatorCore(estimatorDispatcher, mockedEstimator.Object);

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
