//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Monitoring;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ChangeFeedProcessorHealthMonitorCoreTests
    {
        [TestMethod]
        public async Task Delegates_CallsAcquire()
        {
            string token = Guid.NewGuid().ToString();
            bool called = false;
            ChangeFeedProcessorHealthMonitorCore monitor = new ChangeFeedProcessorHealthMonitorCore();
            monitor.SetLeaseAcquireDelegate((string leaseToken) =>
            {
                called = true;
                Assert.AreEqual(token, leaseToken);
                return Task.CompletedTask;
            });

            await monitor.NotifyLeaseAcquireAsync(token);

            Assert.IsTrue(called);
        }

        [TestMethod]
        public async Task Delegates_CallsAcquire_OnFailure()
        {
            string token = Guid.NewGuid().ToString();
            bool called = false;
            ChangeFeedProcessorHealthMonitorCore monitor = new ChangeFeedProcessorHealthMonitorCore();
            monitor.SetLeaseAcquireDelegate((string leaseToken) =>
            {
                called = true;
                Assert.AreEqual(token, leaseToken);
                throw new Exception("Should not fail process");
            });

            await monitor.NotifyLeaseAcquireAsync(token);

            Assert.IsTrue(called);
        }

        [TestMethod]
        public async Task Delegates_CallsRelease()
        {
            string token = Guid.NewGuid().ToString();
            bool called = false;
            ChangeFeedProcessorHealthMonitorCore monitor = new ChangeFeedProcessorHealthMonitorCore();
            monitor.SetLeaseReleaseDelegate((string leaseToken) =>
            {
                called = true;
                Assert.AreEqual(token, leaseToken);
                return Task.CompletedTask;
            });

            await monitor.NotifyLeaseReleaseAsync(token);

            Assert.IsTrue(called);
        }

        [TestMethod]
        public async Task Delegates_CallsRelease_OnFailure()
        {
            string token = Guid.NewGuid().ToString();
            bool called = false;
            ChangeFeedProcessorHealthMonitorCore monitor = new ChangeFeedProcessorHealthMonitorCore();
            monitor.SetLeaseReleaseDelegate((string leaseToken) =>
            {
                called = true;
                Assert.AreEqual(token, leaseToken);
                throw new Exception("Should not fail process");
            });

            await monitor.NotifyLeaseReleaseAsync(token);

            Assert.IsTrue(called);
        }

        [TestMethod]
        public async Task Delegates_CallsError()
        {
            Exception ex = new Exception();
            string token = Guid.NewGuid().ToString();
            bool called = false;
            ChangeFeedProcessorHealthMonitorCore monitor = new ChangeFeedProcessorHealthMonitorCore();
            monitor.SetErrorDelegate((string leaseToken, Exception exception) =>
            {
                called = true;
                Assert.AreEqual(token, leaseToken);
                Assert.ReferenceEquals(ex, exception);
                return Task.CompletedTask;
            });

            await monitor.NotifyErrorAsync(token, ex);

            Assert.IsTrue(called);
        }

        [TestMethod]
        public async Task Delegates_CallsError_OnFailure()
        {
            Exception ex = new Exception();
            string token = Guid.NewGuid().ToString();
            bool called = false;
            ChangeFeedProcessorHealthMonitorCore monitor = new ChangeFeedProcessorHealthMonitorCore();
            monitor.SetErrorDelegate((string leaseToken, Exception exception) =>
            {
                called = true;
                Assert.AreEqual(token, leaseToken);
                Assert.ReferenceEquals(ex, exception);
                throw new Exception("should not fail process");
            });

            await monitor.NotifyErrorAsync(token, ex);

            Assert.IsTrue(called);
        }
    }
}