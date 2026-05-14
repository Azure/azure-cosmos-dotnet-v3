//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class HedgingDetectionStateTests
    {
        [TestMethod]
        public void Defaults_AreEmptyAndHedgingFalse()
        {
            HedgingDetectionState state = new HedgingDetectionState();

            Assert.IsFalse(state.HedgingStarted);
            Assert.AreEqual(0, state.GetRequestedRegionsSnapshot().Count);
            Assert.AreEqual(0, state.GetRespondedRegionsSnapshot().Count);
        }

        [TestMethod]
        public void AppendRequested_NullOrEmptyName_IsIgnored()
        {
            HedgingDetectionState state = new HedgingDetectionState();
            state.AppendRequested(null, RequestedRegionReason.Initial);
            state.AppendRequested(string.Empty, RequestedRegionReason.Hedging);

            Assert.AreEqual(0, state.GetRequestedRegionsSnapshot().Count);
            Assert.IsFalse(state.HedgingStarted);
        }

        [TestMethod]
        public void AppendRequested_Hedging_FlipsHedgingStarted()
        {
            HedgingDetectionState state = new HedgingDetectionState();
            state.AppendRequested("East US", RequestedRegionReason.Initial);
            Assert.IsFalse(state.HedgingStarted);

            state.AppendRequested("West US", RequestedRegionReason.Hedging);
            Assert.IsTrue(state.HedgingStarted);
        }

        [TestMethod]
        public void AppendRequested_NonHedgingReasons_DoNotFlipHedgingStarted()
        {
            HedgingDetectionState state = new HedgingDetectionState();
            state.AppendRequested("r1", RequestedRegionReason.Initial);
            state.AppendRequested("r2", RequestedRegionReason.OperationRetry);
            state.AppendRequested("r3", RequestedRegionReason.RegionFailover);
            state.AppendRequested("r4", RequestedRegionReason.TransportRetry);
            state.AppendRequested("r5", RequestedRegionReason.CircuitBreakerProbe);

            Assert.IsFalse(state.HedgingStarted);
            Assert.AreEqual(5, state.GetRequestedRegionsSnapshot().Count);
        }

        [TestMethod]
        public void AppendRequested_PreservesOrderAndReasonsAndDuplicates()
        {
            HedgingDetectionState state = new HedgingDetectionState();
            state.AppendRequested("East US", RequestedRegionReason.Initial);
            state.AppendRequested("West US", RequestedRegionReason.Hedging);
            state.AppendRequested("East US", RequestedRegionReason.OperationRetry);

            IReadOnlyList<RequestedRegion> snap = state.GetRequestedRegionsSnapshot();
            Assert.AreEqual(3, snap.Count);
            Assert.AreEqual(new RequestedRegion("East US", RequestedRegionReason.Initial), snap[0]);
            Assert.AreEqual(new RequestedRegion("West US", RequestedRegionReason.Hedging), snap[1]);
            Assert.AreEqual(new RequestedRegion("East US", RequestedRegionReason.OperationRetry), snap[2]);
        }

        [TestMethod]
        public void AppendResponded_NullOrEmpty_Ignored_DuplicatesAllowed()
        {
            HedgingDetectionState state = new HedgingDetectionState();
            state.AppendResponded(null);
            state.AppendResponded(string.Empty);
            Assert.AreEqual(0, state.GetRespondedRegionsSnapshot().Count);

            state.AppendResponded("East US");
            state.AppendResponded("West US");
            state.AppendResponded("East US");

            IReadOnlyList<string> snap = state.GetRespondedRegionsSnapshot();
            Assert.AreEqual(3, snap.Count);
            CollectionAssert.AreEqual(new[] { "East US", "West US", "East US" }, (System.Collections.ICollection)snap);
        }

        [TestMethod]
        public void Snapshots_AreIndependentOfSubsequentMutations()
        {
            HedgingDetectionState state = new HedgingDetectionState();
            state.AppendRequested("East US", RequestedRegionReason.Initial);
            state.AppendResponded("East US");

            IReadOnlyList<RequestedRegion> reqSnap = state.GetRequestedRegionsSnapshot();
            IReadOnlyList<string> respSnap = state.GetRespondedRegionsSnapshot();

            state.AppendRequested("West US", RequestedRegionReason.Hedging);
            state.AppendResponded("West US");

            Assert.AreEqual(1, reqSnap.Count);
            Assert.AreEqual(1, respSnap.Count);
        }

        [TestMethod]
        public async Task ConcurrentAppends_AreThreadSafe()
        {
            HedgingDetectionState state = new HedgingDetectionState();
            const int writerCount = 8;
            const int perWriter = 500;

            Task[] writers = new Task[writerCount];
            for (int w = 0; w < writerCount; w++)
            {
                int writerIndex = w;
                writers[w] = Task.Run(() =>
                {
                    for (int i = 0; i < perWriter; i++)
                    {
                        RequestedRegionReason reason = ((writerIndex + i) % 2 == 0)
                            ? RequestedRegionReason.Initial
                            : RequestedRegionReason.Hedging;
                        state.AppendRequested("R" + (i % 3), reason);
                        state.AppendResponded("R" + (i % 3));
                    }
                });
            }

            await Task.WhenAll(writers);

            Assert.AreEqual(writerCount * perWriter, state.GetRequestedRegionsSnapshot().Count);
            Assert.AreEqual(writerCount * perWriter, state.GetRespondedRegionsSnapshot().Count);
            Assert.IsTrue(state.HedgingStarted);
        }
    }
}
