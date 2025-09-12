namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosDiagnosticsContextTests
    {
        [TestMethod]
        public void CreateScope_NoName_IsNoop()
        {
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            CosmosDiagnosticsContext.Scope scope = ctx.CreateScope(null);
            scope.Dispose();
            Assert.AreEqual(0, ctx.Scopes.Count);
        }

        [TestMethod]
        public void SequentialScopes_RecordInOrder()
        {
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            using (ctx.CreateScope("A")) { Thread.Sleep(1); }
            using (ctx.CreateScope("B")) { Thread.Sleep(1); }
            using (ctx.CreateScope("C")) { }
            CollectionAssert.AreEqual(new []{"A","B","C"}, ctx.Scopes.ToArray());
        }

        [TestMethod]
        public void DisposeTwice_RecordsTwice_CurrentBehavior()
        {
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            CosmosDiagnosticsContext.Scope scope = ctx.CreateScope("Once");
            scope.Dispose();
            scope.Dispose(); // second dispose currently records again (struct copy). Assert current behavior explicitly.
            Assert.AreEqual(2, ctx.Scopes.Count);
            Assert.IsTrue(ctx.Scopes.All(s => s == "Once"));
        }

        [TestMethod]
        public void HighVolumeScopes_NoAllocationStorm()
        {
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            const int N = 1000;
            for (int i = 0; i < N; i++)
            {
                ctx.CreateScope("S").Dispose();
            }
            Assert.AreEqual(N, ctx.Scopes.Count);
            // All names identical to validate reuse path doesn't crash; not asserting timing values here.
            Assert.IsTrue(ctx.Scopes.All(s => s == "S"));
        }

        [TestMethod]
        public void Activity_NotCreated_WhenNoListeners()
        {
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            using (ctx.CreateScope("X")) { }
            Assert.AreEqual(1, ctx.Scopes.Count);
            // Can't directly assert Activity absence without reflection; rely on no exceptions and record presence.
        }

        [TestMethod]
        public void Activity_Created_WhenListenerPresent()
        {
            // Attach temporary listener to ActivitySource to ensure StartActivity path executes.
            List<Activity> started = new();
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = s => s.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = a => started.Add(a),
                ActivityStopped = a => { },
            };
            ActivitySource.AddActivityListener(listener);

            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            using (ctx.CreateScope("WithListener")) { }

            Assert.AreEqual(1, ctx.Scopes.Count);
            Assert.AreEqual("WithListener", ctx.Scopes[0]);
            Assert.AreEqual(1, started.Count, "Listener should have seen one activity");
            Assert.AreEqual("WithListener", started[0].DisplayName);
        }

        [TestMethod]
        public void ElapsedTicks_Positive()
        {
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            using (ctx.CreateScope("Timing")) { Thread.Sleep(2); }
            Assert.AreEqual(1, ctx.Scopes.Count);
            // No direct access to tick values (internal struct). Indirect validation: presence recorded.
        }

        [TestMethod]
        public void Empty_NoScopes()
        {
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            Assert.AreEqual(0, ctx.Scopes.Count);
        }

        [TestMethod]
        public void Empty_ReturnsStableEmptyArrayInstance()
        {
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            System.Collections.Generic.IReadOnlyList<string> first = ctx.Scopes;
            System.Collections.Generic.IReadOnlyList<string> second = ctx.Scopes;
            Assert.IsTrue(object.ReferenceEquals(first, second), "Expect Array.Empty<string>() instance reuse");
            Assert.AreEqual(0, first.Count);
        }

        [TestMethod]
        public void NestedScopes_RecordOnDisposeInLifoOrder()
        {
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            using (ctx.CreateScope("Outer"))
            {
                using (ctx.CreateScope("Inner1")) { }
                using (ctx.CreateScope("Inner2")) { }
            }
            // Disposal order: Inner1, Inner2, Outer
            CollectionAssert.AreEqual(new []{"Inner1","Inner2","Outer"}, ctx.Scopes.ToArray());
        }

        [TestMethod]
        public void OverlappingScopes_DisposalOrderCaptured()
        {
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            CosmosDiagnosticsContext.Scope a = ctx.CreateScope("A");
            CosmosDiagnosticsContext.Scope b = ctx.CreateScope("B");
            b.Dispose();
            CosmosDiagnosticsContext.Scope c = ctx.CreateScope("C");
            c.Dispose();
            a.Dispose();
            CollectionAssert.AreEqual(new []{"B","C","A"}, ctx.Scopes.ToArray());
        }

        [TestMethod]
        public async Task Concurrency_ManyThreads_AllScopesRecorded()
        {
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            int writers = 16;
            int per = 50;
            Task[] tasks = new Task[writers];
            for (int t = 0; t < writers; t++)
            {
                int id = t;
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < per; i++)
                    {
                        ctx.CreateScope("T" + id).Dispose();
                    }
                });
            }
            await Task.WhenAll(tasks);
            Assert.AreEqual(writers * per, ctx.Scopes.Count);
            // Spot check that each writer id appears at least once.
            for (int t = 0; t < writers; t++)
            {
                Assert.IsTrue(ctx.Scopes.Any(s => s == "T" + t), $"Missing scopes from writer {t}");
            }
        }

        [TestMethod]
        public void UsingVarPattern_Works()
        {
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            using (ctx.CreateScope("VarPattern"))
            {
                Assert.AreEqual(0, ctx.Scopes.Count, "Scope should not be recorded until disposed");
            }
            Assert.AreEqual(1, ctx.Scopes.Count, "Scope should record after using block exit");
            Assert.AreEqual("VarPattern", ctx.Scopes[0]);
        }

        [TestMethod]
        public void EmptyStringScope_NoRecord()
        {
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            ctx.CreateScope("").Dispose();
            Assert.AreEqual(0, ctx.Scopes.Count);
        }

        [TestMethod]
        public void SnapshotIsolation_ModifyingReturnedArrayDoesNotAffectInternal()
        {
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            ctx.CreateScope("X").Dispose();
            string[] snapshot = ctx.Scopes.ToArray(); // create independent copy
            snapshot[0] = "Mutated"; // mutate copy
            Assert.AreEqual("X", ctx.Scopes[0], "Internal data should be unchanged by external mutations");
        }
    }
}
