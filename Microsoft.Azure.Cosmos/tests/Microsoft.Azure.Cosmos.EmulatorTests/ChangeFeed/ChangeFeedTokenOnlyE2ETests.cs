//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.EmulatorTests.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
    [TestCategory("ChangeFeed")]
    public class ChangeFeedTokenOnlyE2ETests
    {
        public TestContext TestContext { get; set; }

        [DataTestMethod]
        [DataRow(false, false, DisplayName = "LatestVersion-Direct")]
        [DataRow(false, true, DisplayName = "LatestVersion-Gateway")]
        [DataRow(true, false, DisplayName = "AllVersionsAndDeletes-Direct")]
        [DataRow(true, true, DisplayName = "AllVersionsAndDeletes-Gateway")]
        [Timeout(180000)]
        public async Task TokenOnlyNowWithConcurrentIngestion(bool fullFidelity, bool gateway)
        {
            using CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            TaskCompletionSource<bool> captureStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> firstBatchWritten = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> captureFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            CaptureRequestHandler handler = new CaptureRequestHandler(captureStarted, firstBatchWritten);
            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                ConnectionMode = gateway ? ConnectionMode.Gateway : ConnectionMode.Direct,
            };
            clientOptions.CustomHandlers.Add(handler);
            string connectionString = Environment.GetEnvironmentVariable("COSMOSDB_CHANGE_FEED_E2E");
            bool useLiveAccount = !string.IsNullOrEmpty(connectionString);
            using CosmosClient client = useLiveAccount
                ? new CosmosClient(connectionString, clientOptions)
                : TestCommon.CreateCosmosClient(clientOptions);
            Database database = null;
            Task ingestion = null;
            int ingested = 0;
            try
            {
                database = await client.CreateDatabaseAsync(
                    $"cf-token-only-e2e-{Guid.NewGuid():N}", cancellationToken: cancellation.Token);
                ContainerProperties properties = new ContainerProperties("items", "/pk");
                if (fullFidelity && !useLiveAccount)
                {
                    properties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);
                }

                Container container = await database.CreateContainerAsync(
                    properties, throughput: 20000, cancellationToken: cancellation.Token);
                IReadOnlyList<FeedRange> ranges = await container.GetFeedRangesAsync(cancellation.Token);
                Assert.IsTrue(ranges.Count > 1, "This test must exercise a cross-partition cycle.");
                List<string> partitionKeys = CreatePartitionKeys(ranges, properties);
                for (int i = 0; i < partitionKeys.Count; i++)
                {
                    await container.CreateItemAsync(
                        new { id = $"seed-{i}", pk = partitionKeys[i] },
                        new PartitionKey(partitionKeys[i]), cancellationToken: cancellation.Token);
                }

                ingestion = IngestAsync();
                ChangeFeedMode mode = fullFidelity ? ChangeFeedMode.AllVersionsAndDeletes : ChangeFeedMode.LatestVersion;
                using FeedIterator capture = container.GetChangeFeedStreamIterator(
                    ChangeFeedStartFrom.Now(), mode,
                    new ChangeFeedRequestOptions { EnableContinuationTokenOnly = true, PageSizeHint = 0 });
                Assert.IsTrue(capture.HasMoreResults);
                Stopwatch elapsed = Stopwatch.StartNew();
                using ResponseMessage response = await capture.ReadNextAsync(cancellation.Token);
                elapsed.Stop();
                int concurrentWrites = Volatile.Read(ref ingested);
                AssertEmptyCapture(response);
                Assert.IsFalse(capture.HasMoreResults, "One successful read must complete the cycle despite concurrent ingestion.");
                Assert.IsTrue(concurrentWrites >= partitionKeys.Count, "Ingestion must commit writes while the capture call is in progress.");
                for (int i = 0; i < partitionKeys.Count; i++)
                {
                    await container.CreateItemAsync(
                        new { id = $"between-cycles-{i}", pk = partitionKeys[i] },
                        new PartitionKey(partitionKeys[i]), cancellationToken: cancellation.Token);
                }

                int requestsBeforeRepeatedRead = handler.CaptureRequests;
                using ResponseMessage repeated = await capture.ReadNextAsync(cancellation.Token);
                AssertEmptyCapture(repeated);
                Assert.AreEqual(ranges.Count, handler.CaptureRequests - requestsBeforeRepeatedRead,
                    "A 200 response must not stop the token-only cycle before every range is visited.");
                if (useLiveAccount)
                {
                    Assert.AreEqual(HttpStatusCode.OK, repeated.StatusCode, "The live workload must exercise a 200 cycle.");
                    Assert.IsTrue(handler.BackendDocumentCount > 0, "The live workload must exercise suppression of actual service documents.");
                }

                Assert.IsFalse(capture.HasMoreResults);
                captureFinished.TrySetResult(true);
                await ingestion;
                Assert.IsTrue(handler.CaptureRequests >= 2 * ranges.Count);

                string continuation = repeated.Headers.ContinuationToken;
                Assert.IsFalse(string.IsNullOrEmpty(continuation));
                VersionedAndRidCheckedCompositeToken versioned = VersionedAndRidCheckedCompositeToken
                    .MonadicCreateFromCosmosElement(CosmosElement.Parse(continuation)).Result;
                ChangeFeedCrossFeedRangeState state = ChangeFeedCrossFeedRangeState.Monadic
                    .CreateFromCosmosElement(versioned.ContinuationToken).Result;
                foreach (Cosmos.Pagination.FeedRangeState<ChangeFeedState> range in state.FeedRangeStates.ToArray())
                {
                    Assert.IsInstanceOfType<ChangeFeedStateContinuation>(range.State);
                }

                CollectionAssert.AreEquivalent(
                    ranges.Select(range => range.ToString()).ToArray(),
                    state.FeedRangeStates.ToArray().Select(range => range.FeedRange.ToString()).ToArray());

                HashSet<string> pending = new HashSet<string>();
                for (int i = 0; i < partitionKeys.Count; i++)
                {
                    string id = $"after-capture-{i}";
                    pending.Add(id);
                    await container.CreateItemAsync(
                        new { id, pk = partitionKeys[i] },
                        new PartitionKey(partitionKeys[i]), cancellationToken: cancellation.Token);
                }

                using FeedIterator<JObject> latestReader = container.GetChangeFeedIterator<JObject>(
                    ChangeFeedStartFrom.ContinuationToken(continuation), mode);
                using FeedIterator<ChangeFeedItem<JObject>> fullReader = container.GetChangeFeedIterator<ChangeFeedItem<JObject>>(
                    ChangeFeedStartFrom.ContinuationToken(continuation), mode);
                Stopwatch resumeElapsed = Stopwatch.StartNew();
                while (pending.Count > 0 && resumeElapsed.Elapsed < TimeSpan.FromSeconds(30))
                {
                    HttpStatusCode status;
                    if (fullFidelity)
                    {
                        FeedResponse<ChangeFeedItem<JObject>> page = await fullReader.ReadNextAsync(cancellation.Token);
                        status = page.StatusCode;
                        foreach (ChangeFeedItem<JObject> change in page)
                        {
                            Observe(change.Current["id"].Value<string>());
                        }
                    }
                    else
                    {
                        FeedResponse<JObject> page = await latestReader.ReadNextAsync(cancellation.Token);
                        status = page.StatusCode;
                        foreach (JObject item in page)
                        {
                            Observe(item["id"].Value<string>());
                        }
                    }

                    if (status == HttpStatusCode.NotModified && pending.Count > 0)
                    {
                        await Task.Delay(100, cancellation.Token);
                    }
                }

                Assert.AreEqual(0, pending.Count, $"Normal resume missed post-capture writes: {string.Join(", ", pending)}");
                this.TestContext.WriteLine(
                    $"Mode={mode}; Gateway={gateway}; Ranges={ranges.Count}; ConcurrentWrites={concurrentWrites}; " +
                    $"CaptureRequests={handler.CaptureRequests}; Status={response.StatusCode}; RepeatedStatus={repeated.StatusCode}; " +
                    $"Backend200Responses={handler.BackendSuccessResponses}; BackendDocuments={handler.BackendDocumentCount}; " +
                    $"RU={response.Headers.RequestCharge}; CaptureMilliseconds={elapsed.ElapsedMilliseconds}; " +
                    $"PostCaptureWritesObserved={partitionKeys.Count}");

                async Task IngestAsync()
                {
                    await captureStarted.Task.WaitAsync(cancellation.Token);
                    do
                    {
                        int index = Volatile.Read(ref ingested);
                        string pk = partitionKeys[index % partitionKeys.Count];
                        await container.CreateItemAsync(
                            new { id = $"during-capture-{index}", pk },
                            new PartitionKey(pk), cancellationToken: cancellation.Token);
                        if (Interlocked.Increment(ref ingested) >= partitionKeys.Count)
                        {
                            firstBatchWritten.TrySetResult(true);
                        }
                    }
                    while (!captureFinished.Task.IsCompleted);
                }

                void Observe(string id)
                {
                    Assert.IsFalse(id.StartsWith("seed-", StringComparison.Ordinal), "A Now continuation must not replay seeded documents.");
                    Assert.IsFalse(id.StartsWith("between-cycles-", StringComparison.Ordinal), "Resuming must advance past suppressed changes.");
                    pending.Remove(id);
                }
            }
            finally
            {
                try
                {
                    cancellation.Cancel();
                    if (ingestion != null)
                    {
                        try
                        {
                            await ingestion;
                        }
                        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                        {
                        }
                    }
                }
                finally
                {
                    if (database != null)
                    {
                        using CancellationTokenSource cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        using ResponseMessage deleted = await database.DeleteStreamAsync(cancellationToken: cleanup.Token);
                        deleted.EnsureSuccessStatusCode();
                    }
                }
            }
        }

        private static void AssertEmptyCapture(ResponseMessage response)
        {
            Assert.IsTrue(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotModified,
                $"Capture failed: {response.StatusCode}; {response.ErrorMessage}; {response.Diagnostics}");
            Assert.IsTrue(response.Headers.RequestCharge > 0);
            Assert.AreEqual("0", response.Headers[Documents.HttpConstants.HttpHeaders.ItemCount]);
            Assert.IsFalse(string.IsNullOrEmpty(response.Headers.ContinuationToken));
            if (response.Content != null)
            {
                JObject body = TestCommon.SerializerCore.FromStream<JObject>(response.Content);
                Assert.AreEqual(0, ((JArray)body["Documents"]).Count);
            }
        }

        private static List<string> CreatePartitionKeys(IReadOnlyList<FeedRange> ranges, ContainerProperties properties)
        {
            List<string> keys = new List<string>();
            foreach (FeedRangeEpk range in ranges)
            {
                string key = Enumerable.Range(0, 10000)
                    .Select(index => $"pk-{index}")
                    .FirstOrDefault(candidate => range.Range.Contains(
                        new PartitionKey(candidate).InternalKey.GetEffectivePartitionKeyString(properties.PartitionKey)));
                Assert.IsNotNull(key, "Could not select a partition key in every physical range.");
                keys.Add(key);
            }

            return keys;
        }

        private sealed class CaptureRequestHandler : RequestHandler
        {
            private readonly TaskCompletionSource<bool> captureStarted;
            private readonly TaskCompletionSource<bool> firstBatchWritten;
            private int captureRequests;
            private int backendSuccessResponses;
            private int backendDocumentCount;

            public CaptureRequestHandler(TaskCompletionSource<bool> captureStarted, TaskCompletionSource<bool> firstBatchWritten)
            {
                this.captureStarted = captureStarted;
                this.firstBatchWritten = firstBatchWritten;
            }

            public int CaptureRequests => Volatile.Read(ref this.captureRequests);

            public int BackendSuccessResponses => Volatile.Read(ref this.backendSuccessResponses);

            public int BackendDocumentCount => Volatile.Read(ref this.backendDocumentCount);

            public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
            {
                if (request.Headers.PageSize == "0" && Interlocked.Increment(ref this.captureRequests) == 1)
                {
                    this.captureStarted.TrySetResult(true);
                    await this.firstBatchWritten.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                }

                ResponseMessage response = await base.SendAsync(request, cancellationToken);
                if (request.Headers.PageSize == "0" && response.StatusCode == HttpStatusCode.OK && response.Content != null)
                {
                    using (Stream original = response.Content)
                    using (MemoryStream buffered = new MemoryStream())
                    {
                        await original.CopyToAsync(buffered, cancellationToken);
                        byte[] bytes = buffered.ToArray();
                        JObject body = JObject.Parse(Encoding.UTF8.GetString(bytes));
                        Interlocked.Increment(ref this.backendSuccessResponses);
                        Interlocked.Add(ref this.backendDocumentCount, ((JArray)body["Documents"]).Count);
                        response.Content = new MemoryStream(bytes, writable: false);
                    }
                }

                return response;
            }
        }
    }
}
