//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Tracing
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Tests.Query.Metrics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceInfos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TraceWriterTests
    {
        private static readonly QueryMetrics MockQueryMetrics = new QueryMetrics(
            BackendMetricsTests.MockBackendMetrics,
            IndexUtilizationInfoTests.MockIndexUtilizationInfo,
            ClientSideMetricsTests.MockClientSideMetrics);

        [TestMethod]
        public async Task TestTraceChildren()
        {
            Trace rootTrace;
            using (rootTrace = Trace.GetRootTrace(name: "RootTrace"))
            {
                using (ITrace childTrace1 = rootTrace.StartChild("Child1"))
                {
                    using (ITrace child1Child1 = childTrace1.StartChild("Child1Child1"))
                    {
                        Thread.Sleep(100);
                    }

                    using (ITrace child1Child2 = childTrace1.StartChild("Child1Child2"))
                    {
                        await Task.Delay(100);
                    }
                }

                using (ITrace childTrace2 = rootTrace.StartChild("Child2"))
                {
                    using (ITrace child2Child1 = childTrace2.StartChild("Child2Child1"))
                    {
                        await Task.Delay(100);
                    }

                    using (ITrace child2Child2 = childTrace2.StartChild("Child2Child2"))
                    {
                        Thread.Sleep(100);
                    }

                    using (ITrace child2Child3 = childTrace2.StartChild("Child2Child3"))
                    {
                        await Task.Delay(100);
                    }
                }
            }

            string traceString = GetTraceString(rootTrace);
        }

        [TestMethod]
        public void RootTrace()
        {
            Trace rootTrace;
            using (rootTrace = Trace.GetRootTrace(name: "RootTrace"))
            {
            }

            string traceString = GetTraceString(rootTrace);
        }

        [TestMethod]
        public void RootTraceWithInfo()
        {
            Trace rootTrace;
            using (rootTrace = Trace.GetRootTrace(name: "RootTrace"))
            {
                rootTrace.Info = new QueryMetricsTraceInfo(MockQueryMetrics);
            }

            string traceString = GetTraceString(rootTrace);
        }

        [TestMethod]
        public void RootTraceWithOneChild()
        {
            Trace rootTrace;
            using (rootTrace = Trace.GetRootTrace(name: "RootTrace"))
            {
                using (ITrace childTrace1 = rootTrace.StartChild("Child1"))
                {
                }
            }

            string traceString = GetTraceString(rootTrace);
        }

        [TestMethod]
        public void RootTraceWithOneChildWithInfo()
        {
            Trace rootTrace;
            using (rootTrace = Trace.GetRootTrace(name: "RootTrace"))
            {
                using (ITrace childTrace1 = rootTrace.StartChild("Child1"))
                {
                    childTrace1.Info = new QueryMetricsTraceInfo(MockQueryMetrics);
                }
            }

            string traceString = GetTraceString(rootTrace);
        }

        [TestMethod]
        public void RootTraceWithTwoChildren()
        {
            Trace rootTrace;
            using (rootTrace = Trace.GetRootTrace(name: "RootTrace"))
            {
                using (ITrace childTrace1 = rootTrace.StartChild("Child1"))
                {
                }

                using (ITrace childTrace2 = rootTrace.StartChild("Child2"))
                {
                }
            }

            string traceString = GetTraceString(rootTrace);
        }

        [TestMethod]
        public void RootTraceWithTwoChildrenWithInfo()
        {
            Trace rootTrace;
            using (rootTrace = Trace.GetRootTrace(name: "RootTrace"))
            {
                using (ITrace childTrace1 = rootTrace.StartChild("Child1"))
                {
                    childTrace1.Info = new QueryMetricsTraceInfo(MockQueryMetrics);
                }

                using (ITrace childTrace2 = rootTrace.StartChild("Child2"))
                {
                    childTrace2.Info = new QueryMetricsTraceInfo(MockQueryMetrics);
                }
            }

            string traceString = GetTraceString(rootTrace);
        }

        [TestMethod]
        public async Task MockQueryOutput()
        {
            CosmosClientSideRequestStatistics clientSideRequestStatistics = new CosmosClientSideRequestStatistics();
            string id = clientSideRequestStatistics.RecordAddressResolutionStart(new Uri("https://testuri"));
            clientSideRequestStatistics.RecordAddressResolutionEnd(id);

            Documents.DocumentServiceRequest documentServiceRequest = new Documents.DocumentServiceRequest(
                    operationType: Documents.OperationType.Read,
                    resourceIdOrFullName: null,
                    resourceType: Documents.ResourceType.Database,
                    body: null,
                    headers: null,
                    isNameBased: false,
                    authorizationTokenType: Documents.AuthorizationTokenType.PrimaryMasterKey);

            clientSideRequestStatistics.RecordRequest(documentServiceRequest);
            clientSideRequestStatistics.RecordResponse(
                documentServiceRequest,
                new Documents.StoreResult(
                    storeResponse: new Documents.StoreResponse(),
                    exception: null,
                    partitionKeyRangeId: "PkRange",
                    lsn: 42,
                    quorumAckedLsn: 4242,
                    requestCharge: 9000.42,
                    currentReplicaSetSize: 3,
                    currentWriteQuorum: 4,
                    isValid: true,
                    storePhysicalAddress: null,
                    globalCommittedLSN: 2,
                    numberOfReadRegions: 1,
                    itemLSN: 5,
                    sessionToken: null,
                    usingLocalLSN: true,
                    activityId: Guid.NewGuid().ToString()));

            Trace queryTrace;
            using (queryTrace = Trace.GetRootTrace(name: "Cross Partition Query", component: TraceComponent.Query))
            {
                using (ITrace getQueryPlanTrace = queryTrace.StartChild("GetQueryPlan"))
                {
                    using (ITrace gatewayTrace = getQueryPlanTrace.StartChild("Gateway Call", component: TraceComponent.Transport))
                    {
                        Thread.Sleep(1);
                        gatewayTrace.Info = new CosmosDiagnosticsTraceInfo(clientSideRequestStatistics);
                    }
                }

                using (ITrace getPkRanges = queryTrace.StartChild("GetPkRanges"))
                {
                    using (ITrace addressResolution = getPkRanges.StartChild("AddressResolution", component: TraceComponent.Transport))
                    {
                        await Task.Delay(1);
                        addressResolution.Info = new CosmosDiagnosticsTraceInfo(
                            new AddressResolutionStatistics(
                                DateTime.MinValue,
                                DateTime.MinValue,
                                "https://testuri"));
                    }
                }

                using (ITrace queryPkRange1 = queryTrace.StartChild("Query PkRange 1"))
                {
                    using (ITrace continuation1 = queryPkRange1.StartChild("Continuation 1"))
                    {
                        using (ITrace gatewayTrace = continuation1.StartChild("Execute Query Direct", component: TraceComponent.Transport))
                        {
                            await Task.Delay(1);
                            gatewayTrace.Info = new CosmosDiagnosticsTraceInfo(clientSideRequestStatistics);
                        }

                        continuation1.Info = new QueryMetricsTraceInfo(MockQueryMetrics);
                    }
                }

                using (ITrace queryPkRange2 = queryTrace.StartChild("Query PkRange 2"))
                {
                    using (ITrace continuation1 = queryPkRange2.StartChild("Continuation 1"))
                    {
                        using (ITrace gatewayTrace = continuation1.StartChild("Execute Query Direct", component: TraceComponent.Transport))
                        {
                            await Task.Delay(1);
                            gatewayTrace.Info = new CosmosDiagnosticsTraceInfo(clientSideRequestStatistics);
                        }

                        continuation1.Info = new QueryMetricsTraceInfo(MockQueryMetrics);
                    }

                    using (ITrace continuation2 = queryPkRange2.StartChild("Continuation 2"))
                    {
                        using (ITrace gatewayTrace = continuation2.StartChild("Execute Query Direct", component: TraceComponent.Transport))
                        {
                            await Task.Delay(1);
                            gatewayTrace.Info = new CosmosDiagnosticsTraceInfo(clientSideRequestStatistics);
                        }

                        continuation2.Info = new QueryMetricsTraceInfo(MockQueryMetrics);
                    }
                }
            }

            string traceString = GetTraceString(queryTrace);
        }

        private static string GetTraceString(ITrace trace)
        {
            StringWriter stringWriter = new StringWriter();
            TraceWriter.WriteTrace(stringWriter, trace, asciiType: TraceWriter.AsciiType.DoubleLine);
            return stringWriter.ToString();
        }
    }
}
