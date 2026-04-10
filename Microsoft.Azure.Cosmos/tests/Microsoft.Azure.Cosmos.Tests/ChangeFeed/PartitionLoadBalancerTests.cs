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

        [TestMethod]
        public async Task AddLease_ThrowsException_LeaseAddingContinues()
        {
            FailingPartitionController controller = new FailingPartitionController();

            // long acquire interval to ensure that only 1 load balancing iteration is performed in a test run
            TimeSpan leaseAcquireInterval = TimeSpan.FromHours(1);
            PartitionLoadBalancerCore loadBalancer = new PartitionLoadBalancerCore(controller, this.leaseContainer, this.strategy, leaseAcquireInterval);

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