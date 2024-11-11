//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class OrderByCrossPartitionQueryPipelineStageTests
    {
        private static IReadOnlyList<(string serializedToken, CosmosElement element)> TokenTestData()
        {
            Guid guid = Guid.Parse("69D5AB17-C94A-4173-A278-B59D0D9C7C37");
            byte[] randomBytes = guid.ToByteArray();
            string hexString = PartitionKeyInternal.HexConvert.ToHex(randomBytes, 0, randomBytes.Length);

            return new List<(string, CosmosElement)>
            {
                ("[42, 37]", CosmosArray.Parse("[42, 37]")),
                ($@"{{C_Binary(""0x{hexString}"")}}", CosmosBinary.Create(new ReadOnlyMemory<byte>(randomBytes))),
                ("false", CosmosBoolean.Create(false)),
                ($@"{{C_Guid(""{guid}"")}}", CosmosGuid.Create(guid)),
                ("null", CosmosNull.Create()),
                ("1", CosmosInt64.Create(1)),
                ("{\"foo\": false}", CosmosObject.Parse("{\"foo\": false}")),
                ("asdf", CosmosString.Create("asdf"))
            };
        }

        private static IReadOnlyList<(string serializedToken, SqlQueryResumeValue resumeValue)> ResumeValueTestData()
        {
            return new List<(string, SqlQueryResumeValue)>
            {
                ("[]", SqlQueryResumeValue.FromCosmosElement(CosmosUndefined.Create())),
                ("null", SqlQueryResumeValue.FromCosmosElement(CosmosNull.Create())),
                ("false", SqlQueryResumeValue.FromCosmosElement(CosmosBoolean.Create(false))),
                ("true", SqlQueryResumeValue.FromCosmosElement(CosmosBoolean.Create(true))),
                ("1337", SqlQueryResumeValue.FromCosmosElement(CosmosNumber64.Create(1337))),
                ("asdf", SqlQueryResumeValue.FromCosmosElement(CosmosString.Create("asdf"))),
                ("{\"type\":\"array\",\"low\":-6706074647855398782,\"high\":9031114912533472255}", SqlQueryResumeValue.FromOrderByValue(CosmosArray.Parse("[]"))),
                ("{\"type\":\"object\",\"low\":1457042291250783704,\"high\":1493060239874959160}", SqlQueryResumeValue.FromOrderByValue(CosmosObject.Parse("{}")))
            };
        }

        [TestMethod]
        public void MonadicCreate_NullContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c ORDER BY c._ts"),
                targetRanges: new List<FeedRangeEpk>() { FeedRangeEpk.FullRange },
                partitionKey: null,
                orderByColumns: new List<OrderByColumn>()
                {
                    new OrderByColumn("_ts", SortOrder.Ascending)
                },
                queryPaginationOptions: new QueryExecutionOptions(pageSizeHint: 10),
                maxConcurrency: 10,
                nonStreamingOrderBy: false,
                continuationToken: null,
                containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                emitRawOrderByPayload: false);
            Assert.IsTrue(monadicCreate.Succeeded);
        }

        [TestMethod]
        public void MonadicCreate_NonCosmosArrayContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c ORDER BY c._ts"),
                targetRanges: new List<FeedRangeEpk>() { FeedRangeEpk.FullRange },
                partitionKey: null,
                orderByColumns: new List<OrderByColumn>()
                {
                    new OrderByColumn("_ts", SortOrder.Ascending)
                },
                queryPaginationOptions: new QueryExecutionOptions(pageSizeHint: 10),
                maxConcurrency: 10,
                nonStreamingOrderBy: false,
                continuationToken: CosmosObject.Create(new Dictionary<string, CosmosElement>()),
                containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                emitRawOrderByPayload: false);
            Assert.IsTrue(monadicCreate.Failed);
            Assert.IsTrue(monadicCreate.InnerMostException is MalformedContinuationTokenException);
        }

        [TestMethod]
        public void MonadicCreate_EmptyArrayContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c ORDER BY c._ts"),
                targetRanges: new List<FeedRangeEpk>() { FeedRangeEpk.FullRange },
                partitionKey: null,
                orderByColumns: new List<OrderByColumn>()
                {
                    new OrderByColumn("_ts", SortOrder.Ascending)
                },
                queryPaginationOptions: new QueryExecutionOptions(pageSizeHint: 10),
                maxConcurrency: 10,
                nonStreamingOrderBy: false,
                continuationToken: CosmosArray.Create(new List<CosmosElement>()),
                containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                emitRawOrderByPayload: false);
            Assert.IsTrue(monadicCreate.Failed);
            Assert.IsTrue(monadicCreate.InnerMostException is MalformedContinuationTokenException);
        }

        [TestMethod]
        public void MonadicCreate_NonParallelContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c ORDER BY c._ts"),
                targetRanges: new List<FeedRangeEpk>() { FeedRangeEpk.FullRange },
                partitionKey: null,
                orderByColumns: new List<OrderByColumn>()
                {
                    new OrderByColumn("_ts", SortOrder.Ascending)
                },
                queryPaginationOptions: new QueryExecutionOptions(pageSizeHint: 10),
                maxConcurrency: 10,
                nonStreamingOrderBy: false,
                continuationToken: CosmosArray.Create(new List<CosmosElement>() { CosmosString.Create("asdf") }),
                containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                emitRawOrderByPayload: false); 
            Assert.IsTrue(monadicCreate.Failed);
            Assert.IsTrue(monadicCreate.InnerMostException is MalformedContinuationTokenException);
        }

        [TestMethod]
        public void MonadicCreate_SingleOrderByContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();
            IReadOnlyList<(string serializedToken, CosmosElement element)> tokens = TokenTestData();

            foreach ((string serializedToken, CosmosElement element) in tokens)
            {
                ParallelContinuationToken parallelContinuationToken = new ParallelContinuationToken(
                    token: serializedToken,
                    range: new Documents.Routing.Range<string>("A", "B", true, false));

                OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                    parallelContinuationToken,
                    new List<OrderByItem>() { new OrderByItem(CosmosObject.Create(new Dictionary<string, CosmosElement>() { { "item", element} })) },
                    resumeValues: null,
                    rid: "rid",
                    skipCount: 42,
                    filter: "filter");

                TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: mockDocumentContainer.Object,
                    sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c ORDER BY c.item"),
                    targetRanges: new List<FeedRangeEpk>() { new FeedRangeEpk(new Range<string>(min: "A", max: "B", isMinInclusive: true, isMaxInclusive: false)) },
                    partitionKey: null,
                    orderByColumns: new List<OrderByColumn>()
                    {
                    new OrderByColumn("item", SortOrder.Ascending)
                    },
                    queryPaginationOptions: new QueryExecutionOptions(pageSizeHint: 10),
                    maxConcurrency: 10,
                    nonStreamingOrderBy: false,
                    continuationToken: CosmosArray.Create(
                        new List<CosmosElement>()
                        {
                        OrderByContinuationToken.ToCosmosElement(orderByContinuationToken)
                        }),
                    containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                    emitRawOrderByPayload: false);
                Assert.IsTrue(monadicCreate.Succeeded);
            }

            foreach ((string token1, CosmosElement element1) in tokens)
            {
                foreach ((string token2, CosmosElement element2) in tokens)
                {
                    ParallelContinuationToken parallelContinuationToken1 = new ParallelContinuationToken(
                        token: $"[{token1}, {token2}]",
                        range: new Documents.Routing.Range<string>("A", "B", true, false));

                    OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                        parallelContinuationToken1,
                        new List<OrderByItem>()
                        {
                            new OrderByItem(CosmosObject.Create(new Dictionary<string, CosmosElement>(){ { "item1", element1 } })),
                            new OrderByItem(CosmosObject.Create(new Dictionary<string, CosmosElement>(){ { "item2", element2 } }))
                        },
                        resumeValues: null,
                        rid: "rid",
                        skipCount: 42,
                        filter: "filter");

                    TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                        documentContainer: mockDocumentContainer.Object,
                        sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c ORDER BY c.item1, c.item2"),
                        targetRanges: new List<FeedRangeEpk>()
                        {
                            new FeedRangeEpk(new Range<string>(min: "A", max: "B", isMinInclusive: true, isMaxInclusive: false)),
                            new FeedRangeEpk(new Range<string>(min: "B", max: "C", isMinInclusive: true, isMaxInclusive: false)),
                        },
                        partitionKey: null,
                        orderByColumns: new List<OrderByColumn>()
                        {
                               new OrderByColumn("item1", SortOrder.Ascending),
                               new OrderByColumn("item2", SortOrder.Ascending)
                        },
                        queryPaginationOptions: new QueryExecutionOptions(pageSizeHint: 10),
                        maxConcurrency: 10,
                        nonStreamingOrderBy: false,
                        continuationToken: CosmosArray.Create(
                            new List<CosmosElement>()
                            {
                                OrderByContinuationToken.ToCosmosElement(orderByContinuationToken)
                            }),
                        containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                        emitRawOrderByPayload: false);
                    Assert.IsTrue(monadicCreate.Succeeded);
                }
            }
        }

        [TestMethod]
        public void MonadicCreate_MultipleOrderByContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();
            IReadOnlyList<(string serializedToken, CosmosElement element)> tokens = TokenTestData();

            foreach((string token1, CosmosElement element1) in tokens)
            {
                foreach ((string token2, CosmosElement element2) in tokens)
                {
                    ParallelContinuationToken parallelContinuationToken1 = new ParallelContinuationToken(
                        token: token1,
                        range: new Documents.Routing.Range<string>("A", "B", true, false));

                    OrderByContinuationToken orderByContinuationToken1 = new OrderByContinuationToken(
                        parallelContinuationToken1,
                        new List<OrderByItem>() { new OrderByItem(CosmosObject.Create(new Dictionary<string, CosmosElement>() { { "item", element1 } })) },
                        resumeValues: null,
                        rid: "rid",
                        skipCount: 42,
                        filter: "filter");

                    ParallelContinuationToken parallelContinuationToken2 = new ParallelContinuationToken(
                        token: token2,
                        range: new Documents.Routing.Range<string>("B", "C", true, false));

                    OrderByContinuationToken orderByContinuationToken2 = new OrderByContinuationToken(
                        parallelContinuationToken2,
                        new List<OrderByItem>() { new OrderByItem(CosmosObject.Create(new Dictionary<string, CosmosElement>() { { "item", element2 } })) },
                        resumeValues: null,
                        rid: "rid",
                        skipCount: 42,
                        filter: "filter");

                    TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                        documentContainer: mockDocumentContainer.Object,
                        sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c ORDER BY c.item"),
                        targetRanges: new List<FeedRangeEpk>()
                        {
                            new FeedRangeEpk(new Range<string>(min: "A", max: "B", isMinInclusive: true, isMaxInclusive: false)),
                            new FeedRangeEpk(new Range<string>(min: "B", max: "C", isMinInclusive: true, isMaxInclusive: false)),
                        },
                        partitionKey: null,
                        orderByColumns: new List<OrderByColumn>()
                        {
                               new OrderByColumn("item", SortOrder.Ascending)
                        },
                        queryPaginationOptions: new QueryExecutionOptions(pageSizeHint: 10),
                        maxConcurrency: 10,
                        nonStreamingOrderBy: false,
                        continuationToken: CosmosArray.Create(
                            new List<CosmosElement>()
                            {
                                OrderByContinuationToken.ToCosmosElement(orderByContinuationToken1),
                                OrderByContinuationToken.ToCosmosElement(orderByContinuationToken2)
                            }),
                        containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                        emitRawOrderByPayload: false);
                    Assert.IsTrue(monadicCreate.Succeeded);
                }
            }
        }

        [TestMethod]
        public void MonadicCreate_OrderByWithResumeValues()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();
            IReadOnlyList<(string serializedToken, SqlQueryResumeValue resumeValue)> tokens = ResumeValueTestData();

            foreach ((string serializedToken, SqlQueryResumeValue resumeValue) in tokens)
            {
                ParallelContinuationToken parallelContinuationToken = new ParallelContinuationToken(
                    token: serializedToken,
                    range: new Documents.Routing.Range<string>("A", "B", true, false));

                OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                    parallelContinuationToken,
                    orderByItems: null,
                    resumeValues: new List<SqlQueryResumeValue>() { resumeValue},
                    rid: "rid",
                    skipCount: 42,
                    filter: null);

                TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: mockDocumentContainer.Object,
                    sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c ORDER BY c.item"),
                    targetRanges: new List<FeedRangeEpk>() { new FeedRangeEpk(new Range<string>(min: "A", max: "B", isMinInclusive: true, isMaxInclusive: false)) },
                    partitionKey: null,
                    orderByColumns: new List<OrderByColumn>()
                    {
                        new OrderByColumn("item", SortOrder.Ascending)
                    },
                    queryPaginationOptions: new QueryExecutionOptions(pageSizeHint: 10),
                    maxConcurrency: 10,
                    nonStreamingOrderBy: false,
                    continuationToken: CosmosArray.Create(
                        new List<CosmosElement>()
                        {
                            OrderByContinuationToken.ToCosmosElement(orderByContinuationToken)
                        }),
                    containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                    emitRawOrderByPayload: false);
                Assert.IsTrue(monadicCreate.Succeeded);
            }

            // Multiple resume values
            foreach ((string token1, SqlQueryResumeValue resumeValue1) in tokens)
            {
                foreach ((string token2, SqlQueryResumeValue resumeValue2) in tokens)
                {
                    ParallelContinuationToken parallelContinuationToken1 = new ParallelContinuationToken(
                        token: $"[{token1}, {token2}]",
                        range: new Documents.Routing.Range<string>("A", "B", true, false));

                    OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                        parallelContinuationToken1,
                        orderByItems: null,
                        new List<SqlQueryResumeValue>() { resumeValue1, resumeValue2 },
                        rid: "rid",
                        skipCount: 42,
                        filter: null);

                    TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                        documentContainer: mockDocumentContainer.Object,
                        sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c ORDER BY c.item1, c.item2"),
                        targetRanges: new List<FeedRangeEpk>()
                        {
                            new FeedRangeEpk(new Range<string>(min: "A", max: "B", isMinInclusive: true, isMaxInclusive: false)),
                            new FeedRangeEpk(new Range<string>(min: "B", max: "C", isMinInclusive: true, isMaxInclusive: false)),
                        },
                        partitionKey: null,
                        orderByColumns: new List<OrderByColumn>()
                        {
                            new OrderByColumn("item1", SortOrder.Ascending),
                            new OrderByColumn("item2", SortOrder.Ascending)
                        },
                        queryPaginationOptions: new QueryExecutionOptions(pageSizeHint: 10),
                        maxConcurrency: 10,
                        nonStreamingOrderBy: false,
                        continuationToken: CosmosArray.Create(
                            new List<CosmosElement>()
                            {
                                OrderByContinuationToken.ToCosmosElement(orderByContinuationToken)
                            }),
                        containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                        emitRawOrderByPayload: false);
                    Assert.IsTrue(monadicCreate.Succeeded);
                }
            }
        }

        [TestMethod]
        public async Task TestFormattedFiltersForTargetPartitionWithContinuationTokenAsync()
        {
            QueryPage emptyPage = new QueryPage(
                documents: new List<CosmosElement>(),
                requestCharge: 0,
                activityId: string.Empty,
                cosmosQueryExecutionInfo: default,
                distributionPlanSpec: default,
                disallowContinuationTokenMessage: default,
                additionalHeaders: default,
                state: default,
                streaming: default);

            string expectedQuerySpec = "SELECT * FROM c WHERE true ORDER BY c._ts";
            Mock<IDocumentContainer> mockContainer = new Mock<IDocumentContainer>(MockBehavior.Strict);
            mockContainer
                .Setup(
                c => c.MonadicQueryAsync(
                    It.Is<SqlQuerySpec>(sqlQuerySpec => expectedQuerySpec.Equals(sqlQuerySpec.QueryText) && sqlQuerySpec.ResumeFilter.ResumeValues.Count == 1),
                    It.IsAny<FeedRangeState<QueryState>>(),
                    It.IsAny<QueryExecutionOptions>(),
                    NoOpTrace.Singleton,
                    default))
                .ReturnsAsync(TryCatch<QueryPage>.FromResult(emptyPage));

            string continuationToken = @"[{""compositeToken"":{""token"":null,""range"":{""min"":""A"",""max"":""B""}},""orderByItems"":[{""item"":1665482200}],""rid"":""64kUAPYyHHk6XgIAAADACQ=="",""skipCount"":1,""filter"":""( c._ts >= 1665482198 OR IS_STRING(c._ts) OR IS_ARRAY(c._ts) OR IS_OBJECT(c._ts) )""}]";

            IReadOnlyList<FeedRangeEpk> targetRanges = new List<FeedRangeEpk>()
            {
                new FeedRangeEpk(new Range<string>(min: "A", max: "B", isMinInclusive: true, isMaxInclusive: false)),
                new FeedRangeEpk(new Range<string>(min: "B", max: "C", isMinInclusive: true, isMaxInclusive: false))
            };

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c WHERE {documentdb-formattableorderbyquery-filter} ORDER BY c._ts"),
                targetRanges: targetRanges,
                partitionKey: null,
                orderByColumns: new List<OrderByColumn>()
                {
                    new OrderByColumn("c._ts", SortOrder.Ascending)
                },
                queryPaginationOptions: new QueryExecutionOptions(pageSizeHint: 1),
                maxConcurrency: 0,
                nonStreamingOrderBy: false,
                continuationToken: CosmosElement.Parse(continuationToken),
                containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                emitRawOrderByPayload: false);
            Assert.IsTrue(monadicCreate.Succeeded);

            IQueryPipelineStage queryPipelineStage = monadicCreate.Result;
            for (int i = 0; i < targetRanges.Count; ++i)
            {
                Assert.IsTrue(await queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton, cancellationToken: default));
            }

            Assert.IsFalse(await queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton, cancellationToken: default));
        }

        [TestMethod]
        [DataRow(false, DisplayName = "NonStreaming: false")]
        [DataRow(true, DisplayName = "NonStreaming: true")]
        public async Task TestDrainFully_StartFromBeginingAsync_NoDocuments(bool nonStreamingOrderBy)
        {
            int numItems = 0;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: documentContainer,
                sqlQuerySpec: new SqlQuerySpec(@"
                    SELECT c._rid AS _rid, [{""item"": c._ts}] AS orderByItems, c AS payload
                    FROM c
                    WHERE {documentdb-formattableorderbyquery-filter}
                    ORDER BY c._ts"),
                targetRanges: await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default),
                partitionKey: null,
                orderByColumns: new List<OrderByColumn>()
                {
                    new OrderByColumn("c._ts", SortOrder.Ascending)
                },
                queryPaginationOptions: new QueryExecutionOptions(pageSizeHint: 10),
                maxConcurrency: 10,
                nonStreamingOrderBy: nonStreamingOrderBy,
                continuationToken: null,
                containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                emitRawOrderByPayload: false);
            Assert.IsTrue(monadicCreate.Succeeded);
            IQueryPipelineStage queryPipelineStage = monadicCreate.Result;

            List<CosmosElement> documents = new List<CosmosElement>();
            while (await queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton, cancellationToken: default))
            {
                TryCatch<QueryPage> tryGetQueryPage = queryPipelineStage.Current;
                if (tryGetQueryPage.Failed)
                {
                    Assert.Fail(tryGetQueryPage.Exception.ToString());
                }

                QueryPage queryPage = tryGetQueryPage.Result;
                documents.AddRange(queryPage.Documents);

                if (!nonStreamingOrderBy)
                {
                    Assert.AreEqual(42, queryPage.RequestCharge);
                }
            }

            Assert.AreEqual(numItems, documents.Count);
            Assert.IsTrue(documents.OrderBy(document => ((CosmosObject)document)["_ts"]).ToList().SequenceEqual(documents));
        }

        [TestMethod]
        public async Task TestDrain_IncludesResponseHeadersInQueryPage()
        {
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(10);

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: documentContainer,
                sqlQuerySpec: new SqlQuerySpec(@"
                    SELECT c._rid AS _rid, [{""item"": c._ts}] AS orderByItems, c AS payload
                    FROM c
                    WHERE {documentdb-formattableorderbyquery-filter}
                    ORDER BY c._ts"),
                targetRanges: await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default),
                partitionKey: null,
                orderByColumns: new List<OrderByColumn>()
                {
                    new OrderByColumn("c._ts", SortOrder.Ascending)
                },
                queryPaginationOptions: new QueryExecutionOptions(pageSizeHint: 10),
                maxConcurrency: 10,
                nonStreamingOrderBy: false,
                continuationToken: null,
                containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                emitRawOrderByPayload: false);
            Assert.IsTrue(monadicCreate.Succeeded);
            IQueryPipelineStage queryPipelineStage = monadicCreate.Result;

            int countAdditionalHeadersReceived = 0;
            while (await queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton, cancellationToken: default))
            {
                TryCatch<QueryPage> tryGetQueryPage = queryPipelineStage.Current;
                if (tryGetQueryPage.Failed)
                {
                    Assert.Fail(tryGetQueryPage.Exception.ToString());
                }

                QueryPage queryPage = tryGetQueryPage.Result;
                if (queryPage.AdditionalHeaders.Count > 0)
                {
                    ++countAdditionalHeadersReceived;
                }
            }

            int countFeedRanges = (await documentContainer.GetFeedRangesAsync(
                trace: NoOpTrace.Singleton,
                cancellationToken: default))
                .Count;
            Assert.IsTrue(countAdditionalHeadersReceived >= countFeedRanges);
        }

        [TestMethod]
        [DataRow(false, false, false, false, DisplayName = "NonStreaming: false, Use State: false, Allow Splits: false, Allow Merges: false")]
        [DataRow(false, false, false, true, DisplayName = "NonStreaming: false, Use State: false, Allow Splits: false, Allow Merges: true")]
        [DataRow(false, false, true, false, DisplayName = "NonStreaming: false, Use State: false, Allow Splits: true, Allow Merges: false")]
        [DataRow(false, false, true, true, DisplayName = "NonStreaming: false, Use State: false, Allow Splits: true, Allow Merges: true")]
        [DataRow(false, true, false, false, DisplayName = "NonStreaming: false, Use State: true, Allow Splits: false, Allow Merges: false")]
        [DataRow(false, true, false, true, DisplayName = "NonStreaming: false, Use State: true, Allow Splits: false, Allow Merges: true")]
        [DataRow(false, true, true, false, DisplayName = "NonStreaming: false, Use State: true, Allow Splits: true, Allow Merges: false")]
        [DataRow(false, true, true, true, DisplayName = "NonStreaming: false, Use State: true, Allow Splits: true, Allow Merges: true")]
        [DataRow(true, false, false, false, DisplayName = "NonStreaming: true, Use State: false, Allow Splits: false, Allow Merges: false")]
        [DataRow(true, false, false, true, DisplayName = "NonStreaming: true, Use State: false, Allow Splits: false, Allow Merges: true")]
        [DataRow(true, false, true, false, DisplayName = "NonStreaming: true, Use State: false, Allow Splits: true, Allow Merges: false")]
        [DataRow(true, false, true, true, DisplayName = "NonStreaming: true, Use State: false, Allow Splits: true, Allow Merges: true")]
        public async Task TestDrainWithStateSplitsAndMergeAsync(bool nonStreamingOrderBy, bool useState, bool allowSplits, bool allowMerges)
        {
            static async Task<IQueryPipelineStage> CreatePipelineStateAsync(IDocumentContainer documentContainer, CosmosElement continuationToken, bool nonStreamingOrderBy)
            {
                TryCatch<IQueryPipelineStage> monadicQueryPipelineStage = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: documentContainer,
                    sqlQuerySpec: new SqlQuerySpec(@"
                        SELECT c._rid AS _rid, [{""item"": c.pk}] AS orderByItems, c AS payload
                        FROM c
                        WHERE {documentdb-formattableorderbyquery-filter}
                        ORDER BY c.pk"),
                    targetRanges: await documentContainer.GetFeedRangesAsync(
                        trace: NoOpTrace.Singleton,
                        cancellationToken: default),
                    partitionKey: null,
                    orderByColumns: new List<OrderByColumn>()
                    {
                        new OrderByColumn("c.pk", SortOrder.Ascending)
                    },
                    queryPaginationOptions: new QueryExecutionOptions(pageSizeHint: 10),
                    maxConcurrency: 10,
                    nonStreamingOrderBy: nonStreamingOrderBy,
                    continuationToken: continuationToken,
                    containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                    emitRawOrderByPayload: false);
                monadicQueryPipelineStage.ThrowIfFailed();
                IQueryPipelineStage queryPipelineStage = monadicQueryPipelineStage.Result;

                return queryPipelineStage;
            }

            bool verbose = false;
            int numItems = 1000;
            IDocumentContainer inMemoryCollection = await CreateDocumentContainerAsync(numItems);
            IQueryPipelineStage queryPipelineStage = await CreatePipelineStateAsync(inMemoryCollection, continuationToken: null, nonStreamingOrderBy);
            List<CosmosElement> documents = new List<CosmosElement>();
            Random random = new Random();
            while (await queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton, cancellationToken: default))
            {
                TryCatch<QueryPage> tryGetPage = queryPipelineStage.Current;
                tryGetPage.ThrowIfFailed();

                documents.AddRange(tryGetPage.Result.Documents);

                if (useState)
                {
                    QueryPage queryPage;
                    QueryState queryState = null;
                    do
                    {
                        // We need to drain out all the initial empty pages,
                        // since they are non resumable state.
                        Assert.IsTrue(await queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton, cancellationToken: default));
                        TryCatch<QueryPage> tryGetQueryPage = queryPipelineStage.Current;
                        if (tryGetQueryPage.Failed)
                        {
                            Assert.Fail(tryGetQueryPage.Exception.ToString());
                        }

                        queryPage = tryGetQueryPage.Result;
                        documents.AddRange(queryPage.Documents);
                        queryState = queryPage.State;
                    } while ((queryPage.Documents.Count == 0) && (queryState != null));

                    if (queryState == null)
                    {
                        break;
                    }

                    queryPipelineStage = await CreatePipelineStateAsync(inMemoryCollection, queryState.Value, nonStreamingOrderBy);
                }

                if (random.Next() % 2 == 0)
                {
                    if (allowSplits && (random.Next() % 2 == 0))
                    {
                        // Split
                        await inMemoryCollection.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                        List<FeedRangeEpk> ranges = await inMemoryCollection.GetFeedRangesAsync(
                            trace: NoOpTrace.Singleton,
                            cancellationToken: default);
                        FeedRangeInternal randomRangeToSplit = ranges[random.Next(0, ranges.Count)];
                        await inMemoryCollection.SplitAsync(randomRangeToSplit, cancellationToken: default);

                        if (verbose)
                        {
                            System.Diagnostics.Trace.WriteLine($"Split range: {randomRangeToSplit.ToJsonString()}");
                        }
                    }

                    if (allowMerges && (random.Next() % 2 == 0))
                    {
                        // Merge
                        await inMemoryCollection.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                        List<FeedRangeEpk> ranges = await inMemoryCollection.GetFeedRangesAsync(
                            trace: NoOpTrace.Singleton,
                            cancellationToken: default);
                        if (ranges.Count > 1)
                        {
                            ranges = ranges.OrderBy(range => range.Range.Min).ToList();
                            int indexToMerge = random.Next(0, ranges.Count);
                            int adjacentIndex = indexToMerge == (ranges.Count - 1) ? indexToMerge - 1 : indexToMerge + 1;
                            await inMemoryCollection.MergeAsync(ranges[indexToMerge], ranges[adjacentIndex], cancellationToken: default);
                        }

                        if (verbose)
                        {
                            string mergedRanges = string.Join(", ", ranges.Select(range => range.ToJsonString()));
                            System.Diagnostics.Trace.WriteLine($"Merged ranges: {mergedRanges}");
                        }
                    }
                }
            }

            Assert.AreEqual(numItems, documents.Count);
        }

        private static async Task<IDocumentContainer> CreateDocumentContainerAsync(
            int numItems,
            FlakyDocumentContainer.FailureConfigs failureConfigs = null)
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
            {
                Paths = new System.Collections.ObjectModel.Collection<string>()
                {
                    "/pk"
                },
                Kind = PartitionKind.Hash,
                Version = PartitionKeyDefinitionVersion.V2,
            };

            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(partitionKeyDefinition);
            if (failureConfigs != null)
            {
                monadicDocumentContainer = new FlakyDocumentContainer(monadicDocumentContainer, failureConfigs);
            }

            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            for (int i = 0; i < 3; i++)
            {
                await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);
                foreach (FeedRangeInternal range in ranges)
                {
                    await documentContainer.SplitAsync(range, cancellationToken: default);
                }
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
    }
}
