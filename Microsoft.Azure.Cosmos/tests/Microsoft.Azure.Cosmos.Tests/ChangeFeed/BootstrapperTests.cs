//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
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

            Assert.ThrowsException<ArgumentNullException>(() => new BootstrapperCore(null, leaseStore.Object, lockTime, sleepTime));
            Assert.ThrowsException<ArgumentNullException>(() => new BootstrapperCore(synchronizer.Object, null, lockTime, sleepTime));
            Assert.ThrowsException<ArgumentException>(() => new BootstrapperCore(synchronizer.Object, leaseStore.Object, TimeSpan.Zero, sleepTime));
            Assert.ThrowsException<ArgumentException>(() => new BootstrapperCore(synchronizer.Object, leaseStore.Object, lockTime, TimeSpan.Zero));

        }

        [TestMethod]
        public async Task InitializeAsyncOrderIsCorrect()
        {
            Mock<PartitionSynchronizer> synchronizer = new Mock<PartitionSynchronizer>();
            synchronizer.Setup(s => s.CreateMissingLeasesAsync()).Returns(Task.CompletedTask);
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(false);
            leaseStore.Setup(l => l.AcquireInitializationLockAsync(It.IsAny<TimeSpan>())).ReturnsAsync(true);
            leaseStore.Setup(l => l.MarkInitializedAsync()).Returns(Task.CompletedTask);
            leaseStore.Setup(l => l.ReleaseInitializationLockAsync()).ReturnsAsync(true);
            TimeSpan lockTime = TimeSpan.FromSeconds(30);
            TimeSpan sleepTime = TimeSpan.FromSeconds(30);

            BootstrapperCore bootstrapper = new BootstrapperCore(synchronizer.Object, leaseStore.Object, lockTime, sleepTime);

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

            BootstrapperCore bootstrapper = new BootstrapperCore(synchronizer.Object, leaseStore.Object, lockTime, sleepTime);

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

            BootstrapperCore bootstrapper = new BootstrapperCore(synchronizer.Object, leaseStore.Object, lockTime, sleepTime);

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
    }
}