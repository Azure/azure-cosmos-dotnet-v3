//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Bootstrapping;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class BootstrapperTests
    {
        [TestMethod]
        public void ValidatesArguments()
        {
            Mock<PartitionSynchronizer> synchronizer = new Mock<PartitionSynchronizer>();

            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(true);
            TimeSpan lockTime = TimeSpan.FromSeconds(30);
            TimeSpan sleepTime = TimeSpan.FromSeconds(30);

            Assert.ThrowsException<ArgumentNullException>(() => new BootstrapperCore(null, leaseStore.Object, lockTime, sleepTime, () => Task.CompletedTask));
            Assert.ThrowsException<ArgumentNullException>(() => new BootstrapperCore(synchronizer.Object, null, lockTime, sleepTime, () => Task.CompletedTask));
            Assert.ThrowsException<ArgumentException>(() => new BootstrapperCore(synchronizer.Object, leaseStore.Object, TimeSpan.Zero, sleepTime, () => Task.CompletedTask));
            Assert.ThrowsException<ArgumentException>(() => new BootstrapperCore(synchronizer.Object, leaseStore.Object, lockTime, TimeSpan.Zero, () => Task.CompletedTask));
            Assert.ThrowsException<ArgumentNullException>(() => new BootstrapperCore(synchronizer.Object, leaseStore.Object, lockTime, sleepTime, null));

        }

        [TestMethod]
        public async Task InitializeAsyncOrderIsCorrect()
        {
            List<string> operations = new List<string>();
            Mock<PartitionSynchronizer> synchronizer = new Mock<PartitionSynchronizer>();
            synchronizer.Setup(s => s.CreateMissingLeasesAsync())
                .Callback(() => operations.Add("create")).Returns(Task.CompletedTask);
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(false);
            leaseStore.Setup(l => l.AcquireInitializationLockAsync(It.IsAny<TimeSpan>())).ReturnsAsync(true);
            leaseStore.Setup(l => l.MarkInitializedAsync())
                .Callback(() => operations.Add("mark")).Returns(Task.CompletedTask);
            leaseStore.Setup(l => l.ReleaseInitializationLockAsync())
                .Callback(() => operations.Add("release")).ReturnsAsync(true);
            TimeSpan lockTime = TimeSpan.FromSeconds(30);
            TimeSpan sleepTime = TimeSpan.FromSeconds(30);

            BootstrapperCore bootstrapper = new BootstrapperCore(synchronizer.Object, leaseStore.Object, lockTime, sleepTime, () =>
            {
                operations.Add("validate");
                return Task.CompletedTask;
            });

            await bootstrapper.InitializeAsync();
            Mock.Get(leaseStore.Object)
                .Verify(store => store.IsInitializedAsync(), Times.Once);
            Mock.Get(leaseStore.Object)
                .Verify(store => store.AcquireInitializationLockAsync(It.IsAny<TimeSpan>()), Times.Once);
            Mock.Get(leaseStore.Object)
                .Verify(store => store.MarkInitializedAsync(), Times.Once);
            Mock.Get(synchronizer.Object)
                .Verify(store => store.CreateMissingLeasesAsync(), Times.Once);
            Mock.Get(leaseStore.Object)
                .Verify(store => store.ReleaseInitializationLockAsync(), Times.Once);
            CollectionAssert.AreEqual(new[] { "validate", "create", "validate", "mark", "release" }, operations);
        }

        [TestMethod]
        public async Task IfInitializedDoNotRunAnythingElse()
        {
            Mock<PartitionSynchronizer> synchronizer = new Mock<PartitionSynchronizer>();
            synchronizer.Setup(s => s.CreateMissingLeasesAsync()).Returns(Task.CompletedTask);
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(true);
            leaseStore.Setup(l => l.AcquireInitializationLockAsync(It.IsAny<TimeSpan>())).ReturnsAsync(true);
            leaseStore.Setup(l => l.MarkInitializedAsync()).Returns(Task.CompletedTask);
            leaseStore.Setup(l => l.ReleaseInitializationLockAsync()).ReturnsAsync(true);
            TimeSpan lockTime = TimeSpan.FromSeconds(30);
            TimeSpan sleepTime = TimeSpan.FromSeconds(30);

            BootstrapperCore bootstrapper = new BootstrapperCore(synchronizer.Object, leaseStore.Object, lockTime, sleepTime, () => Task.CompletedTask);

            await bootstrapper.InitializeAsync();
            Mock.Get(leaseStore.Object)
                .Verify(store => store.IsInitializedAsync(), Times.Once);
            Mock.Get(leaseStore.Object)
                .Verify(store => store.AcquireInitializationLockAsync(It.IsAny<TimeSpan>()), Times.Never);
            Mock.Get(leaseStore.Object)
                .Verify(store => store.MarkInitializedAsync(), Times.Never);
            Mock.Get(synchronizer.Object)
                .Verify(store => store.CreateMissingLeasesAsync(), Times.Never);
            Mock.Get(leaseStore.Object)
                .Verify(store => store.ReleaseInitializationLockAsync(), Times.Never);
        }

        [TestMethod]
        public async Task IfCannotAcquireRetry()
        {
            Mock<PartitionSynchronizer> synchronizer = new Mock<PartitionSynchronizer>();
            synchronizer.Setup(s => s.CreateMissingLeasesAsync()).Returns(Task.CompletedTask);
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(false);
            leaseStore.SetupSequence(l => l.AcquireInitializationLockAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(false)
                .ReturnsAsync(true);
            leaseStore.Setup(l => l.MarkInitializedAsync()).Returns(Task.CompletedTask);
            leaseStore.Setup(l => l.ReleaseInitializationLockAsync()).ReturnsAsync(true);
            TimeSpan lockTime = TimeSpan.FromSeconds(1);
            TimeSpan sleepTime = TimeSpan.FromSeconds(1);

            BootstrapperCore bootstrapper = new BootstrapperCore(synchronizer.Object, leaseStore.Object, lockTime, sleepTime, () => Task.CompletedTask);

            await bootstrapper.InitializeAsync();
            Mock.Get(leaseStore.Object)
                .Verify(store => store.IsInitializedAsync(), Times.Exactly(2));
            Mock.Get(leaseStore.Object)
                .Verify(store => store.AcquireInitializationLockAsync(It.IsAny<TimeSpan>()), Times.Exactly(2));
            Mock.Get(leaseStore.Object)
                .Verify(store => store.MarkInitializedAsync(), Times.Once);
            Mock.Get(synchronizer.Object)
                .Verify(store => store.CreateMissingLeasesAsync(), Times.Once);
            Mock.Get(leaseStore.Object)
                .Verify(store => store.ReleaseInitializationLockAsync(), Times.Once);
        }

        [DataTestMethod]
        [DataRow("initialized", false)]
        [DataRow("initialized", true)]
        [DataRow("locked", false)]
        [DataRow("locked", true)]
        [DataRow("created", false)]
        [DataRow("created", true)]
        [DataRow("lost-lock", false)]
        [DataRow("lost-lock", true)]
        public async Task ValidatesRacingLeasesBeforeStartingWorkers(string stage, bool compatible)
        {
            const string expectedHash = "configured-query";
            DocumentServiceLease lease = new DocumentServiceLeaseCore { QueryFilterHash = expectedHash };
            Mock<DocumentServiceLeaseManager> leaseManager = new Mock<DocumentServiceLeaseManager>();
            leaseManager.Object.ConfigureQueryFilter(expectedHash);
            Action publishLease = () => lease.QueryFilterHash = compatible ? expectedHash : "other-query";
            if (stage == "initialized")
            {
                publishLease();
            }

            int initializationReads = 0;
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(s => s.IsInitializedAsync()).ReturnsAsync(
                () => stage == "initialized" || (stage == "lost-lock" && initializationReads++ > 0));
            leaseStore.Setup(s => s.AcquireInitializationLockAsync(It.IsAny<TimeSpan>()))
                .Callback(() =>
                {
                    if (stage == "locked" || stage == "lost-lock")
                    {
                        publishLease();
                    }
                })
                .ReturnsAsync(stage != "lost-lock");
            leaseStore.Setup(s => s.MarkInitializedAsync()).Returns(Task.CompletedTask);
            leaseStore.Setup(s => s.ReleaseInitializationLockAsync()).ReturnsAsync(true);
            Mock<PartitionSynchronizer> synchronizer = new Mock<PartitionSynchronizer>();
            synchronizer.Setup(s => s.CreateMissingLeasesAsync())
                .Callback(() =>
                {
                    if (stage == "created")
                    {
                        publishLease();
                    }
                })
                .Returns(Task.CompletedTask);
            int validations = 0;
            BootstrapperCore bootstrapper = new BootstrapperCore(
                synchronizer.Object, leaseStore.Object, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(1),
                () =>
                {
                    validations++;
                    leaseManager.Object.VerifyQueryFilter(lease);
                    return Task.CompletedTask;
                });
            Mock<PartitionController> controller = new Mock<PartitionController>();
            Mock<PartitionLoadBalancer> balancer = new Mock<PartitionLoadBalancer>();
            PartitionManagerCore partitionManager = new PartitionManagerCore(bootstrapper, controller.Object, balancer.Object);

            if (compatible)
            {
                await partitionManager.StartAsync();
            }
            else
            {
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => partitionManager.StartAsync());
            }

            bool acquiredLock = stage == "locked" || stage == "created";
            bool createdLeases = stage == "created" || (stage == "locked" && compatible);
            Assert.AreEqual(createdLeases ? 2 : 1, validations);
            synchronizer.Verify(s => s.CreateMissingLeasesAsync(), createdLeases ? Times.Once() : Times.Never());
            leaseStore.Verify(s => s.MarkInitializedAsync(), acquiredLock && compatible ? Times.Once() : Times.Never());
            leaseStore.Verify(s => s.ReleaseInitializationLockAsync(), acquiredLock ? Times.Once() : Times.Never());
            controller.Verify(c => c.InitializeAsync(), compatible ? Times.Once() : Times.Never());
            balancer.Verify(b => b.Start(), compatible ? Times.Once() : Times.Never());
        }
    }
}