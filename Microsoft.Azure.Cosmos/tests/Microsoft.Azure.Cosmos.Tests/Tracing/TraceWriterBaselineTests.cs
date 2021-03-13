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
        private static readonly QueryMetrics MockQueryMetrics = new QueryMetrics(
            BackendMetricsTests.MockBackendMetrics,
            IndexUtilizationInfoTests.MockIndexUtilizationInfo,
            ClientSideMetricsTests.MockClientSideMetrics);

        private static readonly Dictionary<string, object> DefaultQueryEngineConfiguration = new Dictionary<string, object>()
        {
            {"maxSqlQueryInputLength", 30720},
            {"maxJoinsPerSqlQuery", 5},
            {"maxLogicalAndPerSqlQuery", 200},
            {"maxLogicalOrPerSqlQuery", 200},
            {"maxUdfRefPerSqlQuery", 2},
            {"maxInExpressionItemsCount", 8000},
            {"queryMaxInMemorySortDocumentCount", 500},
            {"maxQueryRequestTimeoutFraction", 0.90},
            {"sqlAllowNonFiniteNumbers", false},
            {"sqlAllowAggregateFunctions", true},
            {"sqlAllowSubQuery", true},
            {"sqlAllowScalarSubQuery", false},
            {"allowNewKeywords", true},
            {"sqlAllowLike", false},
            {"sqlAllowGroupByClause", false},
            {"maxSpatialQueryCells", 12},
            {"spatialMaxGeometryPointCount", 256},
            {"sqlDisableQueryILOptimization", false},
            {"sqlDisableFilterPlanOptimization", false}
        };

        private static readonly QueryPartitionProvider queryPartitionProvider = new QueryPartitionProvider(DefaultQueryEngineConfiguration);
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
                            responseSessionToken: nameof(PointOperationStatisticsTraceDatum.ResponseSessionToken));
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
                            responseSessionToken: default);
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
                        new QueryMetrics(
                            BackendMetricsTests.MockBackendMetrics,
                            IndexUtilizationInfoTests.MockIndexUtilizationInfo,
                            ClientSideMetricsTests.MockClientSideMetrics));
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
                        ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(DateTime.MinValue);

                        Uri uri1 = new Uri("http://someUri1.com");
                        Uri uri2 = new Uri("http://someUri2.com");

                        datum.ContactedReplicas.Add(uri1);
                        datum.ContactedReplicas.Add(uri2);

                        ClientSideRequestStatisticsTraceDatum.AddressResolutionStatistics mockStatistics = new ClientSideRequestStatisticsTraceDatum.AddressResolutionStatistics(
                            DateTime.MinValue,
                            DateTime.MaxValue,
                            "http://localhost.com");
                        datum.EndpointToAddressResolutionStatistics["asdf"] = mockStatistics;
                        datum.EndpointToAddressResolutionStatistics["asdf2"] = mockStatistics;

                        datum.FailedReplicas.Add(uri1);
                        datum.FailedReplicas.Add(uri2);

                        datum.RegionsContacted.Add(uri1);
                        datum.RegionsContacted.Add(uri2);

                        datum.RequestEndTimeUtc = DateTime.MaxValue;

                        StoreResponseStatistics storeResponseStatistics = new StoreResponseStatistics(
                            DateTime.MinValue,
                            DateTime.MaxValue,
                            new Documents.StoreResult(
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
                                activityId: Guid.Empty.ToString()),
                            ResourceType.Document,
                            OperationType.Query,
                            uri1);
                        datum.StoreResponseStatisticsList.Add(storeResponseStatistics);
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
                        ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(DateTime.MinValue);
                        datum.ContactedReplicas.Add(default);

                        ClientSideRequestStatisticsTraceDatum.AddressResolutionStatistics mockStatistics = new ClientSideRequestStatisticsTraceDatum.AddressResolutionStatistics(
                            default,
                            default,
                            targetEndpoint: "asdf");
                        datum.EndpointToAddressResolutionStatistics["asdf"] = default;
                        datum.EndpointToAddressResolutionStatistics["asdf2"] = default;

                        datum.FailedReplicas.Add(default);

                        datum.RegionsContacted.Add(default);

                        datum.RequestEndTimeUtc = default;

                        StoreResponseStatistics storeResponseStatistics = new StoreResponseStatistics(
                            requestStartTime: default,
                            requestResponseTime: default,
                            new Documents.StoreResult(
                                storeResponse: new StoreResponse(),
                                exception: default,
                                partitionKeyRangeId: default,
                                lsn: default,
                                quorumAckedLsn: default,
                                requestCharge: default,
                                currentReplicaSetSize: default,
                                currentWriteQuorum: default,
                                isValid: default,
                                storePhysicalAddress: default,
                                globalCommittedLSN: default,
                                numberOfReadRegions: default,
                                itemLSN: default,
                                sessionToken: default,
                                usingLocalLSN: default,
                                activityId: default),
                            resourceType: default,
                            operationType: default,
                            locationEndpoint: default); ;
                        datum.StoreResponseStatisticsList.Add(storeResponseStatistics);
                        rootTrace.AddDatum("Client Side Request Stats Default", datum);
                    }
                    endLineNumber = GetLineNumber();

                    inputs.Add(new Input("Client Side Request Stats Default", rootTrace, startLineNumber, endLineNumber));
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
                        new Documents.Rntbd.CpuLoadHistory(
                            new ReadOnlyCollection<Documents.Rntbd.CpuLoad>(
                                new List<Documents.Rntbd.CpuLoad>()
                                {
                                    new Documents.Rntbd.CpuLoad(DateTime.MinValue, 42),
                                    new Documents.Rntbd.CpuLoad(DateTime.MinValue, 23),
                                }),
                            monitoringInterval: TimeSpan.MaxValue));
                    rootTrace.AddDatum("CPU History", datum);
                }
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("CPU History", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
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
                int numChildren = 1; // One extra since we need to read one past the last user page to get the null continuation.
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

        private static IQueryPipelineStage CreatePipeline(IDocumentContainer documentContainer, string query, int pageSize = 10, CosmosElement state = null)
        {
            TryCatch<IQueryPipelineStage> tryCreatePipeline = PipelineFactory.MonadicCreate(
                ExecutionEnvironment.Compute,
                documentContainer,
                new SqlQuerySpec(query),
                new List<FeedRangeEpk>() { FeedRangeEpk.FullRange },
                partitionKey: null,
                GetQueryPlan(query),
                new QueryPaginationOptions(pageSizeHint: 10),
                maxConcurrency: 10,
                requestCancellationToken: default,
                requestContinuationToken: state);

            tryCreatePipeline.ThrowIfFailed();

            return tryCreatePipeline.Result;
        }

        private static QueryInfo GetQueryPlan(string query)
        {
            TryCatch<PartitionedQueryExecutionInfoInternal> info = queryPartitionProvider.TryGetPartitionedQueryExecutionInfoInternal(
                new SqlQuerySpec(query),
                partitionKeyDefinition,
                requireFormattableOrderByQuery: true,
                isContinuationExpected: false,
                allowNonValueAggregateQuery: true,
                hasLogicalPartitionKey: false);

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
                catch(Exception ex)
                {
                    throw ex;
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
            private readonly List<TraceForBaselineTesting> children;

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
                this.children = new List<TraceForBaselineTesting>();
                this.data = new Dictionary<string, object>();
            }

            public string Name { get; }

            public Guid Id => Guid.Empty;

            public CallerInfo CallerInfo => new CallerInfo("MemberName", "FilePath", 42);

            public DateTime StartTime => DateTime.MinValue;

            public TimeSpan Duration => TimeSpan.Zero;

            public TraceLevel Level { get; }

            public TraceComponent Component { get; }

            public ITrace Parent { get; }

            public IReadOnlyList<ITrace> Children => this.children;

            public IReadOnlyDictionary<string, object> Data => this.data;

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

            public ITrace StartChild(string name, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
            {
                return this.StartChild(name, TraceComponent.Unknown, TraceLevel.Info, memberName, sourceFilePath, sourceLineNumber);
            }

            public ITrace StartChild(string name, TraceComponent component, TraceLevel level, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
            {
                TraceForBaselineTesting child = new TraceForBaselineTesting(name, level, component, parent: this);
                this.children.Add(child);
                return child;
            }

            public static TraceForBaselineTesting GetRootTrace()
            {
                return new TraceForBaselineTesting("Trace For Baseline Testing", TraceLevel.Info, TraceComponent.Unknown, parent: null);
            }
        }
    }
}
