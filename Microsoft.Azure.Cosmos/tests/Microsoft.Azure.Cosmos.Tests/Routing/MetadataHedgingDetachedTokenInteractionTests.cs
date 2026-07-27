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

    /// <summary>
    /// Confirms the interaction between cross-region metadata hedging
    /// (<see cref="MetadataHedgingStrategy"/>, PR #5999) and the detached metadata
    /// executor (<see cref="MetadataDetachedExecutor"/>, PR #5844) on the shared
    /// <c>ClientCollectionCache</c> Collection-read path.
    ///
    /// <para>
    /// After PR #5844, <c>ClientCollectionCache.GetByRidAsync</c> / <c>GetByNameAsync</c>
    /// invoke:
    /// <code>
    ///   MetadataDetachedExecutor.ExecuteAsync(
    ///       (detachedToken) =&gt; ReadCollectionAsync(..., detachedToken),   // detached CT
    ///       retryPolicyInstance,
    ///       callerCancellationToken)
    /// </code>
    /// and <c>ReadCollectionAsync</c> passes that <b>detached</b> token straight into
    /// <c>metadataHedgingStrategy.ExecuteAsync(..., cancellationToken: cancellationToken)</c>
    /// (see <c>ClientCollectionCache.cs</c>, the <c>metadataHedgingStrategy.ExecuteAsync</c>
    /// call inside <c>ReadCollectionAsync</c>). <c>ReadCollectionAsync</c> is a
    /// token-transparent wrapper between the two components — it forwards the token it is
    /// given without substitution — so the faithful reduction of the production nesting is
    /// <c>ExecuteAsync(ct =&gt; strategy.ExecuteAsync(..., ct), retryPolicy, callerCT)</c>,
    /// which is exactly what these tests exercise with the two <b>real</b> components.
    /// </para>
    ///
    /// <para>
    /// The sibling <see cref="ClientCollectionCacheDetachedWiringTests"/> already pins that
    /// the detached token reaches <c>ReadCollectionAsync</c> through the real cache, but it
    /// wires <b>no</b> hedging strategy (the constructor default is <c>null</c>), so it takes
    /// the non-hedge <c>storeModel.ProcessMessageAsync</c> branch. These tests cover the
    /// remaining, hedge-specific claim raised on the PR #5844 thread: <b>the metadata hedge
    /// runs on the executor's detached token</b>, so a caller cancel surfaces
    /// <see cref="OperationCanceledException"/> on the response path <i>without</i> tearing
    /// down the in-flight hedge, which continues to completion in the background.
    /// </para>
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class MetadataHedgingDetachedTokenInteractionTests
    {
        private static readonly Uri Region1 = new Uri("https://region1.documents.azure.com/");
        private static readonly Uri Region2 = new Uri("https://region2.documents.azure.com/");

        private static readonly TimeSpan ShortThreshold = TimeSpan.FromMilliseconds(150);
        private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Primary confirmation: with the real executor wrapping the real hedging strategy
        /// (the production nesting), the hedge send observes the executor's <b>detached</b>
        /// token — not the caller token — and a mid-flight caller cancel:
        /// <list type="number">
        ///   <item>surfaces <see cref="OperationCanceledException"/> to the caller, yet</item>
        ///   <item>does NOT cancel the detached token the hedge is running on, and</item>
        ///   <item>leaves the hedge running to completion in the background (its side-effects
        ///   accrue), which the executor also surfaces via
        ///   <see cref="MetadataDetachedExecutor.LiveDetachedBackgroundReads"/>.</item>
        /// </list>
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public async Task DetachedExecutorOverHedgingStrategy_HedgeSendRunsOnDetachedToken_SurvivesCallerCancel()
        {
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: true, ppafEnabled: true, optIn: null);
            IDocumentClientRetryPolicy retryPolicy = new Mock<IDocumentClientRetryPolicy>().Object;

            CancellationToken observedPrimaryToken = default;
            CancellationToken observedHedgeToken = default;

            TaskCompletionSource<bool> hedgeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> releaseHedge = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> hedgeSendCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> releasePrimary = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using CancellationTokenSource callerCts = new CancellationTokenSource();

            long baselineLiveReads = MetadataDetachedExecutor.LiveDetachedBackgroundReads;

            // Production nesting: executor(detachedToken => hedgingStrategy.ExecuteAsync(..., detachedToken)).
            Task<MetadataHedgingStrategy.MetadataHedgingResult> callerTask = MetadataDetachedExecutor.ExecuteAsync(
                operation: (detachedToken) => strategy.ExecuteAsync(
                    CreateCollectionReadRequest(),
                    sendToEndpoint: async (request, endpoint, ct) =>
                    {
                        if (endpoint.Equals(Region1))
                        {
                            // Primary: slow past the threshold so a hedge fires, then held so the
                            // hedge is the winner. Capture the token it observed.
                            observedPrimaryToken = ct;
                            await releasePrimary.Task;
                            return CreateResponse(HttpStatusCode.OK);
                        }

                        // Hedge branch (Region2): capture its token, signal it dispatched, then stay
                        // in flight until the test releases it (AFTER cancelling the caller).
                        observedHedgeToken = ct;
                        hedgeStarted.TrySetResult(true);
                        await releaseHedge.Task;
                        hedgeSendCompleted.TrySetResult(true);
                        return CreateResponse(HttpStatusCode.OK);
                    },
                    isFirstReadFeedPage: true,
                    cancellationToken: detachedToken),
                retryPolicy: retryPolicy,
                callerCancellationToken: callerCts.Token);

            // Wait until the hedge actually dispatched (threshold elapsed) so its token is captured.
            await AwaitOrFailAsync(hedgeStarted.Task, "hedge did not fire before the timeout");

            // The hedge is dispatched on the executor's detached token, NOT the caller token.
            Assert.AreNotEqual(callerCts.Token, observedHedgeToken, "Hedge must not run on the caller token.");
            Assert.AreEqual(observedPrimaryToken, observedHedgeToken, "Primary and hedge share the executor's single detached token.");
            Assert.IsFalse(observedHedgeToken.IsCancellationRequested, "The detached token must not be cancelled before the caller cancels.");

            // Caller cancels mid-flight while the hedge is still in flight.
            callerCts.Cancel();

            // (1) The caller surfaces OperationCanceledException on the response path.
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => callerTask);

            // (2) THE CORE CLAIM: the detached token the hedge runs on is NOT cancelled by the
            //     caller cancel — the hedge is decoupled from caller cancellation.
            Assert.IsFalse(
                observedHedgeToken.IsCancellationRequested,
                "Caller cancel must NOT cancel the detached token the hedge is running on.");
            Assert.IsFalse(
                hedgeSendCompleted.Task.IsCompleted,
                "The hedge send should still be in flight (held) immediately after the caller cancelled.");

            // (3) The executor surfaces the caller-abandoned read as a live detached background read.
            Assert.IsTrue(
                MetadataDetachedExecutor.LiveDetachedBackgroundReads > baselineLiveReads,
                "The orphaned detached read should be counted in LiveDetachedBackgroundReads.");

            // Now let the background hedge finish: it runs to completion despite the caller cancel,
            // so its side-effects accrue for later callers (the detach design's benefit).
            releaseHedge.TrySetResult(true);
            releasePrimary.TrySetResult(true);

            await AwaitOrFailAsync(hedgeSendCompleted.Task, "the detached hedge did not run to completion after being released");

            // The live count drains back once the detached read completes (best-effort; the
            // decrement runs on a background continuation).
            await SpinUntilAsync(
                () => MetadataDetachedExecutor.LiveDetachedBackgroundReads <= baselineLiveReads,
                "LiveDetachedBackgroundReads did not drain back to baseline");
        }

        /// <summary>
        /// Contrast baseline documenting the exact delta PR #5844 introduces on the Collection
        /// path. Calling the hedging strategy <b>directly</b> with the caller token (the
        /// pre-#5844 behavior, where <c>ReadCollectionAsync</c> forwarded the caller's
        /// <c>cancellationToken</c>) makes the hedge send observe the <b>caller</b> token, so a
        /// caller cancel flips the very token the hedge is running on. #5844 removes exactly
        /// this coupling by interposing the detached token (verified in the test above).
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public async Task HedgingStrategyDirectOnCallerToken_HedgeSendObservesCallerToken_CancelledByCallerCancel()
        {
            MetadataHedgingStrategy strategy = BuildStrategy(multiRegion: true, ppafEnabled: true, optIn: null);

            CancellationToken observedHedgeToken = default;

            TaskCompletionSource<bool> hedgeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> releaseHedge = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> releasePrimary = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using CancellationTokenSource callerCts = new CancellationTokenSource();

            // Direct call with the caller token — no detached executor interposed.
            Task<MetadataHedgingStrategy.MetadataHedgingResult> execution = strategy.ExecuteAsync(
                CreateCollectionReadRequest(),
                sendToEndpoint: async (request, endpoint, ct) =>
                {
                    if (endpoint.Equals(Region1))
                    {
                        await releasePrimary.Task;
                        return CreateResponse(HttpStatusCode.OK);
                    }

                    observedHedgeToken = ct;
                    hedgeStarted.TrySetResult(true);
                    await releaseHedge.Task;
                    return CreateResponse(HttpStatusCode.OK);
                },
                isFirstReadFeedPage: true,
                cancellationToken: callerCts.Token);

            await AwaitOrFailAsync(hedgeStarted.Task, "hedge did not fire before the timeout");

            // Without the executor, the hedge observes the caller's own token.
            Assert.AreEqual(callerCts.Token, observedHedgeToken, "Direct path: the hedge observes the caller token.");
            Assert.IsFalse(observedHedgeToken.IsCancellationRequested);

            callerCts.Cancel();

            // The same token the hedge is running on is now cancelled — the coupling #5844 removes.
            Assert.IsTrue(
                observedHedgeToken.IsCancellationRequested,
                "Direct path: caller cancel cancels the hedge's token (the coupling #5844 decouples).");

            // Cleanup: release the held sends so the strategy task completes.
            releaseHedge.TrySetResult(true);
            releasePrimary.TrySetResult(true);
            try
            {
                await execution;
            }
            catch
            {
                // The winner/outcome is irrelevant to this contrast; only the observed token matters.
            }
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

        private static DocumentServiceResponse CreateResponse(HttpStatusCode statusCode)
        {
            return new DocumentServiceResponse(
                null,
                new StoreResponseNameValueCollection(),
                statusCode);
        }

        private static async Task AwaitOrFailAsync(Task task, string failureMessage)
        {
            Task winner = await Task.WhenAny(task, Task.Delay(GateTimeout));
            if (winner != task)
            {
                Assert.Fail($"Timed out waiting for: {failureMessage}");
            }

            await task;
        }

        private static async Task SpinUntilAsync(Func<bool> predicate, string description)
        {
            DateTime deadline = DateTime.UtcNow + GateTimeout;
            while (!predicate() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            if (!predicate())
            {
                Assert.Fail($"Timed out waiting for: {description}");
            }
        }
    }
}
