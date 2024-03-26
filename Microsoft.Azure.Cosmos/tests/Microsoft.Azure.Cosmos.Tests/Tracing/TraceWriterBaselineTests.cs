//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using System.Xml;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.ReadFeed;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tests.Query.Metrics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using static Microsoft.Azure.Cosmos.Tracing.TraceData.ClientSideRequestStatisticsTraceDatum;

    [TestClass]
    public sealed class TraceWriterBaselineTests : BaselineTests<TraceWriterBaselineTests.Input, TraceWriterBaselineTests.Output>
    {
        private static readonly Lazy<QueryMetrics> MockQueryMetrics = new Lazy<QueryMetrics>(() => new QueryMetrics(
            ServerSideMetricsTests.ServerSideMetrics,
            IndexUtilizationInfoTests.MockIndexUtilizationInfo,
            ClientSideMetricsTests.MockClientSideMetrics));

        private static readonly Documents.PartitionKeyDefinition partitionKeyDefinition = new Documents.PartitionKeyDefinition()
        {
            Paths = new Collection<string>()
            {
                "/pk"
            },
            Kind = Documents.PartitionKind.Hash,
            Version = Documents.PartitionKeyDefinitionVersion.V2,
        };

        [TestMethod]
        [Timeout(5000)]
        public void Serialization()
        {
            List<Input> inputs = new List<Input>();

            int startLineNumber;
            int endLineNumber;

            //----------------------------------------------------------------
            //  Root Trace
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                TraceForBaselineTesting rootTrace;
                using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                {
                }
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Root Trace", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  Root Trace With Datum
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                TraceForBaselineTesting rootTraceWithDatum;
                using (rootTraceWithDatum = TraceForBaselineTesting.GetRootTrace())
                {
                    rootTraceWithDatum.AddDatum("QueryMetrics", new QueryMetricsTraceDatum(MockQueryMetrics));
                }
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Root Trace With Datum", rootTraceWithDatum, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  Root Trace With One Child
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                TraceForBaselineTesting rootTrace;
                using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                {
                    using (ITrace childTrace1 = rootTrace.StartChild("Child1"))
                    {
                    }
                }
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Root Trace With One Child", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  Root Trace With One Child With Datum
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                TraceForBaselineTesting rootTrace;
                using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                {
                    using (ITrace childTrace1 = rootTrace.StartChild("Child1"))
                    {
                        childTrace1.AddDatum("QueryMetrics", new QueryMetricsTraceDatum(MockQueryMetrics));
                    }
                }
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Root Trace With One Child With Datum", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  Root Trace With Two Children
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                TraceForBaselineTesting rootTrace;
                using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                {
                    using (ITrace childTrace1 = rootTrace.StartChild("Child1"))
                    {
                    }

                    using (ITrace childTrace2 = rootTrace.StartChild("Child2"))
                    {
                    }
                }
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Root Trace With Two Children", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  Root Trace With Two Children With Info
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                TraceForBaselineTesting rootTrace;
                using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                {
                    using (ITrace childTrace1 = rootTrace.StartChild("Child1"))
                    {
                        childTrace1.AddDatum("QueryMetrics", new QueryMetricsTraceDatum(MockQueryMetrics));
                    }

                    using (ITrace childTrace2 = rootTrace.StartChild("Child2"))
                    {
                        childTrace2.AddDatum("QueryMetrics", new QueryMetricsTraceDatum(MockQueryMetrics));
                    }
                }
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Root Trace With Two Children With Info", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  Trace With Grandchidren
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                TraceForBaselineTesting rootTrace;
                using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                {
                    using (ITrace childTrace1 = rootTrace.StartChild(
                        name: "Child1",
                        component: TraceComponent.Unknown,
                        level: TraceLevel.Info))
                    {
                        using (ITrace child1Child1 = childTrace1.StartChild(
                            name: "Child1Child1",
                            component: TraceComponent.Unknown,
                            level: TraceLevel.Info))
                        {
                        }

                        using (ITrace child1Child2 = childTrace1.StartChild(
                            name: "Child1Child2",
                            component: TraceComponent.Unknown,
                            level: TraceLevel.Info))
                        {
                        }
                    }

                    using (ITrace childTrace2 = rootTrace.StartChild(
                        name: "Child2",
                        component: TraceComponent.Unknown,
                        level: TraceLevel.Info))
                    {
                        using (ITrace child2Child1 = childTrace2.StartChild(
                            name: "Child2Child1",
                            component: TraceComponent.Unknown,
                            level: TraceLevel.Info))
                        {
                        }

                        using (ITrace child2Child2 = childTrace2.StartChild(
                            name: "Child2Child2",
                            component: TraceComponent.Unknown,
                            level: TraceLevel.Info))
                        {
                        }

                        using (ITrace child2Child3 = childTrace2.StartChild(
                            name: "Child2Child3",
                            component: TraceComponent.Unknown,
                            level: TraceLevel.Info))
                        {
                        }
                    }
                }

                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Trace With Grandchildren", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Timeout(5000)]
        public void TraceData()
        {
            List<Input> inputs = new List<Input>();

            int startLineNumber;
            int endLineNumber;

            //----------------------------------------------------------------
            //  Point Operation Statistics
            //----------------------------------------------------------------
            {
                {
                    startLineNumber = GetLineNumber();
                    TraceForBaselineTesting rootTrace;
                    using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                    {
                        PointOperationStatisticsTraceDatum datum = new PointOperationStatisticsTraceDatum(
                            activityId: Guid.Empty.ToString(),
                            responseTimeUtc: new DateTime(2020, 1, 2, 3, 4, 5, 6),
                            statusCode: System.Net.HttpStatusCode.OK,
                            subStatusCode: Documents.SubStatusCodes.WriteForbidden,
                            requestCharge: 4,
                            errorMessage: null,
                            method: HttpMethod.Post,
                            requestUri: "http://localhost.com",
                            requestSessionToken: nameof(PointOperationStatisticsTraceDatum.RequestSessionToken),
                            responseSessionToken: nameof(PointOperationStatisticsTraceDatum.ResponseSessionToken),
                            beLatencyInMs: "0.42");
                        rootTrace.AddDatum("Point Operation Statistics", datum);
                    }
                    endLineNumber = GetLineNumber();

                    inputs.Add(new Input("Point Operation Statistics", rootTrace, startLineNumber, endLineNumber));
                }

                {
                    startLineNumber = GetLineNumber();
                    TraceForBaselineTesting rootTrace;
                    using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                    {
                        PointOperationStatisticsTraceDatum datum = new PointOperationStatisticsTraceDatum(
                            activityId: default,
                            responseTimeUtc: default,
                            statusCode: default,
                            subStatusCode: default,
                            requestCharge: default,
                            errorMessage: default,
                            method: default,
                            requestUri: default,
                            requestSessionToken: default,
                            responseSessionToken: default,
                            beLatencyInMs: default);
                        rootTrace.AddDatum("Point Operation Statistics Default", datum);
                    }
                    endLineNumber = GetLineNumber();

                    inputs.Add(new Input("Point Operation Statistics Default", rootTrace, startLineNumber, endLineNumber));
                }
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  Query Metrics
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                TraceForBaselineTesting rootTrace;
                using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                {
                    QueryMetricsTraceDatum datum = new QueryMetricsTraceDatum(
                        new Lazy<QueryMetrics>(() => new QueryMetrics(
                            ServerSideMetricsTests.ServerSideMetrics,
                            IndexUtilizationInfoTests.MockIndexUtilizationInfo,
                            ClientSideMetricsTests.MockClientSideMetrics)));
                    rootTrace.AddDatum("Query Metrics", datum);
                }
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Query Metrics", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  Client Side Request Stats
            //----------------------------------------------------------------
            {
                {
                    startLineNumber = GetLineNumber();
                    TraceForBaselineTesting rootTrace;
                    using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                    {
                        ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(DateTime.MinValue, rootTrace);

                        TransportAddressUri uri1 = new TransportAddressUri(new Uri("http://someUri1.com"));
                        TransportAddressUri uri2 = new TransportAddressUri(new Uri("http://someUri2.com"));

                        datum.ContactedReplicas.Add(uri1);
                        datum.ContactedReplicas.Add(uri2);

                        ClientSideRequestStatisticsTraceDatum.AddressResolutionStatistics mockStatistics = new ClientSideRequestStatisticsTraceDatum.AddressResolutionStatistics(
                            DateTime.MinValue,
                            DateTime.MaxValue,
                            "http://localhost.com");

                        TraceWriterBaselineTests.GetPrivateField<Dictionary<string, AddressResolutionStatistics>>(datum, "endpointToAddressResolutionStats").Add("asdf", mockStatistics);
                        TraceWriterBaselineTests.GetPrivateField<Dictionary<string, AddressResolutionStatistics>>(datum, "endpointToAddressResolutionStats").Add("asdf2", mockStatistics);

                        datum.FailedReplicas.Add(uri1);
                        datum.FailedReplicas.Add(uri2);

                        datum.RegionsContacted.Add(("local", uri1.Uri));
                        datum.RegionsContacted.Add(("local", uri2.Uri));

                        TraceWriterBaselineTests.SetEndRequestTime(datum, DateTime.MaxValue);

                        StoreResponseStatistics storeResponseStatistics = new StoreResponseStatistics(
                            DateTime.MinValue,
                            DateTime.MaxValue,
                            StoreResult.CreateForTesting(transportRequestStats: TraceWriterBaselineTests.CreateTransportRequestStats()).Target,
                            ResourceType.Document,
                            OperationType.Query,
                            "42",
                            uri1.Uri);

                        TraceWriterBaselineTests.GetPrivateField<List<StoreResponseStatistics>>(datum, "storeResponseStatistics").Add(storeResponseStatistics);
                        rootTrace.AddDatum("Client Side Request Stats", datum);
                    }
                    endLineNumber = GetLineNumber();

                    inputs.Add(new Input("Client Side Request Stats", rootTrace, startLineNumber, endLineNumber));
                }

                {
                    startLineNumber = GetLineNumber();
                    TraceForBaselineTesting rootTrace;
                    using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                    {
                        ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(DateTime.MinValue, rootTrace);
                        datum.ContactedReplicas.Add(default);

                        TraceWriterBaselineTests.GetPrivateField<Dictionary<string, AddressResolutionStatistics>>(datum, "endpointToAddressResolutionStats").Add("asdf", default);
                        TraceWriterBaselineTests.GetPrivateField<Dictionary<string, AddressResolutionStatistics>>(datum, "endpointToAddressResolutionStats").Add("asdf2", default);

                        datum.FailedReplicas.Add(default);

                        datum.RegionsContacted.Add(default);

                        TraceWriterBaselineTests.SetEndRequestTime(datum, default);

                        StoreResponseStatistics storeResponseStatistics = new StoreResponseStatistics(
                            requestStartTime: default,
                            requestResponseTime: default,
                            StoreResult.CreateForTesting(storeResponse: new StoreResponse()).Target,
                            resourceType: default,
                            operationType: default,
                            requestSessionToken: default,
                            locationEndpoint: default);

                        TraceWriterBaselineTests.GetPrivateField<List<StoreResponseStatistics>>(datum, "storeResponseStatistics").Add(storeResponseStatistics);
                        rootTrace.AddDatum("Client Side Request Stats Default", datum);
                    }
                    endLineNumber = GetLineNumber();

                    inputs.Add(new Input("Client Side Request Stats Default", rootTrace, startLineNumber, endLineNumber));
                }

                {
                    startLineNumber = GetLineNumber();
                    TraceForBaselineTesting rootTrace;
                    using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                    {
                        ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(DateTime.MinValue, rootTrace);
                        TraceWriterBaselineTests.SetEndRequestTime(datum,DateTime.MaxValue);

                        HttpResponseStatistics httpResponseStatistics = new HttpResponseStatistics(
                            DateTime.MinValue,
                            DateTime.MaxValue,
                            new Uri("http://someUri1.com"),
                            HttpMethod.Get,
                            ResourceType.Document,
                            new HttpResponseMessage(System.Net.HttpStatusCode.OK) { ReasonPhrase = "Success" },
                            exception: null);

                        TraceWriterBaselineTests.GetPrivateField<List<HttpResponseStatistics>>(datum, "httpResponseStatistics").Add(httpResponseStatistics);

                        HttpResponseStatistics httpResponseStatisticsException = new HttpResponseStatistics(
                            DateTime.MinValue,
                            DateTime.MaxValue,
                            new Uri("http://someUri1.com"),
                            HttpMethod.Get,
                            ResourceType.Document,
                            responseMessage: null,
                            exception: new OperationCanceledException());
                        TraceWriterBaselineTests.GetPrivateField<List<HttpResponseStatistics>>(datum, "httpResponseStatistics").Add(httpResponseStatisticsException);

                        rootTrace.AddDatum("Client Side Request Stats", datum);
                    }
                    endLineNumber = GetLineNumber();

                    inputs.Add(new Input("Client Side Request Stats For Gateway Request", rootTrace, startLineNumber, endLineNumber));
                }
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  CPU History
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                TraceForBaselineTesting rootTrace;
                using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                {
                    CpuHistoryTraceDatum datum = new CpuHistoryTraceDatum(
                        new Documents.Rntbd.SystemUsageHistory(
                            new ReadOnlyCollection<Documents.Rntbd.SystemUsageLoad>(
                                new List<Documents.Rntbd.SystemUsageLoad>()
                                {
                                    new Documents.Rntbd.SystemUsageLoad(
                                        DateTime.MinValue,
                                        this.GetThreadInfo(),
                                        42,
                                        1000),
                                    new Documents.Rntbd.SystemUsageLoad(
                                        DateTime.MinValue,
                                        this.GetThreadInfo(),
                                        23,
                                        9000),
                                }),
                            monitoringInterval: TimeSpan.MaxValue));
                    rootTrace.AddDatum("System Info", datum);
                }
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("System Info", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            this.ExecuteTestSuite(inputs);
        }

        private Documents.Rntbd.ThreadInformation GetThreadInfo()
        {
            Documents.Rntbd.ThreadInformation threadInfo = Documents.Rntbd.ThreadInformation.Get();
            Type threadInfoType = threadInfo.GetType();
            FieldInfo prop = threadInfoType.GetField("<AvailableThreads>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            prop.SetValue(threadInfo, null);

            prop = threadInfoType.GetField("<IsThreadStarving>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            prop.SetValue(threadInfo, null);

            prop = threadInfoType.GetField("<MinThreads>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            prop.SetValue(threadInfo, null);

            prop = threadInfoType.GetField("<MaxThreads>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            prop.SetValue(threadInfo, null);

            prop = threadInfoType.GetField("<ThreadWaitIntervalInMs>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            prop.SetValue(threadInfo, null);

            return threadInfo;
        }

        [TestMethod]
        [Timeout(5000)]
        public async Task ScenariosAsync()
        {
            List<Input> inputs = new List<Input>();

            int startLineNumber;
            int endLineNumber;

            //----------------------------------------------------------------
            //  ReadFeed
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                int numItems = 100;
                IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);
                CrossPartitionReadFeedAsyncEnumerator enumerator = CrossPartitionReadFeedAsyncEnumerator.Create(
                    documentContainer,
                    new CrossFeedRangeState<ReadFeedState>(ReadFeedCrossFeedRangeState.CreateFromBeginning().FeedRangeStates),
                    new ReadFeedPaginationOptions(pageSizeHint: 10),
                    cancellationToken: default);

                int numChildren = 1; // One extra since we need to read one past the last user page to get the null continuation.
                TraceForBaselineTesting rootTrace;
                using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                {
                    while (await enumerator.MoveNextAsync(rootTrace))
                    {
                        numChildren++;
                    }
                }

                Assert.AreEqual(numChildren, rootTrace.Children.Count);
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("ReadFeed", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  ChangeFeed
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                int numItems = 100;
                IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);
                CrossPartitionChangeFeedAsyncEnumerator enumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                    documentContainer,
                    new CrossFeedRangeState<ChangeFeedState>(
                        ChangeFeedCrossFeedRangeState.CreateFromBeginning().FeedRangeStates),
                    new ChangeFeedPaginationOptions(
                        ChangeFeedMode.Incremental,
                        pageSizeHint: int.MaxValue),
                    cancellationToken: default);

                int numChildren = 0;
                TraceForBaselineTesting rootTrace;
                using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                {
                    while (await enumerator.MoveNextAsync(rootTrace))
                    {
                        numChildren++;

                        if (enumerator.Current.Result.Page is ChangeFeedNotModifiedPage)
                        {
                            break;
                        }
                    }
                }

                Assert.AreEqual(numChildren, rootTrace.Children.Count);
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("ChangeFeed", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  Query
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                int numItems = 100;
                IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);
                IQueryPipelineStage pipelineStage = CreatePipeline(documentContainer, "SELECT * FROM c", pageSize: 10);

                TraceForBaselineTesting rootTrace;
                int numChildren = (await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, default)).Count; // One extra since we need to read one past the last user page to get the null continuation.
                using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                {
                    while (await pipelineStage.MoveNextAsync(rootTrace))
                    {
                        numChildren++;
                    }
                }

                Assert.AreEqual(numChildren, rootTrace.Children.Count);
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Query", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            this.ExecuteTestSuite(inputs);
        }

        public override Output ExecuteTest(Input input)
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("th-TH", false);
            try
            {
                string text = TraceWriter.TraceToText(input.Trace);
                string json = TraceWriter.TraceToJson(input.Trace);

                return new Output(text, JToken.Parse(json).ToString(Newtonsoft.Json.Formatting.Indented));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        private static async Task<IDocumentContainer> CreateDocumentContainerAsync(
            int numItems,
            FlakyDocumentContainer.FailureConfigs failureConfigs = default)
        {
            Documents.PartitionKeyDefinition partitionKeyDefinition = new Documents.PartitionKeyDefinition()
            {
                Paths = new System.Collections.ObjectModel.Collection<string>()
                    {
                        "/pk"
                    },
                Kind = Documents.PartitionKind.Hash,
                Version = Documents.PartitionKeyDefinitionVersion.V2,
            };

            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(partitionKeyDefinition);
            if (failureConfigs != null)
            {
                monadicDocumentContainer = new FlakyDocumentContainer(monadicDocumentContainer, failureConfigs);
            }

            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            for (int i = 0; i < 3; i++)
            {
                IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);
                foreach (FeedRangeInternal range in ranges)
                {
                    await documentContainer.SplitAsync(range, cancellationToken: default);
                }

                await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
            }

            for (int i = 0; i < numItems; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                while (true)
                {
                    TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(item, cancellationToken: default);
                    if (monadicCreateRecord.Succeeded)
                    {
                        break;
                    }
                }
            }

            return documentContainer;
        }

        internal static TransportRequestStats CreateTransportRequestStats()
        {
            DateTime defaultDateTime = new DateTime(
                year: 2021,
                month: 12,
                day: 31,
                hour: 23,
                minute: 59,
                second: 59,
                millisecond: 59,
                kind: DateTimeKind.Utc);

            TransportRequestStats transportRequestStats = new TransportRequestStats();
            transportRequestStats.RecordState(TransportRequestStats.RequestStage.ChannelAcquisitionStarted);
            transportRequestStats.RecordState(TransportRequestStats.RequestStage.Pipelined);
            transportRequestStats.RecordState(TransportRequestStats.RequestStage.Sent);
            transportRequestStats.RecordState(TransportRequestStats.RequestStage.Received);
            transportRequestStats.RecordState(TransportRequestStats.RequestStage.Completed);

            FieldInfo field = transportRequestStats.GetType().GetField("requestCreatedTime", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(transportRequestStats, defaultDateTime);

            field = transportRequestStats.GetType().GetField("channelAcquisitionStartedTime", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(transportRequestStats, TimeSpan.FromMilliseconds(1));

            field = transportRequestStats.GetType().GetField("requestPipelinedTime", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(transportRequestStats, TimeSpan.FromMilliseconds(1));

            field = transportRequestStats.GetType().GetField("requestSentTime", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(transportRequestStats, TimeSpan.FromMilliseconds(1));

            field = transportRequestStats.GetType().GetField("requestReceivedTime", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(transportRequestStats, TimeSpan.FromMilliseconds(1));

            field = transportRequestStats.GetType().GetField("requestCompletedTime", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(transportRequestStats, TimeSpan.FromMilliseconds(1));

            transportRequestStats.RequestSizeInBytes = 2;
            transportRequestStats.RequestBodySizeInBytes = 1;
            transportRequestStats.ResponseBodySizeInBytes = 1;
            transportRequestStats.ResponseMetadataSizeInBytes = 1;

            transportRequestStats.NumberOfInflightRequestsToEndpoint = 2;
            transportRequestStats.NumberOfOpenConnectionsToEndpoint = 1;
            transportRequestStats.NumberOfInflightRequestsInConnection = 1;
            transportRequestStats.RequestWaitingForConnectionInitialization = true;
            transportRequestStats.ConnectionLastSendTime = defaultDateTime;
            transportRequestStats.ConnectionLastSendAttemptTime = defaultDateTime;
            transportRequestStats.ConnectionLastReceiveTime = defaultDateTime;
            return transportRequestStats;
        }

        internal static T GetPrivateField<T>(
            ClientSideRequestStatisticsTraceDatum datum,
            string propertyName)
        {
            return (T)datum.GetType().GetField(propertyName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(datum);
        }

        internal static void SetEndRequestTime(
            ClientSideRequestStatisticsTraceDatum datum,
            DateTime? dateTime)
        {
            datum.GetType().GetProperty("RequestEndTimeUtc").SetValue(datum, dateTime);
        }

        private static IQueryPipelineStage CreatePipeline(IDocumentContainer documentContainer, string query, int pageSize = 10, CosmosElement state = null)
        {
            TryCatch<IQueryPipelineStage> tryCreatePipeline = PipelineFactory.MonadicCreate(
                ExecutionEnvironment.Compute,
                documentContainer,
                new SqlQuerySpec(query),
                new List<FeedRangeEpk>() { FeedRangeEpk.FullRange },
                partitionKey: null,
                GetQueryPlan(query),
                new QueryPaginationOptions(pageSizeHint: pageSize),
                containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                maxConcurrency: 10,
                requestCancellationToken: default,
                requestContinuationToken: state);

            tryCreatePipeline.ThrowIfFailed();

            return tryCreatePipeline.Result;
        }

        private static QueryInfo GetQueryPlan(string query)
        {
            TryCatch<PartitionedQueryExecutionInfoInternal> info = QueryPartitionProviderTestInstance.Object.TryGetPartitionedQueryExecutionInfoInternal(
                Newtonsoft.Json.JsonConvert.SerializeObject(new SqlQuerySpec(query)),
                partitionKeyDefinition,
                requireFormattableOrderByQuery: true,
                isContinuationExpected: false,
                allowNonValueAggregateQuery: true,
                hasLogicalPartitionKey: false,
                allowDCount: true,
                useSystemPrefix: false,
                geospatialType: Cosmos.GeospatialType.Geography);

            info.ThrowIfFailed();
            return info.Result.QueryInfo;
        }

        private static int GetLineNumber([CallerLineNumber] int lineNumber = 0)
        {
            return lineNumber;
        }

        public sealed class Input : BaselineTestInput
        {
            private static readonly string[] sourceCode = File.ReadAllLines($"Tracing\\{nameof(TraceWriterBaselineTests)}.cs");

            internal Input(string description, ITrace trace, int startLineNumber, int endLineNumber)
                : base(description)
            {
                this.Trace = trace ?? throw new ArgumentNullException(nameof(trace));
                this.StartLineNumber = startLineNumber;
                this.EndLineNumber = endLineNumber;
            }

            internal ITrace Trace { get; }

            public int StartLineNumber { get; }

            public int EndLineNumber { get; }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteElementString(nameof(this.Description), this.Description);
                xmlWriter.WriteStartElement("Setup");
                ArraySegment<string> codeSnippet = new ArraySegment<string>(
                    sourceCode,
                    this.StartLineNumber,
                    this.EndLineNumber - this.StartLineNumber - 1);

                string setup;
                try
                {
                    setup =
                    Environment.NewLine
                    + string
                        .Join(
                            Environment.NewLine,
                            codeSnippet
                                .Select(x => x != string.Empty ? x.Substring("            ".Length) : string.Empty))
                    + Environment.NewLine;
                }
                catch(Exception)
                {
                    throw;
                }
                xmlWriter.WriteCData(setup ?? "asdf");
                xmlWriter.WriteEndElement();
            }
        }

        public sealed class Output : BaselineTestOutput
        {
            public Output(string text, string json)
            {
                this.Text = text ?? throw new ArgumentNullException(nameof(text));
                this.Json = json ?? throw new ArgumentNullException(nameof(json));
            }

            public string Text { get; }

            public string Json { get; }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteStartElement(nameof(this.Text));
                xmlWriter.WriteCData(this.Text);
                xmlWriter.WriteEndElement();

                xmlWriter.WriteStartElement(nameof(this.Json));
                xmlWriter.WriteCData(this.Json);
                xmlWriter.WriteEndElement();
            }
        }

        private sealed class TraceForBaselineTesting : ITrace
        {
            private readonly Dictionary<string, object> data;
            private readonly List<ITrace> children;

            public TraceForBaselineTesting(
                string name,
                TraceLevel level,
                TraceComponent component,
                TraceForBaselineTesting parent)
            {
                this.Name = name ?? throw new ArgumentNullException(nameof(name));
                this.Level = level;
                this.Component = component;
                this.Parent = parent;
                this.children = new List<ITrace>();
                this.data = new Dictionary<string, object>();
            }

            public string Name { get; }

            public Guid Id => Guid.Empty;

            public DateTime StartTime => DateTime.MinValue;

            public TimeSpan Duration => TimeSpan.Zero;

            public TraceLevel Level { get; }

            public TraceComponent Component { get; }

            public TraceSummary Summary { get; }

            public ITrace Parent { get; }

            public IReadOnlyList<ITrace> Children => this.children;

            public IReadOnlyDictionary<string, object> Data => this.data;

            public IReadOnlyList<(string, Uri)> RegionsContacted => new List<(string, Uri)>();

            public void AddDatum(string key, TraceDatum traceDatum)
            {
                this.data[key] = traceDatum;
            }

            public void AddDatum(string key, object value)
            {
                this.data[key] = value;
            }

            public void Dispose()
            {
            }

            public ITrace StartChild(string name)
            {
                return this.StartChild(name, TraceComponent.Unknown, TraceLevel.Info);
            }

            public ITrace StartChild(string name, TraceComponent component, TraceLevel level)
            {
                TraceForBaselineTesting child = new TraceForBaselineTesting(name, level, component, parent: this);
                this.AddChild(child);
                return child;
            }

            public void AddChild(ITrace trace)
            {
                this.children.Add(trace);
            }

            public static TraceForBaselineTesting GetRootTrace()
            {
                return new TraceForBaselineTesting("Trace For Baseline Testing", TraceLevel.Info, TraceComponent.Unknown, parent: null);
            }

            public void UpdateRegionContacted(TraceDatum traceDatum)
            {
                //NoImplementation
            }

            public void AddOrUpdateDatum(string key, object value)
            {
                this.data[key] = value;
            }
        }
    }
}
