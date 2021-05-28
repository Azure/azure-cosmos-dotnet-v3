namespace Microsoft.Azure.Cosmos.Tests.Tracing
{
    using System;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents;
    using System.Reflection;
    using System.Linq;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using static Microsoft.Azure.Cosmos.Tracing.TraceData.ClientSideRequestStatisticsTraceDatum;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Newtonsoft.Json.Linq;

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
        public void ValidateStoreResultSerialization()
        {
            HashSet<string> storeResultProperties = typeof(StoreResult).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name).ToHashSet<string>();
            string datumKey = "ClientStats";
            Trace trace = Trace.GetRootTrace("Test");
            ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow);
            trace.AddDatum(datumKey, datum);

            StoreResult storeResult = new StoreResult(
                storeResponse: new StoreResponse(),
                exception: null,
                partitionKeyRangeId: 42.ToString(),
                lsn: 1337,
                quorumAckedLsn: 23,
                requestCharge: 3.14,
                currentReplicaSetSize: 4,
                currentWriteQuorum: 3,
                isValid: true,
                storePhysicalAddress: new Uri("http://storephysicaladdress.com"),
                globalCommittedLSN: 1234,
                numberOfReadRegions: 13,
                itemLSN: 15,
                sessionToken: new SimpleSessionToken(42),
                usingLocalLSN: true,
                activityId: Guid.Empty.ToString(),
                backendRequestDurationInMs: "4.2");

            StoreResponseStatistics storeResponseStatistics = new StoreResponseStatistics(
                            DateTime.MinValue,
                            DateTime.MaxValue,
                            storeResult,
                            ResourceType.Document,
                            OperationType.Query,
                            new Uri("http://someUri1.com"));

            datum.StoreResponseStatisticsList.Add(storeResponseStatistics);

            CosmosTraceDiagnostics diagnostics = new CosmosTraceDiagnostics(trace);
            string json = diagnostics.ToString();
            JObject jObject = JObject.Parse(json);
            JObject storeResultJObject = jObject["data"][datumKey]["StoreResponseStatistics"][0]["StoreResult"].ToObject<JObject>();
            List<string> jsonPropertyNames = storeResultJObject.Properties().Select(p => p.Name).ToList();

            storeResultProperties.Add("BELatencyInMs");
            storeResultProperties.Remove(nameof(storeResult.BackendRequestDurationInMs));
            storeResultProperties.Add("TransportException");
            storeResultProperties.Remove(nameof(storeResult.Exception));

            foreach (string key in jsonPropertyNames)
            {
                Assert.IsTrue(storeResultProperties.Remove(key), $"Json contains key:{key} not a storeresult property");
            }

            Assert.AreEqual(0, storeResultProperties.Count, $"Json is missing properties: {string.Join(';', storeResultProperties)}");
        }
    }
}
