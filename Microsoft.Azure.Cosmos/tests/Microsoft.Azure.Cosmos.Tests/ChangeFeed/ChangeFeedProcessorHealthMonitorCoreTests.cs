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

        [TestMethod]
        public async Task Error_DefaultNotification_NoDelegate_DoesNotThrow()
        {
            string token = Guid.NewGuid().ToString();
            ChangeFeedProcessorHealthMonitorCore monitor = new ChangeFeedProcessorHealthMonitorCore();

            // No error delegate is registered. The always-on default notification must still run without throwing.
            await monitor.NotifyErrorAsync(token, new Exception("transient failure"));
        }

        [TestMethod]
        public async Task Error_PoisonMessage_NoDelegate_DoesNotThrow()
        {
            string token = Guid.NewGuid().ToString();
            ChangeFeedProcessorHealthMonitorCore monitor = new ChangeFeedProcessorHealthMonitorCore();

            ChangeFeedProcessorUserException poisonException = new ChangeFeedProcessorUserException(
                new Newtonsoft.Json.JsonReaderException("bad payload"),
                new FakeChangeFeedProcessorContext(token));

            // The default notification must surface poison-message failures even with no delegate registered.
            await monitor.NotifyErrorAsync(token, poisonException);
        }

        [TestMethod]
        public async Task Error_PoisonMessage_DefaultRunsAndDelegateStillInvoked()
        {
            string token = Guid.NewGuid().ToString();
            int callCount = 0;
            ChangeFeedProcessorHealthMonitorCore monitor = new ChangeFeedProcessorHealthMonitorCore();
            monitor.SetErrorDelegate((string leaseToken, Exception exception) =>
            {
                callCount++;
                Assert.AreEqual(token, leaseToken);
                Assert.IsInstanceOfType(exception, typeof(ChangeFeedProcessorUserException));
                return Task.CompletedTask;
            });

            ChangeFeedProcessorUserException poisonException = new ChangeFeedProcessorUserException(
                new Newtonsoft.Json.JsonReaderException("bad payload"),
                new FakeChangeFeedProcessorContext(token));

            await monitor.NotifyErrorAsync(token, poisonException);

            // The customer delegate remains additive: it is still invoked after the default notification runs.
            Assert.AreEqual(1, callCount);
        }

        [TestMethod]
        public async Task Error_StuckLease_RepeatedSameError_DoesNotThrowAndKeepsInvokingDelegate()
        {
            string token = Guid.NewGuid().ToString();
            int callCount = 0;
            ChangeFeedProcessorHealthMonitorCore monitor = new ChangeFeedProcessorHealthMonitorCore();
            monitor.SetErrorDelegate((string leaseToken, Exception exception) =>
            {
                callCount++;
                return Task.CompletedTask;
            });

            // Simulate a poison-message loop: the same error signature repeats well past the stuck-lease threshold.
            // The monitor must escalate internally without throwing and must keep invoking the delegate each cycle.
            for (int i = 0; i < 10; i++)
            {
                ChangeFeedProcessorUserException poisonException = new ChangeFeedProcessorUserException(
                    new Newtonsoft.Json.JsonReaderException("bad payload"),
                    new FakeChangeFeedProcessorContext(token));

                await monitor.NotifyErrorAsync(token, poisonException);
            }

            Assert.AreEqual(10, callCount);
        }

        private sealed class FakeChangeFeedProcessorContext : ChangeFeedProcessorContext
        {
            private readonly string leaseToken;

            public FakeChangeFeedProcessorContext(string leaseToken)
            {
                this.leaseToken = leaseToken;
            }

            public override string LeaseToken => this.leaseToken;

            public override CosmosDiagnostics Diagnostics => null;

            public override Headers Headers => null;

            public override FeedRange FeedRange => null;
        }
    }
}