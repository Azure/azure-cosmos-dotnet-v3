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
        [Owner("cosmos-dotnet-sdk")]
        public void Request_AuthorizationHeader_IsRedactedInEtwPayload()
        {
            using CapturingEventListener listener = new CapturingEventListener();

            using HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "https://acct.documents.azure.com/dbs/db/colls/c/docs/id");
            Assert.IsTrue(
                requestMessage.Headers.TryAddWithoutValidation("authorization", SecretAuthHeaderValue),
                "Test setup failed: could not attach authorization header.");

            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            eventSource.Request(
                activityId: Guid.NewGuid(),
                localId: Guid.NewGuid(),
                uri: requestMessage.RequestUri.ToString(),
                resourceType: "docs",
                requestHeaders: requestMessage.Headers);

            EventWrittenEventArgs requestEvent = listener.Events.SingleOrDefault(e => e.EventId == 1);
            Assert.IsNotNull(requestEvent, "Expected Event ID 1 (Request) to be emitted.");

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
        [Owner("cosmos-dotnet-sdk")]
        public void Request_NoAuthorizationHeader_EmitsEmptyString()
        {
            using CapturingEventListener listener = new CapturingEventListener();

            using HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "https://acct.documents.azure.com/");

            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            eventSource.Request(
                activityId: Guid.NewGuid(),
                localId: Guid.NewGuid(),
                uri: requestMessage.RequestUri.ToString(),
                resourceType: "docs",
                requestHeaders: requestMessage.Headers);

            EventWrittenEventArgs requestEvent = listener.Events.SingleOrDefault(e => e.EventId == 1);
            Assert.IsNotNull(requestEvent);
            Assert.AreEqual(string.Empty, requestEvent.Payload[5] as string);
        }

        private sealed class CapturingEventListener : EventListener
        {
            private readonly List<EventWrittenEventArgs> events = new List<EventWrittenEventArgs>();
            private readonly object sync = new object();

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

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (string.Equals(eventSource.Name, "DocumentDBClient", StringComparison.Ordinal))
                {
                    // Keyword 0x1 == DocumentClientEventSource.Keywords.HttpRequestAndResponse
                    this.EnableEvents(eventSource, EventLevel.Verbose, (EventKeywords)1);
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                lock (this.sync)
                {
                    this.events.Add(eventData);
                }
            }
        }
    }
}
