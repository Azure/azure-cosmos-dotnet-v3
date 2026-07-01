//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using PartitionKey = Cosmos.PartitionKey;

    /// <summary>
    /// Emulator tests that validate the precedence of <see cref="RequestOptions.IfMatchEtag"/>
    /// versus <see cref="RequestOptions.IfNoneMatchEtag"/> on point reads and point writes.
    ///
    /// The rules being asserted are:
    /// <list type="bullet">
    /// <item>Point READ (GET): only <c>If-None-Match</c> is evaluated; <c>If-Match</c> is ignored.</item>
    /// <item>Point WRITE (Replace): only <c>If-Match</c> is evaluated; <c>If-None-Match</c> is ignored.</item>
    /// </list>
    ///
    /// Each test row expresses the etag supplied for each header as a "kind":
    /// <c>none</c> (header not set), <c>current</c> (the item's live etag),
    /// <c>stale</c> (a real but superseded etag), or <c>star</c> (the "*" wildcard).
    /// </summary>
    [TestClass]
    public class CosmosItemConditionalTests : BaseCosmosClientHelper
    {
        private Container Container = null;

        /// <summary>
        /// Symbolic etag value used by the data rows. Resolved against the seeded
        /// item at run time so the rows stay readable.
        /// </summary>
        private const string None = "none";
        private const string Current = "current";
        private const string Stale = "stale";
        private const string Star = "star";

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit(validateSinglePartitionKeyRangeCacheCall: true);
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/pk"),
                throughput: 15000,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response.Container);
            this.Container = response.Container;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        // ─── Point reads: only If-None-Match is honoured ────────────────────────

        [TestMethod]
        [DataRow(Current, None, HttpStatusCode.OK, DisplayName = "Read 1: IfMatch=current, IfNoneMatch=none -> 200")]
        [DataRow(Stale, None, HttpStatusCode.OK, DisplayName = "Read 2: IfMatch=stale, IfNoneMatch=none -> 200")]
        [DataRow(Star, None, HttpStatusCode.OK, DisplayName = "Read 3: IfMatch=*, IfNoneMatch=none -> 200")]
        [DataRow(None, Current, HttpStatusCode.NotModified, DisplayName = "Read 4: IfMatch=none, IfNoneMatch=current -> 304")]
        [DataRow(None, Stale, HttpStatusCode.OK, DisplayName = "Read 5: IfMatch=none, IfNoneMatch=stale -> 200")]
        [DataRow(None, Star, HttpStatusCode.NotModified, DisplayName = "Read 6: IfMatch=none, IfNoneMatch=* -> 304")]
        [DataRow(Current, Current, HttpStatusCode.NotModified, DisplayName = "Read 7: IfMatch=current, IfNoneMatch=current -> 304")]
        [DataRow(Current, Stale, HttpStatusCode.OK, DisplayName = "Read 8: IfMatch=current, IfNoneMatch=stale -> 200")]
        [DataRow(Current, Star, HttpStatusCode.NotModified, DisplayName = "Read 9: IfMatch=current, IfNoneMatch=* -> 304")]
        [DataRow(Stale, Current, HttpStatusCode.NotModified, DisplayName = "Read 10: IfMatch=stale, IfNoneMatch=current -> 304")]
        [DataRow(Stale, Star, HttpStatusCode.NotModified, DisplayName = "Read 11: IfMatch=stale, IfNoneMatch=* -> 304")]
        [DataRow(Stale, Stale, HttpStatusCode.OK, DisplayName = "Read 12: IfMatch=stale, IfNoneMatch=stale -> 200")]
        public async Task PointRead_IfMatchVsIfNoneMatchPrecedence(
            string ifMatchKind,
            string ifNoneMatchKind,
            HttpStatusCode expectedStatus)
        {
            SeededItem item = await this.SeedItemAsync();

            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                IfMatchEtag = ResolveEtag(ifMatchKind, item),
                IfNoneMatchEtag = ResolveEtag(ifNoneMatchKind, item),
            };

            using ResponseMessage response = await this.Container.ReadItemStreamAsync(
                id: item.Id,
                partitionKey: new PartitionKey(item.Pk),
                requestOptions: requestOptions,
                cancellationToken: this.cancellationToken);

            Assert.AreEqual(
                expectedStatus,
                response.StatusCode,
                $"Read with IfMatch={ifMatchKind}, IfNoneMatch={ifNoneMatchKind} expected {expectedStatus} " +
                $"but got {response.StatusCode}. Reads must honour only IfNoneMatch.");

            if (expectedStatus == HttpStatusCode.OK)
            {
                Assert.IsNotNull(response.Content, "A 200 read should return a body.");
            }
        }

        // ─── Point writes: only If-Match is honoured ────────────────────────────

        [TestMethod]
        [DataRow(Current, None, HttpStatusCode.OK, DisplayName = "Write 1: IfMatch=current, IfNoneMatch=none -> 200")]
        [DataRow(Stale, None, HttpStatusCode.PreconditionFailed, DisplayName = "Write 2: IfMatch=stale, IfNoneMatch=none -> 412")]
        [DataRow(Star, None, HttpStatusCode.OK, DisplayName = "Write 3: IfMatch=*, IfNoneMatch=none -> 200")]
        [DataRow(None, Current, HttpStatusCode.OK, DisplayName = "Write 4: IfMatch=none, IfNoneMatch=current -> 200")]
        [DataRow(None, Stale, HttpStatusCode.OK, DisplayName = "Write 5: IfMatch=none, IfNoneMatch=stale -> 200")]
        [DataRow(None, Star, HttpStatusCode.OK, DisplayName = "Write 6: IfMatch=none, IfNoneMatch=* -> 200")]
        [DataRow(Current, Current, HttpStatusCode.OK, DisplayName = "Write 7: IfMatch=current, IfNoneMatch=current -> 200")]
        [DataRow(Current, Stale, HttpStatusCode.OK, DisplayName = "Write 8: IfMatch=current, IfNoneMatch=stale -> 200")]
        [DataRow(Current, Star, HttpStatusCode.OK, DisplayName = "Write 9: IfMatch=current, IfNoneMatch=* -> 200")]
        [DataRow(Stale, Current, HttpStatusCode.PreconditionFailed, DisplayName = "Write 10: IfMatch=stale, IfNoneMatch=current -> 412")]
        [DataRow(Stale, Star, HttpStatusCode.PreconditionFailed, DisplayName = "Write 11: IfMatch=stale, IfNoneMatch=* -> 412")]
        [DataRow(Stale, Stale, HttpStatusCode.PreconditionFailed, DisplayName = "Write 12: IfMatch=stale, IfNoneMatch=stale -> 412")]
        [DataRow(Star, Current, HttpStatusCode.OK, DisplayName = "Write 13: IfMatch=*, IfNoneMatch=current -> 200")]
        [DataRow(Star, Star, HttpStatusCode.OK, DisplayName = "Write 14: IfMatch=*, IfNoneMatch=* -> 200")]
        [DataRow(Star, Stale, HttpStatusCode.OK, DisplayName = "Write 15: IfMatch=*, IfNoneMatch=stale -> 200")]
        public async Task PointWrite_IfMatchVsIfNoneMatchPrecedence(
            string ifMatchKind,
            string ifNoneMatchKind,
            HttpStatusCode expectedStatus)
        {
            SeededItem item = await this.SeedItemAsync();

            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                IfMatchEtag = ResolveEtag(ifMatchKind, item),
                IfNoneMatchEtag = ResolveEtag(ifNoneMatchKind, item),
            };

            using Stream payload = ToStream(new TestItem { id = item.Id, pk = item.Pk, value = "replaced" });
            using ResponseMessage response = await this.Container.ReplaceItemStreamAsync(
                streamPayload: payload,
                id: item.Id,
                partitionKey: new PartitionKey(item.Pk),
                requestOptions: requestOptions,
                cancellationToken: this.cancellationToken);

            Assert.AreEqual(
                expectedStatus,
                response.StatusCode,
                $"Write with IfMatch={ifMatchKind}, IfNoneMatch={ifNoneMatchKind} expected {expectedStatus} " +
                $"but got {response.StatusCode}. Writes must honour only IfMatch.");
        }

        // ─── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates an item and then replaces it once so that we have both a live
        /// (current) etag and a real, now-superseded (stale) etag to test with.
        /// </summary>
        private async Task<SeededItem> SeedItemAsync()
        {
            string id = Guid.NewGuid().ToString();
            string pk = Guid.NewGuid().ToString();

            ItemResponse<TestItem> create = await this.Container.CreateItemAsync(
                new TestItem { id = id, pk = pk, value = "v1" },
                new PartitionKey(pk),
                cancellationToken: this.cancellationToken);
            string staleEtag = create.ETag;

            ItemResponse<TestItem> replace = await this.Container.ReplaceItemAsync(
                new TestItem { id = id, pk = pk, value = "v2" },
                id,
                new PartitionKey(pk),
                cancellationToken: this.cancellationToken);
            string currentEtag = replace.ETag;

            Assert.AreNotEqual(staleEtag, currentEtag, "Replace should have produced a new etag.");

            return new SeededItem
            {
                Id = id,
                Pk = pk,
                CurrentEtag = currentEtag,
                StaleEtag = staleEtag,
            };
        }

        private static string ResolveEtag(string kind, SeededItem item)
        {
            return kind switch
            {
                None => null,
                Current => item.CurrentEtag,
                Stale => item.StaleEtag,
                Star => "*",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown etag kind."),
            };
        }

        private static Stream ToStream(TestItem item)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(item)));
        }

        private sealed class SeededItem
        {
            public string Id { get; set; }

            public string Pk { get; set; }

            public string CurrentEtag { get; set; }

            public string StaleEtag { get; set; }
        }

        private sealed class TestItem
        {
            public string id { get; set; }

            public string pk { get; set; }

            public string value { get; set; }
        }
    }
}
