//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Live-account AAD (Microsoft Entra ID) coverage for the data-plane surface, brought to parity with the
    /// Python SDK's AAD lane (Azure/azure-sdk-for-python#46568).
    ///
    /// Why this exists
    /// ---------------
    /// <see cref="CosmosAadLiveTests"/> proves that an Entra token can be acquired, refreshed and used for a
    /// handful of representative operations. That is enough to catch a broken token pipeline, but not enough
    /// to catch an operation whose request path drops or mishandles the authorization header -- patch, batch,
    /// bulk, read-many, change feed continuations, per-request region overrides and hedged cross-region reads
    /// all build their requests differently, and a regression in any one of them is invisible to a CRUD-only
    /// smoke test. Python closed the same gap by making its whole data-plane suite runnable under AAD; this
    /// class is the .NET equivalent for the operations that are reachable here.
    ///
    /// Fixture constraint (the one real divergence from Python)
    /// -------------------------------------------------------
    /// Python's lane keeps a master-key client for setup and only swaps the *data* client to AAD. This
    /// account has local auth disabled, so there is no key client: the CI principal holds only
    /// Cosmos DB Built-in Data Contributor, which grants <c>readMetadata</c> + container actions + item
    /// actions and denies database/container creation (a control-plane operation). Every test therefore runs
    /// against the single pre-created <see cref="AadLiveTestSupport.DatabaseId"/> /
    /// <see cref="AadLiveTestSupport.ContainerId"/> (<c>/pk</c>) fixture, isolating itself behind a unique
    /// partition key and cleaning up in a <c>finally</c> block.
    ///
    /// Consequently the following Python areas are *not* reachable from a data-plane-only token and are
    /// deliberately out of scope rather than silently missing. Each needs a container created with
    /// non-default settings, a second container, or an account-level feature:
    /// hierarchical/sub-partition keys, computed properties, container TTL, composite/vector/full-text
    /// indexing (and therefore vector, full-text, hybrid and semantic-reranker search), autoscale and
    /// throughput control, partition splits, per-partition automatic failover and circuit breaker,
    /// all-versions-and-deletes change feed (needs continuous backup), the change feed processor (needs a
    /// lease container), and server-side scripts. Additionally, throughput buckets have a Python surface with
    /// no .NET equivalent today, so there is nothing to test here.
    ///
    /// Every case is tagged <c>MultiRegionAad</c> and runs in the fail-closed CI lane
    /// (<c>templates/build-test-aad.yml</c>): the lane requires exactly
    /// <see cref="AadLiveTestSupport.ExpectedTestCaseCount"/> passing results and zero skips, so these tests
    /// must be deterministic against a healthy account.
    /// </summary>
    [TestClass]
    [TestCategory(AadLiveTestSupport.TestCategory)]
    public class CosmosAadLiveDataPlaneTests
    {
        private CosmosClient client;
        private Container container;

        [TestInitialize]
        public async Task TestInitializeAsync()
        {
            AadLiveTestSupport.ValidateConfiguration();

            this.client = AadLiveTestSupport.CreateClient();
            this.container = this.client.GetContainer(AadLiveTestSupport.DatabaseId, AadLiveTestSupport.ContainerId);

            await AadLiveTestSupport.ValidateFixtureAsync(this.container);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.client?.Dispose();
            this.client = null;
            this.container = null;
        }

        /// <summary>
        /// Builds (but does not persist) <paramref name="count"/> items that all share
        /// <paramref name="partitionKeyValue"/>.
        /// <see cref="ToDoActivity.CreateRandomItems(Container, int, int, bool, bool)"/> is not usable here
        /// because it both picks its own partition key values and writes the items itself.
        /// </summary>
        private static List<ToDoActivity> BuildItems(int count, string partitionKeyValue, string description = null)
        {
            List<ToDoActivity> items = new List<ToDoActivity>(count);
            for (int i = 0; i < count; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);
                item.taskNum = i;
                if (description != null)
                {
                    item.description = description;
                }

                items.Add(item);
            }

            return items;
        }

        /// <summary>
        /// Patch builds a dedicated request body and uses a different resource operation than replace, so it
        /// exercises an authorization path that plain CRUD does not. Covers Python's patch coverage in
        /// test_crud.
        /// </summary>
        [TestMethod]
        public async Task AadItemPatchAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("patch");
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);

            try
            {
                await this.container.CreateItemAsync(item, new PartitionKey(partitionKeyValue));

                ItemResponse<ToDoActivity> patched = await this.container.PatchItemAsync<ToDoActivity>(
                    item.id,
                    new PartitionKey(partitionKeyValue),
                    new List<PatchOperation>
                    {
                        PatchOperation.Set("/description", "patched-by-aad"),
                        PatchOperation.Increment("/taskNum", 10),
                        PatchOperation.Set("/nullableInt", 42),
                    });

                Assert.AreEqual(HttpStatusCode.OK, patched.StatusCode);
                Assert.AreEqual("patched-by-aad", patched.Resource.description);
                Assert.AreEqual(item.taskNum + 10, patched.Resource.taskNum);
                Assert.AreEqual(42, patched.Resource.nullableInt);

                // Remove is only valid for a property that exists, hence the Set above.
                ItemResponse<ToDoActivity> removed = await this.container.PatchItemAsync<ToDoActivity>(
                    item.id,
                    new PartitionKey(partitionKeyValue),
                    new List<PatchOperation> { PatchOperation.Remove("/nullableInt") });

                Assert.IsNull(removed.Resource.nullableInt);
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, partitionKeyValue, new[] { item.id });
            }
        }

        /// <summary>
        /// A patch with a filter predicate carries the condition as a separate request header, so a failure
        /// here would be invisible to <see cref="AadItemPatchAsync"/>. Asserts both the satisfied and the
        /// unsatisfied predicate, since only the latter proves the predicate reached the server.
        /// </summary>
        [TestMethod]
        public async Task AadItemPatchWithFilterPredicateAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("patchfilter");
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);
            item.taskNum = 100;

            try
            {
                await this.container.CreateItemAsync(item, new PartitionKey(partitionKeyValue));

                ItemResponse<ToDoActivity> matched = await this.container.PatchItemAsync<ToDoActivity>(
                    item.id,
                    new PartitionKey(partitionKeyValue),
                    new List<PatchOperation> { PatchOperation.Set("/description", "predicate-matched") },
                    new PatchItemRequestOptions { FilterPredicate = "FROM c WHERE c.taskNum = 100" });

                Assert.AreEqual("predicate-matched", matched.Resource.description);

                CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(
                    () => this.container.PatchItemAsync<ToDoActivity>(
                        item.id,
                        new PartitionKey(partitionKeyValue),
                        new List<PatchOperation> { PatchOperation.Set("/description", "should-not-apply") },
                        new PatchItemRequestOptions { FilterPredicate = "FROM c WHERE c.taskNum = 999" }));

                Assert.AreEqual(HttpStatusCode.PreconditionFailed, exception.StatusCode);

                ItemResponse<ToDoActivity> unchanged = await this.container.ReadItemAsync<ToDoActivity>(item.id, new PartitionKey(partitionKeyValue));
                Assert.AreEqual("predicate-matched", unchanged.Resource.description);
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, partitionKeyValue, new[] { item.id });
            }
        }

        /// <summary>
        /// Optimistic concurrency: the ETag travels as a request header, and a stale ETag must produce 412
        /// rather than silently overwriting. Also covers the 304 path, which is only observable through the
        /// stream API because the typed API throws on non-success codes. Mirrors Python's etag coverage.
        /// </summary>
        [TestMethod]
        public async Task AadItemConditionalOperationsAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("etag");
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);

            try
            {
                ItemResponse<ToDoActivity> created = await this.container.CreateItemAsync(item, new PartitionKey(partitionKeyValue));
                string originalETag = created.ETag;
                Assert.IsFalse(string.IsNullOrEmpty(originalETag));

                item.description = "conditional-replace";
                ItemResponse<ToDoActivity> replaced = await this.container.ReplaceItemAsync(
                    item,
                    item.id,
                    new PartitionKey(partitionKeyValue),
                    new ItemRequestOptions { IfMatchEtag = originalETag });

                Assert.AreEqual(HttpStatusCode.OK, replaced.StatusCode);
                Assert.AreNotEqual(originalETag, replaced.ETag, "A successful replace must produce a new ETag.");

                item.description = "should-not-apply";
                CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(
                    () => this.container.ReplaceItemAsync(
                        item,
                        item.id,
                        new PartitionKey(partitionKeyValue),
                        new ItemRequestOptions { IfMatchEtag = originalETag }));

                Assert.AreEqual(HttpStatusCode.PreconditionFailed, exception.StatusCode);

                using (ResponseMessage notModified = await this.container.ReadItemStreamAsync(
                    item.id,
                    new PartitionKey(partitionKeyValue),
                    new ItemRequestOptions { IfNoneMatchEtag = replaced.ETag }))
                {
                    Assert.AreEqual(HttpStatusCode.NotModified, notModified.StatusCode);
                }
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, partitionKeyValue, new[] { item.id });
            }
        }

        /// <summary>
        /// The stream APIs bypass the serializer and return the raw <see cref="ResponseMessage"/>, so they
        /// have their own request/response plumbing. Covers Python's *_item stream-equivalent coverage and
        /// the "no exception on error status" contract that the typed API does not have.
        /// </summary>
        [TestMethod]
        public async Task AadItemStreamOperationsAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("stream");
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);
            PartitionKey partitionKey = new PartitionKey(partitionKeyValue);

            try
            {
                using (Stream payload = TestCommon.SerializerCore.ToStream(item))
                using (ResponseMessage created = await this.container.CreateItemStreamAsync(payload, partitionKey))
                {
                    Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
                    Assert.IsTrue(created.Headers.RequestCharge > 0);
                }

                using (ResponseMessage read = await this.container.ReadItemStreamAsync(item.id, partitionKey))
                {
                    Assert.AreEqual(HttpStatusCode.OK, read.StatusCode);
                    Assert.IsNotNull(read.Content);
                }

                item.description = "stream-replaced";
                using (Stream payload = TestCommon.SerializerCore.ToStream(item))
                using (ResponseMessage replaced = await this.container.ReplaceItemStreamAsync(payload, item.id, partitionKey))
                {
                    Assert.AreEqual(HttpStatusCode.OK, replaced.StatusCode);
                }

                item.description = "stream-upserted";
                using (Stream payload = TestCommon.SerializerCore.ToStream(item))
                using (ResponseMessage upserted = await this.container.UpsertItemStreamAsync(payload, partitionKey))
                {
                    Assert.AreEqual(HttpStatusCode.OK, upserted.StatusCode);
                }

                // The stream API surfaces failures as a status code instead of throwing.
                using (ResponseMessage missing = await this.container.ReadItemStreamAsync(Guid.NewGuid().ToString(), partitionKey))
                {
                    Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);
                    Assert.IsFalse(missing.IsSuccessStatusCode);
                }

                using (ResponseMessage deleted = await this.container.DeleteItemStreamAsync(item.id, partitionKey))
                {
                    Assert.AreEqual(HttpStatusCode.NoContent, deleted.StatusCode);
                }
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, partitionKeyValue, new[] { item.id });
            }
        }

        /// <summary>
        /// Disabling the write response payload changes the request headers and makes the SDK return a
        /// response with no body, which is a distinct deserialization path. Mirrors Python's
        /// test_crud_response_payload_on_write_disabled, including the per-request override that re-enables it.
        /// </summary>
        [TestMethod]
        public async Task AadContentResponseOnWriteDisabledAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("nocontent");
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);

            using (CosmosClient noContentClient = AadLiveTestSupport.CreateClient(
                new CosmosClientOptions { EnableContentResponseOnWrite = false }))
            {
                Container noContentContainer = noContentClient.GetContainer(AadLiveTestSupport.DatabaseId, AadLiveTestSupport.ContainerId);

                try
                {
                    ItemResponse<ToDoActivity> created = await noContentContainer.CreateItemAsync(item, new PartitionKey(partitionKeyValue));
                    Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
                    Assert.IsNull(created.Resource, "The client was configured to suppress the write response payload.");
                    Assert.IsTrue(created.RequestCharge > 0, "Headers must still be populated when the payload is suppressed.");

                    // A read is unaffected by the write-payload setting.
                    ItemResponse<ToDoActivity> read = await noContentContainer.ReadItemAsync<ToDoActivity>(item.id, new PartitionKey(partitionKeyValue));
                    Assert.IsNotNull(read.Resource);

                    item.description = "re-enabled-per-request";
                    ItemResponse<ToDoActivity> replaced = await noContentContainer.ReplaceItemAsync(
                        item,
                        item.id,
                        new PartitionKey(partitionKeyValue),
                        new ItemRequestOptions { EnableContentResponseOnWrite = true });

                    Assert.IsNotNull(replaced.Resource, "The per-request override must win over the client-level setting.");
                    Assert.AreEqual("re-enabled-per-request", replaced.Resource.description);
                }
                finally
                {
                    await AadLiveTestSupport.CleanupItemsAsync(noContentContainer, partitionKeyValue, new[] { item.id });
                }
            }
        }

        /// <summary>
        /// ReadMany issues point reads (or a query) per partition and merges the results, so it neither
        /// looks like a point read nor like a query on the wire. Mirrors Python's test_read_items, including
        /// the "missing id is omitted rather than fatal" behaviour.
        /// </summary>
        [TestMethod]
        public async Task AadReadManyItemsAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("readmany");
            List<ToDoActivity> items = CosmosAadLiveDataPlaneTests.BuildItems(3, partitionKeyValue);

            try
            {
                foreach (ToDoActivity item in items)
                {
                    await this.container.CreateItemAsync(item, new PartitionKey(partitionKeyValue));
                }

                List<(string, PartitionKey)> requested = items
                    .Select(item => (item.id, new PartitionKey(partitionKeyValue)))
                    .ToList();

                // An id that does not exist must be skipped rather than failing the whole call.
                requested.Add((Guid.NewGuid().ToString(), new PartitionKey(partitionKeyValue)));

                FeedResponse<ToDoActivity> response = await this.container.ReadManyItemsAsync<ToDoActivity>(requested);

                Assert.AreEqual(items.Count, response.Count);
                Assert.IsTrue(response.RequestCharge > 0);
                CollectionAssert.AreEquivalent(
                    items.Select(item => item.id).ToList(),
                    response.Select(item => item.id).ToList());

                using (ResponseMessage streamResponse = await this.container.ReadManyItemsStreamAsync(requested))
                {
                    Assert.AreEqual(HttpStatusCode.OK, streamResponse.StatusCode);
                    Assert.IsNotNull(streamResponse.Content);
                }
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, partitionKeyValue, items.Select(item => item.id));
            }
        }

        /// <summary>
        /// Bulk mode replaces the whole request pipeline with a batching executor that groups operations by
        /// partition key range, so it is the single most likely place for an auth header to be built
        /// differently. .NET-specific (Python's equivalent is per-operation concurrency), and worth covering
        /// precisely because it has no counterpart to inherit correctness from.
        /// </summary>
        [TestMethod]
        public async Task AadBulkExecutionAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("bulk");
            List<ToDoActivity> items = CosmosAadLiveDataPlaneTests.BuildItems(10, partitionKeyValue);

            using (CosmosClient bulkClient = AadLiveTestSupport.CreateClient(
                new CosmosClientOptions { AllowBulkExecution = true }))
            {
                Container bulkContainer = bulkClient.GetContainer(AadLiveTestSupport.DatabaseId, AadLiveTestSupport.ContainerId);

                try
                {
                    List<Task<ItemResponse<ToDoActivity>>> creates = items
                        .Select(item => bulkContainer.CreateItemAsync(item, new PartitionKey(partitionKeyValue)))
                        .ToList();

                    ItemResponse<ToDoActivity>[] created = await Task.WhenAll(creates);
                    Assert.IsTrue(created.All(response => response.StatusCode == HttpStatusCode.Created));

                    List<Task<ItemResponse<ToDoActivity>>> reads = items
                        .Select(item => bulkContainer.ReadItemAsync<ToDoActivity>(item.id, new PartitionKey(partitionKeyValue)))
                        .ToList();

                    ItemResponse<ToDoActivity>[] read = await Task.WhenAll(reads);
                    Assert.IsTrue(read.All(response => response.StatusCode == HttpStatusCode.OK));
                }
                finally
                {
                    await AadLiveTestSupport.CleanupItemsAsync(bulkContainer, partitionKeyValue, items.Select(item => item.id));
                }
            }
        }

        /// <summary>
        /// Transactional batch is a single request carrying many operations with its own wire format.
        /// Mirrors Python's test_transactional_batch: the success path, and the rollback path where one
        /// failing operation must leave the whole batch unapplied.
        /// </summary>
        [TestMethod]
        public async Task AadTransactionalBatchAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("batch");
            PartitionKey partitionKey = new PartitionKey(partitionKeyValue);
            ToDoActivity toCreate = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);
            ToDoActivity toReplace = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);
            ToDoActivity toDelete = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);

            try
            {
                await this.container.CreateItemAsync(toReplace, partitionKey);
                await this.container.CreateItemAsync(toDelete, partitionKey);

                toReplace.description = "batch-replaced";
                TransactionalBatchResponse batchResponse = await this.container
                    .CreateTransactionalBatch(partitionKey)
                    .CreateItem(toCreate)
                    .ReplaceItem(toReplace.id, toReplace)
                    .PatchItem(toReplace.id, new List<PatchOperation> { PatchOperation.Set("/cost", 12.5) })
                    .ReadItem(toReplace.id)
                    .DeleteItem(toDelete.id)
                    .ExecuteAsync();

                Assert.IsTrue(batchResponse.IsSuccessStatusCode, $"Batch failed with {batchResponse.StatusCode}: {batchResponse.ErrorMessage}");
                Assert.AreEqual(5, batchResponse.Count);
                Assert.AreEqual(HttpStatusCode.Created, batchResponse[0].StatusCode);
                Assert.AreEqual(HttpStatusCode.OK, batchResponse[3].StatusCode);
                Assert.AreEqual(HttpStatusCode.NoContent, batchResponse[4].StatusCode);

                ToDoActivity readBack = batchResponse.GetOperationResultAtIndex<ToDoActivity>(3).Resource;
                Assert.AreEqual("batch-replaced", readBack.description);
                Assert.AreEqual(12.5, readBack.cost);
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(
                    this.container,
                    partitionKeyValue,
                    new[] { toCreate.id, toReplace.id, toDelete.id });
            }
        }

        /// <summary>
        /// A batch whose second operation targets a missing item must fail atomically: the first operation
        /// reports 424 (dependent failure) and nothing is persisted. This is the assertion that proves the
        /// batch really executed server-side as a transaction under an Entra token.
        /// </summary>
        [TestMethod]
        public async Task AadTransactionalBatchRollsBackAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("batchfail");
            PartitionKey partitionKey = new PartitionKey(partitionKeyValue);
            ToDoActivity toCreate = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);

            try
            {
                TransactionalBatchResponse batchResponse = await this.container
                    .CreateTransactionalBatch(partitionKey)
                    .CreateItem(toCreate)
                    .ReadItem(Guid.NewGuid().ToString())
                    .ExecuteAsync();

                Assert.IsFalse(batchResponse.IsSuccessStatusCode);
                Assert.AreEqual(HttpStatusCode.NotFound, batchResponse.StatusCode);
                Assert.AreEqual((HttpStatusCode)424, batchResponse[0].StatusCode, "The successful operation must be reported as a dependent failure.");
                Assert.AreEqual(HttpStatusCode.NotFound, batchResponse[1].StatusCode);

                CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(
                    () => this.container.ReadItemAsync<ToDoActivity>(toCreate.id, partitionKey));
                Assert.AreEqual(HttpStatusCode.NotFound, exception.StatusCode, "The batch must not have persisted the created item.");
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, partitionKeyValue, new[] { toCreate.id });
            }
        }

        /// <summary>
        /// Aggregates run through the query pipeline's aggregate stage and return a shape unrelated to the
        /// stored document, so they exercise a different response path than a projection query. Mirrors
        /// Python's test_aggregate. Scoped to a single partition key so the expected values are exact rather
        /// than "at least".
        /// </summary>
        [TestMethod]
        public async Task AadAggregateQueryAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("aggregate");
            List<ToDoActivity> items = CosmosAadLiveDataPlaneTests.BuildItems(5, partitionKeyValue);
            QueryRequestOptions options = new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKeyValue) };

            try
            {
                foreach (ToDoActivity item in items)
                {
                    await this.container.CreateItemAsync(item, new PartitionKey(partitionKeyValue));
                }

                // taskNum is 0..4 by construction, so every aggregate has a single correct answer.
                Assert.AreEqual(5, await this.ReadSingleValueAsync<int>("SELECT VALUE COUNT(1) FROM c", options));
                Assert.AreEqual(10, await this.ReadSingleValueAsync<int>("SELECT VALUE SUM(c.taskNum) FROM c", options));
                Assert.AreEqual(0, await this.ReadSingleValueAsync<int>("SELECT VALUE MIN(c.taskNum) FROM c", options));
                Assert.AreEqual(4, await this.ReadSingleValueAsync<int>("SELECT VALUE MAX(c.taskNum) FROM c", options));
                Assert.AreEqual(2.0, await this.ReadSingleValueAsync<double>("SELECT VALUE AVG(c.taskNum) FROM c", options));
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, partitionKeyValue, items.Select(item => item.id));
            }
        }

        /// <summary>
        /// ORDER BY is served by a separate pipeline stage that fans out per partition key range and merges
        /// in order, so it makes several authenticated round trips where a simple query makes one. Mirrors
        /// Python's test_orderby. Runs cross-partition (no partition key filter) and isolates its own data
        /// with a marker on <see cref="ToDoActivity.description"/>.
        /// </summary>
        [TestMethod]
        public async Task AadOrderByQueryAsync()
        {
            string marker = $"aad-orderby-{Guid.NewGuid():N}";
            List<ToDoActivity> items = new List<ToDoActivity>();
            for (int i = 0; i < 5; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: AadLiveTestSupport.NewPartitionKeyValue("orderby"));
                item.taskNum = i;
                item.description = marker;
                items.Add(item);
            }

            try
            {
                foreach (ToDoActivity item in items)
                {
                    await this.container.CreateItemAsync(item, new PartitionKey(item.pk));
                }

                QueryDefinition ascending = new QueryDefinition(
                    "SELECT * FROM c WHERE c.description = @marker ORDER BY c.taskNum ASC")
                    .WithParameter("@marker", marker);

                List<ToDoActivity> ascendingResults = await this.DrainQueryAsync<ToDoActivity>(ascending, new QueryRequestOptions { MaxItemCount = 2 });
                CollectionAssert.AreEqual(
                    new[] { 0, 1, 2, 3, 4 },
                    ascendingResults.Select(item => item.taskNum).ToArray());

                QueryDefinition descending = new QueryDefinition(
                    "SELECT * FROM c WHERE c.description = @marker ORDER BY c.taskNum DESC")
                    .WithParameter("@marker", marker);

                List<ToDoActivity> descendingResults = await this.DrainQueryAsync<ToDoActivity>(descending, new QueryRequestOptions { MaxItemCount = 2 });
                CollectionAssert.AreEqual(
                    new[] { 4, 3, 2, 1, 0 },
                    descendingResults.Select(item => item.taskNum).ToArray());
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, items);
            }
        }

        /// <summary>
        /// A cross-partition query that is resumed from a continuation token re-authenticates on the follow-up
        /// request, so it proves the token is applied to more than just the first page. Mirrors Python's
        /// cross-partition / execution-context query coverage.
        /// </summary>
        [TestMethod]
        public async Task AadCrossPartitionQueryWithContinuationAsync()
        {
            string marker = $"aad-xpart-{Guid.NewGuid():N}";
            List<ToDoActivity> items = new List<ToDoActivity>();
            for (int i = 0; i < 6; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: AadLiveTestSupport.NewPartitionKeyValue("xpart"));
                item.taskNum = i;
                item.description = marker;
                items.Add(item);
            }

            try
            {
                foreach (ToDoActivity item in items)
                {
                    await this.container.CreateItemAsync(item, new PartitionKey(item.pk));
                }

                QueryDefinition query = new QueryDefinition("SELECT * FROM c WHERE c.description = @marker")
                    .WithParameter("@marker", marker);

                List<ToDoActivity> collected = new List<ToDoActivity>();
                string continuationToken = null;
                int pageCount = 0;

                // Re-creating the iterator from the continuation token each round forces the resume path
                // rather than the (cached) in-iterator paging path.
                do
                {
                    using (FeedIterator<ToDoActivity> iterator = this.container.GetItemQueryIterator<ToDoActivity>(
                        query,
                        continuationToken,
                        new QueryRequestOptions { MaxItemCount = 2, MaxConcurrency = 2 }))
                    {
                        FeedResponse<ToDoActivity> page = await iterator.ReadNextAsync();
                        collected.AddRange(page);
                        continuationToken = page.ContinuationToken;
                        pageCount++;
                    }
                }
                while (continuationToken != null && pageCount < 20);

                Assert.IsNull(continuationToken, "The query should have drained within the page budget.");
                Assert.IsTrue(pageCount > 1, "MaxItemCount=2 over 6 items must produce more than one page.");
                CollectionAssert.AreEquivalent(
                    items.Select(item => item.id).ToList(),
                    collected.Select(item => item.id).ToList());
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, items);
            }
        }

        /// <summary>
        /// DISTINCT, GROUP BY and OFFSET/LIMIT each add their own pipeline stage on top of the base query.
        /// Mirrors the corresponding Python query tests. Scoped to one partition key so the counts are exact.
        /// </summary>
        [TestMethod]
        public async Task AadDistinctGroupByOffsetLimitQueryAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("groupby");
            PartitionKey partitionKey = new PartitionKey(partitionKeyValue);
            QueryRequestOptions options = new QueryRequestOptions { PartitionKey = partitionKey, MaxItemCount = 2 };

            // Two distinct CamelCase values across six items: "group-a" x4, "group-b" x2.
            List<ToDoActivity> items = CosmosAadLiveDataPlaneTests.BuildItems(6, partitionKeyValue);
            for (int i = 0; i < items.Count; i++)
            {
                items[i].CamelCase = i < 4 ? "group-a" : "group-b";
            }

            try
            {
                foreach (ToDoActivity item in items)
                {
                    await this.container.CreateItemAsync(item, partitionKey);
                }

                List<string> distinct = await this.DrainQueryAsync<string>(
                    new QueryDefinition("SELECT DISTINCT VALUE c.CamelCase FROM c"),
                    options);
                CollectionAssert.AreEquivalent(new[] { "group-a", "group-b" }, distinct);

                List<GroupByResult> grouped = await this.DrainQueryAsync<GroupByResult>(
                    new QueryDefinition("SELECT c.CamelCase AS name, COUNT(1) AS total FROM c GROUP BY c.CamelCase"),
                    options);
                Assert.AreEqual(2, grouped.Count);
                Assert.AreEqual(4, grouped.Single(g => g.name == "group-a").total);
                Assert.AreEqual(2, grouped.Single(g => g.name == "group-b").total);

                List<ToDoActivity> paged = await this.DrainQueryAsync<ToDoActivity>(
                    new QueryDefinition("SELECT * FROM c ORDER BY c.taskNum ASC OFFSET 2 LIMIT 3"),
                    options);
                CollectionAssert.AreEqual(new[] { 2, 3, 4 }, paged.Select(item => item.taskNum).ToArray());
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, partitionKeyValue, items.Select(item => item.id));
            }
        }

        /// <summary>
        /// Feed ranges are how a caller parallelises a query itself, and the per-feed-range overload takes a
        /// different code path than a plain cross-partition query. Mirrors Python's test_feed_range and
        /// test_query_feed_range, including the documented JSON round trip.
        /// </summary>
        [TestMethod]
        public async Task AadFeedRangeQueryAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("feedrange");
            List<ToDoActivity> items = CosmosAadLiveDataPlaneTests.BuildItems(3, partitionKeyValue);

            try
            {
                foreach (ToDoActivity item in items)
                {
                    await this.container.CreateItemAsync(item, new PartitionKey(partitionKeyValue));
                }

                IReadOnlyList<FeedRange> feedRanges = await this.container.GetFeedRangesAsync();
                Assert.IsTrue(feedRanges.Count > 0, "A container always has at least one feed range.");

                QueryDefinition query = new QueryDefinition("SELECT * FROM c WHERE c.pk = @pk")
                    .WithParameter("@pk", partitionKeyValue);

                int found = 0;
                foreach (FeedRange feedRange in feedRanges)
                {
                    using (FeedIterator<ToDoActivity> iterator = this.container.GetItemQueryIterator<ToDoActivity>(
                        feedRange,
                        query))
                    {
                        while (iterator.HasMoreResults)
                        {
                            found += (await iterator.ReadNextAsync()).Count;
                        }
                    }
                }

                Assert.AreEqual(items.Count, found, "Querying every feed range must cover the whole container exactly once.");

                // A feed range is serializable so callers can hand ranges to other workers.
                FeedRange roundTripped = FeedRange.FromJsonString(feedRanges[0].ToJsonString());
                using (FeedIterator<ToDoActivity> iterator = this.container.GetItemQueryIterator<ToDoActivity>(roundTripped, query))
                {
                    Assert.IsTrue(iterator.HasMoreResults);
                    await iterator.ReadNextAsync();
                }

                // A feed range scoped to a single partition key must see all of that key's items.
                // NOTE: the `FeedRange.FromPartitionKey(...)` + `GetItemQueryIterator` combination is
                // deliberately NOT used here - it throws ArgumentOutOfRangeException in the query pipeline
                // (https://github.com/Azure/azure-cosmos-dotnet-v3/issues/6062). The supported way to scope a
                // query to one logical partition, and the one Python's suite exercises, is
                // QueryRequestOptions.PartitionKey.
                int foundInPartition = 0;
                QueryRequestOptions partitionScoped = new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(partitionKeyValue),
                };

                using (FeedIterator<ToDoActivity> iterator = this.container.GetItemQueryIterator<ToDoActivity>(
                    query,
                    requestOptions: partitionScoped))
                {
                    while (iterator.HasMoreResults)
                    {
                        foundInPartition += (await iterator.ReadNextAsync()).Count;
                    }
                }

                Assert.AreEqual(items.Count, foundInPartition);
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, partitionKeyValue, items.Select(item => item.id));
            }
        }

        /// <summary>
        /// The query stream API and the response metadata (RU charge, activity id, diagnostics, index
        /// metrics) are what callers use for telemetry, and index metrics in particular only appear when an
        /// extra request header is honoured. Mirrors Python's test_query_response_headers /
        /// test_cosmos_responses.
        /// </summary>
        [TestMethod]
        public async Task AadQueryResponseMetadataAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("querymeta");
            List<ToDoActivity> items = CosmosAadLiveDataPlaneTests.BuildItems(2, partitionKeyValue);

            try
            {
                foreach (ToDoActivity item in items)
                {
                    await this.container.CreateItemAsync(item, new PartitionKey(partitionKeyValue));
                }

                QueryDefinition query = new QueryDefinition("SELECT * FROM c WHERE c.pk = @pk")
                    .WithParameter("@pk", partitionKeyValue);

                using (FeedIterator<ToDoActivity> iterator = this.container.GetItemQueryIterator<ToDoActivity>(
                    query,
                    requestOptions: new QueryRequestOptions
                    {
                        PartitionKey = new PartitionKey(partitionKeyValue),
                        PopulateIndexMetrics = true,
                    }))
                {
                    FeedResponse<ToDoActivity> response = await iterator.ReadNextAsync();

                    Assert.AreEqual(items.Count, response.Count);
                    Assert.IsTrue(response.RequestCharge > 0);
                    Assert.IsFalse(string.IsNullOrEmpty(response.ActivityId));
                    Assert.IsNotNull(response.Diagnostics);
                    Assert.IsFalse(string.IsNullOrEmpty(response.IndexMetrics), "PopulateIndexMetrics was requested.");
                }

                using (FeedIterator streamIterator = this.container.GetItemQueryStreamIterator(
                    query,
                    requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKeyValue) }))
                {
                    using (ResponseMessage response = await streamIterator.ReadNextAsync())
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                        Assert.IsTrue(response.Headers.RequestCharge > 0);
                        Assert.IsNotNull(response.Content);
                    }
                }
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, partitionKeyValue, items.Select(item => item.id));
            }
        }

        /// <summary>
        /// The LINQ provider builds the SQL text itself, so it is a distinct entry point into the query
        /// pipeline. .NET-specific (Python has no LINQ equivalent) and therefore has no sibling test to
        /// inherit coverage from.
        /// </summary>
        [TestMethod]
        public async Task AadLinqQueryAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("linq");
            List<ToDoActivity> items = CosmosAadLiveDataPlaneTests.BuildItems(4, partitionKeyValue);

            try
            {
                foreach (ToDoActivity item in items)
                {
                    await this.container.CreateItemAsync(item, new PartitionKey(partitionKeyValue));
                }

                IOrderedQueryable<ToDoActivity> queryable = this.container.GetItemLinqQueryable<ToDoActivity>(
                    requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKeyValue) });

                List<ToDoActivity> results = new List<ToDoActivity>();
                using (FeedIterator<ToDoActivity> iterator = queryable
                    .Where(item => item.taskNum >= 2)
                    .OrderBy(item => item.taskNum)
                    .ToFeedIterator())
                {
                    while (iterator.HasMoreResults)
                    {
                        results.AddRange(await iterator.ReadNextAsync());
                    }
                }

                CollectionAssert.AreEqual(new[] { 2, 3 }, results.Select(item => item.taskNum).ToArray());
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, partitionKeyValue, items.Select(item => item.id));
            }
        }

        /// <summary>
        /// Change feed uses its own request headers and its own start-from modes. Scoping the read to a feed
        /// range built from this test's partition key is what makes the expected item count exact on a shared
        /// container. Mirrors Python's test_change_feed start-from coverage.
        /// </summary>
        [TestMethod]
        public async Task AadChangeFeedStartFromAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("changefeed");
            PartitionKey partitionKey = new PartitionKey(partitionKeyValue);
            FeedRange feedRange = FeedRange.FromPartitionKey(partitionKey);
            List<ToDoActivity> items = CosmosAadLiveDataPlaneTests.BuildItems(3, partitionKeyValue);

            try
            {
                foreach (ToDoActivity item in items)
                {
                    await this.container.CreateItemAsync(item, partitionKey);
                }

                List<ToDoActivity> fromBeginning = await this.DrainChangeFeedAsync(ChangeFeedStartFrom.Beginning(feedRange));
                Assert.AreEqual(items.Count, fromBeginning.Count);
                CollectionAssert.AreEquivalent(
                    items.Select(item => item.id).ToList(),
                    fromBeginning.Select(item => item.id).ToList());

                // A generous lower bound keeps this immune to clock skew between the client and the service
                // while still exercising the start-from-time request header.
                List<ToDoActivity> fromTime = await this.DrainChangeFeedAsync(
                    ChangeFeedStartFrom.Time(DateTime.UtcNow.AddHours(-1), feedRange));
                Assert.AreEqual(items.Count, fromTime.Count);

                // "Now" is defined as "changes after this call", so a feed range with no subsequent writes
                // must yield nothing.
                List<ToDoActivity> fromNow = await this.DrainChangeFeedAsync(ChangeFeedStartFrom.Now(feedRange));
                Assert.AreEqual(0, fromNow.Count);
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, partitionKeyValue, items.Select(item => item.id));
            }
        }

        /// <summary>
        /// Resuming the change feed from a continuation token is the pattern every real consumer uses, and it
        /// re-authenticates on each resumed request. Asserts that a resumed reader sees only what was written
        /// after the token was taken.
        /// </summary>
        [TestMethod]
        public async Task AadChangeFeedContinuationAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("cfcontinuation");
            PartitionKey partitionKey = new PartitionKey(partitionKeyValue);
            FeedRange feedRange = FeedRange.FromPartitionKey(partitionKey);
            ToDoActivity first = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);
            ToDoActivity second = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);

            try
            {
                await this.container.CreateItemAsync(first, partitionKey);

                string continuationToken = null;
                List<ToDoActivity> observed = new List<ToDoActivity>();
                using (FeedIterator<ToDoActivity> iterator = this.container.GetChangeFeedIterator<ToDoActivity>(
                    ChangeFeedStartFrom.Beginning(feedRange),
                    ChangeFeedMode.Incremental))
                {
                    // Drain until the feed reports "caught up" (304), which is when the continuation token
                    // is safe to persist.
                    while (iterator.HasMoreResults)
                    {
                        FeedResponse<ToDoActivity> page = await iterator.ReadNextAsync();
                        if (page.StatusCode == HttpStatusCode.NotModified)
                        {
                            continuationToken = page.ContinuationToken;
                            break;
                        }

                        observed.AddRange(page);
                    }
                }

                Assert.AreEqual(1, observed.Count);
                Assert.IsFalse(string.IsNullOrEmpty(continuationToken));

                await this.container.CreateItemAsync(second, partitionKey);

                List<ToDoActivity> resumed = await this.DrainChangeFeedAsync(ChangeFeedStartFrom.ContinuationToken(continuationToken));
                Assert.AreEqual(1, resumed.Count, "Only the change written after the token was taken should be returned.");
                Assert.AreEqual(second.id, resumed[0].id);
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, partitionKeyValue, new[] { first.id, second.id });
            }
        }

        /// <summary>
        /// Session tokens are round-tripped through request/response headers on every operation, and passing
        /// one back explicitly is how a caller pins read-your-writes across clients. Mirrors Python's
        /// test_session / test_latest_session_token. Branches on the account's configured consistency because
        /// a session token is only guaranteed to be issued under session consistency.
        /// </summary>
        [TestMethod]
        public async Task AadSessionTokenAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("session");
            PartitionKey partitionKey = new PartitionKey(partitionKeyValue);
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);

            try
            {
                AccountProperties account = await this.client.ReadAccountAsync();
                bool sessionConsistency = account.Consistency.DefaultConsistencyLevel == ConsistencyLevel.Session;

                ItemResponse<ToDoActivity> created = await this.container.CreateItemAsync(item, partitionKey);
                string sessionToken = created.Headers.Session;

                if (sessionConsistency)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(sessionToken), "A session-consistency account must return a session token.");
                }

                using (CosmosClient freshClient = AadLiveTestSupport.CreateClient())
                {
                    Container freshContainer = freshClient.GetContainer(AadLiveTestSupport.DatabaseId, AadLiveTestSupport.ContainerId);
                    ItemResponse<ToDoActivity> read = await freshContainer.ReadItemAsync<ToDoActivity>(
                        item.id,
                        partitionKey,
                        new ItemRequestOptions { SessionToken = sessionToken });

                    Assert.AreEqual(item.id, read.Resource.id);

                    QueryDefinition query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id").WithParameter("@id", item.id);
                    List<ToDoActivity> queried = await this.DrainQueryAsync<ToDoActivity>(
                        freshContainer,
                        query,
                        new QueryRequestOptions { PartitionKey = partitionKey, SessionToken = sessionToken });

                    Assert.AreEqual(1, queried.Count);
                }
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, partitionKeyValue, new[] { item.id });
            }
        }

        /// <summary>
        /// Request-level knobs (priority level, caller-supplied headers) are serialized alongside the
        /// authorization header, so a change to header handling can break them together. Also asserts the
        /// response metadata callers depend on. Mirrors Python's test_headers.
        /// </summary>
        [TestMethod]
        public async Task AadRequestAndResponseHeadersAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("headers");
            PartitionKey partitionKey = new PartitionKey(partitionKeyValue);
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);
            const string correlationHeader = "x-ms-cosmos-test-correlation-id";
            string correlationValue = Guid.NewGuid().ToString();

            try
            {
                ItemResponse<ToDoActivity> created = await this.container.CreateItemAsync(
                    item,
                    partitionKey,
                    new ItemRequestOptions
                    {
                        PriorityLevel = PriorityLevel.Low,
                        AddRequestHeaders = headers => headers.Add(correlationHeader, correlationValue),
                    });

                Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
                Assert.IsTrue(created.Headers.RequestCharge > 0);
                Assert.IsFalse(string.IsNullOrEmpty(created.Headers.ActivityId));
                Assert.IsFalse(string.IsNullOrEmpty(created.Headers.ETag));
                Assert.IsNotNull(created.Diagnostics);
                Assert.IsTrue(created.Diagnostics.GetClientElapsedTime() > TimeSpan.Zero);

                ItemResponse<ToDoActivity> read = await this.container.ReadItemAsync<ToDoActivity>(
                    item.id,
                    partitionKey,
                    new ItemRequestOptions { PriorityLevel = PriorityLevel.High });

                Assert.AreEqual(item.id, read.Resource.id);
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, partitionKeyValue, new[] { item.id });
            }
        }

        /// <summary>
        /// Every request option is optional, and passing null must not change behaviour. This is the
        /// regression guard for a null-handling bug in the options-to-headers path, which under AAD would
        /// surface as a lost authorization header. Mirrors Python's test_none_options.
        /// </summary>
        [TestMethod]
        public async Task AadNullRequestOptionsAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("nulloptions");
            PartitionKey partitionKey = new PartitionKey(partitionKeyValue);
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);

            try
            {
                await this.container.CreateItemAsync(item, partitionKey, requestOptions: null);
                await this.container.ReadItemAsync<ToDoActivity>(item.id, partitionKey, requestOptions: null);

                item.description = "null-options-replace";
                await this.container.ReplaceItemAsync(item, item.id, partitionKey, requestOptions: null);
                await this.container.UpsertItemAsync(item, partitionKey, requestOptions: null);

                using (FeedIterator<ToDoActivity> iterator = this.container.GetItemQueryIterator<ToDoActivity>(
                    new QueryDefinition("SELECT * FROM c WHERE c.pk = @pk").WithParameter("@pk", partitionKeyValue),
                    continuationToken: null,
                    requestOptions: null))
                {
                    FeedResponse<ToDoActivity> response = await iterator.ReadNextAsync();
                    Assert.AreEqual(1, response.Count);
                }

                ItemResponse<ToDoActivity> deleted = await this.container.DeleteItemAsync<ToDoActivity>(item.id, partitionKey, requestOptions: null);
                Assert.AreEqual(HttpStatusCode.NoContent, deleted.StatusCode);
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, partitionKeyValue, new[] { item.id });
            }
        }

        /// <summary>
        /// Non-ASCII content has to survive UTF-8 encoding in the request body and in query parameters
        /// (which are also carried in the body). Mirrors Python's test_encoding.
        ///
        /// Deliberately keeps the partition key ASCII: a partition key value is additionally serialized into
        /// an HTTP header, and no test in this suite establishes that non-ASCII partition keys round-trip, so
        /// asserting it here would make the strict live AAD lane fail for a reason that has nothing to do
        /// with Entra authentication.
        /// </summary>
        [TestMethod]
        public async Task AadUnicodeEncodingAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("unicode");
            PartitionKey partitionKey = new PartitionKey(partitionKeyValue);
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);
            item.description = "\u00e9\u00e8\u00ea \u4e2d\u6587 \u0440\u0443\u0441\u0441\u043a\u0438\u0439 \ud83d\ude80 \u2713";
            item.CamelCase = "\u201c\u201d~`{}[]|;':,.<> \u7b2c67\u5c4a\u5967\u65af\u5361";

            try
            {
                ItemResponse<ToDoActivity> created = await this.container.CreateItemAsync(item, partitionKey);
                Assert.AreEqual(item.description, created.Resource.description);
                Assert.AreEqual(item.CamelCase, created.Resource.CamelCase);

                ItemResponse<ToDoActivity> read = await this.container.ReadItemAsync<ToDoActivity>(item.id, partitionKey);
                Assert.AreEqual(item.description, read.Resource.description);
                Assert.AreEqual(item.CamelCase, read.Resource.CamelCase);

                List<ToDoActivity> queried = await this.DrainQueryAsync<ToDoActivity>(
                    new QueryDefinition("SELECT * FROM c WHERE c.description = @description")
                        .WithParameter("@description", item.description),
                    new QueryRequestOptions { PartitionKey = partitionKey });

                Assert.AreEqual(1, queried.Count);
                Assert.AreEqual(item.description, queried[0].description);
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, partitionKeyValue, new[] { item.id });
            }
        }

        /// <summary>
        /// The partition key value is serialized into a request header, so each JSON type -- and the two
        /// special values, undefined (<see cref="PartitionKey.None"/>) and explicit null
        /// (<see cref="PartitionKey.Null"/>) -- has its own encoding to get wrong. The stream API is used
        /// because <see cref="ToDoActivity"/> types <c>pk</c> as a string. Mirrors Python's test_partition_key.
        /// </summary>
        [TestMethod]
        public async Task AadPartitionKeyVariationsAsync()
        {
            List<(string Id, PartitionKey PartitionKey, string Json)> cases = new List<(string, PartitionKey, string)>();

            string numericId = Guid.NewGuid().ToString();
            cases.Add((numericId, new PartitionKey(1234.5), $"{{\"id\":\"{numericId}\",\"pk\":1234.5}}"));

            string boolId = Guid.NewGuid().ToString();
            cases.Add((boolId, new PartitionKey(true), $"{{\"id\":\"{boolId}\",\"pk\":true}}"));

            string nullId = Guid.NewGuid().ToString();
            cases.Add((nullId, PartitionKey.Null, $"{{\"id\":\"{nullId}\",\"pk\":null}}"));

            // No "pk" property at all -> an undefined partition key.
            string noneId = Guid.NewGuid().ToString();
            cases.Add((noneId, PartitionKey.None, $"{{\"id\":\"{noneId}\"}}"));

            try
            {
                foreach ((string id, PartitionKey partitionKey, string json) in cases)
                {
                    using (Stream payload = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    using (ResponseMessage created = await this.container.CreateItemStreamAsync(payload, partitionKey))
                    {
                        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode, $"Failed to create item with partition key '{partitionKey}'.");
                    }

                    using (ResponseMessage read = await this.container.ReadItemStreamAsync(id, partitionKey))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, read.StatusCode, $"Failed to read item back with partition key '{partitionKey}'.");
                    }
                }
            }
            finally
            {
                foreach ((string id, PartitionKey partitionKey, string _) in cases)
                {
                    try
                    {
                        using (await this.container.DeleteItemStreamAsync(id, partitionKey))
                        {
                        }
                    }
                    catch (CosmosException)
                    {
                        // Best effort -- see AadLiveTestSupport.CleanupItemsAsync.
                    }
                }
            }
        }

        /// <summary>
        /// Excluding regions per request re-resolves the endpoint for that request only, which is a
        /// different routing path than the client-level preference list. Mirrors Python's
        /// test_excluded_locations. Degrades to asserting the no-op case on a single-region account rather
        /// than skipping, because the CI lane treats a skip as a failure.
        /// </summary>
        [TestMethod]
        public async Task AadExcludeRegionsAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("excluderegion");
            PartitionKey partitionKey = new PartitionKey(partitionKeyValue);
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);

            try
            {
                await this.container.CreateItemAsync(item, partitionKey);

                List<string> readableRegions = (await this.client.ReadAccountAsync())
                    .ReadableRegions
                    .Select(region => region.Name)
                    .ToList();

                Assert.IsTrue(readableRegions.Count > 0);

                // An empty exclusion list must behave exactly like no exclusion list.
                ItemResponse<ToDoActivity> unrestricted = await this.container.ReadItemAsync<ToDoActivity>(
                    item.id,
                    partitionKey,
                    new ItemRequestOptions { ExcludeRegions = new List<string>() });
                Assert.AreEqual(item.id, unrestricted.Resource.id);

                if (readableRegions.Count > 1)
                {
                    string excluded = readableRegions[0];

                    // Excluding a region forces this read onto a different replica than the one the write
                    // landed on, so it has to tolerate replication lag -- see the helper's remarks.
                    ItemResponse<ToDoActivity> restricted = await AadLiveTestSupport.ReadItemToleratingReplicationLagAsync<ToDoActivity>(
                        this.container,
                        item.id,
                        partitionKey,
                        new ItemRequestOptions { ExcludeRegions = new List<string> { excluded } });

                    Assert.AreEqual(item.id, restricted.Resource.id);

                    IReadOnlyList<(string, Uri)> contacted = restricted.Diagnostics.GetContactedRegions();
                    Assert.IsTrue(contacted.Count > 0, "Diagnostics must report the region that served the read.");
                    Assert.IsFalse(
                        contacted.Any(region => string.Equals(region.Item1, excluded, StringComparison.OrdinalIgnoreCase)),
                        $"Region '{excluded}' was excluded but still served the request. Contacted: {string.Join(", ", contacted.Select(region => region.Item1))}");
                }
            }
            finally
            {
                await AadLiveTestSupport.CleanupItemsAsync(this.container, partitionKeyValue, new[] { item.id });
            }
        }

        /// <summary>
        /// The two client-level region preferences are mutually exclusive and are resolved during client
        /// initialisation -- which, under AAD, is also when the first token is fetched. Mirrors Python's
        /// test_effective_preferred_locations / regional routing coverage.
        /// </summary>
        [TestMethod]
        public async Task AadPreferredRegionsAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("preferredregion");
            PartitionKey partitionKey = new PartitionKey(partitionKeyValue);
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);

            List<string> readableRegions;
            using (CosmosClient probe = AadLiveTestSupport.CreateClient())
            {
                readableRegions = (await probe.ReadAccountAsync()).ReadableRegions.Select(region => region.Name).ToList();
            }

            Assert.IsTrue(readableRegions.Count > 0);
            string preferredRegion = readableRegions[0];

            using (CosmosClient preferredRegionsClient = AadLiveTestSupport.CreateClient(
                new CosmosClientOptions { ApplicationPreferredRegions = readableRegions }))
            using (CosmosClient applicationRegionClient = AadLiveTestSupport.CreateClient(
                new CosmosClientOptions { ApplicationRegion = preferredRegion }))
            {
                Container preferredRegionsContainer = preferredRegionsClient.GetContainer(AadLiveTestSupport.DatabaseId, AadLiveTestSupport.ContainerId);
                Container applicationRegionContainer = applicationRegionClient.GetContainer(AadLiveTestSupport.DatabaseId, AadLiveTestSupport.ContainerId);

                try
                {
                    await preferredRegionsContainer.CreateItemAsync(item, partitionKey);

                    ItemResponse<ToDoActivity> viaPreferredRegions = await AadLiveTestSupport.ReadItemToleratingReplicationLagAsync<ToDoActivity>(
                        preferredRegionsContainer,
                        item.id,
                        partitionKey);
                    Assert.AreEqual(item.id, viaPreferredRegions.Resource.id);

                    // Deliberately not asserting *which* region served the read. The account's region order
                    // does not guarantee that readableRegions[0] is the write region, so a read served from
                    // a different preferred region is correct SDK behaviour (failover / read-your-write), and
                    // pinning the identity here would make the lane fail for a routing reason rather than an
                    // authentication one. What must hold is that routing stayed inside the account.
                    IReadOnlyList<(string, Uri)> contacted = viaPreferredRegions.Diagnostics.GetContactedRegions();
                    Assert.IsTrue(contacted.Count > 0, "Diagnostics must report the region that served the read.");
                    Assert.IsTrue(
                        contacted.All(region => readableRegions.Contains(region.Item1, StringComparer.OrdinalIgnoreCase)),
                        $"A read was served from a region outside the account's readable regions. Contacted: {string.Join(", ", contacted.Select(region => region.Item1))}; readable: {string.Join(", ", readableRegions)}.");

                    // A second client resolving its region through ApplicationRegion instead has no session
                    // token from the write above, so this read is the one most exposed to replication lag.
                    ItemResponse<ToDoActivity> viaApplicationRegion = await AadLiveTestSupport.ReadItemToleratingReplicationLagAsync<ToDoActivity>(
                        applicationRegionContainer,
                        item.id,
                        partitionKey);
                    Assert.AreEqual(item.id, viaApplicationRegion.Resource.id);
                }
                finally
                {
                    await AadLiveTestSupport.CleanupItemsAsync(preferredRegionsContainer, partitionKeyValue, new[] { item.id });
                }
            }
        }

        /// <summary>
        /// Cross-region hedging can issue the same logical request to a second region in parallel, so each
        /// hedged request needs its own valid token. A hedged read must still return exactly one correct
        /// result. Mirrors Python's test_availability_strategy.
        /// </summary>
        [TestMethod]
        public async Task AadAvailabilityStrategyAsync()
        {
            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("hedging");
            PartitionKey partitionKey = new PartitionKey(partitionKeyValue);
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);

            List<string> readableRegions;
            using (CosmosClient probe = AadLiveTestSupport.CreateClient())
            {
                readableRegions = (await probe.ReadAccountAsync()).ReadableRegions.Select(region => region.Name).ToList();
            }

            Assert.IsTrue(readableRegions.Count > 1, "The live AAD hedging test requires a multi-region account so there is a second region to hedge to.");

            // A very low threshold makes hedging fire on essentially every request rather than depending on
            // an unpredictable latency spike.
            using (CosmosClient hedgingClient = AadLiveTestSupport.CreateClient(new CosmosClientOptions
            {
                ApplicationPreferredRegions = readableRegions,
                AvailabilityStrategy = AvailabilityStrategy.CrossRegionHedgingStrategy(
                    threshold: TimeSpan.FromMilliseconds(10),
                    thresholdStep: TimeSpan.FromMilliseconds(10)),
            }))
            {
                Container hedgingContainer = hedgingClient.GetContainer(AadLiveTestSupport.DatabaseId, AadLiveTestSupport.ContainerId);

                try
                {
                    await hedgingContainer.CreateItemAsync(item, partitionKey);

                    // Both reads can be answered by a region other than the one the write landed on -- the
                    // hedged one by whichever region replies first, the un-hedged one through the preferred
                    // region order -- so both tolerate replication lag.
                    ItemResponse<ToDoActivity> hedged = await AadLiveTestSupport.ReadItemToleratingReplicationLagAsync<ToDoActivity>(
                        hedgingContainer,
                        item.id,
                        partitionKey);
                    Assert.AreEqual(item.id, hedged.Resource.id);
                    AssertHedgeContextPresent(hedged.Diagnostics.ToString(), readableRegions);

                    // The strategy is also disable-able per request, which is a separate code path.
                    ItemResponse<ToDoActivity> notHedged = await AadLiveTestSupport.ReadItemToleratingReplicationLagAsync<ToDoActivity>(
                        hedgingContainer,
                        item.id,
                        partitionKey,
                        new ItemRequestOptions { AvailabilityStrategy = AvailabilityStrategy.DisabledStrategy() });
                    Assert.AreEqual(item.id, notHedged.Resource.id);
                    Assert.IsFalse(
                        notHedged.Diagnostics.ToString().Contains("\"Hedge Context\"", StringComparison.Ordinal),
                        "Disabling the availability strategy per request should remove hedge diagnostics from the read.");
                }
                finally
                {
                    await AadLiveTestSupport.CleanupItemsAsync(hedgingContainer, partitionKeyValue, new[] { item.id });
                }
            }
        }

        /// <summary>
        /// Container metadata reads are the <c>readMetadata</c> data action and populate the caches every
        /// subsequent request depends on, so a token problem here would cascade. Mirrors Python's
        /// test_container_properties_cache / test_resource_id.
        /// </summary>
        [TestMethod]
        public async Task AadContainerMetadataAndCacheAsync()
        {
            ContainerResponse first = await this.container.ReadContainerAsync();
            Assert.AreEqual(HttpStatusCode.OK, first.StatusCode);
            Assert.AreEqual(AadLiveTestSupport.ContainerId, first.Resource.Id);
            Assert.IsFalse(string.IsNullOrEmpty(first.Resource.SelfLink));
            Assert.IsFalse(string.IsNullOrEmpty(first.Resource.ResourceId));
            CollectionAssert.AreEqual(
                new[] { AadLiveTestSupport.PartitionKeyPath },
                first.Resource.PartitionKeyPaths.ToArray());

            // The second read is served through the same authenticated path; the identifiers must be stable.
            ContainerResponse second = await this.container.ReadContainerAsync();
            Assert.AreEqual(first.Resource.ResourceId, second.Resource.ResourceId);
            Assert.AreEqual(first.Resource.SelfLink, second.Resource.SelfLink);

            IReadOnlyList<FeedRange> feedRanges = await this.container.GetFeedRangesAsync();
            Assert.IsTrue(feedRanges.Count > 0);
        }

        /// <summary>
        /// The AAD scope is derived from the account host by default but can be overridden with
        /// <c>AZURE_COSMOS_AAD_SCOPE_OVERRIDE</c> for sovereign clouds and custom endpoints. The live service
        /// call uses a delegating credential so the test can assert the SDK requested the override scope
        /// without needing a second cloud or a second account.
        /// Mirrors Python's AAD scope coverage in test_aad.
        /// </summary>
        [TestMethod]
        public async Task AadScopeOverrideAsync()
        {
            const string scopeOverrideEnvironmentVariable = "AZURE_COSMOS_AAD_SCOPE_OVERRIDE";
            string originalValue = Environment.GetEnvironmentVariable(scopeOverrideEnvironmentVariable);
            string endpoint = TestCommon.GetAadAccountEndpoint();
            string delegatedScope = string.Format(CultureInfo.InvariantCulture, "https://{0}/.default", new Uri(endpoint).Host);
            string explicitScope = "https://scope-override-test.invalid/.default";
            List<string> requestedScopes = new List<string>();

            string partitionKeyValue = AadLiveTestSupport.NewPartitionKeyValue("scopeoverride");
            PartitionKey partitionKey = new PartitionKey(partitionKeyValue);
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: partitionKeyValue);

            try
            {
                Environment.SetEnvironmentVariable(scopeOverrideEnvironmentVariable, explicitScope);

                // The override is read while the client is being constructed.
                using (CosmosClient scopedClient = AadLiveTestSupport.CreateClient(
                    new RecordingDelegatingTokenCredential(
                        TestCommon.GetAadTokenCredential(),
                        requestContext =>
                        {
                            lock (requestedScopes)
                            {
                                requestedScopes.Add(requestContext.Scopes[0]);
                            }
                        },
                        requestContext => new TokenRequestContext(
                            scopes: new[] { delegatedScope },
                            parentRequestId: requestContext.ParentRequestId,
                            claims: requestContext.Claims,
                            tenantId: requestContext.TenantId,
                            isCaeEnabled: requestContext.IsCaeEnabled))))
                {
                    Container scopedContainer = scopedClient.GetContainer(AadLiveTestSupport.DatabaseId, AadLiveTestSupport.ContainerId);

                    try
                    {
                        await scopedContainer.CreateItemAsync(item, partitionKey);
                        ItemResponse<ToDoActivity> read = await scopedContainer.ReadItemAsync<ToDoActivity>(item.id, partitionKey);
                        Assert.AreEqual(item.id, read.Resource.id);
                        CollectionAssert.Contains(requestedScopes, explicitScope);
                    }
                    finally
                    {
                        await AadLiveTestSupport.CleanupItemsAsync(scopedContainer, partitionKeyValue, new[] { item.id });
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(scopeOverrideEnvironmentVariable, originalValue);
            }
        }

        private async Task<T> ReadSingleValueAsync<T>(string queryText, QueryRequestOptions requestOptions)
        {
            List<T> results = await this.DrainQueryAsync<T>(new QueryDefinition(queryText), requestOptions);
            Assert.AreEqual(1, results.Count, $"Expected exactly one result from '{queryText}'.");
            return results[0];
        }

        private async Task<List<T>> DrainQueryAsync<T>(QueryDefinition query, QueryRequestOptions requestOptions)
        {
            return await this.DrainQueryAsync<T>(this.container, query, requestOptions);
        }

        private async Task<List<T>> DrainQueryAsync<T>(Container container, QueryDefinition query, QueryRequestOptions requestOptions)
        {
            List<T> results = new List<T>();
            using (FeedIterator<T> iterator = container.GetItemQueryIterator<T>(query, requestOptions: requestOptions))
            {
                while (iterator.HasMoreResults)
                {
                    results.AddRange(await iterator.ReadNextAsync());
                }
            }

            return results;
        }

        /// <summary>
        /// Reads the change feed until it reports 304 (caught up), which is the only terminating condition:
        /// a change feed iterator always reports <c>HasMoreResults</c>.
        /// </summary>
        private async Task<List<ToDoActivity>> DrainChangeFeedAsync(ChangeFeedStartFrom startFrom)
        {
            List<ToDoActivity> results = new List<ToDoActivity>();
            using (FeedIterator<ToDoActivity> iterator = this.container.GetChangeFeedIterator<ToDoActivity>(
                startFrom,
                ChangeFeedMode.Incremental))
            {
                while (iterator.HasMoreResults)
                {
                    FeedResponse<ToDoActivity> page = await iterator.ReadNextAsync();
                    if (page.StatusCode == HttpStatusCode.NotModified)
                    {
                        break;
                    }

                    results.AddRange(page);
                }
            }

            return results;
        }

        private sealed class GroupByResult
        {
            public string name { get; set; }

            public int total { get; set; }
        }

        private static void AssertHedgeContextPresent(string diagnostics, IReadOnlyList<string> readableRegions)
        {
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics), "Read diagnostics should be populated.");
            StringAssert.Contains(diagnostics, "\"Hedge Context\"");

            foreach (string region in readableRegions)
            {
                StringAssert.Contains(diagnostics, region);
            }
        }

        private sealed class RecordingDelegatingTokenCredential : TokenCredential
        {
            private readonly TokenCredential innerCredential;
            private readonly Action<TokenRequestContext> onTokenRequested;
            private readonly Func<TokenRequestContext, TokenRequestContext> forwardedRequestContextFactory;

            public RecordingDelegatingTokenCredential(
                TokenCredential innerCredential,
                Action<TokenRequestContext> onTokenRequested,
                Func<TokenRequestContext, TokenRequestContext> forwardedRequestContextFactory = null)
            {
                this.innerCredential = innerCredential ?? throw new ArgumentNullException(nameof(innerCredential));
                this.onTokenRequested = onTokenRequested ?? throw new ArgumentNullException(nameof(onTokenRequested));
                this.forwardedRequestContextFactory = forwardedRequestContextFactory;
            }

            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                this.onTokenRequested(requestContext);
                return this.innerCredential.GetToken(this.GetForwardedRequestContext(requestContext), cancellationToken);
            }

            public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                this.onTokenRequested(requestContext);
                return this.innerCredential.GetTokenAsync(this.GetForwardedRequestContext(requestContext), cancellationToken);
            }

            private TokenRequestContext GetForwardedRequestContext(TokenRequestContext requestContext)
            {
                return this.forwardedRequestContextFactory?.Invoke(requestContext) ?? requestContext;
            }
        }
    }
}
