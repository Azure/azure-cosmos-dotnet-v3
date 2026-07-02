//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.ObjectModel;
    using System.Net;
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
            Assert.AreEqual(Region2, result.WinningEndpoint, "The fast hedge should win.");
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task PrimaryRegionalFailureHedgeWins()
        {
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: true, ppafEnabled: true, optIn: null);

            MetadataHedgingStrategy.MetadataHedgingResult result = await strategy.ExecuteAsync(
                CreateCollectionReadRequest(),
                sendToEndpoint: (request, endpoint, ct) => Task.FromResult(
                    endpoint.Equals(Region1)
                        ? CreateResponse(HttpStatusCode.ServiceUnavailable)
                        : CreateResponse(HttpStatusCode.OK)),
                isFirstReadFeedPage: true,
                cancellationToken: default);

            Assert.IsTrue(result.HedgeFired);
            Assert.AreEqual(Region2, result.WinningEndpoint, "Hedge should win over a primary 503.");
            Assert.AreEqual(HttpStatusCode.OK, result.Response.StatusCode);
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task HedgeAuthRejectYieldsPrimaryOutcome()
        {
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: true, ppafEnabled: true, optIn: null);

            MetadataHedgingStrategy.MetadataHedgingResult result = await strategy.ExecuteAsync(
                CreateCollectionReadRequest(),
                sendToEndpoint: (request, endpoint, ct) => Task.FromResult(
                    endpoint.Equals(Region1)
                        ? CreateResponse(HttpStatusCode.ServiceUnavailable)   // primary regional failure
                        : CreateResponse(HttpStatusCode.Unauthorized)),        // hedge auth reject (rejected)
                isFirstReadFeedPage: true,
                cancellationToken: default);

            // Neither branch is a good winner (503 is a regional failure; hedge 401 is rejected)
            // so the authoritative primary outcome (503) is returned.
            Assert.AreEqual(Region1, result.WinningEndpoint);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, result.Response.StatusCode);
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

            Assert.IsTrue(
                strategy.Threshold > firstAttempt,
                $"Threshold {strategy.Threshold} must exceed the first-attempt timeout {firstAttempt}.");
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
    }
}
