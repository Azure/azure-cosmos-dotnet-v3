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
        public void EvaluateEligibility_NotColdStart_SkipsWithNotColdStart()
        {
            using MetadataHedgingStrategy strategy = BuildStrategy();
            DocumentServiceRequest req = BuildCollectionReadRequest();
            MetadataHedgingContext ctx = NewColdStartContext();
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
            MetadataHedgingOptions options = new MetadataHedgingOptions
            {
                PerClientConcurrencyBudget = 1,
            };
            using MetadataHedgingStrategy strategy = BuildStrategy(
                threshold: TimeSpan.FromMilliseconds(50),
                options: options);

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
            MetadataHedgingOptions options = null)
        {
            gem ??= BuildEndpointManagerMock(new[] { PrimaryEndpoint, HedgeEndpoint }).Object;
            return new MetadataHedgingStrategy(
                globalEndpointManager: gem,
                isHedgingDisabledByGateway: () => killSwitchOn,
                isPpafEnabled: () => ppafEnabled,
                customerOptIn: customerOptIn,
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

        private static MetadataHedgingContext NewColdStartContext()
        {
            return new MetadataHedgingContext
            {
                IsColdStart = true,
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
                options: null,
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
                options: null,
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
                options: null,
                globalEndpointManager: BuildEndpointManagerMock(new[] { PrimaryEndpoint, HedgeEndpoint }).Object,
                isPpafEnabled: () => true);

            Assert.IsNotNull(strategy);
            Assert.AreEqual(
                HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance.FirstAttemptTimeout
                    + MetadataHedgingOptions.DefaultThresholdStep,
                strategy.Threshold);
            Assert.AreEqual(MetadataHedgingOptions.DefaultPerClientConcurrencyBudget, strategy.PerClientConcurrencyBudget);
        }

        [TestMethod]
        [Owner("dkunda")]
        public void CreateIfEnabled_ExplicitTrue_HonorsCustomerThresholdAndBudget()
        {
            MetadataHedgingOptions options = new MetadataHedgingOptions
            {
                Threshold = TimeSpan.FromMilliseconds(750),
                PerClientConcurrencyBudget = 16,
            };

            using MetadataHedgingStrategy strategy = MetadataHedgingStrategy.CreateIfEnabled(
                enableMetadataHedgingForColdStart: true,
                options: options,
                globalEndpointManager: BuildEndpointManagerMock(new[] { PrimaryEndpoint, HedgeEndpoint }).Object,
                isPpafEnabled: () => true);

            Assert.IsNotNull(strategy);
            Assert.AreEqual(TimeSpan.FromMilliseconds(750), strategy.Threshold);
            Assert.AreEqual(16, strategy.PerClientConcurrencyBudget);
        }

        private static IGlobalEndpointManager BuildMockGlobalEndpointManager()
        {
            Mock<IGlobalEndpointManager> mock = new Mock<IGlobalEndpointManager>(MockBehavior.Loose);
            return mock.Object;
        }
    }
}
