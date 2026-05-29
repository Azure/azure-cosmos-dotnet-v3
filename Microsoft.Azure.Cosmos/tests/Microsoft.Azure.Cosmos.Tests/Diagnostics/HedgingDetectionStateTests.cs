//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System.Collections.Generic;
    using System.Reflection;
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

        /// <summary>
        /// Pins the F7 fix on PR #5868: the <c>hedgingStarted</c> backing field MUST be
        /// declared <c>volatile</c> so the public <c>HedgingStarted</c> getter can read it
        /// without acquiring <c>regionLock</c>. Without <c>volatile</c>, the lock-free read
        /// would not see the writer-side flip in a memory-model-correct way and the
        /// reviewer-flagged optimization would silently re-introduce a race.
        /// </summary>
        [TestMethod]
        public void HedgingStartedBackingField_IsDeclaredVolatile()
        {
            FieldInfo field = typeof(HedgingDetectionState).GetField(
                name: "hedgingStarted",
                bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(field, "Backing field 'hedgingStarted' must exist on HedgingDetectionState.");

            // The C# 'volatile' modifier surfaces as the IsVolatile required custom modifier
            // on the field's type signature. Asserting on this catches an accidental drop of
            // the modifier (e.g. during a refactor) without depending on source-level scrape.
            System.Type[] requiredMods = field.GetRequiredCustomModifiers();
            bool hasVolatileModifier = false;
            foreach (System.Type mod in requiredMods)
            {
                if (mod == typeof(System.Runtime.CompilerServices.IsVolatile))
                {
                    hasVolatileModifier = true;
                    break;
                }
            }

            Assert.IsTrue(
                hasVolatileModifier,
                "F7 invariant: 'hedgingStarted' must be 'volatile bool' so the lock-free HedgingStarted getter is memory-model correct.");
        }

        /// <summary>
        /// Functional companion to <see cref="HedgingStartedBackingField_IsDeclaredVolatile"/>:
        /// readers spinning on <c>HedgingStarted</c> must observe <c>true</c> after a single
        /// writer flips it, without ever acquiring <c>regionLock</c>. This catches a regression
        /// where someone re-introduces locking on the read path or drops <c>volatile</c> from
        /// the backing field — the readers would either deadlock against the writer or never
        /// observe the flip.
        /// </summary>
        [TestMethod]
        public async Task HedgingStarted_LockFreeRead_ObservesWriterFlip()
        {
            HedgingDetectionState state = new HedgingDetectionState();
            using CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            // Three readers spin on HedgingStarted concurrently with a single writer that
            // performs many AppendRequested calls (each one takes regionLock). If the read
            // path is genuinely lock-free, the readers will record the flip moment and exit
            // promptly; if it took the lock, they would queue behind the writer and the test
            // would take noticeably longer (or, on stricter builds, expose the contention via
            // a deadline overrun).
            const int readerCount = 3;
            Task<bool>[] readers = new Task<bool>[readerCount];
            for (int r = 0; r < readerCount; r++)
            {
                readers[r] = Task.Run(() =>
                {
                    while (!ct.IsCancellationRequested)
                    {
                        if (state.HedgingStarted)
                        {
                            return true;
                        }
                    }

                    return false;
                });
            }

            // Writer appends a series of non-Hedging entries first (so readers observe the
            // false state across many iterations), then a single Hedging entry to flip the
            // flag.
            Task writer = Task.Run(() =>
            {
                for (int i = 0; i < 200; i++)
                {
                    state.AppendRequested("R" + (i % 4), RequestedRegionReason.Initial);
                }

                state.AppendRequested("West US", RequestedRegionReason.Hedging);
            });

            await writer;

            // Bound the readers; if any reader has not seen true within 5 seconds the
            // lock-free invariant is broken or volatile was dropped.
            Task allReadersDone = Task.WhenAll(readers);
            Task completed = await Task.WhenAny(allReadersDone, Task.Delay(5000, ct));
            cts.Cancel();
            await allReadersDone;

            Assert.AreSame(allReadersDone, completed, "Readers must observe the Hedging flip within the 5-second deadline (F7 lock-free read).");
            foreach (Task<bool> reader in readers)
            {
                Assert.IsTrue(reader.Result, "Every reader must observe HedgingStarted == true after the writer flipped it.");
            }

            Assert.IsTrue(state.HedgingStarted, "Post-condition: HedgingStarted remains true (monotonic).");
        }
    }
}
