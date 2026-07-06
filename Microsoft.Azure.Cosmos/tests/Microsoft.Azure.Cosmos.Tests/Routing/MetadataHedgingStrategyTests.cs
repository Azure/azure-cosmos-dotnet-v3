//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class MetadataHedgingStrategyTests
    {
        private static readonly Uri Region1 = new Uri("https://region1.documents.azure.com/");
        private static readonly Uri Region2 = new Uri("https://region2.documents.azure.com/");

        private static readonly TimeSpan ShortThreshold = TimeSpan.FromMilliseconds(150);

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task PrimaryFastWinsWithoutHedging()
        {
            int hedgeSends = 0;
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: true, ppafEnabled: true, optIn: null);

            MetadataHedgingStrategy.MetadataHedgingResult result = await strategy.ExecuteAsync(
                CreateCollectionReadRequest(),
                sendToEndpoint: (request, endpoint, ct) =>
                {
                    if (!endpoint.Equals(Region1))
                    {
                        Interlocked.Increment(ref hedgeSends);
                    }

                    return Task.FromResult(CreateResponse(HttpStatusCode.OK));
                },
                isFirstReadFeedPage: true,
                cancellationToken: default);

            Assert.IsFalse(result.HedgeFired, "Primary completed before threshold; no hedge expected.");
            Assert.AreEqual(0, hedgeSends, "Hedge must not be dispatched when the primary is fast.");
            Assert.AreEqual(HttpStatusCode.OK, result.Response.StatusCode);
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task SlowPrimaryTriggersHedgeThatWins()
        {
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: true, ppafEnabled: true, optIn: null);

            MetadataHedgingStrategy.MetadataHedgingResult result = await strategy.ExecuteAsync(
                CreateCollectionReadRequest(),
                sendToEndpoint: async (request, endpoint, ct) =>
                {
                    if (endpoint.Equals(Region1))
                    {
                        // Primary is slow past the threshold.
                        await Task.Delay(TimeSpan.FromSeconds(5), ct);
                        return CreateResponse(HttpStatusCode.OK);
                    }

                    return CreateResponse(HttpStatusCode.OK);
                },
                isFirstReadFeedPage: true,
                cancellationToken: default);

            Assert.IsTrue(result.HedgeFired, "Slow primary should trigger a hedge.");
            Assert.IsTrue(result.HedgeWon, "The fast hedge should win.");
            Assert.AreEqual(Region2, result.WinningEndpoint, "The fast hedge should win.");
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task PrimaryRegionalFailureHedgeWins()
        {
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: true, ppafEnabled: true, optIn: null);

            // Production path: metadata reads THROW DocumentClientException for status >= 400, so a
            // primary regional failure (503) arrives as a faulted task, not a returned response.
            MetadataHedgingStrategy.MetadataHedgingResult result = await strategy.ExecuteAsync(
                CreateCollectionReadRequest(),
                sendToEndpoint: (request, endpoint, ct) => endpoint.Equals(Region1)
                    ? Task.FromException<DocumentServiceResponse>(RegionalFailure(HttpStatusCode.ServiceUnavailable))
                    : Task.FromResult(CreateResponse(HttpStatusCode.OK)),
                isFirstReadFeedPage: true,
                cancellationToken: default);

            Assert.IsTrue(result.HedgeFired);
            Assert.IsTrue(result.HedgeWon, "Hedge should win over a primary 503.");
            Assert.AreEqual(Region2, result.WinningEndpoint, "Hedge should win over a primary 503.");
            Assert.AreEqual(HttpStatusCode.OK, result.Response.StatusCode);
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task HedgeAuthRejectYieldsPrimaryOutcome()
        {
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: true, ppafEnabled: true, optIn: null);

            // Primary regional failure (503, thrown); hedge is a cross-region auth reject (401,
            // thrown) which is NOT a regional failure, so the hedge cannot win. The authoritative
            // primary 503 must be rethrown to the caller.
            DocumentClientException thrown = await Assert.ThrowsExceptionAsync<DocumentClientException>(
                () => strategy.ExecuteAsync(
                    CreateCollectionReadRequest(),
                    sendToEndpoint: (request, endpoint, ct) => endpoint.Equals(Region1)
                        ? Task.FromException<DocumentServiceResponse>(RegionalFailure(HttpStatusCode.ServiceUnavailable))
                        : Task.FromException<DocumentServiceResponse>(NonRegionalFailure(HttpStatusCode.Unauthorized)),
                    isFirstReadFeedPage: true,
                    cancellationToken: default));

            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, thrown.StatusCode, "The primary's authoritative 503 must be rethrown.");
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task PrimaryConnectionFailureHedgeWins()
        {
            // A bare HttpRequestException (connection refused / DNS / TLS) to the primary region's
            // gateway means the region is unreachable -- a regional failure, not a bad request -- so a
            // good hedge to another region must win. (PR #5999: align with ClientRetryPolicy, which
            // treats HttpRequestException as an endpoint failure.)
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: true, ppafEnabled: true, optIn: null);

            MetadataHedgingStrategy.MetadataHedgingResult result = await strategy.ExecuteAsync(
                CreateCollectionReadRequest(),
                sendToEndpoint: (request, endpoint, ct) => endpoint.Equals(Region1)
                    ? Task.FromException<DocumentServiceResponse>(new HttpRequestException("Connection refused"))
                    : Task.FromResult(CreateResponse(HttpStatusCode.OK)),
                isFirstReadFeedPage: true,
                cancellationToken: default);

            Assert.IsTrue(result.HedgeFired, "A primary connection failure is regional, so a hedge should fire.");
            Assert.IsTrue(result.HedgeWon, "The hedge should win over a primary connection failure.");
            Assert.AreEqual(Region2, result.WinningEndpoint);
            Assert.AreEqual(HttpStatusCode.OK, result.Response.StatusCode);
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task PrimaryAndHedgeConnectionFailureRethrowsPrimary()
        {
            // Both regions are unreachable (connection failures). Neither branch is a good winner, so
            // the primary's authoritative connection error is rethrown -- the caller's retry policy then
            // classifies it exactly as it would have without hedging.
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: true, ppafEnabled: true, optIn: null);

            HttpRequestException thrown = await Assert.ThrowsExceptionAsync<HttpRequestException>(
                () => strategy.ExecuteAsync(
                    CreateCollectionReadRequest(),
                    sendToEndpoint: (request, endpoint, ct) => endpoint.Equals(Region1)
                        ? Task.FromException<DocumentServiceResponse>(new HttpRequestException("primary refused"))
                        : Task.FromException<DocumentServiceResponse>(new HttpRequestException("hedge refused")),
                    isFirstReadFeedPage: true,
                    cancellationToken: default));

            Assert.AreEqual("primary refused", thrown.Message, "The primary's authoritative connection error must be rethrown.");
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task FastPrimaryDefinitiveErrorIsNeverOverriddenByHedge()
        {
            // Regression guard for the core invariant (PR #5999 finding #1): a FAST, definitive
            // primary error (404 for a deleted/recreated collection) arrives as a faulted task
            // BEFORE the threshold. It is authoritative and must be rethrown verbatim; no hedge
            // may fire, so a stale secondary 200 can never resurrect a deleted collection.
            int hedgeSends = 0;
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: true, ppafEnabled: true, optIn: null);

            DocumentClientException thrown = await Assert.ThrowsExceptionAsync<DocumentClientException>(
                () => strategy.ExecuteAsync(
                    CreateCollectionReadRequest(),
                    sendToEndpoint: (request, endpoint, ct) =>
                    {
                        if (!endpoint.Equals(Region1))
                        {
                            Interlocked.Increment(ref hedgeSends);
                            return Task.FromResult(CreateResponse(HttpStatusCode.OK)); // stale replica "exists"
                        }

                        return Task.FromException<DocumentServiceResponse>(NonRegionalFailure(HttpStatusCode.NotFound));
                    },
                    isFirstReadFeedPage: true,
                    cancellationToken: default));

            Assert.AreEqual(HttpStatusCode.NotFound, thrown.StatusCode, "The primary's authoritative 404 must be rethrown.");
            Assert.AreEqual(0, hedgeSends, "A definitive primary error must not trigger a hedge.");
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task SlowPrimaryDefinitiveErrorArrivingFirstWinsOverInFlightHedge()
        {
            // The primary is authoritative for any outcome it has PRODUCED. Here the primary is slow
            // past the threshold (so a hedge fires) but then SETTLES FIRST with a definitive 409
            // while the hedge is still in flight. That definitive primary answer must win and be
            // rethrown; the still-running hedge cannot override it (PR #5999 finding #1).
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: true, ppafEnabled: true, optIn: null);

            DocumentClientException thrown = await Assert.ThrowsExceptionAsync<DocumentClientException>(
                () => strategy.ExecuteAsync(
                    CreateCollectionReadRequest(),
                    sendToEndpoint: async (request, endpoint, ct) =>
                    {
                        if (endpoint.Equals(Region1))
                        {
                            // Slow enough to fire a hedge, but still settles before the hedge.
                            await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
                            throw NonRegionalFailure(HttpStatusCode.Conflict);
                        }

                        // Hedge stays in flight well past the primary's completion.
                        await Task.Delay(TimeSpan.FromSeconds(5), ct);
                        return CreateResponse(HttpStatusCode.OK);
                    },
                    isFirstReadFeedPage: true,
                    cancellationToken: default));

            Assert.AreEqual(HttpStatusCode.Conflict, thrown.StatusCode, "The primary's authoritative 409 must win over the still-in-flight hedge.");
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task SingleRegionSkipsHedging()
        {
            int totalSends = 0;
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: false, ppafEnabled: true, optIn: null);

            MetadataHedgingStrategy.MetadataHedgingResult result = await strategy.ExecuteAsync(
                CreateCollectionReadRequest(),
                sendToEndpoint: (request, endpoint, ct) =>
                {
                    Interlocked.Increment(ref totalSends);
                    return Task.FromResult(CreateResponse(HttpStatusCode.OK));
                },
                isFirstReadFeedPage: true,
                cancellationToken: default);

            Assert.IsFalse(result.HedgeFired);
            Assert.AreEqual(1, totalSends, "Single-region accounts must send exactly once.");
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task PpafDisabledAndNullOptInSkipsHedging()
        {
            int totalSends = 0;
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: true, ppafEnabled: false, optIn: null);

            MetadataHedgingStrategy.MetadataHedgingResult result = await strategy.ExecuteAsync(
                CreateCollectionReadRequest(),
                sendToEndpoint: (request, endpoint, ct) =>
                {
                    Interlocked.Increment(ref totalSends);
                    return Task.FromResult(CreateResponse(HttpStatusCode.OK));
                },
                isFirstReadFeedPage: true,
                cancellationToken: default);

            Assert.IsFalse(result.HedgeFired, "null opt-in must follow PPAF; PPAF off => no hedge.");
            Assert.AreEqual(1, totalSends);
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task ExplicitOptInOverridesDisabledPpaf()
        {
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: true, ppafEnabled: false, optIn: true);

            MetadataHedgingStrategy.MetadataHedgingResult result = await strategy.ExecuteAsync(
                CreateCollectionReadRequest(),
                sendToEndpoint: async (request, endpoint, ct) =>
                {
                    if (endpoint.Equals(Region1))
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), ct);
                        return CreateResponse(HttpStatusCode.OK);
                    }

                    return CreateResponse(HttpStatusCode.OK);
                },
                isFirstReadFeedPage: true,
                cancellationToken: default);

            Assert.IsTrue(result.HedgeFired, "Explicit opt-in true should enable hedging even when PPAF is off.");
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task NonFirstReadFeedPageSkipsHedging()
        {
            int totalSends = 0;
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: true, ppafEnabled: true, optIn: null);

            MetadataHedgingStrategy.MetadataHedgingResult result = await strategy.ExecuteAsync(
                CreatePartitionKeyRangeReadFeedRequest(),
                sendToEndpoint: (request, endpoint, ct) =>
                {
                    Interlocked.Increment(ref totalSends);
                    return Task.FromResult(CreateResponse(HttpStatusCode.OK));
                },
                isFirstReadFeedPage: false,
                cancellationToken: default);

            Assert.IsFalse(result.HedgeFired, "Only the first PK-range page is hedged.");
            Assert.AreEqual(1, totalSends);
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public void CreateIfEnabledReturnsNullWhenExplicitlyDisabled()
        {
            MetadataHedgingStrategy strategy = MetadataHedgingStrategy.CreateIfEnabled(
                enableMetadataHedging: false,
                globalEndpointManager: BuildEndpointManager(multiRegion: true).Object,
                isPpafEnabled: () => true);

            Assert.IsNull(strategy, "Explicit false is a hard kill-switch.");
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public void ThresholdSitsBetweenFirstAndSecondHttpTimeouts()
        {
            MetadataHedgingStrategy strategy = MetadataHedgingStrategy.CreateIfEnabled(
                enableMetadataHedging: true,
                globalEndpointManager: BuildEndpointManager(multiRegion: true).Object,
                isPpafEnabled: () => true);

            TimeSpan firstAttempt = HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance.FirstAttemptTimeout;

            using IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> enumerator =
                HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance.GetTimeoutEnumerator();
            Assert.IsTrue(
                enumerator.MoveNext(),
                "The control-plane timeout policy must yield a first attempt.");
            Assert.IsTrue(
                enumerator.MoveNext(),
                "This invariant requires the control-plane timeout policy to have at least two attempts (first < threshold < second).");
            TimeSpan secondAttempt = enumerator.Current.requestTimeout;

            Assert.IsTrue(
                strategy.Threshold > firstAttempt,
                $"Threshold {strategy.Threshold} must exceed the first-attempt timeout {firstAttempt}.");
            Assert.IsTrue(
                strategy.Threshold < secondAttempt,
                $"Threshold {strategy.Threshold} must sit below the second-attempt timeout {secondAttempt} so the hedge fires before the long retry.");
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task OperatorKillSwitchSuppressesHedging()
        {
            // Gateway disableCrossRegionalHedging=true is a hard operator override: even with a
            // slow primary, PPAF on, and multi-region, no hedge may fire (PR #5999 finding #4).
            int totalSends = 0;
            MetadataHedgingStrategy strategy = new MetadataHedgingStrategy(
                globalEndpointManager: BuildEndpointManager(multiRegion: true).Object,
                isPpafEnabled: () => true,
                customerOptIn: null,
                threshold: ShortThreshold,
                isCrossRegionalHedgingDisabled: () => true);

            MetadataHedgingStrategy.MetadataHedgingResult result = await strategy.ExecuteAsync(
                CreateCollectionReadRequest(),
                sendToEndpoint: async (request, endpoint, ct) =>
                {
                    Interlocked.Increment(ref totalSends);
                    if (endpoint.Equals(Region1))
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(300), ct);
                    }

                    return CreateResponse(HttpStatusCode.OK);
                },
                isFirstReadFeedPage: true,
                cancellationToken: default);

            Assert.IsFalse(result.HedgeFired, "Operator kill-switch must suppress hedging.");
            Assert.AreEqual(1, totalSends, "Kill-switch on: exactly one (primary) send.");
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task CallerCancellationSurfacesWithoutPhantomHedge()
        {
            // A caller cancel while the primary is in flight must NOT spawn a phantom hedge, and the
            // OperationCanceledException must surface promptly (PR #5999 finding #5).
            int hedgeSends = 0;
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: true, ppafEnabled: true, optIn: null);
            using CancellationTokenSource cts = new CancellationTokenSource();

            Task<MetadataHedgingStrategy.MetadataHedgingResult> execution = strategy.ExecuteAsync(
                CreateCollectionReadRequest(),
                sendToEndpoint: async (request, endpoint, ct) =>
                {
                    if (!endpoint.Equals(Region1))
                    {
                        Interlocked.Increment(ref hedgeSends);
                    }

                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                    return CreateResponse(HttpStatusCode.OK);
                },
                isFirstReadFeedPage: true,
                cancellationToken: cts.Token);

            cts.Cancel();

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => execution);
            Assert.AreEqual(0, hedgeSends, "A cancelled request must not dispatch a phantom hedge.");
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task LosingHedgeResponseIsDisposed()
        {
            // The slow-arriving loser (here the hedge) must have its response disposed so no HTTP
            // body leaks and no unobserved-task exception is raised (PR #5999 finding #6).
            TrackingStream loserBody = new TrackingStream();
            DocumentServiceResponse loser = new DocumentServiceResponse(
                loserBody,
                new StoreResponseNameValueCollection(),
                HttpStatusCode.OK);
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: true, ppafEnabled: true, optIn: null);

            MetadataHedgingStrategy.MetadataHedgingResult result = await strategy.ExecuteAsync(
                CreateCollectionReadRequest(),
                sendToEndpoint: async (request, endpoint, ct) =>
                {
                    if (endpoint.Equals(Region1))
                    {
                        // Primary is slow, so a hedge fires; then the primary wins the race with a
                        // definitive success and the hedge becomes the discarded loser.
                        await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
                        return CreateResponse(HttpStatusCode.OK);
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(400), ct);
                    return loser;
                },
                isFirstReadFeedPage: true,
                cancellationToken: default);

            Assert.AreEqual(Region1, result.WinningEndpoint, "Primary should win once it completes successfully.");

            // The loser is disposed on a background continuation; give it a moment to run.
            for (int i = 0; i < 50 && !loserBody.Disposed; i++)
            {
                await Task.Delay(20);
            }

            Assert.IsTrue(loserBody.Disposed, "The losing hedge response body must be disposed.");
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task HedgeDidNotWinReportsHedgeWonFalse()
        {
            // When the primary wins after a hedge fired, HedgeFired is true but HedgeWon is false so
            // the PKRange caller does NOT pin later pages (PR #5999 finding #3).
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: true, ppafEnabled: true, optIn: null);

            MetadataHedgingStrategy.MetadataHedgingResult result = await strategy.ExecuteAsync(
                CreatePartitionKeyRangeReadFeedRequest(),
                sendToEndpoint: async (request, endpoint, ct) =>
                {
                    if (endpoint.Equals(Region1))
                    {
                        // Slow enough to fire a hedge, then win the race.
                        await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
                        return CreateResponse(HttpStatusCode.OK);
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(400), ct);
                    return CreateResponse(HttpStatusCode.OK);
                },
                isFirstReadFeedPage: true,
                cancellationToken: default);

            Assert.AreEqual(Region1, result.WinningEndpoint);
            Assert.IsTrue(result.HedgeFired, "A hedge fired because the primary was slow.");
            Assert.IsFalse(result.HedgeWon, "Primary won, so later pages must not be pinned.");
        }

        private static MetadataHedgingStrategy BuildStrategy(bool multiRegion, bool ppafEnabled, bool? optIn)
        {
            return new MetadataHedgingStrategy(
                globalEndpointManager: BuildEndpointManager(multiRegion).Object,
                isPpafEnabled: () => ppafEnabled,
                customerOptIn: optIn,
                threshold: ShortThreshold);
        }

        private static Mock<IGlobalEndpointManager> BuildEndpointManager(bool multiRegion)
        {
            ReadOnlyCollection<Uri> readEndpoints = multiRegion
                ? new ReadOnlyCollection<Uri>(new[] { Region1, Region2 })
                : new ReadOnlyCollection<Uri>(new[] { Region1 });

            Mock<IGlobalEndpointManager> mock = new Mock<IGlobalEndpointManager>(MockBehavior.Loose);
            mock.Setup(m => m.ReadEndpoints).Returns(readEndpoints);
            mock.Setup(m => m.GetApplicableEndpoints(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>()))
                .Returns(readEndpoints);
            mock.Setup(m => m.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>()))
                .Returns(Region1);
            mock.Setup(m => m.GetLocation(Region1)).Returns("Region1");
            mock.Setup(m => m.GetLocation(Region2)).Returns("Region2");
            return mock;
        }

        private static DocumentServiceRequest CreateCollectionReadRequest()
        {
            return DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Collection,
                "/dbs/db/colls/coll",
                AuthorizationTokenType.PrimaryMasterKey,
                new RequestNameValueCollection());
        }

        private static DocumentServiceRequest CreatePartitionKeyRangeReadFeedRequest()
        {
            return DocumentServiceRequest.Create(
                OperationType.ReadFeed,
                "coll-rid",
                ResourceType.PartitionKeyRange,
                AuthorizationTokenType.PrimaryMasterKey,
                new RequestNameValueCollection());
        }

        private static DocumentServiceResponse CreateResponse(HttpStatusCode statusCode)
        {
            return new DocumentServiceResponse(
                null,
                new StoreResponseNameValueCollection(),
                statusCode);
        }

        // A regional failure: the region (not the request) is at fault, so a hedge is worthwhile.
        private static DocumentClientException RegionalFailure(HttpStatusCode statusCode)
        {
            return new DocumentClientException(
                message: $"Regional failure {statusCode}",
                innerException: null,
                responseHeaders: new StoreResponseNameValueCollection(),
                statusCode: statusCode,
                requestUri: null);
        }

        // A definitive, non-regional failure (404 / 409 / 401 / ...): the request itself is at
        // fault, so it is authoritative and must never be overridden by a hedge.
        private static DocumentClientException NonRegionalFailure(HttpStatusCode statusCode)
        {
            return new DocumentClientException(
                message: $"Non-regional failure {statusCode}",
                innerException: null,
                responseHeaders: new StoreResponseNameValueCollection(),
                statusCode: statusCode,
                requestUri: null);
        }

        // A stream whose disposal can be observed, to assert the losing branch's response body is
        // released (DocumentServiceResponse.Dispose disposes its body stream).
        private sealed class TrackingStream : MemoryStream
        {
            public bool Disposed { get; private set; }

            protected override void Dispose(bool disposing)
            {
                this.Disposed = true;
                base.Dispose(disposing);
            }
        }
    }
}
