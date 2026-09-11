// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.DistributedTransaction
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="DistributedTransactionDispatchTracker"/>, which owns the emission rules
    /// for the <c>x-ms-cosmos-internal-is-dtx-retry</c> and
    /// <c>x-ms-cosmos-internal-is-dtx-cross-region-redirect</c> headers.
    ///
    /// Two normative points a regression would silently break: the first dispatch of an idempotency
    /// token reports neither signal, and the cross-region signal is sticky for the lifetime of that
    /// token. Rotation starting a token on a fresh tracker is
    /// <see cref="DistributedTransactionServerRequestTests"/>' concern.
    /// </summary>
    [TestClass]
    public class DistributedTransactionDispatchTrackerTests
    {
        private const string EastUs = "East US";
        private const string WestUs = "West US";

        [TestMethod]
        public void RecordDispatch_FirstDispatchOfToken_ReportsNeitherSignal()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            (bool IsRetry, bool IsCrossRegionRedirect) signals = tracker.RecordDispatch(EastUs);

            Assert.IsFalse(signals.IsRetry);
            Assert.IsFalse(signals.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void RecordDispatch_RegionUnchanged_IsRetryWithoutCrossingBoundary()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(EastUs);
            (bool IsRetry, bool IsCrossRegionRedirect) signals = tracker.RecordDispatch(EastUs);

            Assert.IsTrue(signals.IsRetry);
            Assert.IsFalse(signals.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void RecordDispatch_RegionDiffersFromOriginalDispatch_CrossesBoundary()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(EastUs);
            (bool IsRetry, bool IsCrossRegionRedirect) signals = tracker.RecordDispatch(WestUs);

            Assert.IsTrue(signals.IsRetry);
            Assert.IsTrue(signals.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void RecordDispatch_AfterCrossingBoundary_StaysTrueWithinNewRegion()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(WestUs);
            (bool IsRetry, bool IsCrossRegionRedirect) signals = tracker.RecordDispatch(WestUs);

            Assert.IsTrue(signals.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void RecordDispatch_AfterCrossingBoundary_StaysTrueWhenRoutedBackToOriginRegion()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(WestUs);
            (bool IsRetry, bool IsCrossRegionRedirect) signals = tracker.RecordDispatch(EastUs);

            Assert.IsTrue(signals.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void RecordDispatch_RegionsComparedCaseInsensitively_DoesNotCrossBoundary()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(EastUs);
            (bool IsRetry, bool IsCrossRegionRedirect) signals = tracker.RecordDispatch("east us");

            Assert.IsFalse(signals.IsCrossRegionRedirect);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        public void RecordDispatch_UnresolvableFirstRegion_ReportsRedirectOnceNextRegionResolves(string region)
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            (bool IsRetry, bool IsCrossRegionRedirect) first = tracker.RecordDispatch(region);
            Assert.IsFalse(first.IsCrossRegionRedirect);

            (bool IsRetry, bool IsCrossRegionRedirect) next = tracker.RecordDispatch(EastUs);
            Assert.IsTrue(next.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void RecordDispatch_UnresolvableRegionAfterKnownRegion_ReportsRedirect()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(EastUs);

            (bool IsRetry, bool IsCrossRegionRedirect) unresolved = tracker.RecordDispatch(null);
            Assert.IsTrue(
                unresolved.IsCrossRegionRedirect,
                "An unresolved retry may have crossed a region boundary, so it must report the safe conservative signal.");

            (bool IsRetry, bool IsCrossRegionRedirect) next = tracker.RecordDispatch(WestUs);
            Assert.IsTrue(next.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void RecordDispatch_UnresolvableRegionAfterCrossingBoundary_StaysTrue()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(WestUs);
            (bool IsRetry, bool IsCrossRegionRedirect) signals = tracker.RecordDispatch(null);

            Assert.IsTrue(signals.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void RecordDispatch_UnresolvableRegion_StillCountsTowardsRetry()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            // Retry-ness is a property of the token, not of where the dispatch landed.
            (bool IsRetry, bool IsCrossRegionRedirect) first = tracker.RecordDispatch(null);
            Assert.IsFalse(first.IsRetry);

            (bool IsRetry, bool IsCrossRegionRedirect) next = tracker.RecordDispatch(null);
            Assert.IsTrue(next.IsRetry);
            Assert.IsTrue(
                next.IsCrossRegionRedirect,
                "Two unresolved dispatches cannot be proven to have stayed in one region.");
        }

        [TestMethod]
        public void RecordDispatch_ReturnedSnapshot_DoesNotChangeAfterLaterDispatches()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();
            (bool IsRetry, bool IsCrossRegionRedirect) first = tracker.RecordDispatch(EastUs);
            (bool IsRetry, bool IsCrossRegionRedirect) sameRegion = tracker.RecordDispatch(EastUs);
            (bool IsRetry, bool IsCrossRegionRedirect) crossRegion = tracker.RecordDispatch(WestUs);

            Assert.AreEqual((false, false), first);
            Assert.AreEqual((true, false), sameRegion);
            Assert.AreEqual((true, true), crossRegion);
        }

        [TestMethod]
        public void StampDispatchHeaders_TrackerPresent_ReportsRetryBeforeCrossingBoundary()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            using (DocumentServiceRequest request = DistributedTransactionDispatchTrackerTests.CreateRequest())
            {
                tracker.StampDispatchHeaders(request, EastUs);
                DistributedTransactionDispatchTrackerTests.AssertHeaders(request, bool.FalseString, bool.FalseString);

                tracker.StampDispatchHeaders(request, EastUs);
                DistributedTransactionDispatchTrackerTests.AssertHeaders(request, bool.TrueString, bool.FalseString);

                tracker.StampDispatchHeaders(request, WestUs);
                DistributedTransactionDispatchTrackerTests.AssertHeaders(request, bool.TrueString, bool.TrueString);
            }
        }

        [TestMethod]
        public void RequestMessageClone_PreservesTypedTracker()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();
            using (RequestMessage request = new RequestMessage
            {
                ResourceType = ResourceType.DistributedTransactionBatch,
                OperationType = OperationType.CommitDistributedTransaction,
                DistributedTransactionDispatchTracker = tracker,
            })
            using (RequestMessage clone = request.Clone(request.Trace, cloneContent: null))
            {
                Assert.AreSame(tracker, clone.DistributedTransactionDispatchTracker);
            }
        }

        [TestMethod]
        public void StampDispatchHeaders_NullRequest_ThrowsWithoutRecordingDispatch()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => tracker.StampDispatchHeaders(null, EastUs));

            Assert.AreEqual("request", exception.ParamName);
            Assert.AreEqual((false, false), tracker.RecordDispatch(WestUs));
        }

        [TestMethod]
        public void StampDispatchHeaders_UnresolvableRegionAfterCrossing_KeepsBothHeadersTrue()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            using (DocumentServiceRequest request = DistributedTransactionDispatchTrackerTests.CreateRequest())
            {
                tracker.StampDispatchHeaders(request, EastUs);
                tracker.StampDispatchHeaders(request, WestUs);

                tracker.StampDispatchHeaders(request, null);

                DistributedTransactionDispatchTrackerTests.AssertHeaders(request, bool.TrueString, bool.TrueString);
            }
        }

        [TestMethod]
        public void StampDispatchHeaders_ConcurrentRequests_UseConsistentSignalSnapshots()
        {
            const int DispatchCount = 1000;
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();
            int firstDispatchCount = 0;
            int inconsistentSnapshotCount = 0;

            Parallel.For(
                0,
                DispatchCount,
                dispatchIndex =>
                {
                    using (DocumentServiceRequest request =
                        DistributedTransactionDispatchTrackerTests.CreateRequest())
                    {
                        string regionName = dispatchIndex % 2 == 0
                            ? DistributedTransactionDispatchTrackerTests.EastUs
                            : DistributedTransactionDispatchTrackerTests.WestUs;

                        tracker.StampDispatchHeaders(request, regionName);

                        bool isRetry = bool.Parse(
                            request.Headers[DistributedTransactionConstants.IsDtxRetry]);
                        bool isCrossRegionRedirect = bool.Parse(
                            request.Headers[DistributedTransactionConstants.IsDtxCrossRegionRedirect]);

                        if (!isRetry)
                        {
                            Interlocked.Increment(ref firstDispatchCount);
                        }

                        if (!isRetry && isCrossRegionRedirect)
                        {
                            Interlocked.Increment(ref inconsistentSnapshotCount);
                        }
                    }
                });

            Assert.AreEqual(1, firstDispatchCount);
            Assert.AreEqual(0, inconsistentSnapshotCount);
            (bool IsRetry, bool IsCrossRegionRedirect) final = tracker.RecordDispatch(EastUs);
            Assert.IsTrue(final.IsRetry);
            Assert.IsTrue(final.IsCrossRegionRedirect);
        }

        private static void AssertHeaders(
            DocumentServiceRequest request,
            string expectedIsRetry,
            string expectedIsCrossRegionRedirect)
        {
            Assert.AreEqual(expectedIsRetry, request.Headers[DistributedTransactionConstants.IsDtxRetry]);
            Assert.AreEqual(expectedIsCrossRegionRedirect, request.Headers[DistributedTransactionConstants.IsDtxCrossRegionRedirect]);
        }

        private static DocumentServiceRequest CreateRequest()
        {
            return DocumentServiceRequest.Create(
                OperationType.CommitDistributedTransaction,
                ResourceType.DistributedTransactionBatch,
                AuthorizationTokenType.PrimaryMasterKey);
        }
    }
}
