namespace Microsoft.Azure.Cosmos.Tests.Tracing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using static Microsoft.Azure.Cosmos.Tracing.TraceData.ClientSideRequestStatisticsTraceDatum;

    [TestClass]
    public class TraceTests
    {
        [TestMethod]
        public void TestRootTrace()
        {
            Trace rootTrace;
            using (rootTrace = Trace.GetRootTrace(name: "RootTrace"))
            {
                Assert.IsNotNull(rootTrace);
                rootTrace.SetWalkingStateRecursively();
                Assert.IsNotNull(rootTrace.Children);
                Assert.AreEqual(0, rootTrace.Children.Count);
                Assert.AreEqual(rootTrace.Component, TraceComponent.Unknown);
                Assert.AreNotEqual(rootTrace.Id, Guid.Empty);
                Assert.IsNotNull(rootTrace.Data);
                Assert.AreEqual(0, rootTrace.Data.Count);
                Assert.AreEqual(rootTrace.Level, TraceLevel.Verbose);
                Assert.AreEqual(rootTrace.Name, "RootTrace");
                Assert.IsNull(rootTrace.Parent);
            }

            Assert.IsTrue(rootTrace.Duration > TimeSpan.Zero);
        }

        [TestMethod]
        public void TestAddChild()
        {
            Trace oneChild = Trace.GetRootTrace(name: "OneChild");
            Trace twoChild = Trace.GetRootTrace(name: "TwoChild");
            Trace rootTrace;
            using (rootTrace = Trace.GetRootTrace(name: "RootTrace"))
            {
                rootTrace.AddChild(oneChild);
                rootTrace.AddChild(twoChild);
            }

            rootTrace.SetWalkingStateRecursively();
            Assert.AreEqual(2, rootTrace.Children.Count);
            Assert.AreEqual(oneChild, rootTrace.Children[0]);
            Assert.AreEqual(twoChild, rootTrace.Children[1]);
        }

        [TestMethod]
        public void TestTraceChildren()
        {
            using (Trace rootTrace = Trace.GetRootTrace(name: "RootTrace", component: TraceComponent.Query, level: TraceLevel.Info))
            {
                using (ITrace childTrace1 = rootTrace.StartChild("Child1" /*inherits parent's component*/))
                {
                }

                using (ITrace childTrace2 = rootTrace.StartChild("Child2", component: TraceComponent.Transport, TraceLevel.Info))
                {
                }

                rootTrace.SetWalkingStateRecursively();
                Assert.AreEqual(rootTrace.Children.Count, 2);
                Assert.AreEqual(rootTrace.Children[0].Component, TraceComponent.Query);
                Assert.AreEqual(rootTrace.Children[1].Component, TraceComponent.Transport);
            }
        }

        [TestMethod]
        public void TestNoOpTrace()
        {
            using (NoOpTrace rootTrace = NoOpTrace.Singleton)
            {
                using (ITrace childTrace1 = rootTrace.StartChild("Child1"))
                {
                }

                using (ITrace childTrace2 = rootTrace.StartChild("Child2"))
                {
                }

                Assert.AreEqual(rootTrace.Children.Count, 0);
            }
        }

        [TestMethod]
        [Timeout(5000)]
        public void ValidateStoreResultSerialization()
        {
            HashSet<string> storeResultProperties = typeof(StoreResult).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name).ToHashSet<string>();
            string datumKey = "ClientStats";
            Trace trace = Trace.GetRootTrace("Test");
            ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, trace);
            trace.AddDatum(datumKey, datum);

            ReferenceCountedDisposable<StoreResult> storeResult = StoreResult.CreateForTesting(storeResponse: new StoreResponse());

            StoreResponseStatistics storeResponseStatistics = new StoreResponseStatistics(
                            DateTime.MinValue,
                            DateTime.MaxValue,
                            storeResult.Target,
                            ResourceType.Document,
                            OperationType.Query,
                            "42",
                            new Uri("http://someUri1.com"),
                            "region1");

            ((List<StoreResponseStatistics>)datum.GetType().GetField("storeResponseStatistics", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(datum)).Add(storeResponseStatistics);

            CosmosTraceDiagnostics diagnostics = new CosmosTraceDiagnostics(trace);
            string json = diagnostics.ToString();
            JObject jObject = JObject.Parse(json);
            JObject storeResultJObject = jObject["data"][datumKey]["StoreResponseStatistics"][0]["StoreResult"].ToObject<JObject>();
            List<string> jsonPropertyNames = storeResultJObject.Properties().Select(p => p.Name).ToList();

            storeResultProperties.Add("BELatencyInMs");
            storeResultProperties.Remove(nameof(storeResult.Target.BackendRequestDurationInMs));
            storeResultProperties.Add("TransportException");
            storeResultProperties.Remove(nameof(storeResult.Target.Exception));
            storeResultProperties.Add("transportRequestTimeline");
            storeResultProperties.Remove(nameof(storeResult.Target.TransportRequestStats));

            foreach (string key in jsonPropertyNames)
            {
                Assert.IsTrue(storeResultProperties.Remove(key), $"Json contains key:{key} not a storeresult property");
            }

            Assert.AreEqual(0, storeResultProperties.Count, $"Json is missing properties: {string.Join(';', storeResultProperties)}");
        }

        [TestMethod]
        public void TestAddOrUpdateDatumThreadSafety()
        {
            Trace trace = Trace.GetRootTrace("ThreadSafetyTest");

            // Create multiple threads to access the dictionary concurrently
            const int numThreads = 10;
            const int operationsPerThread = 100;

            // Use a list to keep track of the tasks
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < numThreads; i++)
            {
                int threadIndex = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < operationsPerThread; j++)
                    {
                        string key = $"key_{threadIndex}_{j}";
                        object value = j;

                        // Perform operations that would previously cause thread safety issues
                        trace.AddOrUpdateDatum(key, value);

                        // Also test AddDatum
                        try
                        {
                            trace.AddDatum($"add_{threadIndex}_{j}", value);
                        }
                        catch (ArgumentException)
                        {
                            // Ignore key already exists exceptions which may occur
                            // when threads try to add the same key
                        }
                    }
                }));
            }

            // Wait for all tasks to complete
            Task.WaitAll(tasks.ToArray());

            trace.SetWalkingStateRecursively();

            // Verify the data dictionary has entries
            Assert.IsTrue(trace.Data.Count > 0);
        }

        [TestMethod]
        [DoNotParallelize]
        public void TestMaxChildCountSuppressesExcessChildrenViaStartChild()
        {
            int originalMaxChildCount = Trace.MaxChildCount;
            try
            {
                Trace.MaxChildCount = 3;
                using (Trace rootTrace = Trace.GetRootTrace(name: "RootTrace"))
                {
                    List<ITrace> returnedChildren = new List<ITrace>();
                    for (int i = 0; i < 10; i++)
                    {
                        returnedChildren.Add(rootTrace.StartChild($"Child{i}"));
                    }

                    // The first MaxChildCount children are real Trace nodes...
                    for (int i = 0; i < 3; i++)
                    {
                        Assert.IsInstanceOfType(returnedChildren[i], typeof(Trace));
                    }

                    // ...and everything beyond the limit is suppressed (NoOpTrace) but
                    // still shares the operation's TraceSummary so aggregates are kept.
                    for (int i = 3; i < 10; i++)
                    {
                        Assert.IsInstanceOfType(returnedChildren[i], typeof(NoOpTrace));
                        Assert.AreSame(rootTrace.Summary, returnedChildren[i].Summary);
                    }

                    Assert.AreEqual(7, rootTrace.SuppressedChildCount);

                    rootTrace.SetWalkingStateRecursively();
                    Assert.AreEqual(3, rootTrace.Children.Count);
                    Assert.IsTrue(
                        rootTrace.Data.TryGetValue(Trace.TruncatedChildTraceCountKey, out object suppressed),
                        "Truncation should be surfaced as a datum on the node.");
                    Assert.AreEqual(7L, suppressed);
                }
            }
            finally
            {
                Trace.MaxChildCount = originalMaxChildCount;
            }
        }

        [TestMethod]
        [DoNotParallelize]
        public void TestMaxChildCountSuppressesExcessChildrenViaAddChild()
        {
            int originalMaxChildCount = Trace.MaxChildCount;
            try
            {
                Trace.MaxChildCount = 2;
                using (Trace rootTrace = Trace.GetRootTrace(name: "RootTrace"))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        rootTrace.AddChild(Trace.GetRootTrace(name: $"Child{i}"));
                    }

                    Assert.AreEqual(3, rootTrace.SuppressedChildCount);

                    rootTrace.SetWalkingStateRecursively();
                    Assert.AreEqual(2, rootTrace.Children.Count);
                    Assert.IsTrue(
                        rootTrace.Data.TryGetValue(Trace.TruncatedChildTraceCountKey, out object suppressed),
                        "Truncation should be surfaced as a datum on the node.");
                    Assert.AreEqual(3L, suppressed);
                }
            }
            finally
            {
                Trace.MaxChildCount = originalMaxChildCount;
            }
        }

        [TestMethod]
        public void TestDefaultMaxChildCountDoesNotTruncateNormalTraces()
        {
            // The default limit must be comfortably above realistic per-node breadth so
            // normal operations (and existing diagnostics baselines) are never truncated.
            Assert.IsTrue(Trace.MaxChildCount >= 1000);

            using (Trace rootTrace = Trace.GetRootTrace(name: "RootTrace"))
            {
                for (int i = 0; i < 50; i++)
                {
                    using (rootTrace.StartChild($"Child{i}"))
                    {
                    }
                }

                Assert.AreEqual(0, rootTrace.SuppressedChildCount);

                rootTrace.SetWalkingStateRecursively();
                Assert.AreEqual(50, rootTrace.Children.Count);
                Assert.IsFalse(rootTrace.Data.ContainsKey(Trace.TruncatedChildTraceCountKey));
            }
        }

        [TestMethod]
        [DoNotParallelize]
        public void TestMaxChildCountConcurrentStartChildDoesNotOrphanChildren()
        {
            int originalMaxChildCount = Trace.MaxChildCount;
            try
            {
                const int limit = 50;
                const int attempts = 500;
                Trace.MaxChildCount = limit;
                using (Trace rootTrace = Trace.GetRootTrace(name: "RootTrace"))
                {
                    ConcurrentBag<ITrace> returned = new ConcurrentBag<ITrace>();
                    Parallel.For(0, attempts, i =>
                    {
                        returned.Add(rootTrace.StartChild($"Child{i}"));
                    });

                    rootTrace.SetWalkingStateRecursively();

                    // Exactly 'limit' real children are retained under the node.
                    Assert.AreEqual(limit, rootTrace.Children.Count);

                    // Every returned real Trace must actually be in the tree (no orphans
                    // from the lock-free pre-check racing with the locked enforcement).
                    HashSet<ITrace> retained = new HashSet<ITrace>(rootTrace.Children);
                    int realReturned = 0;
                    foreach (ITrace trace in returned)
                    {
                        if (trace is Trace)
                        {
                            realReturned++;
                            Assert.IsTrue(retained.Contains(trace), "A real Trace was returned but never added to the tree (orphaned).");
                        }
                        else
                        {
                            Assert.IsInstanceOfType(trace, typeof(NoOpTrace));
                        }
                    }

                    Assert.AreEqual(attempts, returned.Count);
                    Assert.AreEqual(limit, realReturned);
                    Assert.AreEqual(attempts - limit, rootTrace.SuppressedChildCount);
                }
            }
            finally
            {
                Trace.MaxChildCount = originalMaxChildCount;
            }
        }

        [TestMethod]
        [DoNotParallelize]
        public void TestSuppressedChildSummaryAggregatesToParent()
        {
            int originalMaxChildCount = Trace.MaxChildCount;
            try
            {
                Trace.MaxChildCount = 1;
                using (Trace rootTrace = Trace.GetRootTrace(name: "RootTrace"))
                {
                    using (rootTrace.StartChild("Retained"))
                    {
                    }

                    // Suppressed, but must still share the operation's TraceSummary so
                    // imperatively-updated aggregates (e.g. failed count) are not lost.
                    ITrace suppressed = rootTrace.StartChild("Suppressed");
                    Assert.IsInstanceOfType(suppressed, typeof(NoOpTrace));
                    Assert.AreSame(rootTrace.Summary, suppressed.Summary);

                    suppressed.Summary.IncrementFailedCount();
                    Assert.AreEqual(1, rootTrace.Summary.GetFailedCount());
                }
            }
            finally
            {
                Trace.MaxChildCount = originalMaxChildCount;
            }
        }

        [TestMethod]
        [DoNotParallelize]
        public void TestMaxChildCountRejectsNonPositiveValues()
        {
            int originalMaxChildCount = Trace.MaxChildCount;
            try
            {
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => Trace.MaxChildCount = 0);
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => Trace.MaxChildCount = -1);
                Assert.AreEqual(originalMaxChildCount, Trace.MaxChildCount);
            }
            finally
            {
                Trace.MaxChildCount = originalMaxChildCount;
            }
        }

        [TestMethod]
        [DoNotParallelize]
        public void TestTruncationSurfacesPartialResultsInDiagnosticsJson()
        {
            int originalMaxChildCount = Trace.MaxChildCount;
            try
            {
                Trace.MaxChildCount = 3;
                Trace rootTrace = Trace.GetRootTrace(name: "RootTrace");
                for (int i = 0; i < 10; i++)
                {
                    using (rootTrace.StartChild($"Child{i}"))
                    {
                    }
                }

                CosmosTraceDiagnostics diagnostics = new CosmosTraceDiagnostics(rootTrace);
                JObject jObject = JObject.Parse(diagnostics.ToString());

                Assert.IsTrue(
                    (bool)jObject["Summary"]["PartialResults"],
                    "Truncated diagnostics must be marked with PartialResults in the Summary.");
            }
            finally
            {
                Trace.MaxChildCount = originalMaxChildCount;
            }
        }

        [TestMethod]
        public void TestNonTruncatedDiagnosticsJsonHasNoPartialResults()
        {
            Trace rootTrace = Trace.GetRootTrace(name: "RootTrace");
            using (rootTrace.StartChild("Child"))
            {
            }

            CosmosTraceDiagnostics diagnostics = new CosmosTraceDiagnostics(rootTrace);
            JObject jObject = JObject.Parse(diagnostics.ToString());

            Assert.IsNotNull(jObject["Summary"], "Root diagnostics should always contain a Summary.");
            Assert.IsNull(
                jObject["Summary"]["PartialResults"],
                "Non-truncated diagnostics must not be marked with PartialResults.");
        }

        [TestMethod]
        public void TestNoOpTraceSingletonSummaryIsNotNull()
        {
            // Regression guard for static-field initialization order in NoOpTrace:
            // NoOpTraceSummary must be initialized before Singleton so the shared
            // singleton never exposes a null Summary. A null here NREs every caller
            // that reads the trace's Summary on the request path (for example
            // TransportHandler.ProcessMessageAsync -> Summary.UpdateRegionContacted),
            // which surfaced as request-path NullReferenceExceptions in emulator tests.
            Assert.IsNotNull(NoOpTrace.Singleton.Summary);
            Assert.AreSame(NoOpTrace.NoOpTraceSummary, NoOpTrace.Singleton.Summary);

            // StartChild on the singleton returns the singleton itself; its Summary must
            // still be non-null so the transport happy path does not NRE.
            ITrace child = NoOpTrace.Singleton.StartChild(
                name: "Child",
                component: TraceComponent.Transport,
                level: TraceLevel.Info);
            Assert.IsNotNull(child.Summary);
        }
    }
}