namespace Microsoft.Azure.Cosmos.Tests.Tracing
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class ClientSideRequestStatisticsTraceDatumTests
    {
        private static readonly HttpResponseMessage response = new HttpResponseMessage();
        private static readonly HttpRequestMessage request = new HttpRequestMessage();
        private static readonly Uri uri = new Uri("http://someUri1.com");
        private static readonly DocumentServiceRequest requestDsr = DocumentServiceRequest.Create(OperationType.Read, resourceType: ResourceType.Document, authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);
        private static readonly StoreResult storeResult = StoreResult.CreateForTesting(storeResponse: new StoreResponse()).Target;

        /// <summary>
        /// This test is needed because different parts of the SDK use the same ClientSideRequestStatisticsTraceDatum across multiple
        /// threads. It's even possible that there are background threads referencing the same instance.
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task ConcurrentUpdateEndpointToAddressResolutionStatisticsTests()
        {
            await this.ConcurrentUpdateTestHelper<KeyValuePair<string, ClientSideRequestStatisticsTraceDatum.AddressResolutionStatistics>>(
                (clientSideRequestStatistics, cancellationToken) => this.UpdateAddressesInBackground(clientSideRequestStatistics, cancellationToken),
                (clientSideRequestStatistics) => clientSideRequestStatistics.EndpointToAddressResolutionStatistics);
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task ConcurrentUpdateHttpResponseStatisticsListTests()
        {
            await this.ConcurrentUpdateTestHelper<ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics>(
                (clientSideRequestStatistics, cancellationToken) => this.UpdateHttpResponsesInBackground(clientSideRequestStatistics, cancellationToken),
                (clientSideRequestStatistics) => clientSideRequestStatistics.HttpResponseStatisticsList);
        }

        [TestMethod]
        public void DuplicateContactedReplicasTests()
        {
            ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, Trace.GetRootTrace(nameof(DuplicateContactedReplicasTests)));
            clientSideRequestStatisticsTraceDatum.ContactedReplicas.Add(new TransportAddressUri(new Uri("http://storephysicaladdress1.com")));
            clientSideRequestStatisticsTraceDatum.ContactedReplicas.Add(new TransportAddressUri(new Uri("http://storephysicaladdress2.com")));
            clientSideRequestStatisticsTraceDatum.ContactedReplicas.Add(new TransportAddressUri(new Uri("http://storephysicaladdress2.com")));
            clientSideRequestStatisticsTraceDatum.ContactedReplicas.Add(new TransportAddressUri(new Uri("http://storephysicaladdress2.com")));
            clientSideRequestStatisticsTraceDatum.ContactedReplicas.Add(new TransportAddressUri(new Uri("http://storephysicaladdress2.com")));
            clientSideRequestStatisticsTraceDatum.ContactedReplicas.Add(new TransportAddressUri(new Uri("http://storephysicaladdress3.com")));
            ITrace trace = Trace.GetRootTrace("test");
            trace.AddDatum("stats", clientSideRequestStatisticsTraceDatum);
            string json = new CosmosTraceDiagnostics(trace).ToString();
            JObject jobject = JObject.Parse(json);
            JToken contactedReplicas = jobject["data"]["stats"]["ContactedReplicas"];
            Assert.AreEqual(3, contactedReplicas.Count());
            int count = contactedReplicas[0]["Count"].Value<int>();
            Assert.AreEqual(1, count);
            string uri = contactedReplicas[0]["Uri"].Value<string>();
            Assert.AreEqual("http://storephysicaladdress1.com/", uri);

            count = contactedReplicas[1]["Count"].Value<int>();
            Assert.AreEqual(4, count);
            uri = contactedReplicas[1]["Uri"].Value<string>();
            Assert.AreEqual("http://storephysicaladdress2.com/", uri);

            count = contactedReplicas[2]["Count"].Value<int>();
            Assert.AreEqual(1, count);
            uri = contactedReplicas[2]["Uri"].Value<string>();
            Assert.AreEqual("http://storephysicaladdress3.com/", uri);
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task ConcurrentUpdateStoreResponseStatisticsListTests()
        {
            await this.ConcurrentUpdateTestHelper<ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics>(
                (clientSideRequestStatistics, cancellationToken) => this.UpdateStoreResponseStatisticsListInBackground(clientSideRequestStatistics, cancellationToken),
                (clientSideRequestStatistics) => clientSideRequestStatistics.StoreResponseStatisticsList);
        }

        [TestMethod]
        public void VerifyIClientSideRequestStatisticsNullTests()
        {
            IClientSideRequestStatistics clientSideRequestStatistics = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, Trace.GetRootTrace(nameof(VerifyIClientSideRequestStatisticsNullTests)));
            Assert.IsNotNull(clientSideRequestStatistics.ContactedReplicas);
            Assert.IsNotNull(clientSideRequestStatistics.FailedReplicas);
            Assert.IsNotNull(clientSideRequestStatistics.RegionsContacted);
        }

        /// <summary>
        /// Reproduces the cross-region read hedging race that caused
        /// `InvalidOperationException: Collection was modified; enumeration operation may
        /// not execute.` in `TraceWriter.TraceJsonWriter.Visit(ClientSideRequestStatisticsTraceDatum)`.
        /// `ContactedReplicas`, `FailedReplicas`, and `RegionsContacted` are exposed as
        /// raw `List`/`HashSet`; the Direct-package store-reader paths mutate them while
        /// the diagnostics tree is serialized for OTel logging on another thread.
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task ConcurrentSerializationOfMutableCollectionsDoesNotThrow()
        {
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(
                DateTime.UtcNow,
                Trace.GetRootTrace(nameof(ConcurrentSerializationOfMutableCollectionsDoesNotThrow)));

            ITrace trace = Trace.GetRootTrace("test");
            trace.AddDatum("stats", datum);

            Task mutationTask = Task.Run(() =>
            {
                int i = 0;
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    TransportAddressUri replica = new TransportAddressUri(new Uri($"rntbd://host{i % 32}/replica/{i}"));
                    datum.ContactedReplicas.Add(replica);
                    datum.FailedReplicas.Add(replica);
                    datum.RegionsContacted.Add(($"region-{i % 8}", new Uri($"https://region{i % 8}.documents.azure.com")));

                    // Periodically trim to keep memory bounded while still racing.
                    if (i % 2048 == 0)
                    {
                        datum.ContactedReplicas.Clear();
                        datum.FailedReplicas.Clear();
                        datum.RegionsContacted.Clear();
                    }

                    i++;
                }
            });

            // Serialize the diagnostics tree repeatedly while the background task mutates
            // the underlying collections. Before the fix this throws within ~100 iterations.
            try
            {
                for (int i = 0; i < 500; i++)
                {
                    string json = new CosmosTraceDiagnostics(trace).ToString();
                    Assert.IsNotNull(json);
                }
            }
            finally
            {
                cancellationTokenSource.Cancel();
            }

            await mutationTask;
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task ConcurrentTraceSummaryAggregationDoesNotThrow()
        {
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(
                DateTime.UtcNow,
                Trace.GetRootTrace(nameof(ConcurrentTraceSummaryAggregationDoesNotThrow)));

            Task mutationTask = Task.Run(() =>
            {
                int i = 0;
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    datum.RegionsContacted.Add(($"region-{i % 8}", new Uri($"https://region{i % 8}.documents.azure.com")));
                    if (i % 1024 == 0)
                    {
                        datum.RegionsContacted.Clear();
                    }
                    i++;
                }
            });

            try
            {
                TraceSummary summary = new TraceSummary();
                for (int i = 0; i < 500; i++)
                {
                    summary.UpdateRegionContacted(datum);
                }
            }
            finally
            {
                cancellationTokenSource.Cancel();
            }

            await mutationTask;
        }

        [TestMethod]
        public void SnapshotListReturnsEmptyForNull()
        {
            IReadOnlyList<int> snapshot = ConcurrentCollectionSnapshot.SnapshotList<int>(null);

            Assert.IsNotNull(snapshot);
            Assert.AreEqual(0, snapshot.Count);
        }

        [TestMethod]
        public void SnapshotListCopiesAllElementsInOrder()
        {
            List<string> source = new List<string> { "a", "b", "c" };

            IReadOnlyList<string> snapshot = ConcurrentCollectionSnapshot.SnapshotList(source);

            CollectionAssert.AreEqual(source, snapshot.ToList());
            // The snapshot must be a defensive copy, not the same reference.
            Assert.AreNotSame(source, snapshot);
        }

        [TestMethod]
        public void SnapshotListRetriesAndSucceedsOnTransientConcurrencyException()
        {
            FlakyReadOnlyList<int> source = new FlakyReadOnlyList<int>(new[] { 1, 2, 3 }, throwAttempts: 2);

            IReadOnlyList<int> snapshot = ConcurrentCollectionSnapshot.SnapshotList(source);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, snapshot.ToList());
            Assert.AreEqual(3, source.Attempts, "Expected two failed attempts followed by a successful one.");
        }

        [TestMethod]
        public void SnapshotListReturnsEmptyAfterMaxAttempts()
        {
            FlakyReadOnlyList<int> source = new FlakyReadOnlyList<int>(new[] { 1, 2, 3 }, throwAttempts: int.MaxValue);

            IReadOnlyList<int> snapshot = ConcurrentCollectionSnapshot.SnapshotList(source);

            Assert.AreEqual(0, snapshot.Count);
            Assert.AreEqual(5, source.Attempts, "Expected the helper to give up after the max retry count.");
        }

        [TestMethod]
        public void SnapshotCollectionReturnsEmptyForNull()
        {
            IReadOnlyList<int> snapshot = ConcurrentCollectionSnapshot.SnapshotCollection<int>(null);

            Assert.IsNotNull(snapshot);
            Assert.AreEqual(0, snapshot.Count);
        }

        [TestMethod]
        public void SnapshotCollectionCopiesHashSetElements()
        {
            HashSet<int> source = new HashSet<int> { 1, 2, 3 };

            IReadOnlyList<int> snapshot = ConcurrentCollectionSnapshot.SnapshotCollection(source);

            CollectionAssert.AreEquivalent(source.ToList(), snapshot.ToList());
        }

        [TestMethod]
        public void SnapshotCollectionUsesReadOnlyListFastPath()
        {
            // A List<T> is IReadOnlyList<T>; SnapshotCollection should route to SnapshotList
            // (index copy) and still return every element in order.
            List<string> source = new List<string> { "x", "y", "z" };

            IReadOnlyList<string> snapshot = ConcurrentCollectionSnapshot.SnapshotCollection((IEnumerable<string>)source);

            CollectionAssert.AreEqual(source, snapshot.ToList());
            Assert.AreNotSame(source, snapshot);
        }

        [TestMethod]
        public void SnapshotCollectionRetriesAndSucceedsOnTransientConcurrencyException()
        {
            FlakyEnumerable<int> source = new FlakyEnumerable<int>(new[] { 1, 2, 3 }, throwAttempts: 2);

            IReadOnlyList<int> snapshot = ConcurrentCollectionSnapshot.SnapshotCollection(source);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, snapshot.ToList());
            Assert.AreEqual(3, source.Attempts, "Expected two failed attempts followed by a successful one.");
        }

        [TestMethod]
        public void SnapshotCollectionReturnsEmptyAfterMaxAttempts()
        {
            FlakyEnumerable<int> source = new FlakyEnumerable<int>(new[] { 1, 2, 3 }, throwAttempts: int.MaxValue);

            IReadOnlyList<int> snapshot = ConcurrentCollectionSnapshot.SnapshotCollection(source);

            Assert.AreEqual(0, snapshot.Count);
            Assert.AreEqual(5, source.Attempts, "Expected the helper to give up after the max retry count.");
        }

        private async Task ConcurrentUpdateTestHelper<T>(
            Action<ClientSideRequestStatisticsTraceDatum, CancellationToken> backgroundUpdater,
            Func<ClientSideRequestStatisticsTraceDatum, IEnumerable<T>> getList)
        {
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, Trace.GetRootTrace(nameof(ConcurrentUpdateTestHelper)));

            Task backgroundTask = Task.Run(() => backgroundUpdater(datum, cancellationTokenSource.Token));

            // Wait for the background thread to start
            for (int i = 0; i < 100; i++)
            {
                if (getList(datum).Any())
                {
                    break;
                }

                if (backgroundTask.Exception != null || backgroundTask.IsCompleted || backgroundTask.IsFaulted || backgroundTask.IsCanceled)
                {
                    Assert.Fail($"BackgroundTask stopped running. {backgroundTask.Exception}");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }

            Assert.IsTrue(getList(datum).Any(), $"BackgroundTask never started running.");

            foreach (T item in getList(datum))
            {
                Assert.IsNotNull(item);
            }

            int count = getList(datum).Count();
            using (IEnumerator<T> enumerator = getList(datum).GetEnumerator())
            {
                // Wait for the background thread to start
                for (int i = 0; i < 100; i++)
                {
                    // IEnumerator should not block items being added to the list
                    if (getList(datum).Count() != count)
                    {
                        break;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(50));
                }

                Assert.IsTrue(getList(datum).Count() > count, "Background task never updated the list.");
            }

            cancellationTokenSource.Cancel();
        }

        private void UpdateAddressesInBackground(
            ClientSideRequestStatisticsTraceDatum datum,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string key = datum.RecordAddressResolutionStart(ClientSideRequestStatisticsTraceDatumTests.uri);
                datum.RecordAddressResolutionEnd(key);
            }
        }

        private void UpdateHttpResponsesInBackground(
            ClientSideRequestStatisticsTraceDatum datum,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                datum.RecordHttpResponse(
                    ClientSideRequestStatisticsTraceDatumTests.request,
                    ClientSideRequestStatisticsTraceDatumTests.response,
                    Documents.ResourceType.Document,
                    DateTime.UtcNow - TimeSpan.FromSeconds(5));
            }
        }

        private void UpdateStoreResponseStatisticsListInBackground(
            ClientSideRequestStatisticsTraceDatum datum,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                datum.RecordRequest(ClientSideRequestStatisticsTraceDatumTests.requestDsr);
                datum.RecordResponse(
                    ClientSideRequestStatisticsTraceDatumTests.requestDsr,
                    ClientSideRequestStatisticsTraceDatumTests.storeResult,
                    DateTime.MinValue,
                    DateTime.MaxValue);
            }
        }

        /// <summary>
        /// An <see cref="IReadOnlyList{T}"/> whose indexer throws a transient concurrency
        /// exception on the first <paramref name="throwAttempts"/> copy passes, then succeeds.
        /// Used to exercise the retry path of <see cref="ConcurrentCollectionSnapshot.SnapshotList{T}"/>.
        /// </summary>
        private sealed class FlakyReadOnlyList<T> : IReadOnlyList<T>
        {
            private readonly IReadOnlyList<T> items;
            private readonly int throwAttempts;

            public FlakyReadOnlyList(IReadOnlyList<T> items, int throwAttempts)
            {
                this.items = items;
                this.throwAttempts = throwAttempts;
            }

            public int Attempts { get; private set; }

            public int Count
            {
                get
                {
                    // Read once at the start of each copy pass.
                    this.Attempts++;
                    return this.items.Count;
                }
            }

            public T this[int index]
            {
                get
                {
                    if (this.Attempts <= this.throwAttempts)
                    {
                        throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                    }

                    return this.items[index];
                }
            }

            public IEnumerator<T> GetEnumerator() => this.items.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => this.items.GetEnumerator();
        }

        /// <summary>
        /// An <see cref="IEnumerable{T}"/> (not an <see cref="IReadOnlyList{T}"/> or
        /// <see cref="ICollection{T}"/>) whose enumeration throws a transient concurrency
        /// exception on the first <paramref name="throwAttempts"/> passes, then succeeds.
        /// Used to exercise the retry path of <see cref="ConcurrentCollectionSnapshot.SnapshotCollection{T}"/>.
        /// </summary>
        private sealed class FlakyEnumerable<T> : IEnumerable<T>
        {
            private readonly IReadOnlyList<T> items;
            private readonly int throwAttempts;

            public FlakyEnumerable(IReadOnlyList<T> items, int throwAttempts)
            {
                this.items = items;
                this.throwAttempts = throwAttempts;
            }

            public int Attempts { get; private set; }

            public IEnumerator<T> GetEnumerator()
            {
                int attempt = ++this.Attempts;
                foreach (T item in this.items)
                {
                    if (attempt <= this.throwAttempts)
                    {
                        throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                    }

                    yield return item;
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
    }
}