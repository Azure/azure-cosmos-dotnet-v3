//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class PartitionLoadBalancerTests
    {
        private readonly DocumentServiceLeaseContainer leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();
        private readonly LoadBalancingStrategy strategy = Mock.Of<LoadBalancingStrategy>();
        private readonly ChangeFeedProcessorHealthMonitor monitor = Mock.Of<ChangeFeedProcessorHealthMonitor>();

        /// <summary>
        /// Repro for GitHub issue #3453: When AddOrUpdateLeaseAsync throws during lease acquisition,
        /// the error should be reported to ChangeFeedProcessorHealthMonitor.NotifyErrorAsync.
        /// Before the fix, errors were only logged via DefaultTrace but NOT reported to the health monitor,
        /// making it impossible for users to detect lease acquisition failures programmatically.
        /// </summary>
        [TestMethod]
        public async Task AddLease_ThrowsException_LeaseAddingContinues()
        {
            FailingPartitionController controller = new FailingPartitionController();

            // long acquire interval to ensure that only 1 load balancing iteration is performed in a test run
            TimeSpan leaseAcquireInterval = TimeSpan.FromHours(1);
            PartitionLoadBalancerCore loadBalancer = new PartitionLoadBalancerCore(controller, this.leaseContainer, this.strategy, leaseAcquireInterval, this.monitor);

            Mock.Get(this.strategy)
                .Setup(s => s.SelectLeasesToTake(It.IsAny<IEnumerable<DocumentServiceLease>>()))
                .Returns(new[] { Mock.Of<DocumentServiceLease>(), Mock.Of<DocumentServiceLease>() });

            Mock.Get(this.leaseContainer)
                .Setup(m => m.GetAllLeasesAsync())
                .ReturnsAsync(new[] { Mock.Of<DocumentServiceLease>(), Mock.Of<DocumentServiceLease>() });

            loadBalancer.Start();
            await loadBalancer.StopAsync();

            Mock.Get(this.strategy)
                .Verify(s => s.SelectLeasesToTake(It.IsAny<IEnumerable<DocumentServiceLease>>()), Times.Once);

            Mock.Get(this.leaseContainer)
                .Verify(m => m.GetAllLeasesAsync(), Times.Once);

            Assert.AreEqual(2, controller.HitCount);

            // Verify that the monitor was notified of errors
            Mock.Get(this.monitor)
                .Verify(m => m.NotifyErrorAsync(It.IsAny<string>(), It.IsAny<Exception>()), Times.Exactly(2));
        }

        /// <summary>
        /// Repro for GitHub issue #3453: When GetAllLeasesAsync throws during load balancing iteration,
        /// the error should be reported to ChangeFeedProcessorHealthMonitor.NotifyErrorAsync.
        /// Before the fix, errors were only logged via DefaultTrace but NOT reported to the health monitor,
        /// making it impossible for users to detect load balancer failures programmatically.
        /// </summary>
        [TestMethod]
        public async Task GetAllLeases_ThrowsException_MonitorNotified()
        {
            PartitionController controller = Mock.Of<PartitionController>();

            TimeSpan leaseAcquireInterval = TimeSpan.FromHours(1);
            PartitionLoadBalancerCore loadBalancer = new PartitionLoadBalancerCore(controller, this.leaseContainer, this.strategy, leaseAcquireInterval, this.monitor);

            Mock.Get(this.leaseContainer)
                .Setup(m => m.GetAllLeasesAsync())
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            loadBalancer.Start();
            await loadBalancer.StopAsync();

            // Verify that the monitor was notified of the error
            Mock.Get(this.monitor)
                .Verify(m => m.NotifyErrorAsync("PartitionLoadBalancer", It.IsAny<Exception>()), Times.Once);
        }

        private class FailingPartitionController : PartitionController
        {
            public int HitCount { get; private set; }

            public override Task AddOrUpdateLeaseAsync(DocumentServiceLease lease)
            {
                this.HitCount++;
                throw new ArgumentException();
            }

            public override Task InitializeAsync()
            {
                return Task.CompletedTask;
            }

            public override Task ShutdownAsync()
            {
                return Task.CompletedTask;
            }
        }
    }
}