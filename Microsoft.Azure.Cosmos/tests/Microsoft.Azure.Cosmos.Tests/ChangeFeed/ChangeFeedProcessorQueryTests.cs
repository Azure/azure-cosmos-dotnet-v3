//------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ChangeFeedProcessorQueryTests
    {
        [DataTestMethod]
        [DataRow(false, false, false)]
        [DataRow(true, false, false)]
        [DataRow(false, true, false)]
        [DataRow(true, true, false)]
        [DataRow(false, true, true)]
        [DataRow(true, true, true)]
        public async Task EmptyPagesAdvanceReadsWithoutAcknowledgingManualBatches(bool stream, bool manual, bool acknowledge)
        {
            int calls = 0;
            int delivered = 0;
            List<string> checkpoints = new List<string>();
            List<ResponseMessage> responses = new List<ResponseMessage>();
            TestHandler handler = new TestHandler((request, cancellationToken) =>
            {
                int call = calls++;
                Assert.IsTrue(call < 5);
                Assert.AreEqual(call == 0 ? "saved-progress" : "progress-" + (call - 1), request.Headers.IfNoneMatch);
                ResponseMessage response = CreatePage(
                    call == 4 ? HttpStatusCode.TooManyRequests : call == 3 ? HttpStatusCode.NotModified : HttpStatusCode.OK,
                    "progress-" + call);
                if (call == 0)
                {
                    response.Content.Dispose();
                    response.Content = new MemoryStream(Encoding.UTF8.GetBytes("{\"Documents\":[{\"id\":\"match\"}],\"_count\":1}"));
                }

                responses.Add(response);
                return Task.FromResult(response);
            });
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(builder => builder.AddCustomHandlers(handler));
            using ChangeFeedPartitionKeyResultSetIteratorCore iterator = CreateIterator(client, new ChangeFeedQuerySpec("SELECT * FROM c"));
            Mock<PartitionCheckpointer> checkpointer = new Mock<PartitionCheckpointer>();
            checkpointer.Setup(c => c.CheckpointPartitionAsync(It.IsAny<string>()))
                .Callback<string>(continuation => checkpoints.Add(continuation))
                .Returns(Task.CompletedTask);
            async Task OnChangesAsync(int count, Func<Task> checkpoint)
            {
                delivered += count;
                if (acknowledge && count > 0)
                {
                    await checkpoint();
                }
            }

            ChangeFeedObserverFactory factory = stream
                ? new ChangeFeedObserverFactoryCore(async (context, changes, checkpoint, token) =>
                {
                    using StreamReader reader = new StreamReader(changes, leaveOpen: true);
                    await OnChangesAsync(((JArray)JObject.Parse(await reader.ReadToEndAsync())["Documents"]).Count, checkpoint);
                })
                : new ChangeFeedObserverFactoryCore<JObject>(
                    (context, changes, checkpoint, token) => OnChangesAsync(changes.Count, checkpoint),
                    new CosmosSerializerCore(null));
            ChangeFeedObserver observer = new CheckpointerObserverFactory(factory, withManualCheckpointing: manual).CreateObserver();
            FeedProcessorCore processor = new FeedProcessorCore(
                observer, iterator, new ProcessorOptions { LeaseToken = "0", FeedPollDelay = TimeSpan.Zero }, checkpointer.Object);
            try
            {
                CosmosException error = await Assert.ThrowsExceptionAsync<CosmosException>(() => processor.RunAsync(CancellationToken.None));
                Assert.AreEqual(HttpStatusCode.TooManyRequests, error.StatusCode);
                Assert.AreEqual(5, calls);
                Assert.AreEqual(1, delivered);
                CollectionAssert.AreEqual(
                    manual ? acknowledge ? new[] { "progress-0" } : Array.Empty<string>() : new[] { "progress-0", "progress-1", "progress-2" },
                    checkpoints);
            }
            finally
            {
                foreach (ResponseMessage response in responses)
                {
                    response.Dispose();
                }
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task QueryReadDiagnosticsDoNotContainSqlHashOrDocumentBody(bool fullFidelity)
        {
            const string query = "SELECT * FROM c WHERE c.privateValue = \"query-private-value\"";
            ChangeFeedQuerySpec spec = new ChangeFeedQuerySpec(query);
            TestHandler handler = new TestHandler((request, token) =>
            {
                ResponseMessage response = new ResponseMessage(HttpStatusCode.OK)
                {
                    Content = new MemoryStream(Encoding.UTF8.GetBytes("{\"Documents\":[{\"id\":\"document-private-value\"}],\"_count\":1}"))
                };
                response.Headers.ETag = "progress";
                response.Headers.RequestCharge = 2.5;
                response.Headers.ActivityId = "activity";
                return Task.FromResult(response);
            });
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(builder => builder.AddCustomHandlers(handler));
            using ChangeFeedPartitionKeyResultSetIteratorCore iterator = CreateIterator(client, spec, fullFidelity);
            using ITrace trace = Trace.GetRootTrace("Filtered read", TraceComponent.ChangeFeed, TraceLevel.Info);
            using ResponseMessage response = await iterator.ReadNextAsync(trace);
            Assert.AreEqual(2.5, response.Headers.RequestCharge);
            Assert.AreEqual("activity", response.Headers.ActivityId);
            string diagnostics = new CosmosTraceDiagnostics(trace).ToString();
            Assert.IsFalse(diagnostics.Contains("query-private-value", StringComparison.Ordinal));
            Assert.IsFalse(diagnostics.Contains("document-private-value", StringComparison.Ordinal));
            Assert.IsFalse(diagnostics.Contains(ChangeFeedHelper.GetQueryFilterHash(spec), StringComparison.Ordinal));
        }

        [DataTestMethod]
        [DataRow(false, HttpStatusCode.BadRequest)]
        [DataRow(true, HttpStatusCode.BadRequest)]
        [DataRow(false, HttpStatusCode.TooManyRequests)]
        [DataRow(true, HttpStatusCode.TooManyRequests)]
        public async Task FailedReadRetainsContinuation(bool filtered, HttpStatusCode failure)
        {
            int calls = 0;
            TestHandler handler = new TestHandler((request, cancellationToken) =>
            {
                Assert.AreEqual("saved-progress", request.Headers.IfNoneMatch);
                ResponseMessage response = new ResponseMessage(calls++ == 0 ? failure : HttpStatusCode.OK);
                response.Headers.ETag = calls == 1 ? "error-etag" : "next-progress";
                return Task.FromResult(response);
            });
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(builder => builder.AddCustomHandlers(handler));
            using ChangeFeedPartitionKeyResultSetIteratorCore iterator = ChangeFeedPartitionKeyResultSetIteratorCore.Create(
                lease: new DocumentServiceLeaseCore { LeaseToken = "0" },
                mode: ChangeFeedMode.LatestVersion,
                continuationToken: "saved-progress",
                maxItemCount: 7,
                container: (ContainerInternal)client.GetContainer("myDb", "myContainer"),
                startTime: null,
                startFromBeginning: false,
                querySpec: filtered ? new ChangeFeedQuerySpec("SELECT * FROM c") : null);

            using ResponseMessage failed = await iterator.ReadNextAsync(NoOpTrace.Singleton);
            Assert.AreEqual(failure, failed.StatusCode);
            Assert.IsNull(failed.Headers.ContinuationToken);
            Assert.IsFalse(iterator.HasMoreResults);
            using ResponseMessage retried = await iterator.ReadNextAsync(NoOpTrace.Singleton);
            Assert.AreEqual("next-progress", retried.Headers.ContinuationToken);
            Assert.AreEqual(2, calls);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task QueryReadsPreserveProtocolAndOwnEachPayload(bool fullFidelity, bool epk)
        {
            const string query = " SELECT * FROM c WHERE c.name = \"\u00e9\" ";
            List<Stream> payloads = new List<Stream>();
            Mock<CosmosSerializer> customerSerializer = new Mock<CosmosSerializer>(MockBehavior.Strict);
            TestHandler handler = new TestHandler(async (request, cancellationToken) =>
            {
                int call = payloads.Count;
                Assert.AreEqual(HttpMethod.Post, request.Method);
                Assert.AreEqual(Documents.OperationType.ReadFeed, request.OperationType);
                Assert.AreEqual(bool.TrueString, request.Headers[Documents.HttpConstants.HttpHeaders.IsQuery]);
                Assert.AreEqual(Documents.RuntimeConstants.MediaTypes.QueryJson, request.Headers.ContentType);
                Assert.AreEqual("7", request.Headers.PageSize);
                Assert.AreEqual(call == 0 ? "saved-progress" : "progress-" + (call - 1), request.Headers.IfNoneMatch);
                Assert.AreEqual(
                    fullFidelity ? Documents.HttpConstants.A_IMHeaderValues.FullFidelityFeed : Documents.HttpConstants.A_IMHeaderValues.IncrementalFeed,
                    request.Headers[Documents.HttpConstants.HttpHeaders.A_IM]);
                if (fullFidelity)
                {
                    Assert.IsTrue(request.UseGatewayMode);
                    Assert.AreEqual(
                        Documents.Constants.ChangeFeedWireFormatVersions.SeparateMetadataWithCrts,
                        request.Headers[Documents.HttpConstants.HttpHeaders.ChangeFeedWireFormatVersion]);
                }

                if (epk)
                {
                    Assert.AreEqual("AA", request.Headers.StartEpk);
                    Assert.AreEqual("BB", request.Headers.EndEpk);
                }
                else
                {
                    Assert.IsNotNull(request.PartitionKeyRangeId);
                }

                Assert.IsNotNull(request.Content);
                Assert.IsTrue(request.Content.CanRead);
                foreach (Stream previous in payloads)
                {
                    Assert.AreNotSame(previous, request.Content);
                    Assert.IsFalse(previous.CanRead);
                }

                payloads.Add(request.Content);
                using StreamReader reader = new StreamReader(request.Content, leaveOpen: true);
                JObject body = JObject.Parse(await reader.ReadToEndAsync());
                Assert.AreEqual(query, (string)body["query"]);
                Assert.IsFalse((bool)body["enableQueryOnPreviousImage"]);
                Assert.AreEqual(2, body.Count);
                return CreatePage(call == 1 ? HttpStatusCode.NotModified : HttpStatusCode.OK, "progress-" + call);
            });
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(
                builder => builder.AddCustomHandlers(handler).WithCustomSerializer(customerSerializer.Object));
            using ChangeFeedPartitionKeyResultSetIteratorCore iterator = CreateIterator(
                client, new ChangeFeedQuerySpec(query), fullFidelity, epk);

            for (int call = 0; call < 3; call++)
            {
                using ResponseMessage response = await iterator.ReadNextAsync(NoOpTrace.Singleton);
                Assert.AreEqual("progress-" + call, response.Headers.ContinuationToken);
                Assert.AreEqual(call != 1, iterator.HasMoreResults);
                Assert.IsFalse(payloads[call].CanRead);
            }

            Assert.AreEqual(3, payloads.Count);
            customerSerializer.VerifyNoOtherCalls();
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        public async Task MissingOrEmptyInternalQueryRetainsGet(string query)
        {
            int calls = 0;
            TestHandler handler = new TestHandler((request, cancellationToken) =>
            {
                calls++;
                Assert.AreEqual(HttpMethod.Get, request.Method);
                Assert.IsNull(request.Content);
                Assert.IsNull(request.Headers[Documents.HttpConstants.HttpHeaders.IsQuery]);
                return Task.FromResult(CreatePage(HttpStatusCode.OK, "next-progress"));
            });
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(builder => builder.AddCustomHandlers(handler));
            using ChangeFeedPartitionKeyResultSetIteratorCore iterator = CreateIterator(
                client, query == null ? null : new ChangeFeedQuerySpec(query));

            using ResponseMessage response = await iterator.ReadNextAsync(NoOpTrace.Singleton);

            Assert.AreEqual(1, calls);
            Assert.AreEqual("next-progress", response.Headers.ContinuationToken);
        }

        [DataTestMethod]
        [DataRow("now")]
        [DataRow("beginning")]
        [DataRow("time")]
        [DataRow("continuation")]
        public async Task QueryRetainsStartPositionHeaders(string start)
        {
            DateTime time = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            int calls = 0;
            TestHandler handler = new TestHandler((request, cancellationToken) =>
            {
                calls++;
                Assert.AreEqual(start == "now" ? "*" : start == "continuation" ? "saved-progress" : null, request.Headers.IfNoneMatch);
                Assert.AreEqual(
                    start == "time" ? time.ToString("r", CultureInfo.InvariantCulture) : null,
                    request.Headers[Documents.HttpConstants.HttpHeaders.IfModifiedSince]);
                return Task.FromResult(CreatePage(HttpStatusCode.OK, "next-progress"));
            });
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(builder => builder.AddCustomHandlers(handler));
            using ChangeFeedPartitionKeyResultSetIteratorCore iterator = ChangeFeedPartitionKeyResultSetIteratorCore.Create(
                lease: new DocumentServiceLeaseCore { LeaseToken = "0" },
                mode: ChangeFeedMode.LatestVersion,
                continuationToken: start == "continuation" ? "saved-progress" : null,
                maxItemCount: null,
                container: (ContainerInternal)client.GetContainer("myDb", "myContainer"),
                startTime: start == "time" ? time : null,
                startFromBeginning: start == "beginning",
                querySpec: new ChangeFeedQuerySpec("SELECT * FROM c"));

            using ResponseMessage response = await iterator.ReadNextAsync(NoOpTrace.Singleton);

            Assert.AreEqual(1, calls);
        }

        [TestMethod]
        public async Task CancellationDisposesPayloadWithoutAdvancing()
        {
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Stream payload = null;
            int calls = 0;
            TestHandler handler = new TestHandler((request, cancellationToken) =>
            {
                Assert.AreEqual("saved-progress", request.Headers.IfNoneMatch);
                if (calls++ == 0)
                {
                    payload = request.Content;
                    Assert.AreEqual(cancellation.Token, cancellationToken);
                    cancellation.Cancel();
                    throw new OperationCanceledException(cancellationToken);
                }

                Assert.AreNotSame(payload, request.Content);
                return Task.FromResult(CreatePage(HttpStatusCode.OK, "next-progress"));
            });
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(builder => builder.AddCustomHandlers(handler));
            using ChangeFeedPartitionKeyResultSetIteratorCore iterator = CreateIterator(client, new ChangeFeedQuerySpec("SELECT * FROM c"));

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => iterator.ReadNextAsync(NoOpTrace.Singleton, cancellation.Token));

            Assert.IsNotNull(payload);
            Assert.IsFalse(payload.CanRead);
            using ResponseMessage retried = await iterator.ReadNextAsync(NoOpTrace.Singleton);
            Assert.AreEqual("next-progress", retried.Headers.ContinuationToken);
            Assert.AreEqual(2, calls);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task FactoryCapturesQueryAndPropagatesOneReadFailure(bool filtered)
        {
            const string query = "SELECT * FROM c WHERE c.state = \"WA\"";
            int calls = 0;
            TestHandler handler = new TestHandler(async (request, cancellationToken) =>
            {
                calls++;
                Assert.AreEqual(filtered ? HttpMethod.Post : HttpMethod.Get, request.Method);
                if (filtered)
                {
                    using StreamReader reader = new StreamReader(request.Content, leaveOpen: true);
                    Assert.AreEqual(query, (string)JObject.Parse(await reader.ReadToEndAsync())["query"]);
                }
                else
                {
                    Assert.IsNull(request.Content);
                }

                return new ResponseMessage(HttpStatusCode.BadRequest);
            });
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(builder => builder.AddCustomHandlers(handler));
            ChangeFeedProcessorOptions options = new ChangeFeedProcessorOptions
            {
                QuerySpec = filtered ? new ChangeFeedQuerySpec(query) : null,
                FeedPollDelay = TimeSpan.Zero
            };
            FeedProcessorFactoryCore factory = new FeedProcessorFactoryCore(
                (ContainerInternal)client.GetContainer("myDb", "myContainer"), options, Mock.Of<DocumentServiceLeaseCheckpointer>());
            Mock<ChangeFeedObserver> observer = new Mock<ChangeFeedObserver>(MockBehavior.Strict);
            FeedProcessor processor = factory.Create(new DocumentServiceLeaseCore { LeaseToken = "0" }, observer.Object);
            options.QuerySpec = new ChangeFeedQuerySpec("SELECT * FROM c WHERE c.state = \"OR\"");

            CosmosException error = await Assert.ThrowsExceptionAsync<CosmosException>(() => processor.RunAsync(CancellationToken.None));

            Assert.AreEqual(HttpStatusCode.BadRequest, error.StatusCode);
            Assert.AreEqual(1, calls);
            observer.VerifyNoOtherCalls();
        }

        private static ChangeFeedPartitionKeyResultSetIteratorCore CreateIterator(
            CosmosClient client, ChangeFeedQuerySpec query, bool fullFidelity = false, bool epk = false)
        {
            DocumentServiceLease lease = epk
                ? new DocumentServiceLeaseCoreEpk
                {
                    LeaseToken = "AA-BB",
                    FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false))
                }
                : new DocumentServiceLeaseCore { LeaseToken = "0" };
            return ChangeFeedPartitionKeyResultSetIteratorCore.Create(
                lease: lease,
                mode: fullFidelity ? ChangeFeedMode.AllVersionsAndDeletes : ChangeFeedMode.LatestVersion,
                continuationToken: "saved-progress",
                maxItemCount: 7,
                container: (ContainerInternal)client.GetContainer("myDb", "myContainer"),
                startTime: null,
                startFromBeginning: false,
                querySpec: query);
        }

        private static ResponseMessage CreatePage(HttpStatusCode status, string etag)
        {
            ResponseMessage response = new ResponseMessage(status)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes("{\"Documents\":[],\"_count\":0}"))
            };
            response.Headers.ETag = etag;
            return response;
        }
    }
}
