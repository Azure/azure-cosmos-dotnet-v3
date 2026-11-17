//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory(AadLiveTestSupport.TestCategory)]
    public class CosmosAadLiveLeaseTests
    {
        public TestContext TestContext { get; set; }

        [DataTestMethod]
        [DataRow(false, ConnectionMode.Gateway)]
        [DataRow(true, ConnectionMode.Gateway)]
        [DataRow(false, ConnectionMode.Direct)]
        [DataRow(true, ConnectionMode.Direct)]
        public async Task SequentialCheckpointsUseResponseETagsAsync(bool useEpkLease, ConnectionMode connectionMode)
        {
            AadLiveTestSupport.ValidateConfiguration();

            const int checkpointCount = 100;
            bool recording = false;
            int recoveryReads = 0;
            double requestCharge = 0;
            List<(HttpStatusCode Status, string IfMatch, string ETag, bool Suppressed, bool Empty)> replaces = new ();
            RequestHandlerHelper recorder = new RequestHandlerHelper
            {
                CallBackOnResponse = (request, response) =>
                {
                    if (recording && request.ResourceType == Documents.ResourceType.Document)
                    {
                        requestCharge += response.Headers.RequestCharge;
                        if (request.OperationType == Documents.OperationType.Read)
                        {
                            recoveryReads++;
                        }
                        else if (request.OperationType == Documents.OperationType.Replace)
                        {
                            replaces.Add((
                                response.StatusCode,
                                request.Headers[Documents.HttpConstants.HttpHeaders.IfMatch],
                                response.Headers.ETag,
                                request.RequestOptions is ItemRequestOptions options && options.EnableContentResponseOnWrite == false
                                    && request.Headers[Documents.HttpConstants.HttpHeaders.Prefer] == Documents.HttpConstants.HttpHeaderValues.PreferReturnMinimal,
                                response.Content == null || (response.Content.CanSeek && response.Content.Length == 0)));
                        }
                    }

                    return response;
                },
            };
            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                ConnectionMode = connectionMode,
            };
            clientOptions.CustomHandlers.Add(recorder);
            using CosmosClient client = AadLiveTestSupport.CreateClient(clientOptions);
            Container container = client.GetContainer(AadLiveTestSupport.DatabaseId, AadLiveTestSupport.ContainerId);
            await AadLiveTestSupport.ValidateFixtureAsync(container);
            ContainerProperties properties = await container.ReadContainerAsync();
            Assert.AreEqual(AadLiveTestSupport.PartitionKeyPath, properties.PartitionKeyPath);

            string leaseId = $"cfp-etag-{Guid.NewGuid():N}";
            DocumentServiceLease lease = useEpkLease
                ? new DocumentServiceLeaseCoreEpk { LeaseId = leaseId, LeaseToken = "AA-BB" }
                : new DocumentServiceLeaseCore { LeaseId = leaseId, LeaseToken = "0" };
            lease.Owner = leaseId;
            lease.ContinuationToken = "initial";
            lease.FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false));
            lease.Properties["test"] = leaseId;

            // Lease documents have no /pk field. Reuse the live fixture's undefined partition
            // with the existing non-partitioned lease request factory, without changing the lease schema.
            ItemResponse<DocumentServiceLease> created = await container.CreateItemAsync(
                lease,
                PartitionKey.None,
                new ItemRequestOptions { EnableContentResponseOnWrite = true });
            try
            {
                Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
                Assert.IsNotNull(created.Resource);
                Assert.IsFalse(string.IsNullOrWhiteSpace(created.ETag));
                Assert.AreEqual(created.ETag, created.Resource.ConcurrencyToken);
                Assert.AreEqual(lease.GetType(), created.Resource.GetType());
                DocumentServiceLease cachedLease = created.Resource;
                PartitionCheckpointerCore checkpointer = new PartitionCheckpointerCore(
                    new DocumentServiceLeaseCheckpointerCore(
                        new DocumentServiceLeaseUpdaterCosmos(container),
                        new SinglePartitionRequestOptionsFactory()),
                    cachedLease);

                Stopwatch elapsed = Stopwatch.StartNew();
                recording = true;
                try
                {
                    // Do not start a processor: renewal and load balancing would introduce other writers.
                    for (int checkpoint = 1; checkpoint <= checkpointCount; checkpoint++)
                    {
                        await checkpointer.CheckpointPartitionAsync($"continuation-{checkpoint}");
                    }
                }
                finally
                {
                    recording = false;
                    elapsed.Stop();
                    this.TestContext.WriteLine(
                        "Lease={0}, mode={1}, replaces={2}, successful={3}, conflicts={4}, recoveryReads={5}, RU={6:F2}, elapsedMs={7:F2}",
                        useEpkLease ? "EPK" : "PKRange",
                        connectionMode,
                        replaces.Count,
                        replaces.Count(replace => replace.Status == HttpStatusCode.OK),
                        replaces.Count(replace => replace.Status == HttpStatusCode.PreconditionFailed),
                        recoveryReads,
                        requestCharge,
                        elapsed.Elapsed.TotalMilliseconds);
                }

                Assert.AreEqual(0, replaces.Count(replace => replace.Status == HttpStatusCode.PreconditionFailed), "Sequential checkpoints must not conflict with their own preceding writes.");
                Assert.AreEqual(0, recoveryReads, "No conflict-recovery reads should be necessary.");
                Assert.AreEqual(checkpointCount, replaces.Count);
                string precedingETag = created.ETag;
                foreach ((HttpStatusCode status, string ifMatch, string etag, bool suppressed, bool empty) in replaces)
                {
                    Assert.AreEqual(HttpStatusCode.OK, status);
                    Assert.AreEqual(precedingETag, ifMatch, "If-Match must use the preceding successful response ETag.");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(etag));
                    Assert.AreNotEqual(precedingETag, etag, "Every checkpoint changes the continuation and must receive a fresh ETag.");
                    Assert.IsTrue(suppressed, "The request must retain EnableContentResponseOnWrite=false and Prefer: return=minimal.");
                    Assert.IsTrue(empty, "Successful lease Replace responses must have no body.");
                    precedingETag = etag;
                }

                Assert.AreEqual(created.ETag, cachedLease.ConcurrencyToken, "The shared input must not acquire a snapshot's response ETag.");

                ItemResponse<DocumentServiceLease> persisted = await container.ReadItemAsync<DocumentServiceLease>(leaseId, PartitionKey.None);
                Assert.AreEqual(lease.GetType(), persisted.Resource.GetType());
                Assert.AreEqual($"continuation-{checkpointCount}", persisted.Resource.ContinuationToken);
                Assert.AreEqual(precedingETag, persisted.ETag);
                Assert.AreEqual(persisted.ETag, persisted.Resource.ConcurrencyToken);
                Assert.AreEqual(lease.Owner, persisted.Resource.Owner);
                Assert.AreEqual(leaseId, persisted.Resource.Properties["test"]);
            }
            finally
            {
                using ResponseMessage deleted = await container.DeleteItemStreamAsync(leaseId, PartitionKey.None);
                deleted.EnsureSuccessStatusCode();
            }
        }
    }
}
