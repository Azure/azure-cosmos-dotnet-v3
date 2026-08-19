// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.DistributedTransaction
{
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="DistributedTransactionDispatchTracker"/>, which owns the emission rules
    /// for the <c>x-ms-cosmos-internal-is-dtx-retry</c> and
    /// <c>x-ms-cosmos-internal-is-dtx-cross-region-redirect</c> headers.
    ///
    /// Three normative points a regression would silently break: the first dispatch of an idempotency
    /// token reports neither signal, the cross-region signal is sticky for the lifetime of that token,
    /// and both signals reset when the token rotates.
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

            tracker.RecordDispatch(EastUs);

            Assert.IsFalse(tracker.IsRetry);
            Assert.IsFalse(tracker.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void RecordDispatch_RegionUnchanged_IsRetryWithoutCrossingBoundary()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(EastUs);

            Assert.IsTrue(tracker.IsRetry);
            Assert.IsFalse(tracker.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void RecordDispatch_RegionDiffersFromOriginalDispatch_CrossesBoundary()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(WestUs);

            Assert.IsTrue(tracker.IsRetry);
            Assert.IsTrue(tracker.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void RecordDispatch_AfterCrossingBoundary_StaysTrueWithinNewRegion()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(WestUs);
            tracker.RecordDispatch(WestUs);

            Assert.IsTrue(tracker.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void RecordDispatch_AfterCrossingBoundary_StaysTrueWhenRoutedBackToOriginRegion()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(WestUs);
            tracker.RecordDispatch(EastUs);

            Assert.IsTrue(tracker.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void RecordDispatch_RegionsComparedCaseInsensitively_DoesNotCrossBoundary()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch("east us");

            Assert.IsFalse(tracker.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void RecordDispatch_UnresolvableRegionBeforeAnyKnownRegion_ReportsRedirectOnceOneResolves()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(null);
            tracker.RecordDispatch(string.Empty);

            // Nothing has been placed in a known region yet, so there is no boundary to have crossed.
            Assert.IsFalse(tracker.IsCrossRegionRedirect);

            // Those dispatches may have landed elsewhere, so East US cannot be trusted as the origin.
            tracker.RecordDispatch(EastUs);
            Assert.IsTrue(tracker.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void RecordDispatch_UnresolvableRegionAfterKnownRegion_DoesNotDiscardOriginRegion()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(EastUs);

            tracker.RecordDispatch(null);
            Assert.IsFalse(tracker.IsCrossRegionRedirect);

            // The unresolvable dispatch must not have overwritten East US, so West US still crosses.
            tracker.RecordDispatch(WestUs);
            Assert.IsTrue(tracker.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void RecordDispatch_UnresolvableRegionAfterCrossingBoundary_StaysTrue()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(WestUs);
            tracker.RecordDispatch(null);

            Assert.IsTrue(tracker.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void RecordDispatch_UnresolvableRegion_StillCountsTowardsRetry()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            // Retry-ness is a property of the token, not of where the dispatch landed.
            tracker.RecordDispatch(null);
            Assert.IsFalse(tracker.IsRetry);

            tracker.RecordDispatch(null);
            Assert.IsTrue(tracker.IsRetry);
            Assert.IsFalse(tracker.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void ResetForNewToken_AfterCrossingBoundary_ClearsStickySignal()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(WestUs);
            Assert.IsTrue(tracker.IsCrossRegionRedirect);

            tracker.ResetForNewToken();

            tracker.RecordDispatch(WestUs);
            Assert.IsFalse(tracker.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void ResetForNewToken_ClearsRetrySignal()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(EastUs);
            Assert.IsTrue(tracker.IsRetry);

            tracker.ResetForNewToken();

            tracker.RecordDispatch(EastUs);
            Assert.IsFalse(tracker.IsRetry);
        }

        [TestMethod]
        public void ResetForNewToken_ClearsOriginalDispatchRegion()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(EastUs);
            tracker.ResetForNewToken();

            // The new token has no record in any region, so this is a first dispatch, not a crossing.
            tracker.RecordDispatch(WestUs);
            Assert.IsFalse(tracker.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void ResetForNewToken_AfterUnresolvableDispatch_DoesNotChargeItToTheNewToken()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            tracker.RecordDispatch(null);
            tracker.ResetForNewToken();

            // The rotated token has never been dispatched anywhere, so the unnamed dispatch its
            // predecessor made cannot make the new token's first dispatch a crossing.
            tracker.RecordDispatch(EastUs);
            Assert.IsFalse(tracker.IsCrossRegionRedirect);
        }

        [TestMethod]
        public void StampDispatchHeaders_TrackerPresent_ReportsRetryBeforeCrossingBoundary()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            using (DocumentServiceRequest request = DistributedTransactionDispatchTrackerTests.CreateRequestWithTracker(tracker))
            {
                DistributedTransactionDispatchTracker.StampDispatchHeaders(request, EastUs);
                DistributedTransactionDispatchTrackerTests.AssertHeaders(request, bool.FalseString, bool.FalseString);

                DistributedTransactionDispatchTracker.StampDispatchHeaders(request, EastUs);
                DistributedTransactionDispatchTrackerTests.AssertHeaders(request, bool.TrueString, bool.FalseString);

                DistributedTransactionDispatchTracker.StampDispatchHeaders(request, WestUs);
                DistributedTransactionDispatchTrackerTests.AssertHeaders(request, bool.TrueString, bool.TrueString);
            }
        }

        [TestMethod]
        public void StampDispatchHeaders_NoTrackerInProperties_OmitsBothHeaders()
        {
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.DistributedTransactionBatch,
                AuthorizationTokenType.PrimaryMasterKey))
            {
                request.Properties = new Dictionary<string, object>();

                DistributedTransactionDispatchTracker.StampDispatchHeaders(request, EastUs);

                DistributedTransactionDispatchTrackerTests.AssertHeaders(request, null, null);
            }
        }

        [TestMethod]
        public void StampDispatchHeaders_NullProperties_OmitsBothHeaders()
        {
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.CommitDistributedTransaction,
                ResourceType.DistributedTransactionBatch,
                AuthorizationTokenType.PrimaryMasterKey))
            {
                request.Properties = null;

                DistributedTransactionDispatchTracker.StampDispatchHeaders(request, EastUs);

                DistributedTransactionDispatchTrackerTests.AssertHeaders(request, null, null);
            }
        }

        [TestMethod]
        public void StampDispatchHeaders_ForeignValueUnderTrackerKey_OmitsBothHeaders()
        {
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.CommitDistributedTransaction,
                ResourceType.DistributedTransactionBatch,
                AuthorizationTokenType.PrimaryMasterKey))
            {
                request.Properties = new Dictionary<string, object>
                {
                    [DistributedTransactionDispatchTracker.PropertyKey] = "not a tracker"
                };

                DistributedTransactionDispatchTracker.StampDispatchHeaders(request, EastUs);

                DistributedTransactionDispatchTrackerTests.AssertHeaders(request, null, null);
            }
        }

        [TestMethod]
        public void StampDispatchHeaders_NullRequest_DoesNotThrow()
        {
            DistributedTransactionDispatchTracker.StampDispatchHeaders(null, EastUs);
        }

        [TestMethod]
        public void StampDispatchHeaders_UnresolvableRegionAfterCrossing_KeepsBothHeadersTrue()
        {
            DistributedTransactionDispatchTracker tracker = new DistributedTransactionDispatchTracker();

            using (DocumentServiceRequest request = DistributedTransactionDispatchTrackerTests.CreateRequestWithTracker(tracker))
            {
                DistributedTransactionDispatchTracker.StampDispatchHeaders(request, EastUs);
                DistributedTransactionDispatchTracker.StampDispatchHeaders(request, WestUs);

                DistributedTransactionDispatchTracker.StampDispatchHeaders(request, null);

                DistributedTransactionDispatchTrackerTests.AssertHeaders(request, bool.TrueString, bool.TrueString);
            }
        }

        private static void AssertHeaders(
            DocumentServiceRequest request,
            string expectedIsRetry,
            string expectedIsCrossRegionRedirect)
        {
            Assert.AreEqual(expectedIsRetry, request.Headers[DistributedTransactionConstants.IsDtxRetry]);
            Assert.AreEqual(expectedIsCrossRegionRedirect, request.Headers[DistributedTransactionConstants.IsDtxCrossRegionRedirect]);
        }

        private static DocumentServiceRequest CreateRequestWithTracker(DistributedTransactionDispatchTracker tracker)
        {
            DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.CommitDistributedTransaction,
                ResourceType.DistributedTransactionBatch,
                AuthorizationTokenType.PrimaryMasterKey);

            request.Properties = new Dictionary<string, object>
            {
                [DistributedTransactionDispatchTracker.PropertyKey] = tracker
            };

            return request;
        }
    }
}
