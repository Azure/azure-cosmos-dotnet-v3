using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.ChangeFeed.Bootstrapping;
using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    [TestClass]
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
            List<string> expectedOrderOfEvents = new List<string>()
            {
                "IsInitializedAsync",
                "AcquireInitializationLockAsync",
                "CreateMissingLeasesAsync",
                "MarkInitializedAsync",
                "ReleaseInitializationLockAsync"
            };

            List<string> events = new List<string>();

            Mock<PartitionSynchronizer> synchronizer = new Mock<PartitionSynchronizer>();
            synchronizer.Setup(s => s.CreateMissingLeasesAsync()).Returns(() =>
            {
                events.Add("CreateMissingLeasesAsync");
                return Task.CompletedTask;
            });
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(() =>
            {
                events.Add("IsInitializedAsync");
                return false;
            });
            leaseStore.Setup(l => l.AcquireInitializationLockAsync(It.IsAny<TimeSpan>())).ReturnsAsync(() =>
            {
                events.Add("AcquireInitializationLockAsync");
                return true;
            });
            leaseStore.Setup(l => l.MarkInitializedAsync()).Returns(() =>
            {
                events.Add("MarkInitializedAsync");
                return Task.CompletedTask;
            });
            leaseStore.Setup(l => l.ReleaseInitializationLockAsync()).ReturnsAsync(() =>
            {
                events.Add("ReleaseInitializationLockAsync");
                return true;
            });
            TimeSpan lockTime = TimeSpan.FromSeconds(30);
            TimeSpan sleepTime = TimeSpan.FromSeconds(30);

            BootstrapperCore bootstrapper = new BootstrapperCore(synchronizer.Object, leaseStore.Object, lockTime, sleepTime);

            await bootstrapper.InitializeAsync();
            CollectionAssert.AreEqual(expectedOrderOfEvents, events);
        }

        [TestMethod]
        public async Task IfInitializedDoNotRunAnythingElse()
        {
            List<string> expectedOrderOfEvents = new List<string>()
            {
                "IsInitializedAsync"
            };

            List<string> events = new List<string>();

            Mock<PartitionSynchronizer> synchronizer = new Mock<PartitionSynchronizer>();
            synchronizer.Setup(s => s.CreateMissingLeasesAsync()).Returns(() =>
            {
                events.Add("CreateMissingLeasesAsync");
                return Task.CompletedTask;
            });
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(() =>
            {
                events.Add("IsInitializedAsync");
                return true;
            });
            leaseStore.Setup(l => l.AcquireInitializationLockAsync(It.IsAny<TimeSpan>())).ReturnsAsync(() =>
            {
                events.Add("AcquireInitializationLockAsync");
                return true;
            });
            leaseStore.Setup(l => l.MarkInitializedAsync()).Returns(() =>
            {
                events.Add("MarkInitializedAsync");
                return Task.CompletedTask;
            });
            leaseStore.Setup(l => l.ReleaseInitializationLockAsync()).ReturnsAsync(() =>
            {
                events.Add("ReleaseInitializationLockAsync");
                return true;
            });
            TimeSpan lockTime = TimeSpan.FromSeconds(30);
            TimeSpan sleepTime = TimeSpan.FromSeconds(30);

            BootstrapperCore bootstrapper = new BootstrapperCore(synchronizer.Object, leaseStore.Object, lockTime, sleepTime);

            await bootstrapper.InitializeAsync();
            CollectionAssert.AreEqual(expectedOrderOfEvents, events);
        }

        [TestMethod]
        public async Task IfCannotAcquireRetry()
        {
            // Includes expected retry
            List<string> expectedOrderOfEvents = new List<string>()
            {
                "IsInitializedAsync",
                "AcquireInitializationLockAsync",
                "IsInitializedAsync",
                "AcquireInitializationLockAsync",
                "CreateMissingLeasesAsync",
                "MarkInitializedAsync",
                "ReleaseInitializationLockAsync"
            };

            int iteration = 1;

            List<string> events = new List<string>();

            Mock<PartitionSynchronizer> synchronizer = new Mock<PartitionSynchronizer>();
            synchronizer.Setup(s => s.CreateMissingLeasesAsync()).Returns(() =>
            {
                events.Add("CreateMissingLeasesAsync");
                return Task.CompletedTask;
            });
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(() =>
            {
                events.Add("IsInitializedAsync");
                return false;
            });
            leaseStore.Setup(l => l.AcquireInitializationLockAsync(It.IsAny<TimeSpan>())).ReturnsAsync(() =>
            {
                events.Add("AcquireInitializationLockAsync");
                // Only acquire on retry
                return (iteration++ > 1);
            });
            leaseStore.Setup(l => l.MarkInitializedAsync()).Returns(() =>
            {
                events.Add("MarkInitializedAsync");
                return Task.CompletedTask;
            });
            leaseStore.Setup(l => l.ReleaseInitializationLockAsync()).ReturnsAsync(() =>
            {
                events.Add("ReleaseInitializationLockAsync");
                return true;
            });
            TimeSpan lockTime = TimeSpan.FromSeconds(1);
            TimeSpan sleepTime = TimeSpan.FromSeconds(1);

            BootstrapperCore bootstrapper = new BootstrapperCore(synchronizer.Object, leaseStore.Object, lockTime, sleepTime);

            await bootstrapper.InitializeAsync();
            CollectionAssert.AreEqual(expectedOrderOfEvents, events);
        }
    }
}
