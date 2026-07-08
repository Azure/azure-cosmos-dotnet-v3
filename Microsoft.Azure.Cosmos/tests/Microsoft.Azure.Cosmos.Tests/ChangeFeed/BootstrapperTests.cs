//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Bootstrapping;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
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

        [TestMethod]
        public async Task InitializeAsync_WithCosmosException_ShouldRetryAndSucceed()
        {
            // Arrange — CreateMissingLeasesAsync fails once with a 503 (simulating
            // a regional failure already marked by MetadataRequestThrottleRetryPolicy),
            // then succeeds on the second attempt.
            Mock<PartitionSynchronizer> synchronizer = new Mock<PartitionSynchronizer>();

            CosmosException cosmosException = CosmosExceptionFactory.CreateServiceUnavailableException(
                message: "Service Unavailable",
                headers: new Headers
                {
                    ActivityId = Guid.NewGuid().ToString(),
                    SubStatusCode = SubStatusCodes.TransportGenerated503
                },
                trace: NoOpTrace.Singleton,
                innerException: null);

            synchronizer.SetupSequence(s => s.CreateMissingLeasesAsync())
                .Throws(cosmosException)
                .Returns(Task.CompletedTask);

            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(false);
            leaseStore.Setup(l => l.AcquireInitializationLockAsync(It.IsAny<TimeSpan>())).ReturnsAsync(true);
            leaseStore.Setup(l => l.MarkInitializedAsync()).Returns(Task.CompletedTask);
            leaseStore.Setup(l => l.ReleaseInitializationLockAsync()).ReturnsAsync(true);

            TimeSpan lockTime = TimeSpan.FromSeconds(1);
            TimeSpan sleepTime = TimeSpan.FromMilliseconds(10);

            BootstrapperCore bootstrapper = new BootstrapperCore(
                synchronizer.Object, leaseStore.Object, lockTime, sleepTime);

            // Act.
            await bootstrapper.InitializeAsync();

            // Assert — CreateMissingLeasesAsync was called twice (first failed, second succeeded).
            synchronizer.Verify(s => s.CreateMissingLeasesAsync(), Times.Exactly(2));
            leaseStore.Verify(l => l.MarkInitializedAsync(), Times.Once);
            // Lock acquired and released each iteration.
            leaseStore.Verify(l => l.AcquireInitializationLockAsync(It.IsAny<TimeSpan>()), Times.Exactly(2));
            leaseStore.Verify(l => l.ReleaseInitializationLockAsync(), Times.Exactly(2));
        }

        [TestMethod]
        public async Task InitializeAsync_WithCosmosException_ReleasesLockBeforeRetryDelay()
        {
            // Arrange — CreateMissingLeasesAsync fails once with a 503, then succeeds.
            // Verifies the lock is released BEFORE the retry delay begins (not held for
            // the full sleep duration), so peer CFP instances are not blocked from
            // acquiring the lock while this instance is merely sleeping before its retry.
            Mock<PartitionSynchronizer> synchronizer = new Mock<PartitionSynchronizer>();

            CosmosException cosmosException = CosmosExceptionFactory.CreateServiceUnavailableException(
                message: "Service Unavailable",
                headers: new Headers
                {
                    ActivityId = Guid.NewGuid().ToString(),
                    SubStatusCode = SubStatusCodes.TransportGenerated503
                },
                trace: NoOpTrace.Singleton,
                innerException: null);

            synchronizer.SetupSequence(s => s.CreateMissingLeasesAsync())
                .Throws(cosmosException)
                .Returns(Task.CompletedTask);

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            TimeSpan? releaseElapsed = null;

            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(false);
            leaseStore.Setup(l => l.AcquireInitializationLockAsync(It.IsAny<TimeSpan>())).ReturnsAsync(true);
            leaseStore.Setup(l => l.MarkInitializedAsync()).Returns(Task.CompletedTask);
            leaseStore.Setup(l => l.ReleaseInitializationLockAsync())
                .Callback(() =>
                {
                    // Only capture the first release (the one that precedes the retry delay).
                    releaseElapsed ??= stopwatch.Elapsed;
                })
                .ReturnsAsync(true);

            TimeSpan lockTime = TimeSpan.FromSeconds(1);
            TimeSpan sleepTime = TimeSpan.FromMilliseconds(300);

            BootstrapperCore bootstrapper = new BootstrapperCore(
                synchronizer.Object, leaseStore.Object, lockTime, sleepTime);

            // Act.
            await bootstrapper.InitializeAsync();
            TimeSpan totalElapsed = stopwatch.Elapsed;

            // Assert — the release happened well before the sleep duration elapsed (i.e.
            // before/at the start of the delay, not after it), while the overall call still
            // took at least the full sleep duration.
            Assert.IsTrue(releaseElapsed.HasValue);
            Assert.IsTrue(
                releaseElapsed.Value < sleepTime,
                $"Expected lock release ({releaseElapsed.Value.TotalMilliseconds}ms) to happen before the retry delay ({sleepTime.TotalMilliseconds}ms) elapsed.");
            Assert.IsTrue(
                totalElapsed >= sleepTime,
                $"Expected the overall call ({totalElapsed.TotalMilliseconds}ms) to take at least the sleep duration ({sleepTime.TotalMilliseconds}ms).");
        }

        [TestMethod]
        public async Task InitializeAsync_WithCosmosException_FromIsInitializedAsync_ShouldRetryAndSucceed()
        {
            // Arrange — IsInitializedAsync fails once with a regional 503, then succeeds.
            // Verifies that a regional failure on the "outer" calls (IsInitializedAsync,
            // AcquireInitializationLockAsync) is retried the same way as CreateMissingLeasesAsync,
            // rather than escaping the loop entirely.
            Mock<PartitionSynchronizer> synchronizer = new Mock<PartitionSynchronizer>();
            synchronizer.Setup(s => s.CreateMissingLeasesAsync()).Returns(Task.CompletedTask);

            CosmosException cosmosException = CosmosExceptionFactory.CreateServiceUnavailableException(
                message: "Service Unavailable",
                headers: new Headers
                {
                    ActivityId = Guid.NewGuid().ToString(),
                    SubStatusCode = SubStatusCodes.TransportGenerated503
                },
                trace: NoOpTrace.Singleton,
                innerException: null);

            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.SetupSequence(l => l.IsInitializedAsync())
                .Throws(cosmosException)
                .ReturnsAsync(false);
            leaseStore.Setup(l => l.AcquireInitializationLockAsync(It.IsAny<TimeSpan>())).ReturnsAsync(true);
            leaseStore.Setup(l => l.MarkInitializedAsync()).Returns(Task.CompletedTask);
            leaseStore.Setup(l => l.ReleaseInitializationLockAsync()).ReturnsAsync(true);

            TimeSpan lockTime = TimeSpan.FromSeconds(1);
            TimeSpan sleepTime = TimeSpan.FromMilliseconds(10);

            BootstrapperCore bootstrapper = new BootstrapperCore(
                synchronizer.Object, leaseStore.Object, lockTime, sleepTime);

            // Act.
            await bootstrapper.InitializeAsync();

            // Assert.
            leaseStore.Verify(l => l.IsInitializedAsync(), Times.Exactly(2));
            leaseStore.Verify(l => l.MarkInitializedAsync(), Times.Once);
            // No lock should have been acquired/released on the failed first attempt.
            leaseStore.Verify(l => l.AcquireInitializationLockAsync(It.IsAny<TimeSpan>()), Times.Once);
            leaseStore.Verify(l => l.ReleaseInitializationLockAsync(), Times.Once);
        }

        [TestMethod]
        public async Task InitializeAsync_WithHttpRequestException_ShouldRetryAndSucceed()
        {
            // Arrange — CreateMissingLeasesAsync fails once with HttpRequestException,
            // then succeeds on the second attempt.
            Mock<PartitionSynchronizer> synchronizer = new Mock<PartitionSynchronizer>();
            synchronizer.SetupSequence(s => s.CreateMissingLeasesAsync())
                .Throws(new HttpRequestException("Connection refused"))
                .Returns(Task.CompletedTask);

            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(false);
            leaseStore.Setup(l => l.AcquireInitializationLockAsync(It.IsAny<TimeSpan>())).ReturnsAsync(true);
            leaseStore.Setup(l => l.MarkInitializedAsync()).Returns(Task.CompletedTask);
            leaseStore.Setup(l => l.ReleaseInitializationLockAsync()).ReturnsAsync(true);

            TimeSpan lockTime = TimeSpan.FromSeconds(1);
            TimeSpan sleepTime = TimeSpan.FromMilliseconds(10);

            BootstrapperCore bootstrapper = new BootstrapperCore(
                synchronizer.Object, leaseStore.Object, lockTime, sleepTime);

            // Act.
            await bootstrapper.InitializeAsync();

            // Assert.
            synchronizer.Verify(s => s.CreateMissingLeasesAsync(), Times.Exactly(2));
            leaseStore.Verify(l => l.MarkInitializedAsync(), Times.Once);
        }

        [TestMethod]
        public async Task InitializeAsync_WithCosmosException_ShouldThrowAfterMaxRetries()
        {
            // Arrange — CreateMissingLeasesAsync always fails.
            Mock<PartitionSynchronizer> synchronizer = new Mock<PartitionSynchronizer>();

            CosmosException cosmosException = CosmosExceptionFactory.CreateServiceUnavailableException(
                message: "Service Unavailable",
                headers: new Headers
                {
                    ActivityId = Guid.NewGuid().ToString(),
                    SubStatusCode = SubStatusCodes.TransportGenerated503
                },
                trace: NoOpTrace.Singleton,
                innerException: null);

            synchronizer.Setup(s => s.CreateMissingLeasesAsync()).Throws(cosmosException);

            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(false);
            leaseStore.Setup(l => l.AcquireInitializationLockAsync(It.IsAny<TimeSpan>())).ReturnsAsync(true);
            leaseStore.Setup(l => l.MarkInitializedAsync()).Returns(Task.CompletedTask);
            leaseStore.Setup(l => l.ReleaseInitializationLockAsync()).ReturnsAsync(true);

            TimeSpan lockTime = TimeSpan.FromSeconds(1);
            TimeSpan sleepTime = TimeSpan.FromMilliseconds(10);

            BootstrapperCore bootstrapper = new BootstrapperCore(
                synchronizer.Object, leaseStore.Object, lockTime, sleepTime);

            // Act & Assert — should throw after exhausting MaxInitializationRetries.
            CosmosException thrown = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => bootstrapper.InitializeAsync());

            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, thrown.StatusCode);

            // 1 original attempt + MaxInitializationRetries retries = total calls.
            synchronizer.Verify(
                s => s.CreateMissingLeasesAsync(),
                Times.Exactly(BootstrapperCore.MaxInitializationRetries + 1));

            leaseStore.Verify(l => l.MarkInitializedAsync(), Times.Never);
        }

        [TestMethod]
        public async Task InitializeAsync_WithHttpRequestException_ShouldThrowAfterMaxRetries()
        {
            // Arrange — CreateMissingLeasesAsync always fails with HttpRequestException.
            Mock<PartitionSynchronizer> synchronizer = new Mock<PartitionSynchronizer>();
            synchronizer.Setup(s => s.CreateMissingLeasesAsync())
                .Throws(new HttpRequestException("Connection refused"));

            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(false);
            leaseStore.Setup(l => l.AcquireInitializationLockAsync(It.IsAny<TimeSpan>())).ReturnsAsync(true);
            leaseStore.Setup(l => l.MarkInitializedAsync()).Returns(Task.CompletedTask);
            leaseStore.Setup(l => l.ReleaseInitializationLockAsync()).ReturnsAsync(true);

            TimeSpan lockTime = TimeSpan.FromSeconds(1);
            TimeSpan sleepTime = TimeSpan.FromMilliseconds(10);

            BootstrapperCore bootstrapper = new BootstrapperCore(
                synchronizer.Object, leaseStore.Object, lockTime, sleepTime);

            // Act & Assert — should throw after exhausting MaxInitializationRetries.
            await Assert.ThrowsExceptionAsync<HttpRequestException>(
                () => bootstrapper.InitializeAsync());

            // 1 original attempt + MaxInitializationRetries retries = total calls.
            synchronizer.Verify(
                s => s.CreateMissingLeasesAsync(),
                Times.Exactly(BootstrapperCore.MaxInitializationRetries + 1));

            leaseStore.Verify(l => l.MarkInitializedAsync(), Times.Never);
        }

        [TestMethod]
        public async Task InitializeAsync_WithNonRetryableCosmosException_ShouldThrowImmediately()
        {
            // Arrange — 404 NotFound is not a regional failure and should NOT be retried.
            Mock<PartitionSynchronizer> synchronizer = new Mock<PartitionSynchronizer>();

            CosmosException notFoundException = CosmosExceptionFactory.CreateNotFoundException(
                message: "Container not found",
                headers: new Headers
                {
                    ActivityId = Guid.NewGuid().ToString(),
                },
                trace: NoOpTrace.Singleton);

            synchronizer.Setup(s => s.CreateMissingLeasesAsync()).Throws(notFoundException);

            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(false);
            leaseStore.Setup(l => l.AcquireInitializationLockAsync(It.IsAny<TimeSpan>())).ReturnsAsync(true);
            leaseStore.Setup(l => l.MarkInitializedAsync()).Returns(Task.CompletedTask);
            leaseStore.Setup(l => l.ReleaseInitializationLockAsync()).ReturnsAsync(true);

            TimeSpan lockTime = TimeSpan.FromSeconds(1);
            TimeSpan sleepTime = TimeSpan.FromMilliseconds(10);

            BootstrapperCore bootstrapper = new BootstrapperCore(
                synchronizer.Object, leaseStore.Object, lockTime, sleepTime);

            // Act & Assert — should throw immediately without retrying.
            CosmosException thrown = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => bootstrapper.InitializeAsync());

            Assert.AreEqual(HttpStatusCode.NotFound, thrown.StatusCode);

            // Only 1 attempt — no retries for non-regional errors.
            synchronizer.Verify(s => s.CreateMissingLeasesAsync(), Times.Once);
            leaseStore.Verify(l => l.MarkInitializedAsync(), Times.Never);
        }
    }
}