namespace Microsoft.Azure.Cosmos.Tests.Tracing
{
    using System;
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
    }
}