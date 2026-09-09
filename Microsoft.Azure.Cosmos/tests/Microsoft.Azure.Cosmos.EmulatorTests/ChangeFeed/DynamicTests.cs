//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DynamicTests : BaseChangeFeedClientHelper
    {
        private ContainerInternal Container;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.ChangeFeedTestInit();

            string PartitionKey = "/pk";
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                throughput: 10000,
                cancellationToken: this.cancellationToken);
            this.Container = (ContainerInternal)response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task TestWithRunningProcessor()
        {
            int partitionKey = 0;
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            int processedDocCount = 0;
            string accumulator = string.Empty;
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", async (ChangeFeedProcessorContext context, IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    await this.ValidateContextAsync(context);
                    processedDocCount += docs.Count();
                    foreach (dynamic doc in docs)
                    {
                        accumulator += doc.id.ToString() + ".";
                    }

                    if (processedDocCount == 10)
                    {
                        allDocsProcessed.Set();
                    }
                })
                .WithInstanceName("random")
                .WithLeaseContainer(this.LeaseContainer).Build();

            // Start the processor, insert 1 document to generate a checkpoint
            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            foreach (int id in Enumerable.Range(0, 10))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString(), pk = partitionKey });
            }

            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            await processor.StopAsync();
            Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");
            Assert.AreEqual("0.1.2.3.4.5.6.7.8.9.", accumulator);
        }

        [TestMethod]
        public async Task TestWithRunningProcessor_ImmediateWriteAfterStart()
        {
            int partitionKey = 0;
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            int processedDocCount = 0;
            string accumulator = string.Empty;
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", async (ChangeFeedProcessorContext context, IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    await this.ValidateContextAsync(context);
                    processedDocCount += docs.Count();
                    foreach (dynamic doc in docs)
                    {
                        accumulator += doc.id.ToString() + ".";
                    }

                    if (processedDocCount >= 10)
                    {
                        allDocsProcessed.Set();
                    }
                })
                .WithInstanceName("random")
                .WithLeaseContainer(this.LeaseContainer).Build();

            await processor.StartAsync();
            foreach (int id in Enumerable.Range(0, 10))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString(), pk = partitionKey });
            }

            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            await processor.StopAsync();
            Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");
            Assert.AreEqual("0.1.2.3.4.5.6.7.8.9.", accumulator);
        }

        [TestMethod]
        public async Task TestWithRunningProcessor_WithManualCheckpoint()
        {
            int leaseAcquireCount = 0;
            int leaseReleaseCount = 0;
            int errorCount = 0;
            Exception exceptionToPropagate = new Exception("Stop here");
            int partitionKey = 0;
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            int processedDocCount = 0;
            string accumulator = string.Empty;
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilderWithManualCheckpoint("test", async (ChangeFeedProcessorContext context, IReadOnlyCollection<dynamic> docs, Func<Task> checkpointAsync, CancellationToken token) =>
                {
                    await this.ValidateContextAsync(context);
                    processedDocCount += docs.Count();
                    foreach (dynamic doc in docs)
                    {
                        accumulator += doc.id.ToString() + ".";
                    }

                    if (processedDocCount == 3)
                    {
                        // Throwing on the 3rd document, since we checkpointed only on the 1st, we would repeat 2nd and 3rd
                        throw exceptionToPropagate;
                    }

                    if (processedDocCount == 1) {
                        // Checkpointing on the first document to be able to have a point to rollback to
                        await checkpointAsync();
                    }

                    if (processedDocCount == 12)
                    {
                        allDocsProcessed.Set();
                    }

                })
                .WithInstanceName("random")
                .WithMaxItems(1)
                .WithLeaseAcquireNotification((string leaseToken) =>
                {
                    leaseAcquireCount++;
                    return Task.CompletedTask;
                })
                .WithLeaseReleaseNotification((string leaseToken) =>
                {
                    leaseReleaseCount++;
                    return Task.CompletedTask;
                })
                .WithErrorNotification(async (string leaseToken, Exception exception) =>
                {
                    errorCount++;
                    ChangeFeedProcessorUserException cfpException = exception as ChangeFeedProcessorUserException;
                    Assert.IsNotNull(cfpException);
                    Assert.ReferenceEquals(exceptionToPropagate, exception.InnerException);
                    await this.ValidateContextAsync(cfpException.ChangeFeedProcessorContext);
                })
                .WithLeaseContainer(this.LeaseContainer).Build();

            // Start the processor, insert 1 document to generate a checkpoint
            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            foreach (int id in Enumerable.Range(0, 10))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString(), pk = partitionKey });
            }

            bool isStartOk = allDocsProcessed.WaitOne(30 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            await processor.StopAsync();
            Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");
            Assert.AreEqual("0.1.2.1.2.3.4.5.6.7.8.9.", accumulator);

            // Make sure the notification APIs got all the events
            Assert.IsTrue(leaseAcquireCount > 0);
            Assert.IsTrue(leaseReleaseCount > 0);
            Assert.AreEqual(1, errorCount);
        }

        [TestMethod]
        public async Task TestWithFixedLeaseContainer()
        {
            await NonPartitionedContainerHelper.CreateNonPartitionedContainer(
                    this.database,
                    "fixedLeases");

            Container fixedLeasesContainer = this.GetClient().GetContainer(this.database.Id, "fixedLeases");

            try
            {

                int partitionKey = 0;
                ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

                int processedDocCount = 0;
                string accumulator = string.Empty;
                ChangeFeedProcessor processor = this.Container
                    .GetChangeFeedProcessorBuilder("test", async (ChangeFeedProcessorContext context, IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                    {
                        await this.ValidateContextAsync(context);
                        processedDocCount += docs.Count();
                        foreach (dynamic doc in docs)
                        {
                            accumulator += doc.id.ToString() + ".";
                        }

                        if (processedDocCount == 10)
                        {
                            allDocsProcessed.Set();
                        }
                    })
                    .WithInstanceName("random")
                    .WithLeaseContainer(fixedLeasesContainer).Build();

                // Start the processor, insert 1 document to generate a checkpoint
                await processor.StartAsync();
                await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
                foreach (int id in Enumerable.Range(0, 10))
                {
                    await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString(), pk = partitionKey });
                }

                bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);
                await processor.StopAsync();
                Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");
                Assert.AreEqual("0.1.2.3.4.5.6.7.8.9.", accumulator);
            }
            finally
            {
                await fixedLeasesContainer.DeleteContainerAsync();
            }
        }

        [TestMethod]
        public async Task TestReducePageSizeScenario()
        {
            int partitionKey = 0;
            // Create some docs to make sure that one separate response is returned for 1st execute of query before retries.
            // These are to make sure continuation token is passed along during retries.
            string sprocId = "createTwoDocs";
            string sprocBody = @"function(startIndex) { for (var i = 0; i < 2; ++i) __.createDocument(
                            __.getSelfLink(),
                            { id: 'doc' + (i + startIndex).toString(), value: 'y'.repeat(1500000), pk:0 },
                            err => { if (err) throw err;}
                        );}";

            Scripts scripts = this.Container.Scripts;

            StoredProcedureResponse storedProcedureResponse =
                await scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));

            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            int processedDocCount = 0;
            string accumulator = string.Empty;
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", async (ChangeFeedProcessorContext context, IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    await this.ValidateContextAsync(context);
                    processedDocCount += docs.Count();
                    foreach (dynamic doc in docs)
                    {
                        accumulator += doc.id.ToString() + ".";
                    }

                    if (processedDocCount == 5)
                    {
                        allDocsProcessed.Set();
                    }
                })
                .WithStartFromBeginning()
                .WithInstanceName("random")
                .WithMaxItems(6)
                .WithLeaseContainer(this.LeaseContainer).Build();

            // Generate the payload
            await scripts.ExecuteStoredProcedureAsync<object>(
                sprocId, 
                new PartitionKey(partitionKey),
                new dynamic[] { 0 });

            // Create 3 docs each 1.5MB. All 3 do not fit into MAX_RESPONSE_SIZE (4 MB). 2nd and 3rd are in same transaction.
            string content = string.Format("{{\"id\": \"doc2\", \"value\": \"{0}\", \"pk\": 0}}", new string('x', 1500000));
            await this.Container.CreateItemAsync(JsonConvert.DeserializeObject<dynamic>(content), new PartitionKey(partitionKey));

            await scripts.ExecuteStoredProcedureAsync<object>(sprocId, new PartitionKey(partitionKey), new dynamic[] { 3 });

            await processor.StartAsync();
            // Letting processor initialize and pickup changes
            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            await processor.StopAsync();
            Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");
            Assert.AreEqual("doc0.doc1.doc2.doc3.doc4.", accumulator);
        }

        [TestMethod]
        public async Task TestWithStartTime_Beginning()
        {
            int partitionKey = 0;

            foreach (int id in Enumerable.Range(0, 5))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = $"doc{id}", pk = partitionKey });
            }

            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            int processedDocCount = 0;
            string accumulator = string.Empty;
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", async (ChangeFeedProcessorContext context, IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    await this.ValidateContextAsync(context);
                    Assert.IsTrue(docs.Count > 0);
                    processedDocCount += docs.Count;
                    foreach (dynamic doc in docs)
                    {
                        accumulator += doc.id.ToString() + ".";
                    }

                    if (processedDocCount == 5)
                    {
                        allDocsProcessed.Set();
                    }
                })
                .WithStartTime(DateTime.MinValue.ToUniversalTime())
                .WithInstanceName("random")
                .WithLeaseContainer(this.LeaseContainer).Build();

            await processor.StartAsync();
            // Letting processor initialize and pickup changes
            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            await processor.StopAsync();
            Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");
            Assert.AreEqual("doc0.doc1.doc2.doc3.doc4.", accumulator);
        }

        [TestMethod]
        public async Task TestWithStartTime_CustomTime()
        {
            int partitionKey = 0;

            foreach (int id in Enumerable.Range(0, 5))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = $"doc{id}", pk = partitionKey });
            }

            await Task.Delay(1000);

            DateTime now = DateTime.UtcNow;

            await Task.Delay(1000);

            foreach (int id in Enumerable.Range(5, 5))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = $"doc{id}", pk = partitionKey });
            }

            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            int processedDocCount = 0;
            string accumulator = string.Empty;
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", async (ChangeFeedProcessorContext context, IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    await this.ValidateContextAsync(context);
                    Assert.IsTrue(docs.Count > 0);
                    processedDocCount += docs.Count;
                    foreach (dynamic doc in docs)
                    {
                        accumulator += doc.id.ToString() + ".";
                    }

                    if (processedDocCount == 5)
                    {
                        allDocsProcessed.Set();
                    }
                })
                .WithStartTime(now)
                .WithInstanceName("random")
                .WithLeaseContainer(this.LeaseContainer).Build();

            await processor.StartAsync();
            // Letting processor initialize and pickup changes
            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            await processor.StopAsync();
            Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");
            Assert.AreEqual("doc5.doc6.doc7.doc8.doc9.", accumulator);
        }

        [TestMethod]
        public async Task TestWithStartTime_ValidatesEtagInHeaders()
        {
            ChangeFeedHeaderValidationHandler headerHandler = new ChangeFeedHeaderValidationHandler();

            foreach (int id in Enumerable.Range(0, 100))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = $"doc{id}", pk = Guid.NewGuid().ToString() });
            }

            // Inject validating handler
            CosmosClient client = this.GetClient();
            RequestHandler currentInnerHandler = client.RequestHandler.InnerHandler;
            client.RequestHandler.InnerHandler = headerHandler;
            headerHandler.InnerHandler = currentInnerHandler;

            try
            {
                DateTime startTime = DateTime.UtcNow.AddMinutes(-5);
                string expectedIfModifiedSince = startTime.ToString("r", System.Globalization.CultureInfo.InvariantCulture);

                ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

                int processedDocCount = 0;
                Container.ChangeFeedHandlerWithManualCheckpoint<dynamic> onChangesDelegate = async (
                    ChangeFeedProcessorContext context,
                    IReadOnlyCollection<dynamic> docs,
                    Func<Task> checkpointAsync,
                    CancellationToken token) =>
                    {
                        processedDocCount += docs.Count;

                        // Persist the continuation token to the lease before signaling the test,
                        // so the checkpoint is guaranteed to complete before the processor is
                        // stopped and a new one is constructed against the same lease.
                        await checkpointAsync();

                        allDocsProcessed.Set();
                    };
                ChangeFeedProcessor processor = this.Container
                    .GetChangeFeedProcessorBuilderWithManualCheckpoint("test", onChangesDelegate)
                    .WithStartTime(startTime)
                    .WithInstanceName("random")
                    .WithLeaseContainer(this.LeaseContainer).Build();
               
                await StartProcessorAndValidateHeadersAsync(processor, allDocsProcessed, headerHandler, expectedIfModifiedSince);
                Dictionary<string, string> continuationTokens = await this.ValidateLeaseDocumentsAsync(startTime);

                allDocsProcessed.Reset();

                processor = this.Container
                    .GetChangeFeedProcessorBuilderWithManualCheckpoint("test", onChangesDelegate)
                    .WithInstanceName("random")
                    .WithLeaseContainer(this.LeaseContainer).Build();

                await StartProcessorAndValidateHeadersAsync(processor, allDocsProcessed, headerHandler, expectedIfModifiedSince, processDocuments: false);
                continuationTokens = await this.ValidateLeaseDocumentsAsync(startTime, continuationTokens);

                allDocsProcessed.Reset();

                DateTime startTime1 = startTime + TimeSpan.FromMinutes(1);
                processor = this.Container
                    .GetChangeFeedProcessorBuilderWithManualCheckpoint("test", onChangesDelegate)
                    .WithInstanceName("random")
                    .WithStartTime(startTime1)
                    .WithLeaseContainer(this.LeaseContainer).Build();

                expectedIfModifiedSince = startTime1.ToString("r", System.Globalization.CultureInfo.InvariantCulture);

                await StartProcessorAndValidateHeadersAsync(processor, allDocsProcessed, headerHandler, expectedIfModifiedSince, processDocuments: false);
                continuationTokens = await this.ValidateLeaseDocumentsAsync(startTime1, continuationTokens);

                allDocsProcessed.Reset();
                processor = this.Container
                    .GetChangeFeedProcessorBuilderWithManualCheckpoint("test", onChangesDelegate)
                    .WithInstanceName("random")
                    .WithLeaseContainer(this.LeaseContainer).Build();

                await StartProcessorAndValidateHeadersAsync(processor, allDocsProcessed, headerHandler, expectedIfModifiedSince, processDocuments: false);
                continuationTokens = await this.ValidateLeaseDocumentsAsync(startTime1, continuationTokens);

                allDocsProcessed.Reset();
                startTime1 = DateTime.MinValue;
                processor = this.Container
                    .GetChangeFeedProcessorBuilderWithManualCheckpoint("test", onChangesDelegate)
                    .WithInstanceName("random")
                    .WithStartTime(startTime1)
                    .WithLeaseContainer(this.LeaseContainer).Build();

                await StartProcessorAndValidateHeadersAsync(processor, allDocsProcessed, headerHandler, expectedIfModifiedSince: null, processDocuments: false);
                await this.ValidateLeaseDocumentsAsync(expectedStartTime: null, continuationTokens);
            }
            finally
            {
                // Restore original handler chain
                client.RequestHandler.InnerHandler = currentInnerHandler;
            }
        }

        private static async Task StartProcessorAndValidateHeadersAsync(
            ChangeFeedProcessor processor,
            ManualResetEvent allDocsProcessed,
            ChangeFeedHeaderValidationHandler headerHandler,
            string expectedIfModifiedSince,
            bool processDocuments = true)
        {
            headerHandler.CapturedRequests.Clear();
            await processor.StartAsync();
            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            await processor.StopAsync();
            if (processDocuments)
            {
                Assert.IsTrue(isStartOk, "Timed out waiting for docs to process");
            }

            bool foundSubsequentWithEtag = false;
            ulong changeFeedWithStartTimePostMergeFlag = (ulong)Microsoft.Azure.Documents.SDKSupportedCapabilities.ChangeFeedWithStartTimePostMerge;
            for (int i = 0; i < headerHandler.CapturedRequests.Count; i++)
            {
                RequestMessage subsequentRequest = headerHandler.CapturedRequests[i];
                string subsequentIfNoneMatch = subsequentRequest.Headers.IfNoneMatch;
                if (subsequentIfNoneMatch != null)
                {
                    foundSubsequentWithEtag = true;
                    Assert.AreNotEqual("*", subsequentIfNoneMatch, "If-None-Match should be a specific etag, not '*'.");
                }

                string ifModifiedSince = subsequentRequest.Headers[Microsoft.Azure.Documents.HttpConstants.HttpHeaders.IfModifiedSince];

                if (expectedIfModifiedSince != null)
                {
                    Assert.IsNotNull(ifModifiedSince, "If-Modified-Since header should be set on the first change feed request with start time.");
                    Assert.AreEqual(expectedIfModifiedSince, ifModifiedSince, "If-Modified-Since header value should match the start time.");
                }
                else
                {
                    Assert.IsNull(ifModifiedSince, "If-Modified-Since header should not be set when start time is cleared.");
                }

                string sdkCapabilities = subsequentRequest.Headers[Microsoft.Azure.Documents.HttpConstants.HttpHeaders.SDKSupportedCapabilities];
                Assert.IsNotNull(sdkCapabilities, "SDKSupportedCapabilities header should be present on change feed requests.");
                ulong capabilitiesValue = ulong.Parse(sdkCapabilities);
                Assert.IsTrue(
                    (capabilitiesValue & changeFeedWithStartTimePostMergeFlag) == changeFeedWithStartTimePostMergeFlag,
                    $"SDKSupportedCapabilities header should include ChangeFeedWithStartTimePostMerge flag. Actual value: {capabilitiesValue}");
            }

            Assert.IsTrue(foundSubsequentWithEtag, "Expected at least one subsequent request with If-None-Match (etag) header.");
        }

        private async Task ValidateContextAsync(ChangeFeedProcessorContext changeFeedProcessorContext)
        {
            Assert.IsNotNull(changeFeedProcessorContext.LeaseToken);
            Assert.IsNotNull(changeFeedProcessorContext.Diagnostics);
            Assert.IsNotNull(changeFeedProcessorContext.Headers);
            Assert.IsNotNull(changeFeedProcessorContext.Headers.Session);
            Assert.IsTrue(changeFeedProcessorContext.Headers.RequestCharge > 0);
            string diagnosticsAsString = changeFeedProcessorContext.Diagnostics.ToString();
            Assert.IsTrue(diagnosticsAsString.Contains("Change Feed Processor Read Next Async"));

            await this.ValidateFeedRangeAsync(changeFeedProcessorContext.FeedRange);
        }

        private async Task ValidateFeedRangeAsync(FeedRange feedRange)
        {
            Assert.IsNotNull(feedRange);
            
            IEnumerable<string> partitionKeyRanges = await this.Container.GetPartitionKeyRangesAsync(feedRange);

            Assert.IsNotNull(partitionKeyRanges);
        }

        /// <summary>
        /// <summary>
        /// Validates that all lease documents in the lease container have the expected StartTime
        /// and verifies ContinuationToken state. Returns the per-lease continuation tokens observed
        /// so callers can assert they are preserved / advance across processor restarts.
        /// </summary>
        /// <param name="expectedStartTime">The StartTime expected on every lease, or null if it should be cleared.</param>
        /// <param name="previousContinuationTokens">
        /// Continuation tokens captured from a prior validation. When provided, every lease that had a
        /// token before must still have a non-empty token whose LSN does not regress across the restart.
        /// </param>
        private async Task<Dictionary<string, string>> ValidateLeaseDocumentsAsync(
            DateTime? expectedStartTime,
            IReadOnlyDictionary<string, string> previousContinuationTokens = null)
        {
            using FeedIterator<JObject> iterator = this.LeaseContainer.GetItemQueryIterator<JObject>();
            int leaseCount = 0;
            bool foundLeaseWithContinuation = false;
            Dictionary<string, string> continuationTokens = new Dictionary<string, string>();
            while (iterator.HasMoreResults)
            {
                FeedResponse<JObject> page = await iterator.ReadNextAsync();
                foreach (JObject lease in page)
                {
                    string leaseId = lease.Value<string>("id");
                    if (leaseId.Contains(".info") || leaseId.Contains(".lock"))
                    {
                        // Skip store initialization markers
                        continue;
                    }

                    leaseCount++;

                    string continuationToken = lease.Value<string>("ContinuationToken");
                    if (!string.IsNullOrEmpty(continuationToken))
                    {
                        foundLeaseWithContinuation = true;

                        // The change feed continuation token is the response ETag, i.e. a (quoted) numeric LSN.
                        Assert.IsTrue(
                            TryParseLsn(continuationToken, out long _),
                            $"Lease '{leaseId}' ContinuationToken '{continuationToken}' should be a numeric LSN etag.");

                        continuationTokens[leaseId] = continuationToken;
                    }

                    // Once a lease has checkpointed, restarting the processor must never wipe or roll back
                    // its progress (that would indicate data loss / an unwanted re-anchor).
                    if (previousContinuationTokens != null
                        && previousContinuationTokens.TryGetValue(leaseId, out string previousToken))
                    {
                        Assert.IsFalse(
                            string.IsNullOrEmpty(continuationToken),
                            $"Lease '{leaseId}' lost its ContinuationToken across restart (progress wiped).");

                        if (TryParseLsn(previousToken, out long previousLsn)
                            && TryParseLsn(continuationToken, out long currentLsn))
                        {
                            Assert.IsTrue(
                                currentLsn >= previousLsn,
                                $"Lease '{leaseId}' ContinuationToken regressed across restart: {previousToken} -> {continuationToken}.");
                        }
                    }

                    JToken startTimeToken = lease["StartTime"];
                    if (expectedStartTime.HasValue)
                    {
                        Assert.IsNotNull(startTimeToken, $"Lease '{leaseId}' should have StartTime persisted.");
                        DateTime actualStartTime = startTimeToken.Value<DateTime>();
                        Assert.AreEqual(expectedStartTime.Value, actualStartTime, $"Lease '{leaseId}' StartTime mismatch.");
                    }
                    else
                    {
                        Assert.IsNull(startTimeToken, $"Lease '{leaseId}' should not have StartTime when cleared.");
                    }
                }
            }

            Assert.IsTrue(leaseCount > 0, "Expected at least one lease document in the lease container.");
            Assert.IsTrue(foundLeaseWithContinuation, "Expected at least one lease with a ContinuationToken.");

            return continuationTokens;
        }

        /// <summary>
        /// Parses a change feed continuation token (the response ETag, optionally surrounded by quotes)
        /// into its numeric LSN.
        /// </summary>
        private static bool TryParseLsn(string continuationToken, out long lsn)
        {
            lsn = 0;
            if (string.IsNullOrEmpty(continuationToken))
            {
                return false;
            }

            string trimmed = continuationToken.Trim().Trim('"');
            return long.TryParse(
                trimmed,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out lsn);
        }
    }

    internal class ChangeFeedHeaderValidationHandler : RequestHandler
    {
        public List<RequestMessage> CapturedRequests { get; } = new List<RequestMessage>();

        public override Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            if (request.ResourceType == Microsoft.Azure.Documents.ResourceType.Document
                && request.OperationType == Microsoft.Azure.Documents.OperationType.ReadFeed)
            {
                this.CapturedRequests.Add(request);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
