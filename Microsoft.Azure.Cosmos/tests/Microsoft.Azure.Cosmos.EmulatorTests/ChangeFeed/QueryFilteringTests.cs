//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.ChangeFeed
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class QueryFilteringTests : BaseCosmosClientHelper
    {
        [TestInitialize]
        public async Task Initialize()
        {
            await this.TestInit();
            this.cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(2));
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await this.TestCleanup();
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task PullFiltersBothModesAndComposesWithPartitionKey(bool fullFidelity, bool gateway)
        {
            QueryReadHandler handler = new QueryReadHandler();
            using CosmosClient client = TestCommon.CreateCosmosClient(useGateway: gateway, builder => builder.AddCustomHandlers(handler));
            Container container = await this.CreateContainerAsync(client, fullFidelity);
            ChangeFeedMode mode = fullFidelity ? ChangeFeedMode.AllVersionsAndDeletes : ChangeFeedMode.LatestVersion;
            const string query = "SELECT * FROM c WHERE c.age > 5";
            string continuation;
            using (FeedIterator initial = container.GetChangeFeedStreamIteratorWithQuery(
                ChangeFeedStartFrom.Now(FeedRange.FromPartitionKey(new PartitionKey("one"))), mode, query, new ChangeFeedRequestOptions()))
            using (ResponseMessage response = await initial.ReadNextAsync(this.cancellationToken))
            {
                Assert.AreEqual(HttpStatusCode.NotModified, response.StatusCode);
                continuation = response.ContinuationToken;
            }

            await container.CreateItemAsync(new { id = "matching", pk = "one", age = 10 });
            await container.CreateItemAsync(new { id = "excluded", pk = "one", age = 1 });
            await container.CreateItemAsync(new { id = "other-partition", pk = "two", age = 10 });
            List<string> ids = new List<string>();
            int pages = 0;
            using FeedIterator<JObject> iterator = container.GetChangeFeedIteratorWithQuery<JObject>(
                ChangeFeedStartFrom.ContinuationToken(continuation), mode, query, new ChangeFeedRequestOptions { PageSizeHint = 1 });
            while (iterator.HasMoreResults)
            {
                FeedResponse<JObject> response = await iterator.ReadNextAsync(this.cancellationToken);
                Assert.IsTrue(++pages <= 10, "The fixed change feed backlog did not drain.");
                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }

                foreach (JObject item in response)
                {
                    ids.Add((string)(fullFidelity ? item["current"]["id"] : item["id"]));
                }
            }

            CollectionAssert.AreEqual(new[] { "matching" }, ids);
            Assert.IsTrue(handler.SawQuery);
        }

        [TestMethod]
        public async Task ExistingApisRetainUnfilteredReads()
        {
            QueryReadHandler handler = new QueryReadHandler();
            using CosmosClient client = TestCommon.CreateCosmosClient(builder => builder.AddCustomHandlers(handler));
            Container container = await this.CreateContainerAsync(client, fullFidelity: false);
            await container.CreateItemAsync(new { id = "unfiltered", pk = "one" });
            using FeedIterator<JObject> iterator = container.GetChangeFeedIterator<JObject>(
                ChangeFeedStartFrom.Beginning(), ChangeFeedMode.LatestVersion);
            FeedResponse<JObject> response = await iterator.ReadNextAsync(this.cancellationToken);
            Assert.AreEqual("unfiltered", (string)response.Single()["id"]);

            using FeedIterator stream = container.GetChangeFeedStreamIterator(
                ChangeFeedStartFrom.Beginning(), ChangeFeedMode.LatestVersion);
            using ResponseMessage streamResponse = await stream.ReadNextAsync(this.cancellationToken);
            streamResponse.EnsureSuccessStatusCode();
            using StreamReader reader = new StreamReader(streamResponse.Content);
            JArray documents = (JArray)JObject.Parse(await reader.ReadToEndAsync())["Documents"];
            Assert.AreEqual("unfiltered", (string)documents.Single()["id"]);
            Assert.IsTrue(handler.SawUnfilteredRead);
            Assert.IsFalse(handler.SawQuery);
        }

        [DataTestMethod]
        [DataRow("invalid SQL")]
        [DataRow("SELECT * FROM c JOIN child IN c.children WHERE child.age > 5")]
        public async Task ServiceErrorsAreNotConvertedToEmptyPages(string query)
        {
            using CosmosClient client = TestCommon.CreateCosmosClient();
            Container container = await this.CreateContainerAsync(client, fullFidelity: false);
            using FeedIterator stream = container.GetChangeFeedStreamIteratorWithQuery(
                ChangeFeedStartFrom.Beginning(), ChangeFeedMode.LatestVersion, query, new ChangeFeedRequestOptions());
            using ResponseMessage response = await stream.ReadNextAsync(this.cancellationToken);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            using FeedIterator<JObject> typed = container.GetChangeFeedIteratorWithQuery<JObject>(
                ChangeFeedStartFrom.Beginning(), ChangeFeedMode.LatestVersion, query, new ChangeFeedRequestOptions());
            CosmosException error = await Assert.ThrowsExceptionAsync<CosmosException>(() => typed.ReadNextAsync(this.cancellationToken));
            Assert.AreEqual(HttpStatusCode.BadRequest, error.StatusCode);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task PullContinuationAllowsChangingQueryWithoutReplayingEarlierChanges(bool fullFidelity, bool gateway)
        {
            QueryReadHandler handler = new QueryReadHandler();
            using CosmosClient client = TestCommon.CreateCosmosClient(useGateway: gateway, builder => builder.AddCustomHandlers(handler));
            Container container = await this.CreateContainerAsync(client, fullFidelity);
            ChangeFeedMode mode = fullFidelity ? ChangeFeedMode.AllVersionsAndDeletes : ChangeFeedMode.LatestVersion;
            const string originalQuery = "SELECT * FROM c WHERE c.state = \"WA\"";
            const string changedQuery = "SELECT * FROM c WHERE c.state = \"OR\"";
            string start;
            using (FeedIterator initial = container.GetChangeFeedStreamIteratorWithQuery(
                ChangeFeedStartFrom.Now(FeedRange.FromPartitionKey(new PartitionKey("one"))),
                mode, originalQuery, new ChangeFeedRequestOptions()))
            using (ResponseMessage response = await initial.ReadNextAsync(this.cancellationToken))
            {
                Assert.AreEqual(HttpStatusCode.NotModified, response.StatusCode);
                start = response.ContinuationToken;
                Assert.IsFalse(string.IsNullOrEmpty(start));
            }

            await container.CreateItemAsync(new { id = "or-before", pk = "one", state = "OR" }, cancellationToken: this.cancellationToken);
            await container.CreateItemAsync(new { id = "wa-before", pk = "one", state = "WA" }, cancellationToken: this.cancellationToken);
            (List<string> originalIds, string checkpoint) = await this.DrainQueryAsync(container, mode, originalQuery, start);
            CollectionAssert.AreEqual(new[] { "wa-before" }, originalIds);
            Assert.AreNotEqual(start, checkpoint);

            (List<string> resumedIds, _) = await this.DrainQueryAsync(container, mode, changedQuery, checkpoint);
            Assert.AreEqual(0, resumedIds.Count, "Changing the query must not replay changes before the saved physical progress.");
            (List<string> controlIds, _) = await this.DrainQueryAsync(container, mode, changedQuery, start);
            CollectionAssert.AreEqual(new[] { "or-before" }, controlIds);

            await container.CreateItemAsync(new { id = "or-after", pk = "one", state = "OR" }, cancellationToken: this.cancellationToken);
            await container.CreateItemAsync(new { id = "wa-after", pk = "one", state = "WA" }, cancellationToken: this.cancellationToken);
            (List<string> changedIds, _) = await this.DrainQueryAsync(container, mode, changedQuery, checkpoint);
            CollectionAssert.AreEqual(new[] { "or-after" }, changedIds);
            Assert.IsTrue(handler.SawQuery);
        }

        [DataTestMethod]
        [DataRow(false, null)]
        [DataRow(true, null)]
        [DataRow(false, "")]
        [DataRow(true, "")]
        [DataRow(false, "eWdVXz97p6d0Din52ndNUubhHkoWGGoYEaKKE5WbZvM=")]
        [DataRow(true, "eWdVXz97p6d0Din52ndNUubhHkoWGGoYEaKKE5WbZvM=")]
        public async Task LeaseFingerprintSurvivesBackendRoundTrip(bool epkLease, string hash)
        {
            using CosmosClient client = TestCommon.CreateCosmosClient();
            Container leases = await client.GetDatabase(this.database.Id).CreateContainerAsync(
                Guid.NewGuid().ToString(), "/id", cancellationToken: this.cancellationToken);
            string id = Guid.NewGuid().ToString();
            DocumentServiceLease lease = epkLease
                ? new DocumentServiceLeaseCoreEpk { LeaseId = id }
                : new DocumentServiceLeaseCore { LeaseId = id };
            lease.QueryFilterHash = hash;
            lease.Owner = "owner";
            lease.ContinuationToken = "saved-progress";
            PartitionKey partitionKey = new PartitionKey(id);
            await leases.CreateItemAsync(lease, partitionKey, cancellationToken: this.cancellationToken);
            ItemResponse<DocumentServiceLease> read = await leases.ReadItemAsync<DocumentServiceLease>(
                id, partitionKey, cancellationToken: this.cancellationToken);
            DocumentServiceLease persisted = read.Resource;
            Assert.AreEqual(lease.GetType(), persisted.GetType());
            Assert.AreEqual(hash, persisted.QueryFilterHash);
            Assert.AreEqual(lease.Owner, persisted.Owner);
            Assert.AreEqual(lease.ContinuationToken, persisted.ContinuationToken);

            using ResponseMessage raw = await leases.ReadItemStreamAsync(
                id, partitionKey, cancellationToken: this.cancellationToken);
            raw.EnsureSuccessStatusCode();
            using StreamReader reader = new StreamReader(raw.Content);
            JObject document = JObject.Parse(await reader.ReadToEndAsync());
            if (hash == null)
            {
                Assert.IsNull(document.Property("QueryFilterHash"));
            }
            else
            {
                Assert.AreEqual(hash, (string)document["QueryFilterHash"]);
            }

            DocumentServiceLeaseManager matching = Mock.Of<DocumentServiceLeaseManager>();
            matching.ConfigureQueryFilter(hash);
            matching.VerifyQueryFilter(persisted);
            DocumentServiceLeaseManager incompatible = Mock.Of<DocumentServiceLeaseManager>();
            incompatible.ConfigureQueryFilter(ChangeFeedHelper.GetQueryFilterHash(
                new ChangeFeedQuerySpec("SELECT * FROM c WHERE c.state = \"OR\"")));
            Assert.ThrowsException<ArgumentException>(() => incompatible.VerifyQueryFilter(persisted));
            Assert.AreEqual(hash, persisted.QueryFilterHash);
            Assert.AreEqual(lease.Owner, persisted.Owner);
            Assert.AreEqual(lease.ContinuationToken, persisted.ContinuationToken);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task ConcreteLeaseManagersRejectDifferentQueryWithoutReleasingOwner(bool epkLease, bool filtered)
        {
            using CosmosClient client = TestCommon.CreateCosmosClient();
            Container monitored = await this.CreateContainerAsync(client, fullFidelity: false);
            Container leases = await client.GetDatabase(this.database.Id).CreateContainerAsync(
                Guid.NewGuid().ToString(), "/id", cancellationToken: this.cancellationToken);
            ContainerInternal leaseContainer = (ContainerInternal)leases;
            RequestOptionsFactory partitionKeys = new PartitionedByIdCollectionRequestOptionsFactory();
            DocumentServiceLeaseUpdater updater = new DocumentServiceLeaseUpdaterCosmos(leases);
            DocumentServiceLeaseManagerCosmos owner = new DocumentServiceLeaseManagerCosmos(
                (ContainerInternal)monitored, leaseContainer, updater,
                new DocumentServiceLeaseStoreManagerOptions { HostName = "owner", Mode = ChangeFeedMode.LatestVersion }, partitionKeys);
            string hash = filtered
                ? ChangeFeedHelper.GetQueryFilterHash(new ChangeFeedQuerySpec("SELECT * FROM c WHERE c.state = \"WA\""))
                : null;
            owner.ConfigureQueryFilter(hash);
            Func<DocumentServiceLeaseManager, Task<DocumentServiceLease>> create = manager => epkLease
                ? manager.CreateLeaseIfNotExistAsync(new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false)), "saved-progress")
                : manager.CreateLeaseIfNotExistAsync(new Documents.PartitionKeyRange { Id = "0", MinInclusive = "", MaxExclusive = "FF" }, "saved-progress");
            DocumentServiceLease lease = await create(owner);
            Assert.AreEqual(hash, lease.QueryFilterHash);
            await owner.AcquireAsync(lease);
            PartitionKey partitionKey = new PartitionKey(lease.Id);
            lease = (await leases.ReadItemAsync<DocumentServiceLease>(lease.Id, partitionKey, cancellationToken: this.cancellationToken)).Resource;
            string etag = lease.ConcurrencyToken;
            Assert.AreEqual("owner", lease.Owner);
            DocumentServiceLeaseManagerCosmos contender = new DocumentServiceLeaseManagerCosmos(
                (ContainerInternal)monitored, leaseContainer, updater,
                new DocumentServiceLeaseStoreManagerOptions { HostName = "contender", Mode = ChangeFeedMode.LatestVersion }, partitionKeys);
            contender.ConfigureQueryFilter(ChangeFeedHelper.GetQueryFilterHash(
                new ChangeFeedQuerySpec("SELECT * FROM c WHERE c.state = \"OR\"")));

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => create(contender));
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => contender.AcquireAsync(lease));
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => contender.ReleaseAsync(lease));

            DocumentServiceLease unchanged = (await leases.ReadItemAsync<DocumentServiceLease>(
                lease.Id, partitionKey, cancellationToken: this.cancellationToken)).Resource;
            Assert.AreEqual(etag, unchanged.ConcurrencyToken);
            Assert.AreEqual("owner", unchanged.Owner);
            Assert.AreEqual(hash, unchanged.QueryFilterHash);
            Assert.AreEqual("saved-progress", unchanged.ContinuationToken);

            lease = await owner.RenewAsync(unchanged);
            lease.Properties["custom"] = "value";
            lease = await owner.UpdatePropertiesAsync(lease);
            DocumentServiceLeaseCheckpointer checkpointer = new DocumentServiceLeaseCheckpointerCore(updater, partitionKeys);
            lease = await checkpointer.CheckpointAsync(lease, "next-progress");
            await owner.ReleaseAsync(lease);
            DocumentServiceLease released = (await leases.ReadItemAsync<DocumentServiceLease>(
                lease.Id, partitionKey, cancellationToken: this.cancellationToken)).Resource;
            Assert.AreEqual(hash, released.QueryFilterHash);
            Assert.AreEqual("next-progress", released.ContinuationToken);
            Assert.AreEqual("value", released.Properties["custom"]);
            Assert.IsNull(released.Owner);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task ProcessorFiltersCheckpointsAndRestarts(bool stream, bool manualCheckpoint)
        {
            using CosmosClient client = TestCommon.CreateCosmosClient();
            Container monitored = await this.CreateContainerAsync(client, fullFidelity: false);
            Container leases = await client.GetDatabase(this.database.Id).CreateContainerAsync(
                Guid.NewGuid().ToString(), "/id", cancellationToken: this.cancellationToken);
            const string query = "SELECT * FROM c WHERE c.age > 5";
            ConcurrentQueue<string> delivered = new ConcurrentQueue<string>();
            TaskCompletionSource<bool> received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            string expectedId = "first";

            async Task HandleAsync(IEnumerable<JObject> changes, Func<Task> checkpoint)
            {
                foreach (JObject change in changes)
                {
                    delivered.Enqueue((string)change["id"]);
                }

                if (checkpoint != null)
                {
                    await checkpoint();
                }

                if (delivered.Contains(expectedId))
                {
                    received.TrySetResult(true);
                }
            }

            async Task HandleStreamAsync(Stream changes, Func<Task> checkpoint)
            {
                using StreamReader reader = new StreamReader(changes, Encoding.UTF8, true, 1024, leaveOpen: true);
                JObject response = JObject.Parse(await reader.ReadToEndAsync());
                await HandleAsync(response["Documents"].Cast<JObject>(), checkpoint);
            }

            ChangeFeedProcessor CreateProcessor(string sql)
            {
                ChangeFeedProcessorBuilder builder = stream
                    ? manualCheckpoint
                        ? monitored.GetChangeFeedProcessorBuilderWithManualCheckpoint(
                            "filtered", (context, changes, checkpoint, token) => HandleStreamAsync(changes, checkpoint))
                        : monitored.GetChangeFeedProcessorBuilder(
                            "filtered", (context, changes, token) => HandleStreamAsync(changes, null))
                    : manualCheckpoint
                        ? monitored.GetChangeFeedProcessorBuilderWithManualCheckpoint<JObject>(
                            "filtered", (context, changes, checkpoint, token) => HandleAsync(changes, checkpoint))
                        : monitored.GetChangeFeedProcessorBuilder<JObject>(
                            "filtered", (context, changes, token) => HandleAsync(changes, null));
                return builder.WithQuery(sql)
                    .WithLeaseContainer(leases)
                    .WithInstanceName("host")
                    .WithStartFromBeginning()
                    .WithPollInterval(TimeSpan.FromMilliseconds(50))
                    .WithErrorNotification((lease, exception) =>
                    {
                        received.TrySetException(exception);
                        return Task.CompletedTask;
                    })
                    .Build();
            }

            await monitored.CreateItemAsync(new { id = "first", pk = "one", age = 10 });
            await monitored.CreateItemAsync(new { id = "excluded", pk = "one", age = 1 });
            ChangeFeedProcessor processor = CreateProcessor(query);
            await processor.StartAsync();
            try
            {
                await received.Task.WaitAsync(this.cancellationToken);
            }
            finally
            {
                await processor.StopAsync();
            }

            using (FeedIterator<DocumentServiceLease> iterator = leases.GetItemQueryIterator<DocumentServiceLease>(
                "SELECT * FROM c WHERE IS_DEFINED(c.LeaseToken)"))
            {
                List<DocumentServiceLease> saved = new List<DocumentServiceLease>();
                while (iterator.HasMoreResults)
                {
                    saved.AddRange(await iterator.ReadNextAsync(this.cancellationToken));
                }

                Assert.IsTrue(saved.Count > 0);
                Assert.IsTrue(saved.Any(lease => !string.IsNullOrEmpty(lease.ContinuationToken)));
                Assert.IsTrue(saved.All(lease => lease.QueryFilterHash == ChangeFeedHelper.GetQueryFilterHash(new ChangeFeedQuerySpec(query))));
            }

            ChangeFeedProcessor incompatible = CreateProcessor("SELECT * FROM c WHERE c.age < 5");
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => incompatible.StartAsync());
            expectedId = "second";
            received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await monitored.CreateItemAsync(new { id = "second", pk = "one", age = 20 });
            processor = CreateProcessor(query);
            await processor.StartAsync();
            try
            {
                await received.Task.WaitAsync(this.cancellationToken);
            }
            finally
            {
                await processor.StopAsync();
            }

            CollectionAssert.AreEquivalent(new[] { "first", "second" }, delivered.ToArray());
        }

        [DataTestMethod]
        [DataRow("SELECT * FROM c", 2)]
        [DataRow("SELECT * FROM c WHERE c.age = 10", 1)]
        [DataRow("SELECT * FROM c WHERE c.age > 5", 1)]
        [DataRow("SELECT * FROM c WHERE ARRAY_CONTAINS(c.tags, \"selected\")", 1)]
        [DataRow("SELECT * FROM c WHERE IS_DEFINED(c.selected)", 1)]
        [DataRow("SELECT * FROM c WHERE c.age < 0", 0)]
        public async Task SupportedPredicateCategoriesFilterChanges(string query, int expected)
        {
            using CosmosClient client = TestCommon.CreateCosmosClient();
            Container container = await this.CreateContainerAsync(client, fullFidelity: false);
            await container.CreateItemAsync(new { id = "matching", pk = "one", age = 10, tags = new[] { "selected" }, selected = true });
            await container.CreateItemAsync(new { id = "excluded", pk = "one", age = 1, tags = Array.Empty<string>() });
            using FeedIterator<JObject> iterator = container.GetChangeFeedIteratorWithQuery<JObject>(
                ChangeFeedStartFrom.Beginning(), ChangeFeedMode.LatestVersion, query, new ChangeFeedRequestOptions { PageSizeHint = 1 });
            List<string> ids = new List<string>();
            int pages = 0;
            while (iterator.HasMoreResults)
            {
                FeedResponse<JObject> page = await iterator.ReadNextAsync(this.cancellationToken);
                Assert.IsTrue(++pages <= 10);
                ids.AddRange(page.Select(item => (string)item["id"]));
                if (page.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }
            }

            Assert.AreEqual(expected, ids.Count);
            Assert.AreEqual(expected, ids.Distinct().Count());
            if (expected == 1)
            {
                Assert.AreEqual("matching", ids.Single());
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task OrdinaryParameterizedPostQueriesRemainIndependent(bool gateway)
        {
            QueryReadHandler handler = new QueryReadHandler();
            using CosmosClient client = TestCommon.CreateCosmosClient(useGateway: gateway, builder => builder.AddCustomHandlers(handler));
            Container container = await this.CreateContainerAsync(client, fullFidelity: false);
            await container.CreateItemAsync(new { id = "ordinary", pk = "one", age = 10 });
            using FeedIterator<int> iterator = container.GetItemQueryIterator<int>(
                new QueryDefinition("SELECT VALUE c.age FROM c WHERE c.age > @min").WithParameter("@min", 5),
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey("one") });
            List<int> values = new List<int>();
            while (iterator.HasMoreResults)
            {
                values.AddRange(await iterator.ReadNextAsync(this.cancellationToken));
            }

            CollectionAssert.AreEqual(new[] { 10 }, values);
            Assert.IsTrue(handler.SawOrdinaryQuery);
            Assert.IsFalse(handler.SawQuery);
            Assert.IsFalse(handler.SawUnfilteredRead);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task QueryComposesWithEffectivePartitionRanges(bool gateway)
        {
            using CosmosClient client = TestCommon.CreateCosmosClient(useGateway: gateway);
            Container container = await this.CreateContainerAsync(client, fullFidelity: false);
            ContainerProperties properties = await container.ReadContainerAsync(cancellationToken: this.cancellationToken);
            List<string>[] expected = { new List<string>(), new List<string>() };
            List<(string Id, string Epk)> matching = new List<(string Id, string Epk)>();
            for (int i = 0; i < 20; i++)
            {
                string key = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                await container.CreateItemAsync(new { id = key, pk = key, selected = i % 2 == 0 });
                if (i % 2 == 0)
                {
                    string epk = new PartitionKey(key).InternalKey.GetEffectivePartitionKeyString(properties.PartitionKey);
                    matching.Add((key, epk));
                }
            }

            string boundary = matching.OrderBy(item => item.Epk, StringComparer.Ordinal).ElementAt(matching.Count / 2).Epk;
            foreach ((string id, string epk) in matching)
            {
                expected[string.CompareOrdinal(epk, boundary) < 0 ? 0 : 1].Add(id);
            }

            Assert.IsTrue(expected.All(ids => ids.Count > 0), "The fixed data set must exercise both EPK ranges.");
            for (int range = 0; range < 2; range++)
            {
                FeedRange feedRange = new FeedRangeEpk(new Documents.Routing.Range<string>(
                    range == 0 ? "" : boundary, range == 0 ? boundary : "FF", true, false));
                using FeedIterator<JObject> iterator = container.GetChangeFeedIteratorWithQuery<JObject>(
                    ChangeFeedStartFrom.Beginning(feedRange), ChangeFeedMode.LatestVersion,
                    "SELECT * FROM c WHERE c.selected = true", new ChangeFeedRequestOptions { PageSizeHint = 2 });
                List<string> actual = new List<string>();
                int pages = 0;
                while (iterator.HasMoreResults)
                {
                    FeedResponse<JObject> page = await iterator.ReadNextAsync(this.cancellationToken);
                    Assert.IsTrue(++pages <= 30);
                    actual.AddRange(page.Select(item => (string)item["id"]));
                    if (page.StatusCode == HttpStatusCode.NotModified)
                    {
                        break;
                    }
                }

                CollectionAssert.AreEquivalent(expected[range], actual);
            }
        }

        [TestMethod]
        public async Task LargeFilteredResponsesDrainWithoutLoss()
        {
            using CosmosClient client = TestCommon.CreateCosmosClient();
            Container container = await this.CreateContainerAsync(client, fullFidelity: false);
            List<string> expected = new List<string>();
            string payload = new string('x', 200000);
            for (int i = 0; i < 30; i++)
            {
                string id = i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
                expected.Add(id);
                await container.CreateItemAsync(new { id, pk = "one", payload });
            }

            using FeedIterator<JObject> iterator = container.GetChangeFeedIteratorWithQuery<JObject>(
                ChangeFeedStartFrom.Beginning(FeedRange.FromPartitionKey(new PartitionKey("one"))),
                ChangeFeedMode.LatestVersion, "SELECT * FROM c", new ChangeFeedRequestOptions { PageSizeHint = 1000 });
            List<string> actual = new List<string>();
            int successfulPages = 0;
            while (iterator.HasMoreResults)
            {
                FeedResponse<JObject> page = await iterator.ReadNextAsync(this.cancellationToken);
                if (page.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }

                Assert.IsTrue(++successfulPages <= 30);
                Assert.IsFalse(string.IsNullOrEmpty(page.ContinuationToken));
                actual.AddRange(page.Select(item => (string)item["id"]));
            }

            Assert.IsTrue(successfulPages > 1, "The backlog exceeds the response-size limit despite the large page-size hint.");
            CollectionAssert.AreEqual(expected, actual);
        }

        private async Task<(List<string> Ids, string Continuation)> DrainQueryAsync(
            Container container,
            ChangeFeedMode mode,
            string query,
            string continuation)
        {
            List<string> ids = new List<string>();
            using FeedIterator<JObject> iterator = container.GetChangeFeedIteratorWithQuery<JObject>(
                ChangeFeedStartFrom.ContinuationToken(continuation), mode, query, new ChangeFeedRequestOptions { PageSizeHint = 1 });
            for (int page = 0; page < 10; page++)
            {
                FeedResponse<JObject> response = await iterator.ReadNextAsync(this.cancellationToken);
                Assert.IsFalse(string.IsNullOrEmpty(response.ContinuationToken));
                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    return (ids, response.ContinuationToken);
                }

                foreach (JObject item in response)
                {
                    ids.Add((string)(mode == ChangeFeedMode.AllVersionsAndDeletes ? item["current"]["id"] : item["id"]));
                }
            }

            throw new AssertFailedException("The fixed change feed backlog did not drain within ten pages.");
        }

        private async Task<Container> CreateContainerAsync(CosmosClient client, bool fullFidelity)
        {
            ContainerProperties properties = new ContainerProperties(Guid.NewGuid().ToString(), "/pk");
            if (fullFidelity)
            {
                properties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);
            }

            return await client.GetDatabase(this.database.Id).CreateContainerAsync(properties);
        }

        private sealed class QueryReadHandler : RequestHandler
        {
            public bool SawQuery { get; private set; }

            public bool SawUnfilteredRead { get; private set; }

            public bool SawOrdinaryQuery { get; private set; }

            public override Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
            {
                if (request.OperationType == Documents.OperationType.Query)
                {
                    Assert.AreEqual(HttpMethod.Post, request.Method);
                    Assert.IsNull(request.Headers["A-IM"]);
                    this.SawOrdinaryQuery = true;
                }

                if (request.Headers["A-IM"] != null)
                {
                    if (request.Content == null)
                    {
                        Assert.AreEqual(HttpMethod.Get, request.Method);
                        Assert.IsNull(request.Headers["x-ms-documentdb-isquery"]);
                        this.SawUnfilteredRead = true;
                    }
                    else
                    {
                        Assert.AreEqual(HttpMethod.Post, request.Method);
                        Assert.AreEqual(bool.TrueString, request.Headers["x-ms-documentdb-isquery"]);
                        Assert.AreEqual("application/query+json", request.Headers["Content-Type"]);
                        this.SawQuery = true;
                    }
                }

                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
