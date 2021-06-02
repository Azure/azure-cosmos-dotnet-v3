namespace Microsoft.Azure.Cosmos.Tests.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ClientSideRequestStatisticsTraceDatumTests
    {
        private static readonly HttpResponseMessage response = new HttpResponseMessage();
        private static readonly HttpRequestMessage request = new HttpRequestMessage();
        private static readonly Uri uri = new Uri("http://someUri1.com");
        private static readonly DocumentServiceRequest requestDsr = DocumentServiceRequest.Create(OperationType.Read, resourceType: ResourceType.Document, authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);
        private static readonly StoreResult storeResult = new Documents.StoreResult(
            storeResponse: new StoreResponse(),
            exception: null,
            partitionKeyRangeId: 42.ToString(),
            lsn: 1337,
            quorumAckedLsn: 23,
            requestCharge: 3.14,
            currentReplicaSetSize: 4,
            currentWriteQuorum: 3,
            isValid: true,
            storePhysicalAddress: new Uri("http://storephysicaladdress.com"),
            globalCommittedLSN: 1234,
            numberOfReadRegions: 13,
            itemLSN: 15,
            sessionToken: new SimpleSessionToken(42),
            usingLocalLSN: true,
            activityId: Guid.Empty.ToString(),
            backendRequestDurationInMs: "4.2",
            transportRequestStats: TraceWriterBaselineTests.CreateTransportRequestStats());

        /// <summary>
        /// This test is needed because different parts of the SDK use the same ClientSideRequestStatisticsTraceDatum across multiple
        /// threads. It's even possible that there are background threads referencing the same instance.
        /// </summary>
        [TestMethod]
        [Timeout(10000)]
        public async Task ConcurrentUpdateEndpointToAddressResolutionStatisticsTests()
        {
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow);

            Task backgroundTask = Task.Run(() => this.UpdateAddressesInBackground(datum, cancellationTokenSource.Token));

            // Wait for the background thread to start
            while (!datum.EndpointToAddressResolutionStatistics.Any())
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }

            foreach(KeyValuePair<string, ClientSideRequestStatisticsTraceDatum.AddressResolutionStatistics> address in datum.EndpointToAddressResolutionStatistics)
            {
                Assert.IsNotNull(address);
            }

            cancellationTokenSource.Cancel();
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task ConcurrentUpdateHttpResponseStatisticsListTests()
        {
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow);

            Task backgroundTask = Task.Run(() => this.UpdateHttpResponsesInBackground(datum, cancellationTokenSource.Token));

            // Wait for the background thread to start
            while (!datum.HttpResponseStatisticsList.Any())
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }

            foreach (ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics httpResponseStatistics in datum.HttpResponseStatisticsList)
            {
                Assert.IsNotNull(httpResponseStatistics);
            }

            cancellationTokenSource.Cancel();
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task ConcurrentUpdateStoreResponseStatisticsListTests()
        {
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow);

            Task backgroundTask = Task.Run(() => this.UpdateHttpResponsesInBackground(datum, cancellationTokenSource.Token));

            // Wait for the background thread to start
            while (!datum.HttpResponseStatisticsList.Any())
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }

            foreach (ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics storeResponseStatistics in datum.StoreResponseStatisticsList)
            {
                Assert.IsNotNull(storeResponseStatistics);
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
                    ClientSideRequestStatisticsTraceDatumTests.storeResult);
            }
        }
    }
}
