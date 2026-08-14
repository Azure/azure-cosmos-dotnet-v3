// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.DistributedTransaction
{
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="DistributedTransactionCrossRegionRetryTracker"/>, which owns the
    /// emission rules for the <c>x-ms-cosmos-dtx-cross-region-retry</c> header.
    ///
    /// Two normative points a regression would silently break: the signal is sticky for the lifetime
    /// of an idempotency token, and it resets when the token rotates.
    /// </summary>
    [TestClass]
    public class DistributedTransactionCrossRegionRetryTrackerTests
    {
        private const string EastUs = "East US";
        private const string WestUs = "West US";

        [TestMethod]
        public void RecordDispatch_FirstDispatchOfToken_DoesNotCrossBoundary()
        {
            DistributedTransactionCrossRegionRetryTracker tracker = new DistributedTransactionCrossRegionRetryTracker();

            tracker.RecordDispatch(EastUs);

            Assert.IsFalse(tracker.HasCrossedRegionBoundary);
        }

        [TestMethod]
        public void RecordDispatch_RegionUnchanged_DoesNotCrossBoundary()
        {
            DistributedTransactionCrossRegionRetryTracker tracker = new DistributedTransactionCrossRegionRetryTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(EastUs);

            Assert.IsFalse(tracker.HasCrossedRegionBoundary);
        }

        [TestMethod]
        public void RecordDispatch_RegionDiffersFromPreviousDispatch_CrossesBoundary()
        {
            DistributedTransactionCrossRegionRetryTracker tracker = new DistributedTransactionCrossRegionRetryTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(WestUs);

            Assert.IsTrue(tracker.HasCrossedRegionBoundary);
        }

        [TestMethod]
        public void RecordDispatch_AfterCrossingBoundary_StaysTrueWithinNewRegion()
        {
            DistributedTransactionCrossRegionRetryTracker tracker = new DistributedTransactionCrossRegionRetryTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(WestUs);
            tracker.RecordDispatch(WestUs);

            Assert.IsTrue(tracker.HasCrossedRegionBoundary);
        }

        [TestMethod]
        public void RecordDispatch_AfterCrossingBoundary_StaysTrueWhenRoutedBackToOriginRegion()
        {
            DistributedTransactionCrossRegionRetryTracker tracker = new DistributedTransactionCrossRegionRetryTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(WestUs);
            tracker.RecordDispatch(EastUs);

            Assert.IsTrue(tracker.HasCrossedRegionBoundary);
        }

        [TestMethod]
        public void RecordDispatch_RegionsComparedCaseInsensitively_DoesNotCrossBoundary()
        {
            DistributedTransactionCrossRegionRetryTracker tracker = new DistributedTransactionCrossRegionRetryTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch("east us");

            Assert.IsFalse(tracker.HasCrossedRegionBoundary);
        }

        [TestMethod]
        public void RecordDispatch_UnresolvableRegionOnFirstDispatch_LeavesStateUntouched()
        {
            DistributedTransactionCrossRegionRetryTracker tracker = new DistributedTransactionCrossRegionRetryTracker();

            tracker.RecordDispatch(null);
            tracker.RecordDispatch(string.Empty);
            Assert.IsFalse(tracker.HasCrossedRegionBoundary);

            // Nothing was recorded, so the first resolvable region is still a first dispatch.
            tracker.RecordDispatch(EastUs);
            Assert.IsFalse(tracker.HasCrossedRegionBoundary);
        }

        [TestMethod]
        public void RecordDispatch_UnresolvableRegionAfterKnownRegion_DoesNotDiscardLastRegion()
        {
            DistributedTransactionCrossRegionRetryTracker tracker = new DistributedTransactionCrossRegionRetryTracker();

            tracker.RecordDispatch(EastUs);

            tracker.RecordDispatch(null);
            Assert.IsFalse(tracker.HasCrossedRegionBoundary);

            // The unresolvable dispatch must not have overwritten East US, so West US still crosses.
            tracker.RecordDispatch(WestUs);
            Assert.IsTrue(tracker.HasCrossedRegionBoundary);
        }

        [TestMethod]
        public void RecordDispatch_UnresolvableRegionAfterCrossingBoundary_StaysTrue()
        {
            DistributedTransactionCrossRegionRetryTracker tracker = new DistributedTransactionCrossRegionRetryTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(WestUs);
            tracker.RecordDispatch(null);

            Assert.IsTrue(tracker.HasCrossedRegionBoundary);
        }

        [TestMethod]
        public void ResetForNewToken_AfterCrossingBoundary_ClearsStickySignal()
        {
            DistributedTransactionCrossRegionRetryTracker tracker = new DistributedTransactionCrossRegionRetryTracker();

            tracker.RecordDispatch(EastUs);
            tracker.RecordDispatch(WestUs);
            Assert.IsTrue(tracker.HasCrossedRegionBoundary);

            tracker.ResetForNewToken();

            tracker.RecordDispatch(WestUs);
            Assert.IsFalse(tracker.HasCrossedRegionBoundary);
        }

        [TestMethod]
        public void ResetForNewToken_ClearsLastDispatchRegion()
        {
            DistributedTransactionCrossRegionRetryTracker tracker = new DistributedTransactionCrossRegionRetryTracker();

            tracker.RecordDispatch(EastUs);
            tracker.ResetForNewToken();

            // The new token has no record in any region, so this is a first dispatch, not a crossing.
            tracker.RecordDispatch(WestUs);
            Assert.IsFalse(tracker.HasCrossedRegionBoundary);
        }

        [TestMethod]
        public void StampCrossRegionRetryHeader_TrackerPresent_StampsFalseThenTrueOnRegionChange()
        {
            DistributedTransactionCrossRegionRetryTracker tracker = new DistributedTransactionCrossRegionRetryTracker();

            using (DocumentServiceRequest request = DistributedTransactionCrossRegionRetryTrackerTests.CreateRequestWithTracker(tracker))
            {
                DistributedTransactionCrossRegionRetryTracker.StampCrossRegionRetryHeader(request, EastUs);
                Assert.AreEqual(bool.FalseString, request.Headers[DistributedTransactionConstants.CrossRegionRetryHeader]);

                DistributedTransactionCrossRegionRetryTracker.StampCrossRegionRetryHeader(request, WestUs);
                Assert.AreEqual(bool.TrueString, request.Headers[DistributedTransactionConstants.CrossRegionRetryHeader]);

                DistributedTransactionCrossRegionRetryTracker.StampCrossRegionRetryHeader(request, WestUs);
                Assert.AreEqual(bool.TrueString, request.Headers[DistributedTransactionConstants.CrossRegionRetryHeader]);
            }
        }

        [TestMethod]
        public void StampCrossRegionRetryHeader_NoTrackerInProperties_OmitsHeaderEntirely()
        {
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.DistributedTransactionBatch,
                AuthorizationTokenType.PrimaryMasterKey))
            {
                request.Properties = new Dictionary<string, object>();

                DistributedTransactionCrossRegionRetryTracker.StampCrossRegionRetryHeader(request, EastUs);

                Assert.IsNull(request.Headers[DistributedTransactionConstants.CrossRegionRetryHeader]);
            }
        }

        [TestMethod]
        public void StampCrossRegionRetryHeader_NullProperties_OmitsHeaderEntirely()
        {
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.CommitDistributedTransaction,
                ResourceType.DistributedTransactionBatch,
                AuthorizationTokenType.PrimaryMasterKey))
            {
                request.Properties = null;

                DistributedTransactionCrossRegionRetryTracker.StampCrossRegionRetryHeader(request, EastUs);

                Assert.IsNull(request.Headers[DistributedTransactionConstants.CrossRegionRetryHeader]);
            }
        }

        [TestMethod]
        public void StampCrossRegionRetryHeader_ForeignValueUnderTrackerKey_OmitsHeaderEntirely()
        {
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.CommitDistributedTransaction,
                ResourceType.DistributedTransactionBatch,
                AuthorizationTokenType.PrimaryMasterKey))
            {
                request.Properties = new Dictionary<string, object>
                {
                    [DistributedTransactionCrossRegionRetryTracker.PropertyKey] = "not a tracker"
                };

                DistributedTransactionCrossRegionRetryTracker.StampCrossRegionRetryHeader(request, EastUs);

                Assert.IsNull(request.Headers[DistributedTransactionConstants.CrossRegionRetryHeader]);
            }
        }

        [TestMethod]
        public void StampCrossRegionRetryHeader_NullRequest_DoesNotThrow()
        {
            DistributedTransactionCrossRegionRetryTracker.StampCrossRegionRetryHeader(null, EastUs);
        }

        [TestMethod]
        public void StampCrossRegionRetryHeader_UnresolvableRegionAfterCrossing_KeepsHeaderTrue()
        {
            DistributedTransactionCrossRegionRetryTracker tracker = new DistributedTransactionCrossRegionRetryTracker();

            using (DocumentServiceRequest request = DistributedTransactionCrossRegionRetryTrackerTests.CreateRequestWithTracker(tracker))
            {
                DistributedTransactionCrossRegionRetryTracker.StampCrossRegionRetryHeader(request, EastUs);
                DistributedTransactionCrossRegionRetryTracker.StampCrossRegionRetryHeader(request, WestUs);

                DistributedTransactionCrossRegionRetryTracker.StampCrossRegionRetryHeader(request, null);

                Assert.AreEqual(bool.TrueString, request.Headers[DistributedTransactionConstants.CrossRegionRetryHeader]);
            }
        }

        private static DocumentServiceRequest CreateRequestWithTracker(DistributedTransactionCrossRegionRetryTracker tracker)
        {
            DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.CommitDistributedTransaction,
                ResourceType.DistributedTransactionBatch,
                AuthorizationTokenType.PrimaryMasterKey);

            request.Properties = new Dictionary<string, object>
            {
                [DistributedTransactionCrossRegionRetryTracker.PropertyKey] = tracker
            };

            return request;
        }
    }
}
