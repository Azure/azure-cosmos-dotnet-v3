//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class MetadataHedgingStrategyTests
    {
        private static readonly Uri PrimaryEndpoint = new Uri("https://acct-eastus.documents.azure.com/");
        private static readonly Uri HedgeEndpoint = new Uri("https://acct-westus.documents.azure.com/");
        private const string PrimaryRegion = "East US";
        private const string HedgeRegion = "West US";

        // ---------------------------------------------------------------
        // Eligibility matrix
        // ---------------------------------------------------------------

        [TestMethod]
        [Owner("dkunda")]
        public void EvaluateEligibility_OptInDisabled_SkipsWithOptInDisabled()
        {
            using MetadataHedgingStrategy strategy = BuildStrategy(isOptInEnabled: false);
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext(ResourceType.Collection);

            MetadataHedgeEligibility result = strategy.EvaluateEligibility(req, ctx);

            Assert.IsFalse(result.IsEligible);
            Assert.AreEqual(MetadataHedgeSkipReason.OptInDisabled, result.SkipReason);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void EvaluateEligibility_KillSwitchOn_SkipsWithGatewayKillSwitch()
        {
            using MetadataHedgingStrategy strategy = BuildStrategy(killSwitchOn: true);
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext(ResourceType.Collection);

            MetadataHedgeEligibility result = strategy.EvaluateEligibility(req, ctx);

            Assert.IsFalse(result.IsEligible);
            Assert.AreEqual(MetadataHedgeSkipReason.GatewayKillSwitchOn, result.SkipReason);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void EvaluateEligibility_PpafDisabled_SkipsWithPpafDisabled()
        {
            using MetadataHedgingStrategy strategy = BuildStrategy(ppafEnabled: false);
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext(ResourceType.Collection);

            MetadataHedgeEligibility result = strategy.EvaluateEligibility(req, ctx);

            Assert.IsFalse(result.IsEligible);
            Assert.AreEqual(MetadataHedgeSkipReason.PpafDisabled, result.SkipReason);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void EvaluateEligibility_NotColdStart_SkipsWithNotColdStart()
        {
            using MetadataHedgingStrategy strategy = BuildStrategy();
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext(ResourceType.Collection);
            ctx.IsColdStart = false;

            MetadataHedgeEligibility result = strategy.EvaluateEligibility(req, ctx);

            Assert.IsFalse(result.IsEligible);
            Assert.AreEqual(MetadataHedgeSkipReason.NotColdStart, result.SkipReason);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void EvaluateEligibility_AlreadyHedged_SkipsWithAlreadyHedgedThisOperation()
        {
            using MetadataHedgingStrategy strategy = BuildStrategy();
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext(ResourceType.Collection);
            ctx.TryMarkHedgedThisOperation();

            MetadataHedgeEligibility result = strategy.EvaluateEligibility(req, ctx);

            Assert.IsFalse(result.IsEligible);
            Assert.AreEqual(MetadataHedgeSkipReason.AlreadyHedgedThisOperation, result.SkipReason);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void EvaluateEligibility_DocumentResource_SkipsWithResourceTypeNotSupported()
        {
            using MetadataHedgingStrategy strategy = BuildStrategy();
            DocumentServiceRequest req = DocumentServiceRequest.Create(
                OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            MetadataHedgingContext ctx = NewColdStartContext(ResourceType.Document);

            MetadataHedgeEligibility result = strategy.EvaluateEligibility(req, ctx);

            Assert.IsFalse(result.IsEligible);
            Assert.AreEqual(MetadataHedgeSkipReason.ResourceTypeNotSupported, result.SkipReason);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void EvaluateEligibility_PartitionKeyRange_NonFirstPage_Skips()
        {
            using MetadataHedgingStrategy strategy = BuildStrategy();
            DocumentServiceRequest req = DocumentServiceRequest.Create(
                OperationType.ReadFeed, ResourceType.PartitionKeyRange, AuthorizationTokenType.PrimaryMasterKey);
            MetadataHedgingContext ctx = NewColdStartContext(ResourceType.PartitionKeyRange);
            ctx.IsFirstReadFeedPage = false;

            MetadataHedgeEligibility result = strategy.EvaluateEligibility(req, ctx);

            Assert.IsFalse(result.IsEligible);
            Assert.AreEqual(MetadataHedgeSkipReason.NotFirstReadFeedPage, result.SkipReason);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void EvaluateEligibility_SingleRegion_SkipsWithSingleRegion()
        {
            Mock<IGlobalEndpointManager> gem = BuildEndpointManagerMock(new[] { PrimaryEndpoint });
            using MetadataHedgingStrategy strategy = BuildStrategy(gem.Object);
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext(ResourceType.Collection);

            MetadataHedgeEligibility result = strategy.EvaluateEligibility(req, ctx);

            Assert.IsFalse(result.IsEligible);
            Assert.AreEqual(MetadataHedgeSkipReason.SingleRegion, result.SkipReason);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void EvaluateEligibility_ExcludedRegionsLeaveNoTarget_Skips()
        {
            Mock<IGlobalEndpointManager> gem = BuildEndpointManagerMock(
                allReadEndpoints: new[] { PrimaryEndpoint, HedgeEndpoint },
                applicableEndpoints: new[] { PrimaryEndpoint });
            using MetadataHedgingStrategy strategy = BuildStrategy(gem.Object);
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext(ResourceType.Collection);

            MetadataHedgeEligibility result = strategy.EvaluateEligibility(req, ctx);

            Assert.IsFalse(result.IsEligible);
            Assert.AreEqual(MetadataHedgeSkipReason.ExcludedRegionLeavesNoTarget, result.SkipReason);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void EvaluateEligibility_TwoRegions_ColdStart_Eligible()
        {
            using MetadataHedgingStrategy strategy = BuildStrategy();
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext(ResourceType.Collection);

            MetadataHedgeEligibility result = strategy.EvaluateEligibility(req, ctx);

            Assert.IsTrue(result.IsEligible);
            Assert.AreEqual(MetadataHedgeSkipReason.None, result.SkipReason);
        }

        // ---------------------------------------------------------------
        // ExecuteAsync — primary-only paths
        // ---------------------------------------------------------------

        [TestMethod]
        [Owner("dkunda")]
        public async Task ExecuteAsync_NotEligible_DoesNotConsumeHedgeBudget()
        {
            using MetadataHedgingStrategy strategy = BuildStrategy(isOptInEnabled: false);
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext(ResourceType.Collection);
            int callCount = 0;

            MetadataHedgingResult result = await strategy.ExecuteAsync(
                req,
                (r, u, ct) =>
                {
                    Interlocked.Increment(ref callCount);
                    Assert.AreEqual(PrimaryEndpoint, u);
                    return Task.FromResult(NewOkResponse());
                },
                ctx,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(1, callCount);
            Assert.IsFalse(result.HedgeFired);
            Assert.AreEqual(PrimaryEndpoint, result.WinningEndpoint);
            Assert.AreEqual(strategy.PerClientConcurrencyBudget, strategy.AvailableBudget);
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ExecuteAsync_PrimaryWinsBeforeThreshold_DoesNotDispatchHedge()
        {
            using MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromSeconds(5));
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext(ResourceType.Collection);
            int callCount = 0;

            MetadataHedgingResult result = await strategy.ExecuteAsync(
                req,
                (r, u, ct) =>
                {
                    Interlocked.Increment(ref callCount);
                    return Task.FromResult(NewOkResponse());
                },
                ctx,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(1, callCount);
            Assert.IsFalse(result.HedgeFired);
            Assert.AreEqual((int)HttpStatusCode.OK, (int)result.Response.StatusCode);
            Assert.AreEqual(PrimaryEndpoint, result.WinningEndpoint);
            Assert.AreEqual(strategy.PerClientConcurrencyBudget, strategy.AvailableBudget);
        }

        // ---------------------------------------------------------------
        // ExecuteAsync — hedge dispatch paths
        // ---------------------------------------------------------------

        [TestMethod]
        [Owner("dkunda")]
        public async Task ExecuteAsync_PrimarySlowHedgeFast_HedgeWinsAndPrimaryCancelled_NoOceEscapes()
        {
            using MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromMilliseconds(30));
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext(ResourceType.Collection);
            TrackedResponse primaryResp = NewTrackingOkResponse();
            TrackedResponse hedgeResp = NewTrackingOkResponse();
            TaskCompletionSource<DocumentServiceResponse> primaryGate = new TaskCompletionSource<DocumentServiceResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            MetadataHedgingResult result = await strategy.ExecuteAsync(
                req,
                async (r, u, ct) =>
                {
                    if (u.Equals(PrimaryEndpoint))
                    {
                        using (ct.Register(() => primaryGate.TrySetResult(primaryResp)))
                        {
                            return await primaryGate.Task.ConfigureAwait(false);
                        }
                    }

                    await Task.Delay(5, CancellationToken.None).ConfigureAwait(false);
                    return hedgeResp;
                },
                ctx,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsTrue(result.HedgeFired);
            Assert.AreSame(hedgeResp, result.Response);
            Assert.AreEqual(HedgeEndpoint, result.WinningEndpoint);
            Assert.AreEqual(HedgeRegion, result.WinningRegion);

            // Late loser disposal — wait briefly for the background continuation
            // to run, then assert the primary response was disposed exactly once
            // and no exception escaped to the caller.
            await WaitForAsync(() => primaryResp.DisposeCount == 1, TimeSpan.FromSeconds(2));
            Assert.AreEqual(1, primaryResp.DisposeCount, "primary response should be disposed by background cleanup");
            Assert.AreEqual(0, hedgeResp.DisposeCount, "winning hedge response must NOT be disposed");
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ExecuteAsync_HedgeReturns401_PrimaryLateOk_PrimaryWinsAndAuth401Recorded()
        {
            await AssertHedgeAuthRejection(HttpStatusCode.Unauthorized, "Auth401");
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ExecuteAsync_HedgeReturns403_PrimaryLateOk_PrimaryWinsAndAuth403Recorded()
        {
            await AssertHedgeAuthRejection(HttpStatusCode.Forbidden, "Auth403");
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ExecuteAsync_BudgetExhausted_FallsBackToPrimaryOnly()
        {
            MetadataHedgingOptions options = new MetadataHedgingOptions
            {
                PerClientConcurrencyBudget = 1,
            };
            using MetadataHedgingStrategy strategy = BuildStrategy(
                threshold: TimeSpan.FromMilliseconds(50),
                options: options);

            DocumentServiceRequest req1 = BuildCollectionReadRequest();
            DocumentServiceRequest req2 = BuildCollectionReadRequest();
            MetadataHedgingContext ctx1 = NewColdStartContext(ResourceType.Collection);
            MetadataHedgingContext ctx2 = NewColdStartContext(ResourceType.Collection);

            TrackedResponse primary1 = NewTrackingOkResponse();
            TrackedResponse hedge1 = NewTrackingOkResponse();
            TaskCompletionSource<bool> primary1Hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> hedge1Hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Fire the first hedge to consume the budget; both branches block.
            Task<MetadataHedgingResult> first = strategy.ExecuteAsync(
                req1,
                async (r, u, ct) =>
                {
                    if (u.Equals(PrimaryEndpoint))
                    {
                        using (ct.Register(() => primary1Hold.TrySetResult(true)))
                        {
                            await primary1Hold.Task.ConfigureAwait(false);
                        }

                        return primary1;
                    }

                    using (ct.Register(() => hedge1Hold.TrySetResult(true)))
                    {
                        await hedge1Hold.Task.ConfigureAwait(false);
                    }

                    return hedge1;
                },
                ctx1,
                NoOpTrace.Singleton,
                CancellationToken.None);

            // Wait until the first hedge fires and budget hits zero.
            await WaitForAsync(() => strategy.AvailableBudget == 0, TimeSpan.FromSeconds(2));

            // Second call should observe BudgetExhausted and fall back to primary-only.
            int call2Count = 0;
            MetadataHedgingResult second = await strategy.ExecuteAsync(
                req2,
                (r, u, ct) =>
                {
                    Interlocked.Increment(ref call2Count);
                    Assert.AreEqual(PrimaryEndpoint, u);
                    return Task.FromResult(NewOkResponse());
                },
                ctx2,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(1, call2Count);
            Assert.IsFalse(second.HedgeFired);
            Assert.AreEqual(MetadataHedgeSkipReason.BudgetExhausted, second.Diagnostics.SkipReason);

            // Release the first call so it cleans up.
            primary1Hold.TrySetResult(true);
            hedge1Hold.TrySetResult(true);
            await first.ConfigureAwait(false);
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ExecuteAsync_NetFramework_ManyConcurrentHedges_NoStackOverflow()
        {
            // Regression guard for design §5.12 — ensures the SendOneAsync
            // Task.Yield boundary prevents deep synchronous stacks when many
            // hedges fire and cancel one another concurrently.
            MetadataHedgingOptions options = new MetadataHedgingOptions
            {
                PerClientConcurrencyBudget = 50,
            };
            using MetadataHedgingStrategy strategy = BuildStrategy(
                threshold: TimeSpan.FromMilliseconds(5),
                options: options);

            List<Task<MetadataHedgingResult>> tasks = new List<Task<MetadataHedgingResult>>();
            for (int i = 0; i < 50; i++)
            {
                DocumentServiceRequest req = BuildCollectionReadRequest();
                MetadataHedgingContext ctx = NewColdStartContext(ResourceType.Collection);
                tasks.Add(strategy.ExecuteAsync(
                    req,
                    async (r, u, ct) =>
                    {
                        if (u.Equals(PrimaryEndpoint))
                        {
                            // Primary blocks past the threshold then completes — exercises
                            // the cancel-after-loss path on the primary branch.
                            try
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(200), ct).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                                // Loser cancellation must not escape ExecuteAsync.
                                throw;
                            }

                            return NewOkResponse();
                        }

                        await Task.Yield();
                        return NewOkResponse();
                    },
                    ctx,
                    NoOpTrace.Singleton,
                    CancellationToken.None));
            }

            MetadataHedgingResult[] results = await Task.WhenAll(tasks);
            Assert.AreEqual(50, results.Length);
            Assert.IsTrue(results.All(r => r.HedgeFired), "all 50 attempts should have dispatched a hedge");
            Assert.IsTrue(results.All(r => r.WinningEndpoint == HedgeEndpoint));

            // Budget must be fully restored after background cleanup.
            await WaitForAsync(() => strategy.AvailableBudget == 50, TimeSpan.FromSeconds(5));
            Assert.AreEqual(50, strategy.AvailableBudget);
        }

        // ---------------------------------------------------------------
        // IsAcceptableWinner unit tests
        // ---------------------------------------------------------------

        [TestMethod]
        [Owner("dkunda")]
        public void IsAcceptableWinner_Primary503_Rejected()
        {
            DocumentServiceResponse resp = new DocumentServiceResponse(
                Stream.Null, new StoreResponseNameValueCollection(), HttpStatusCode.ServiceUnavailable);
            Assert.IsFalse(MetadataHedgingStrategy.IsAcceptableWinner(resp, HedgeBranch.Primary));
        }

        [TestMethod]
        [Owner("dkunda")]
        public void IsAcceptableWinner_Primary401_Accepted()
        {
            DocumentServiceResponse resp = new DocumentServiceResponse(
                Stream.Null, new StoreResponseNameValueCollection(), HttpStatusCode.Unauthorized);
            Assert.IsTrue(MetadataHedgingStrategy.IsAcceptableWinner(resp, HedgeBranch.Primary));
        }

        [TestMethod]
        [Owner("dkunda")]
        public void IsAcceptableWinner_Hedge401_Rejected()
        {
            DocumentServiceResponse resp = new DocumentServiceResponse(
                Stream.Null, new StoreResponseNameValueCollection(), HttpStatusCode.Unauthorized);
            Assert.IsFalse(MetadataHedgingStrategy.IsAcceptableWinner(resp, HedgeBranch.Hedge));
        }

        [TestMethod]
        [Owner("dkunda")]
        public void IsAcceptableWinner_Hedge403_Rejected()
        {
            DocumentServiceResponse resp = new DocumentServiceResponse(
                Stream.Null, new StoreResponseNameValueCollection(), HttpStatusCode.Forbidden);
            Assert.IsFalse(MetadataHedgingStrategy.IsAcceptableWinner(resp, HedgeBranch.Hedge));
        }

        [TestMethod]
        [Owner("dkunda")]
        public void IsAcceptableWinner_Hedge200_Accepted()
        {
            DocumentServiceResponse resp = new DocumentServiceResponse(
                Stream.Null, new StoreResponseNameValueCollection(), HttpStatusCode.OK);
            Assert.IsTrue(MetadataHedgingStrategy.IsAcceptableWinner(resp, HedgeBranch.Hedge));
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private async Task AssertHedgeAuthRejection(HttpStatusCode hedgeStatus, string expectedHedgeOutcome)
        {
            using MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromMilliseconds(30));
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext(ResourceType.Collection);
            TrackedResponse hedgeResp = NewTrackedResponse(hedgeStatus);
            TrackedResponse primaryResp = NewTrackingOkResponse();

            MetadataHedgingResult result = await strategy.ExecuteAsync(
                req,
                async (r, u, ct) =>
                {
                    if (u.Equals(PrimaryEndpoint))
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(80), CancellationToken.None).ConfigureAwait(false);
                        return primaryResp;
                    }

                    await Task.Delay(5, CancellationToken.None).ConfigureAwait(false);
                    return hedgeResp;
                },
                ctx,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsTrue(result.HedgeFired);
            Assert.AreSame(primaryResp, result.Response);
            Assert.AreEqual(PrimaryEndpoint, result.WinningEndpoint);
            Assert.AreEqual(expectedHedgeOutcome, result.Diagnostics.HedgeOutcome);

            await WaitForAsync(() => hedgeResp.DisposeCount == 1, TimeSpan.FromSeconds(2));
            Assert.AreEqual(1, hedgeResp.DisposeCount);
        }

        private static MetadataHedgingStrategy BuildStrategy(
            IGlobalEndpointManager gem = null,
            bool isOptInEnabled = true,
            bool killSwitchOn = false,
            bool ppafEnabled = true,
            TimeSpan? threshold = null,
            MetadataHedgingOptions options = null)
        {
            gem ??= BuildEndpointManagerMock(new[] { PrimaryEndpoint, HedgeEndpoint }).Object;
            return new MetadataHedgingStrategy(
                globalEndpointManager: gem,
                isHedgingDisabledByGateway: () => killSwitchOn,
                isPpafEnabled: () => ppafEnabled,
                isOptInEnabled: isOptInEnabled,
                threshold: threshold ?? TimeSpan.FromMilliseconds(100),
                options: options ?? new MetadataHedgingOptions());
        }

        private static Mock<IGlobalEndpointManager> BuildEndpointManagerMock(
            IList<Uri> allReadEndpoints,
            IList<Uri> applicableEndpoints = null)
        {
            Mock<IGlobalEndpointManager> mock = new Mock<IGlobalEndpointManager>(MockBehavior.Loose);
            ReadOnlyCollection<Uri> reads = new ReadOnlyCollection<Uri>(allReadEndpoints);
            ReadOnlyCollection<Uri> applicable = new ReadOnlyCollection<Uri>(applicableEndpoints ?? allReadEndpoints);

            mock.Setup(g => g.ReadEndpoints).Returns(reads);
            mock.Setup(g => g.GetApplicableEndpoints(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>()))
                .Returns(applicable);
            mock.Setup(g => g.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>())).Returns(PrimaryEndpoint);
            mock.Setup(g => g.GetLocation(PrimaryEndpoint)).Returns(PrimaryRegion);
            mock.Setup(g => g.GetLocation(HedgeEndpoint)).Returns(HedgeRegion);
            return mock;
        }

        private static MetadataHedgingContext NewColdStartContext(ResourceType resourceType)
        {
            return new MetadataHedgingContext
            {
                IsColdStart = true,
                ResourceType = resourceType,
                IsFirstReadFeedPage = true,
            };
        }

        private static DocumentServiceRequest BuildCollectionReadRequest()
        {
            return DocumentServiceRequest.Create(
                OperationType.Read, ResourceType.Collection, AuthorizationTokenType.PrimaryMasterKey);
        }

        private static DocumentServiceResponse NewOkResponse()
        {
            return new DocumentServiceResponse(Stream.Null, new StoreResponseNameValueCollection(), HttpStatusCode.OK);
        }

        private static TrackedResponse NewTrackingOkResponse() => NewTrackedResponse(HttpStatusCode.OK);

        private static TrackedResponse NewTrackedResponse(HttpStatusCode status)
        {
            TrackingStream stream = new TrackingStream();
            DocumentServiceResponse response = new DocumentServiceResponse(
                stream, new StoreResponseNameValueCollection(), status);
            return new TrackedResponse(response, stream);
        }

        private static async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (predicate())
                {
                    return;
                }

                await Task.Delay(10).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Pairs a real <see cref="DocumentServiceResponse"/> with the
        /// <see cref="TrackingStream"/> instance handed in as its body so the
        /// test can observe whether the response was disposed (the response's
        /// <c>Dispose</c> propagates to the underlying stream).
        /// </summary>
        private sealed class TrackedResponse
        {
            private readonly DocumentServiceResponse response;
            private readonly TrackingStream stream;

            public TrackedResponse(DocumentServiceResponse response, TrackingStream stream)
            {
                this.response = response;
                this.stream = stream;
            }

            public int DisposeCount => this.stream.DisposeCount;

            public static implicit operator DocumentServiceResponse(TrackedResponse t) => t.response;
        }

        private sealed class TrackingStream : MemoryStream
        {
            private int disposeCount;

            public int DisposeCount => Volatile.Read(ref this.disposeCount);

            protected override void Dispose(bool disposing)
            {
                Interlocked.Increment(ref this.disposeCount);
                base.Dispose(disposing);
            }
        }

        // ---------------------------------------------------------------
        // Phase-default resolver (Stage 6)
        // ---------------------------------------------------------------

        [TestMethod]
        [Owner("dkunda")]
        public void ResolveOptIn_NullCustomerSetting_FollowsPhaseDefault()
        {
            Assert.AreEqual(MetadataHedgingStrategy.PhaseDefault, MetadataHedgingStrategy.ResolveOptIn(null));
        }

        [TestMethod]
        [Owner("dkunda")]
        public void ResolveOptIn_ExplicitTrue_OverridesPhaseDefault()
        {
            Assert.IsTrue(MetadataHedgingStrategy.ResolveOptIn(true));
        }

        [TestMethod]
        [Owner("dkunda")]
        public void ResolveOptIn_ExplicitFalse_OverridesPhaseDefault()
        {
            Assert.IsFalse(MetadataHedgingStrategy.ResolveOptIn(false));
        }

        [TestMethod]
        [Owner("dkunda")]
        public void PhaseDefault_Phase1_IsFalse()
        {
            Assert.IsFalse(MetadataHedgingStrategy.PhaseDefault, "Phase 1 default must be off; bump this assertion when promoting to a later phase.");
        }
    }
}
