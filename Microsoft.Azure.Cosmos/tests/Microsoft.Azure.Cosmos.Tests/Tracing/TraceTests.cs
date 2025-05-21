namespace Microsoft.Azure.Cosmos.Tests.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Json;
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
            
            // Use HashSet to track the actual keys we expect to be successfully added
            HashSet<string> expectedUniqueKeys = new HashSet<string>();
            
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
                            string addKey = $"add_{threadIndex}_{j}";
                            trace.AddDatum(addKey, value);
                            
                            // Keep track of successfully added keys
                            lock (expectedUniqueKeys)
                            {
                                expectedUniqueKeys.Add(addKey);
                            }
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
            
            // Verify all AddOrUpdateDatum operations succeeded
            // Each thread adds unique keys, so we should have numThreads * operationsPerThread entries
            int expectedAddOrUpdateCount = numThreads * operationsPerThread;
            int addOrUpdateKeysFound = trace.Data.Keys.Count(k => k.StartsWith("key_"));
            Assert.AreEqual(expectedAddOrUpdateCount, addOrUpdateKeysFound, 
                "All AddOrUpdateDatum operations should succeed with unique keys");
            
            // Verify the keys added via AddDatum match our tracked collection
            foreach (string expectedKey in expectedUniqueKeys)
            {
                Assert.IsTrue(trace.Data.ContainsKey(expectedKey), 
                    $"Expected key {expectedKey} not found in dictionary");
            }
            
            // Verify the total number of keys is the sum of AddOrUpdate keys and successful AddDatum keys
            Assert.AreEqual(expectedAddOrUpdateCount + expectedUniqueKeys.Count, trace.Data.Count, 
                "Total key count should equal AddOrUpdate keys plus successful AddDatum keys");
        }
    }
}