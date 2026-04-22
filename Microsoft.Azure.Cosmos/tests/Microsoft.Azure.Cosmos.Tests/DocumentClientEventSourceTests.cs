//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.Linq;
    using System.Net.Http;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DocumentClientEventSourceTests
    {
        private const string SecretAuthHeaderValue =
            "type=master&ver=1.0&sig=DOCDBAUTHSECRETSIGNATUREabcdef0123456789";

        /// <summary>
        /// Regression test for the credential-leak issue where the
        /// "DocumentDBClient" EventSource (Event ID 1 - Request) wrote the
        /// raw Authorization HTTP header into the ETW event payload.
        /// Any subscriber at Verbose level (for example a Geneva / GCS
        /// EtwProvider named "DocumentDBClient") would have captured the
        /// master-key HMAC, resource token, or AAD Bearer token in plaintext.
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public void Request_AuthorizationHeader_IsRedactedInEtwPayload()
        {
            // Force creation of the EventSource singleton BEFORE the listener is
            // constructed so that EnableEvents below binds to a live source. Under
            // parallel CI test execution the OnEventSourceCreated callback on a
            // subclassed EventListener can race with the subclass field initializer,
            // so we avoid relying on it entirely and enable events explicitly.
            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            using CapturingEventListener listener = new CapturingEventListener(eventSource);

            using HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "https://acct.documents.azure.com/dbs/db/colls/c/docs/id");
            Assert.IsTrue(
                requestMessage.Headers.TryAddWithoutValidation("authorization", SecretAuthHeaderValue),
                "Test setup failed: could not attach authorization header.");

            Guid activityId = Guid.NewGuid();
            eventSource.Request(
                activityId: activityId,
                localId: Guid.NewGuid(),
                uri: requestMessage.RequestUri.ToString(),
                resourceType: "docs",
                requestHeaders: requestMessage.Headers);

            // Pin the assertion to this test's activityId so that concurrently-running
            // tests which also emit Event 1 from the shared DocumentClientEventSource
            // singleton don't cause SingleOrDefault to throw or match the wrong event.
            EventWrittenEventArgs requestEvent = listener.Events
                .FirstOrDefault(e => e.EventId == 1
                    && e.Payload != null
                    && e.Payload.Count > 0
                    && e.Payload[0] is Guid g
                    && g == activityId);
            Assert.IsNotNull(requestEvent, "Expected Event ID 1 (Request) for this test's activityId to be emitted.");

            // The Authorization field is the 6th payload slot (index 5): activityId(0), localId(1),
            // uri(2), resourceType(3), accept(4), authorization(5), ...
            Assert.IsNotNull(requestEvent.Payload, "Event payload must not be null.");
            Assert.IsTrue(requestEvent.Payload.Count > 5, "Event payload is smaller than expected.");

            string authorizationPayload = requestEvent.Payload[5] as string;
            Assert.AreEqual(
                "REDACTED",
                authorizationPayload,
                "Authorization header must be redacted before being written to the ETW payload.");

            // Defense in depth: no payload field anywhere should leak the secret value.
            foreach (object field in requestEvent.Payload)
            {
                if (field is string s)
                {
                    Assert.IsFalse(
                        s.IndexOf("DOCDBAUTHSECRETSIGNATURE", StringComparison.Ordinal) >= 0,
                        $"Event payload field contained the secret authorization signature: '{s}'.");
                }
            }
        }

        /// <summary>
        /// When no Authorization header is present (for example internal
        /// plumbing requests) the redaction logic must not change behaviour:
        /// an empty string should still be emitted, not the literal "REDACTED".
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public void Request_NoAuthorizationHeader_EmitsEmptyString()
        {
            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            using CapturingEventListener listener = new CapturingEventListener(eventSource);

            using HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "https://acct.documents.azure.com/");

            Guid activityId = Guid.NewGuid();
            eventSource.Request(
                activityId: activityId,
                localId: Guid.NewGuid(),
                uri: requestMessage.RequestUri.ToString(),
                resourceType: "docs",
                requestHeaders: requestMessage.Headers);

            EventWrittenEventArgs requestEvent = listener.Events
                .FirstOrDefault(e => e.EventId == 1
                    && e.Payload != null
                    && e.Payload.Count > 0
                    && e.Payload[0] is Guid g
                    && g == activityId);
            Assert.IsNotNull(requestEvent);
            Assert.AreEqual(string.Empty, requestEvent.Payload[5] as string);
        }

        private sealed class CapturingEventListener : EventListener
        {
            // Initialize fields via field initializers so they are assigned before any
            // OnEventSourceCreated callback can fire from the base ctor. Under parallel
            // CI execution the base EventListener constructor dispatches
            // OnEventSourceCreated on the constructing thread while the derived ctor
            // has not completed, so any field assigned in the derived ctor body would
            // still be null at callback time.
            private readonly List<EventWrittenEventArgs> events = new List<EventWrittenEventArgs>();
            private readonly object sync = new object();
            private readonly EventSource targetEventSource;

            public CapturingEventListener(EventSource target)
            {
                this.targetEventSource = target ?? throw new ArgumentNullException(nameof(target));

                // Enable events explicitly after base construction so we do not race with
                // OnEventSourceCreated. Keyword 0x1 == DocumentClientEventSource.Keywords.HttpRequestAndResponse.
                this.EnableEvents(target, EventLevel.Verbose, (EventKeywords)1);
            }

            public IReadOnlyList<EventWrittenEventArgs> Events
            {
                get
                {
                    lock (this.sync)
                    {
                        return this.events.ToList();
                    }
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                // Guard against events from sources other than the one we explicitly
                // targeted (the base EventListener receives all sources until filtered).
                if (this.targetEventSource != null && eventData.EventSource != this.targetEventSource)
                {
                    return;
                }

                // this.events may still be null if OnEventWritten fires from the base
                // constructor before our field initializers run (observed under parallel
                // test execution in CI). Ignore those pre-initialization events.
                if (this.events == null)
                {
                    return;
                }

                lock (this.sync)
                {
                    this.events.Add(eventData);
                }
            }
        }
    }
}
