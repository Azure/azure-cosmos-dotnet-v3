//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    /// <summary>
    /// Locks in the contract of <see cref="EncryptionCustomEventSource"/>: best-effort
    /// distributed-cache failures emit Release-visible warnings on the
    /// <c>Azure-Cosmos-Encryption-Custom</c> EventSource so subscribers like
    /// <c>dotnet-trace</c> / <c>AzureEventSourceListener</c> can observe them even when
    /// no <see cref="System.Diagnostics.ActivityListener"/> is attached.
    /// </summary>
    [TestClass]
    public class EncryptionCustomEventSourceTests
    {
        private const string EventSourceName = "Azure-Cosmos-Encryption-Custom";

        // Event IDs as declared in EncryptionCustomEventSource.
        private const int DistributedCacheReadFailedEventId = 1;
        private const int DistributedCacheWriteFailedEventId = 2;
        private const int DistributedCacheBackgroundWriteFailedEventId = 3;
        private const int DistributedCacheRemoveFailedEventId = 4;
        private const int DistributedCacheIdMismatchEventId = 5;

        private static DataEncryptionKeyProperties NewDek(string id)
        {
            return new DataEncryptionKeyProperties(
                id,
                "AEAD_AES_256_CBC_HMAC_SHA256",
                new byte[] { 1, 2, 3 },
                new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                DateTime.UtcNow);
        }

        [TestMethod]
        public async Task BackgroundWriteFailure_EmitsWarning()
        {
            using CapturingEventListener listener = new (EventSourceName, EventLevel.Warning);

            Mock<IDistributedCache> mockCache = new ();
            mockCache
                .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("synthetic L2 SetAsync failure"));

            DekCache cache = new (
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: mockCache.Object,
                cacheKeyPrefix: "evtest");

            cache.SetDekProperties("dek1", NewDek("dek1"));
            await cache.WhenAllPendingWritesAsync();

            EventWrittenEventArgs evt = listener.WaitForEvent(DistributedCacheBackgroundWriteFailedEventId);
            Assert.IsNotNull(evt, "Expected DistributedCacheBackgroundWriteFailed event to be raised on the EventSource.");
            Assert.AreEqual(EventLevel.Warning, evt.Level);
            AssertPayloadContains(evt, "dek1", typeof(InvalidOperationException).FullName);
            Assert.AreEqual("other", evt.Payload[2] as string, "Third payload element should be the stable exception category.");
        }

        [TestMethod]
        public async Task ReadFailure_EmitsWarning()
        {
            using CapturingEventListener listener = new (EventSourceName, EventLevel.Warning);

            Mock<IDistributedCache> mockCache = new ();
            mockCache
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("synthetic L2 GetAsync failure"));
            mockCache
                .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            DekCache cache = new (
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: mockCache.Object,
                cacheKeyPrefix: "evtest");

            // Cold-miss path forces a TryGetFromDistributedCacheAsync call which throws the synthetic exception.
            await cache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) => Task.FromResult(NewDek(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            EventWrittenEventArgs evt = listener.WaitForEvent(DistributedCacheReadFailedEventId);
            Assert.IsNotNull(evt, "Expected DistributedCacheReadFailed event to be raised on the EventSource.");
            Assert.AreEqual(EventLevel.Warning, evt.Level);
            AssertPayloadContains(evt, "dek1", typeof(InvalidOperationException).FullName);
            Assert.AreEqual("other", evt.Payload[2] as string, "Third payload element should be the stable exception category.");
        }

        [TestMethod]
        public async Task RemoveFailure_EmitsWarning()
        {
            using CapturingEventListener listener = new (EventSourceName, EventLevel.Warning);

            Mock<IDistributedCache> mockCache = new ();
            mockCache
                .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("synthetic L2 RemoveAsync failure"));

            DekCache cache = new (
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: mockCache.Object,
                cacheKeyPrefix: "evtest");

            await cache.RemoveAsync("dek1");

            EventWrittenEventArgs evt = listener.WaitForEvent(DistributedCacheRemoveFailedEventId);
            Assert.IsNotNull(evt, "Expected DistributedCacheRemoveFailed event to be raised on the EventSource.");
            Assert.AreEqual(EventLevel.Warning, evt.Level);
            AssertPayloadContains(evt, "dek1", typeof(InvalidOperationException).FullName);
            Assert.AreEqual("other", evt.Payload[2] as string, "Third payload element should be the stable exception category.");
        }

        [TestMethod]
        public void NullExceptionArgument_DoesNotThrowAndDoesNotEmit()
        {
            using CapturingEventListener listener = new (EventSourceName, EventLevel.Warning);

            // Defensive: helper signatures accept Exception; passing null must not throw and must not
            // raise an event (otherwise we'd advertise an empty payload to subscribers).
            EncryptionCustomEventSource.DistributedCacheReadFailed("dek1", null);
            EncryptionCustomEventSource.DistributedCacheWriteFailed("dek1", null);
            EncryptionCustomEventSource.DistributedCacheBackgroundWriteFailed("dek1", null);
            EncryptionCustomEventSource.DistributedCacheRemoveFailed("dek1", null);

            Assert.AreEqual(0, listener.EventCount, "No events should fire when exception is null.");
        }

        [TestMethod]
        // REQ: Distributed cache Id-mismatch warnings must emit the requested dekId and observed payload Id verbatim.
        // SOURCE: PR #5428 review comment 3255385237.
        public void IdMismatch_EmitsWarningWithRequestedAndObservedIds()
        {
            using CapturingEventListener listener = new (EventSourceName, EventLevel.Warning);

            EncryptionCustomEventSource.DistributedCacheIdMismatch("requestedDek", "imposterId");

            EventWrittenEventArgs evt = listener.WaitForEvent(DistributedCacheIdMismatchEventId);
            Assert.IsNotNull(evt, "Expected DistributedCacheIdMismatch event to be raised on the EventSource.");
            Assert.AreEqual(EventLevel.Warning, evt.Level);
            Assert.IsNotNull(evt.Payload, "Event payload must not be null.");
            Assert.AreEqual(2, evt.Payload.Count, "Id-mismatch payload should contain exactly the requested and observed DEK ids.");
            Assert.AreEqual("requestedDek", evt.Payload[0] as string, "First payload element should be the requested dekId.");
            Assert.AreEqual("imposterId", evt.Payload[1] as string, "Second payload element should be the observed payload Id.");
        }

        [TestMethod]
        // REQ: Exception-bearing EventSource payloads must never include exception.Message text, only the stable category.
        // SOURCE: PR #5428 review comment 3255385238.
        public void EventPayload_DoesNotContainExceptionMessage_AcrossAllFailureChannels()
        {
            using CapturingEventListener listener = new (EventSourceName, EventLevel.Warning);

            EncryptionCustomEventSource.DistributedCacheReadFailed("readDek", new InvalidOperationException("READ REDIS-ENDPOINT-12345.cache.windows.net"));
            EncryptionCustomEventSource.DistributedCacheWriteFailed("writeDek", new InvalidOperationException("WRITE REDIS-ENDPOINT-12345.cache.windows.net"));
            EncryptionCustomEventSource.DistributedCacheBackgroundWriteFailed("backgroundDek", new InvalidOperationException("BACKGROUND REDIS-ENDPOINT-12345.cache.windows.net"));
            EncryptionCustomEventSource.DistributedCacheRemoveFailed("removeDek", new InvalidOperationException("REMOVE REDIS-ENDPOINT-12345.cache.windows.net"));

            this.AssertPayloadDoesNotContainSentinel(listener.WaitForEvent(DistributedCacheReadFailedEventId));
            this.AssertPayloadDoesNotContainSentinel(listener.WaitForEvent(DistributedCacheWriteFailedEventId));
            this.AssertPayloadDoesNotContainSentinel(listener.WaitForEvent(DistributedCacheBackgroundWriteFailedEventId));
            this.AssertPayloadDoesNotContainSentinel(listener.WaitForEvent(DistributedCacheRemoveFailedEventId));
        }

        [TestMethod]
        // REQ: Exception categorization must map known exception families to stable payload categories.
        // SOURCE: PR #5428 review comment 3255385238.
        public void CategorizeException_ReturnsExpectedCategory_ForKnownTypes()
        {
            MethodInfo categorize = typeof(EncryptionCustomEventSource).GetMethod("CategorizeException", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(categorize, "EncryptionCustomEventSource must keep a private static CategorizeException helper.");

            Assert.AreEqual("timeout", this.InvokeCategorizeException(categorize, new OperationCanceledException()));
            Assert.AreEqual("timeout", this.InvokeCategorizeException(categorize, new TimeoutException()));
            Assert.AreEqual("connection", this.InvokeCategorizeException(categorize, new SocketException((int)SocketError.ConnectionReset)));
            Assert.AreEqual("connection", this.InvokeCategorizeException(categorize, new HttpRequestException("synthetic")));
            Assert.AreEqual("deserialize", this.InvokeCategorizeException(categorize, new JsonReaderException("synthetic")));
            Assert.AreEqual("deserialize", this.InvokeCategorizeException(categorize, new InvalidDataException("synthetic")));
            Assert.AreEqual("other", this.InvokeCategorizeException(categorize, new Exception("synthetic")));
        }

        private static void AssertPayloadContains(EventWrittenEventArgs evt, string expectedDekId, string expectedExceptionType)
        {
            Assert.IsNotNull(evt.Payload, "Event payload must not be null.");
            // Event payload contract: (dekId, exceptionType, category).
            Assert.IsTrue(evt.Payload.Count >= 3, $"Event payload should contain at least 3 elements (dekId, exceptionType, category). Got {evt.Payload.Count}.");
            Assert.AreEqual(expectedDekId, evt.Payload[0] as string, "First payload element should be dekId.");
            Assert.AreEqual(expectedExceptionType, evt.Payload[1] as string, "Second payload element should be exception type.");
        }

        private void AssertPayloadDoesNotContainSentinel(EventWrittenEventArgs evt)
        {
            Assert.IsNotNull(evt, "Expected the EventSource warning to be emitted.");
            Assert.IsNotNull(evt.Payload, "Event payload must not be null.");
            Assert.IsFalse(
                evt.Payload.OfType<string>().Any(s => s.Contains("REDIS-ENDPOINT", StringComparison.Ordinal)),
                "EventSource payload must NEVER include exception.Message — would leak Redis endpoints / SQL params / HTTP credentials.");
        }

        private string InvokeCategorizeException(MethodInfo categorizeMethod, Exception exception)
        {
            return (string)categorizeMethod.Invoke(null, new object[] { exception });
        }

        /// <summary>
        /// EventListener that subscribes to a single named EventSource and captures matching events.
        /// </summary>
        /// <remarks>
        /// EventListener subscriptions are global per-process; we filter by source name in
        /// <see cref="OnEventSourceCreated"/> and by source identity in <see cref="OnEventWritten"/>
        /// so noise from unrelated EventSources cannot leak into assertions.
        /// </remarks>
        private sealed class CapturingEventListener : EventListener
        {
            private readonly string sourceName;
            private readonly EventLevel level;
            private readonly object sync = new ();
            private readonly List<EventWrittenEventArgs> events = new ();
            private EventSource subscribedSource;
            private readonly ManualResetEventSlim eventReceived = new (false);

            public CapturingEventListener(string sourceName, EventLevel level)
            {
                this.sourceName = sourceName;
                this.level = level;

                // EventListener's base ctor already iterated and called OnEventSourceCreated for
                // every pre-existing EventSource — at that point `this.sourceName` was still null
                // because parameter assignment happens AFTER base(). Re-scan now and enable events
                // on any matching source we missed. OnEventSourceCreated still handles sources
                // created later (e.g. on first test run when the Singleton is lazily created).
                foreach (EventSource existing in EventSource.GetSources())
                {
                    if (string.Equals(existing.Name, this.sourceName, StringComparison.Ordinal))
                    {
                        this.subscribedSource = existing;
                        this.EnableEvents(existing, this.level, EventKeywords.None);
                        break;
                    }
                }
            }

            public int EventCount
            {
                get { lock (this.sync) { return this.events.Count; } }
            }

            /// <summary>
            /// Waits up to the given timeout for an event with the requested ID and returns it.
            /// </summary>
            public EventWrittenEventArgs WaitForEvent(int eventId, int timeoutMs = 5000)
            {
                int waited = 0;
                const int step = 50;
                while (waited <= timeoutMs)
                {
                    lock (this.sync)
                    {
                        for (int i = 0; i < this.events.Count; i++)
                        {
                            if (this.events[i].EventId == eventId)
                            {
                                return this.events[i];
                            }
                        }
                    }

                    this.eventReceived.Wait(step);
                    this.eventReceived.Reset();
                    waited += step;
                }

                return null;
            }

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                // sourceName may still be null here if base() called us before constructor body
                // assigned the field — see the GetSources() rescan in the ctor for that case.
                if (this.sourceName != null
                    && string.Equals(eventSource.Name, this.sourceName, StringComparison.Ordinal))
                {
                    this.subscribedSource = eventSource;
                    this.EnableEvents(eventSource, this.level, EventKeywords.None);
                }

                base.OnEventSourceCreated(eventSource);
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                if (this.subscribedSource != null && eventData.EventSource == this.subscribedSource)
                {
                    lock (this.sync)
                    {
                        this.events.Add(eventData);
                    }

                    this.eventReceived.Set();
                }

                base.OnEventWritten(eventData);
            }

            public override void Dispose()
            {
                this.eventReceived.Dispose();
                base.Dispose();
            }
        }
    }
}
