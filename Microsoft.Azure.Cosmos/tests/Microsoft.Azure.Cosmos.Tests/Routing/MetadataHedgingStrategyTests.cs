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
    using static Microsoft.Azure.Cosmos.Routing.MetadataHedgingStrategy;
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
            using MetadataHedgingStrategy strategy = BuildStrategy(customerOptIn: false);
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext();

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
            MetadataHedgingContext ctx = NewColdStartContext();

            MetadataHedgeEligibility result = strategy.EvaluateEligibility(req, ctx);

            Assert.IsFalse(result.IsEligible);
            Assert.AreEqual(MetadataHedgeSkipReason.GatewayKillSwitchOn, result.SkipReason);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void EvaluateEligibility_PpafDisabled_SkipsWithPpafDisabled()
        {
            using MetadataHedgingStrategy strategy = BuildStrategy(customerOptIn: null, ppafEnabled: false);
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext();

            MetadataHedgeEligibility result = strategy.EvaluateEligibility(req, ctx);

            Assert.IsFalse(result.IsEligible);
            Assert.AreEqual(MetadataHedgeSkipReason.PpafDisabled, result.SkipReason);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void EvaluateEligibility_NullOptIn_PpafEnabled_IsEligible()
        {
            using MetadataHedgingStrategy strategy = BuildStrategy(customerOptIn: null, ppafEnabled: true);
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext();

            MetadataHedgeEligibility result = strategy.EvaluateEligibility(req, ctx);

            Assert.IsTrue(result.IsEligible);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void EvaluateEligibility_ExplicitOptIn_PpafDisabled_IsEligible()
        {
            using MetadataHedgingStrategy strategy = BuildStrategy(customerOptIn: true, ppafEnabled: false);
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext();

            MetadataHedgeEligibility result = strategy.EvaluateEligibility(req, ctx);

            Assert.IsTrue(result.IsEligible);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void EvaluateEligibility_NotColdStart_StillEligible()
        {
            // Hedging is no longer gated on cold start: a steady-state refresh
            // read (IsColdStart == false) of a supported metadata type must be
            // eligible on the same terms as the cold-start read.
            using MetadataHedgingStrategy strategy = BuildStrategy();
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext();
            ctx.IsColdStart = false;

            MetadataHedgeEligibility result = strategy.EvaluateEligibility(req, ctx);

            Assert.IsTrue(result.IsEligible, "warm (non-cold-start) metadata reads must be eligible for hedging");
            Assert.AreEqual(MetadataHedgeSkipReason.None, result.SkipReason);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void EvaluateEligibility_NotColdStart_PartitionKeyRangeFirstPage_StillEligible()
        {
            // A warm PartitionKeyRange ReadFeed first page is still eligible; the
            // first-page gate is orthogonal to cold start.
            using MetadataHedgingStrategy strategy = BuildStrategy();
            DocumentServiceRequest req = DocumentServiceRequest.Create(
                OperationType.ReadFeed, ResourceType.PartitionKeyRange, AuthorizationTokenType.PrimaryMasterKey);
            MetadataHedgingContext ctx = NewWarmContext();

            MetadataHedgeEligibility result = strategy.EvaluateEligibility(req, ctx);

            Assert.IsTrue(result.IsEligible, "warm PKRange first-page reads must be eligible for hedging");
            Assert.AreEqual(MetadataHedgeSkipReason.None, result.SkipReason);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void EvaluateEligibility_NotColdStart_UnsupportedResource_StillSkipsByType()
        {
            // The request-type restriction must continue to apply for warm reads:
            // a non-metadata resource is rejected by type regardless of cold start.
            using MetadataHedgingStrategy strategy = BuildStrategy();
            DocumentServiceRequest req = DocumentServiceRequest.Create(
                OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            MetadataHedgingContext ctx = NewWarmContext();

            MetadataHedgeEligibility result = strategy.EvaluateEligibility(req, ctx);

            Assert.IsFalse(result.IsEligible);
            Assert.AreEqual(MetadataHedgeSkipReason.ResourceTypeNotSupported, result.SkipReason);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void EvaluateEligibility_AlreadyHedged_SkipsWithAlreadyHedgedThisOperation()
        {
            using MetadataHedgingStrategy strategy = BuildStrategy();
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext();
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
            MetadataHedgingContext ctx = NewColdStartContext();

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
            MetadataHedgingContext ctx = NewColdStartContext();
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
            MetadataHedgingContext ctx = NewColdStartContext();

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
            MetadataHedgingContext ctx = NewColdStartContext();

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
            MetadataHedgingContext ctx = NewColdStartContext();

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
            using MetadataHedgingStrategy strategy = BuildStrategy(customerOptIn: false);
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext();
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
            MetadataHedgingContext ctx = NewColdStartContext();
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
            MetadataHedgingContext ctx = NewColdStartContext();
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
        public async Task ExecuteAsync_WarmRead_PrimarySlowHedgeFast_HedgeWins()
        {
            // Regression for the broadened scope: a non-cold-start (refresh) read
            // must still dispatch a hedge when the primary is slow and return the
            // hedge winner — proving hedging is not limited to cold start.
            using MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromMilliseconds(30));
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewWarmContext();
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

            Assert.IsFalse(ctx.IsColdStart, "this regression specifically exercises the warm-read path");
            Assert.IsTrue(result.HedgeFired, "a warm (non-cold-start) read with a slow primary must still hedge");
            Assert.AreSame(hedgeResp, result.Response);
            Assert.AreEqual(HedgeEndpoint, result.WinningEndpoint);
            Assert.AreEqual(HedgeRegion, result.WinningRegion);

            await WaitForAsync(() => primaryResp.DisposeCount == 1, TimeSpan.FromSeconds(2));
            Assert.AreEqual(1, primaryResp.DisposeCount, "primary response should be disposed by background cleanup");
            Assert.AreEqual(0, hedgeResp.DisposeCount, "winning hedge response must NOT be disposed");
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ExecuteAsync_WarmRead_PrimaryWinsBeforeThreshold_DoesNotDispatchHedge()
        {
            // A warm read whose primary is healthy must NOT fire a hedge — the same
            // amplification guard that protects cold start protects refresh reads,
            // so a fast secondary cannot bombard the gateway on every refresh.
            using MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromMilliseconds(500));
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewWarmContext();
            int hedgeSends = 0;

            MetadataHedgingResult result = await strategy.ExecuteAsync(
                req,
                (r, u, ct) =>
                {
                    if (!u.Equals(PrimaryEndpoint))
                    {
                        Interlocked.Increment(ref hedgeSends);
                    }

                    return Task.FromResult<DocumentServiceResponse>(NewOkResponse());
                },
                ctx,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsFalse(result.HedgeFired, "a healthy warm-read primary must not trigger a hedge");
            Assert.AreEqual(0, hedgeSends, "no hedge request should be dispatched when the primary wins fast");
            Assert.AreEqual(PrimaryEndpoint, result.WinningEndpoint);
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ExecuteAsync_PrimaryFailsFastBeforeThreshold_HedgeFiresImmediately_HedgeWins()
        {
            // A deliberately large threshold guarantees the hedge timer never elapses on
            // its own. The only way the hedge can fire is the fast-fail fall-through: the
            // primary returns a non-acceptable (503) regional failure before the threshold,
            // so the strategy must dispatch the hedge immediately rather than wait.
            using MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromMilliseconds(500));
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext();
            TrackedResponse primaryResp = NewTrackedResponse(HttpStatusCode.ServiceUnavailable);
            TrackedResponse hedgeResp = NewTrackingOkResponse();

            MetadataHedgingResult result = await strategy.ExecuteAsync(
                req,
                async (r, u, ct) =>
                {
                    if (u.Equals(PrimaryEndpoint))
                    {
                        // Primary fails fast (regional failure) well before the threshold.
                        return primaryResp;
                    }

                    await Task.Delay(5, CancellationToken.None).ConfigureAwait(false);
                    return hedgeResp;
                },
                ctx,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsTrue(result.HedgeFired, "a degraded primary that fails fast must trigger the hedge, not bypass it");
            Assert.AreSame(hedgeResp, result.Response);
            Assert.AreEqual(HedgeEndpoint, result.WinningEndpoint);
            Assert.AreEqual(HedgeRegion, result.WinningRegion);

            // The hedge fired on the fast-fail path, so the recorded elapsed time is BELOW
            // the threshold (the timer was cancelled, not awaited) — guards the histogram
            // invariant for this branch.
            Assert.IsTrue(result.Diagnostics.HedgeFiredElapsedMs.HasValue, "hedge-fired elapsed should be recorded");
            Assert.IsTrue(
                result.Diagnostics.HedgeFiredElapsedMs.Value < result.Diagnostics.ThresholdMs,
                $"hedge-fired elapsed ({result.Diagnostics.HedgeFiredElapsedMs.Value}ms) should be below threshold ({result.Diagnostics.ThresholdMs}ms) on the fast-fail path");

            // The primary's 503 is the loser and must be disposed by background cleanup;
            // the winning hedge response must NOT be disposed.
            await WaitForAsync(() => primaryResp.DisposeCount == 1, TimeSpan.FromSeconds(2));
            Assert.AreEqual(1, primaryResp.DisposeCount, "primary 503 response should be disposed by background cleanup");
            Assert.AreEqual(0, hedgeResp.DisposeCount, "winning hedge response must NOT be disposed");
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ExecuteAsync_PrimaryAndHedge_RouteIndependentClonesToDistinctEndpoints()
        {
            using MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromMilliseconds(30));
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext();

            DocumentServiceRequest primaryBranchRequest = null;
            DocumentServiceRequest hedgeBranchRequest = null;
            TaskCompletionSource<DocumentServiceResponse> primaryGate =
                new TaskCompletionSource<DocumentServiceResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            MetadataHedgingResult result = await strategy.ExecuteAsync(
                req,
                async (r, u, ct) =>
                {
                    // Mirror the production caller: each branch routes the request it
                    // was handed. With a shared request this would race; with per-branch
                    // clones each routing is independent.
                    r.RequestContext.RouteToLocation(u);

                    if (u.Equals(PrimaryEndpoint))
                    {
                        primaryBranchRequest = r;

                        // Slow primary: stays pending until the hedge wins and cancels it.
                        using (ct.Register(() => primaryGate.TrySetResult(NewOkResponse())))
                        {
                            return await primaryGate.Task.ConfigureAwait(false);
                        }
                    }

                    hedgeBranchRequest = r;
                    await Task.Delay(5, CancellationToken.None).ConfigureAwait(false);
                    return NewOkResponse();
                },
                ctx,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsTrue(result.HedgeFired);
            Assert.AreEqual(HedgeEndpoint, result.WinningEndpoint);

            // The concurrent branches must each operate on an independent request clone,
            // not the shared original — otherwise one branch's RouteToLocation overwrites
            // the other's target region (no hedge benefit + corrupted region telemetry).
            Assert.IsNotNull(primaryBranchRequest);
            Assert.IsNotNull(hedgeBranchRequest);
            Assert.AreNotSame(primaryBranchRequest, hedgeBranchRequest, "primary and hedge must use distinct request clones");
            Assert.AreNotSame(req, primaryBranchRequest, "primary branch must not send the caller's original request");
            Assert.AreNotSame(req, hedgeBranchRequest, "hedge branch must not send the caller's original request");
            Assert.AreEqual(PrimaryEndpoint, primaryBranchRequest.RequestContext.LocationEndpointToRoute, "primary clone must stay routed to the primary endpoint");
            Assert.AreEqual(HedgeEndpoint, hedgeBranchRequest.RequestContext.LocationEndpointToRoute, "hedge clone must stay routed to the hedge endpoint");
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
            using MetadataHedgingStrategy strategy = BuildStrategy(
                threshold: TimeSpan.FromMilliseconds(50),
                perClientConcurrencyBudget: 1);

            DocumentServiceRequest req1 = BuildCollectionReadRequest();
            DocumentServiceRequest req2 = BuildCollectionReadRequest();
            MetadataHedgingContext ctx1 = NewColdStartContext();
            MetadataHedgingContext ctx2 = NewColdStartContext();

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
            using MetadataHedgingStrategy strategy = BuildStrategy(
                threshold: TimeSpan.FromMilliseconds(5),
                perClientConcurrencyBudget: 50);

            List<Task<MetadataHedgingResult>> tasks = new List<Task<MetadataHedgingResult>>();
            for (int i = 0; i < 50; i++)
            {
                DocumentServiceRequest req = BuildCollectionReadRequest();
                MetadataHedgingContext ctx = NewColdStartContext();
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
        // ExecuteAsync — fault paths (budget restoration / fallback)
        // ---------------------------------------------------------------

        [TestMethod]
        [Owner("dkunda")]
        public async Task ExecuteAsync_HedgeEndpointResolutionFaults_ReleasesBudgetPermit()
        {
            // Eligibility resolves the applicable endpoints once (returns both, so the request is
            // eligible and the budget permit is taken). The subsequent hedge-endpoint resolution then
            // throws — simulating a concurrent location-cache refresh/failover after the permit is held.
            // The permit must still be released (regression guard for the leak that would otherwise
            // drive AvailableBudget to 0 for the lifetime of the client).
            Mock<IGlobalEndpointManager> gem = new Mock<IGlobalEndpointManager>(MockBehavior.Loose);
            gem.Setup(g => g.ReadEndpoints).Returns(new ReadOnlyCollection<Uri>(new[] { PrimaryEndpoint, HedgeEndpoint }));
            gem.Setup(g => g.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>())).Returns(PrimaryEndpoint);
            gem.Setup(g => g.GetLocation(It.IsAny<Uri>())).Returns(PrimaryRegion);
            gem.SetupSequence(g => g.GetApplicableEndpoints(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>()))
                .Returns(new ReadOnlyCollection<Uri>(new[] { PrimaryEndpoint, HedgeEndpoint }))
                .Throws(new InvalidOperationException("location cache refresh in flight"));

            using MetadataHedgingStrategy strategy = new MetadataHedgingStrategy(
                globalEndpointManager: gem.Object,
                isHedgingDisabledByGateway: () => false,
                isPpafEnabled: () => true,
                customerOptIn: true,
                threshold: TimeSpan.FromMilliseconds(100));

            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext();

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => strategy.ExecuteAsync(
                req,
                (r, u, ct) => Task.FromResult(NewOkResponse()),
                ctx,
                NoOpTrace.Singleton,
                CancellationToken.None));

            Assert.AreEqual(
                strategy.PerClientConcurrencyBudget,
                strategy.AvailableBudget,
                "the hedge budget permit must be released when hedge-endpoint resolution faults");
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ExecuteAsync_BothBranchesRegionalFailure_PrefersPrimaryOutcome()
        {
            // Primary fails fast (503) before the threshold, so the hedge fires immediately; the hedge
            // also returns a regional failure (503). With neither branch an acceptable winner, the
            // strategy must prefer the primary's outcome (so the metadata retry policy classifies the
            // failure normally) and still record TotalAttempts == 2.
            using MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromMilliseconds(500));
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext();
            TrackedResponse primaryResp = NewTrackedResponse(HttpStatusCode.ServiceUnavailable);
            TrackedResponse hedgeResp = NewTrackedResponse(HttpStatusCode.ServiceUnavailable);

            MetadataHedgingResult result = await strategy.ExecuteAsync(
                req,
                async (r, u, ct) =>
                {
                    if (u.Equals(PrimaryEndpoint))
                    {
                        return primaryResp;
                    }

                    await Task.Delay(5, CancellationToken.None).ConfigureAwait(false);
                    return hedgeResp;
                },
                ctx,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsTrue(result.HedgeFired, "a degraded primary that fails fast must trigger the hedge");
            Assert.AreEqual(PrimaryEndpoint, result.WinningEndpoint, "both branches 503: prefer the primary outcome");
            Assert.AreEqual(PrimaryRegion, result.WinningRegion);
            Assert.AreEqual((int)HttpStatusCode.ServiceUnavailable, (int)result.Response.StatusCode);
            Assert.AreEqual(2, result.Diagnostics.TotalAttempts);

            // Loser (hedge 503) is disposed by background cleanup; the budget is restored.
            await WaitForAsync(() => strategy.AvailableBudget == strategy.PerClientConcurrencyBudget, TimeSpan.FromSeconds(2));
            Assert.AreEqual(strategy.PerClientConcurrencyBudget, strategy.AvailableBudget, "budget must be restored after a both-failure fallback");
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ExecuteAsync_FaultedWinner_RethrowsThroughObserveWinningTaskAndReleasesBudget()
        {
            // Primary throws fast (before the threshold), so the hedge fires; the hedge then returns a
            // regional failure (503). The fallback selects the (faulted) primary as the winner, which
            // must surface verbatim through ObserveWinningTaskAsync — and the budget must still be
            // released by the background cleanup of the loser.
            using MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromMilliseconds(500));
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext();
            TrackedResponse hedgeResp = NewTrackedResponse(HttpStatusCode.ServiceUnavailable);
            InvalidOperationException primaryFault = new InvalidOperationException("primary transport fault");

            InvalidOperationException thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => strategy.ExecuteAsync(
                req,
                async (r, u, ct) =>
                {
                    if (u.Equals(PrimaryEndpoint))
                    {
                        throw primaryFault;
                    }

                    await Task.Delay(5, CancellationToken.None).ConfigureAwait(false);
                    return hedgeResp;
                },
                ctx,
                NoOpTrace.Singleton,
                CancellationToken.None));

            Assert.AreSame(primaryFault, thrown, "the faulted winner's exception must propagate verbatim via ObserveWinningTaskAsync");

            await WaitForAsync(() => strategy.AvailableBudget == strategy.PerClientConcurrencyBudget, TimeSpan.FromSeconds(2));
            Assert.AreEqual(strategy.PerClientConcurrencyBudget, strategy.AvailableBudget, "budget must be restored after a faulted winner");
        }

        // ---------------------------------------------------------------
        // Cancellation behaviour
        // ---------------------------------------------------------------

        /// <summary>
        /// Validates the fix for the timerCts / phantom-hedge bug: when the caller cancels their
        /// token during the threshold window the strategy must throw OperationCanceledException
        /// (not silently dispatch a hedge or return a result) and must restore the budget permit.
        /// </summary>
        [TestMethod]
        [Owner("NaluTripician")]
        public async Task ExecuteAsync_CallerCancellationFiredDuringHedgeTimer_ThrowsOceAndRestoresBudget()
        {
            // Use a long threshold so the timer is still running when we cancel.
            using MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromSeconds(10));
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext();

            using CancellationTokenSource cts = new CancellationTokenSource();

            // Cancel after a short delay (well before the 10 s threshold).
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150)).ConfigureAwait(false);
                cts.Cancel();
            });

            // Primary blocks until it receives the cancellation signal.
            // TaskCanceledException (a subclass of OperationCanceledException) is expected; use
            // a try/catch rather than ThrowsExceptionAsync because MSTest checks the exact type.
            bool threw = false;
            try
            {
                await strategy.ExecuteAsync(
                    req,
                    async (r, u, ct) =>
                    {
                        await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                        return NewOkResponse(); // unreachable
                    },
                    ctx,
                    NoOpTrace.Singleton,
                    cts.Token);
            }
            catch (OperationCanceledException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "ExecuteAsync must throw OperationCanceledException when the caller's token is cancelled during the threshold window");

            // Budget permit must be fully restored; no slot was consumed permanently.
            await WaitForAsync(
                () => strategy.AvailableBudget == strategy.PerClientConcurrencyBudget,
                TimeSpan.FromSeconds(3));
            Assert.AreEqual(
                strategy.PerClientConcurrencyBudget,
                strategy.AvailableBudget,
                "budget permit must be released after caller cancels during the threshold window");
        }

        // ---------------------------------------------------------------
        // TryGetHedgeAuthRejectStatus — completed vs faulted classification
        // ---------------------------------------------------------------

        [TestMethod]
        [Owner("dkunda")]
        public void TryGetHedgeAuthRejectStatus_CompletedResponse_401Or403_ReturnsStatus()
        {
            DocumentServiceResponse unauthorized = new DocumentServiceResponse(
                Stream.Null, new StoreResponseNameValueCollection(), HttpStatusCode.Unauthorized);
            DocumentServiceResponse forbidden = new DocumentServiceResponse(
                Stream.Null, new StoreResponseNameValueCollection(), HttpStatusCode.Forbidden);

            Assert.AreEqual(HttpStatusCode.Unauthorized, MetadataHedgingStrategy.TryGetHedgeAuthRejectStatus(unauthorized, null));
            Assert.AreEqual(HttpStatusCode.Forbidden, MetadataHedgingStrategy.TryGetHedgeAuthRejectStatus(forbidden, null));
        }

        [TestMethod]
        [Owner("dkunda")]
        public void TryGetHedgeAuthRejectStatus_FaultedDocumentClientException_401Or403_ReturnsStatus()
        {
            // The GatewayStoreModel path throws 401/403 as a DocumentClientException rather than
            // returning a response, so the faulted-task branch must still surface the auth signal.
            AggregateException unauthorized = new AggregateException(
                new DocumentClientException("denied", null, HttpStatusCode.Unauthorized));
            AggregateException forbidden = new AggregateException(
                new DocumentClientException("denied", null, HttpStatusCode.Forbidden));

            Assert.AreEqual(HttpStatusCode.Unauthorized, MetadataHedgingStrategy.TryGetHedgeAuthRejectStatus(null, unauthorized));
            Assert.AreEqual(HttpStatusCode.Forbidden, MetadataHedgingStrategy.TryGetHedgeAuthRejectStatus(null, forbidden));
        }

        [TestMethod]
        [Owner("dkunda")]
        public void TryGetHedgeAuthRejectStatus_NonAuthOrNull_ReturnsNull()
        {
            DocumentServiceResponse ok = new DocumentServiceResponse(
                Stream.Null, new StoreResponseNameValueCollection(), HttpStatusCode.OK);
            AggregateException serverError = new AggregateException(
                new DocumentClientException("boom", null, HttpStatusCode.ServiceUnavailable));

            Assert.IsNull(MetadataHedgingStrategy.TryGetHedgeAuthRejectStatus(ok, null));
            Assert.IsNull(MetadataHedgingStrategy.TryGetHedgeAuthRejectStatus(null, serverError));
            Assert.IsNull(MetadataHedgingStrategy.TryGetHedgeAuthRejectStatus(null, null));
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
            MetadataHedgingContext ctx = NewColdStartContext();
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
            bool? customerOptIn = true,
            bool killSwitchOn = false,
            bool ppafEnabled = true,
            TimeSpan? threshold = null,
            int perClientConcurrencyBudget = MetadataHedgingStrategy.DefaultPerClientConcurrencyBudget)
        {
            gem ??= BuildEndpointManagerMock(new[] { PrimaryEndpoint, HedgeEndpoint }).Object;
            return new MetadataHedgingStrategy(
                globalEndpointManager: gem,
                isHedgingDisabledByGateway: () => killSwitchOn,
                isPpafEnabled: () => ppafEnabled,
                customerOptIn: customerOptIn,
                threshold: threshold ?? TimeSpan.FromMilliseconds(100),
                perClientConcurrencyBudget: perClientConcurrencyBudget);
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

        private static MetadataHedgingContext NewColdStartContext()
        {
            return new MetadataHedgingContext
            {
                IsColdStart = true,
                IsFirstReadFeedPage = true,
            };
        }

        private static MetadataHedgingContext NewWarmContext()
        {
            return new MetadataHedgingContext
            {
                IsColdStart = false,
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
        // Opt-in resolver (follows PPAF when customer setting is null)
        // ---------------------------------------------------------------

        [TestMethod]
        [Owner("dkunda")]
        public void ResolveOptIn_NullCustomerSetting_FollowsPpafState()
        {
            Assert.IsTrue(MetadataHedgingStrategy.ResolveOptIn(null, isPpafEnabled: true));
            Assert.IsFalse(MetadataHedgingStrategy.ResolveOptIn(null, isPpafEnabled: false));
        }

        [TestMethod]
        [Owner("dkunda")]
        public void ResolveOptIn_ExplicitTrue_OverridesPpafState()
        {
            Assert.IsTrue(MetadataHedgingStrategy.ResolveOptIn(true, isPpafEnabled: false));
        }

        [TestMethod]
        [Owner("dkunda")]
        public void ResolveOptIn_ExplicitFalse_OverridesPpafState()
        {
            Assert.IsFalse(MetadataHedgingStrategy.ResolveOptIn(false, isPpafEnabled: true));
        }

        // ---------------------------------------------------------------
        // CreateIfEnabled — wiring from CosmosClientOptions (Stage 7)
        // ---------------------------------------------------------------

        [TestMethod]
        [Owner("dkunda")]
        public void CreateIfEnabled_NullOptIn_BuildsStrategy_DefersToPpafAtRequestTime()
        {
            using MetadataHedgingStrategy strategy = MetadataHedgingStrategy.CreateIfEnabled(
                enableMetadataHedgingForColdStart: null,
                globalEndpointManager: BuildEndpointManagerMock(new[] { PrimaryEndpoint, HedgeEndpoint }).Object,
                isPpafEnabled: () => true);

            Assert.IsNotNull(strategy);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void CreateIfEnabled_ExplicitFalse_ReturnsNull()
        {
            MetadataHedgingStrategy strategy = MetadataHedgingStrategy.CreateIfEnabled(
                enableMetadataHedgingForColdStart: false,
                globalEndpointManager: BuildEndpointManagerMock(new[] { PrimaryEndpoint }).Object,
                isPpafEnabled: () => true);

            Assert.IsNull(strategy);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void CreateIfEnabled_ExplicitTrue_BuildsStrategyWithDefaultThreshold()
        {
            using MetadataHedgingStrategy strategy = MetadataHedgingStrategy.CreateIfEnabled(
                enableMetadataHedgingForColdStart: true,
                globalEndpointManager: BuildEndpointManagerMock(new[] { PrimaryEndpoint, HedgeEndpoint }).Object,
                isPpafEnabled: () => true);

            Assert.IsNotNull(strategy);
            Assert.AreEqual(
                HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance.FirstAttemptTimeout
                    + MetadataHedgingStrategy.DefaultThresholdStep,
                strategy.Threshold);
            Assert.AreEqual(MetadataHedgingStrategy.DefaultPerClientConcurrencyBudget, strategy.PerClientConcurrencyBudget);
        }

        private static IGlobalEndpointManager BuildMockGlobalEndpointManager()
        {
            Mock<IGlobalEndpointManager> mock = new Mock<IGlobalEndpointManager>(MockBehavior.Loose);
            return mock.Object;
        }

        // ---------------------------------------------------------------
        // Environment-variable opt-in resolution
        // (AZURE_COSMOS_METADATA_HEDGING_ENABLED)
        // ---------------------------------------------------------------

        private const string MetadataHedgingEnvVar = "AZURE_COSMOS_METADATA_HEDGING_ENABLED";

        [TestMethod]
        [Owner("dkunda")]
        [DataRow("true", true)]
        [DataRow("True", true)]
        [DataRow("false", false)]
        public void GetMetadataHedgingOptIn_NullOption_UsesEnvironmentVariable(string envValue, bool expected)
        {
            RunWithMetadataHedgingEnvVar(envValue, () =>
            {
                bool? resolved = Microsoft.Azure.Cosmos.ConfigurationManager.GetMetadataHedgingOptIn();
                Assert.AreEqual(expected, resolved);
            });
        }

        [TestMethod]
        [Owner("dkunda")]
        public void GetMetadataHedgingOptIn_NullOption_UnsetEnv_ReturnsNull_FollowsPpaf()
        {
            RunWithMetadataHedgingEnvVar(null, () =>
            {
                Assert.IsNull(Microsoft.Azure.Cosmos.ConfigurationManager.GetMetadataHedgingOptIn());
            });
        }

        [TestMethod]
        [Owner("dkunda")]
        public void GetMetadataHedgingOptIn_NullOption_NonBooleanEnv_ReturnsNull()
        {
            RunWithMetadataHedgingEnvVar("not-a-bool", () =>
            {
                Assert.IsNull(Microsoft.Azure.Cosmos.ConfigurationManager.GetMetadataHedgingOptIn());
            });
        }

        private static void RunWithMetadataHedgingEnvVar(string value, Action action)
        {
            string original = Environment.GetEnvironmentVariable(MetadataHedgingEnvVar);
            try
            {
                Environment.SetEnvironmentVariable(MetadataHedgingEnvVar, value);
                action();
            }
            finally
            {
                Environment.SetEnvironmentVariable(MetadataHedgingEnvVar, original);
            }
        }
    }
}
