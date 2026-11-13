// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using HttpConstants = Microsoft.Azure.Documents.HttpConstants;
    using PartitionKey = Cosmos.PartitionKey;
    using SubStatusCodes = Microsoft.Azure.Documents.SubStatusCodes;
    using WFConstants = Microsoft.Azure.Documents.WFConstants;

    /// <summary>
    /// Disposable live-account probe for DTX request headers. Set COSMOS_DTX_ENDPOINT and
    /// COSMOS_DTX_KEY, then run with --filter FullyQualifiedName~DistributedTransactionHeaderE2ETests.
    /// The cross-region case requires at least two writable regions.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    [TestCategory("DistributedTransaction")]
    public class DistributedTransactionHeaderE2ETests
    {
        private CosmosClient client;
        private HeaderCaptureHandler handler;
        private string databaseId;
        private bool databaseCreated;
        private Container container;

        [TestInitialize]
        public async Task TestInitialize()
        {
            string endpoint = Environment.GetEnvironmentVariable("COSMOS_DTX_ENDPOINT");
            string key = Environment.GetEnvironmentVariable("COSMOS_DTX_KEY");
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
            {
                Assert.Inconclusive("Set COSMOS_DTX_ENDPOINT and COSMOS_DTX_KEY to run the live DTX header tests.");
            }

            this.handler = new HeaderCaptureHandler();
            this.client = new CosmosClient(endpoint, key, new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                ConsistencyLevel = ConsistencyLevel.Session,
                HttpClientFactory = () => new HttpClient(this.handler, disposeHandler: false)
            });

            this.databaseId = "DtxHeaderE2E-" + Guid.NewGuid().ToString("N");
            Database database = (await this.client.CreateDatabaseAsync(this.databaseId)).Database;
            this.databaseCreated = true;
            this.container = (await database.CreateContainerAsync(
                new ContainerProperties("items", "/pk"))).Container;
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            try
            {
                if (this.client != null && this.databaseCreated)
                {
                    await this.client.GetDatabase(this.databaseId).DeleteAsync();
                }
            }
            finally
            {
                this.client?.Dispose();
                this.handler?.Dispose();
            }
        }

        [TestMethod]
        public async Task FirstDispatch_HasBothHeadersFalse()
        {
            using DistributedTransactionResponse response = await this.ExecuteWriteAsync();

            Assert.IsTrue(response.IsSuccessStatusCode, response.StatusCode.ToString());
            AssertDispatches(this.handler.Dispatches, (false, false));
        }

        [TestMethod]
        public async Task TimeoutRetry_SameRegion_HasRetryTrueAndRedirectFalse()
        {
            this.handler.InjectOnce = HttpStatusCode.RequestTimeout;

            using DistributedTransactionResponse response = await this.ExecuteWriteAsync();

            Assert.IsTrue(response.IsSuccessStatusCode, response.StatusCode.ToString());
            AssertDispatches(this.handler.Dispatches, (false, false), (true, false));
            Assert.AreEqual(this.handler.Dispatches[0].Uri.Host, this.handler.Dispatches[1].Uri.Host);
        }

        [TestMethod]
        public async Task WriteForbiddenRetry_OtherRegion_HasBothHeadersTrue()
        {
            AccountProperties account = await this.client.ReadAccountAsync();
            if (account.WritableRegions.Count() < 2)
            {
                Assert.Inconclusive("A cross-region DTX retry requires at least two writable regions.");
            }

            this.handler.InjectOnce = HttpStatusCode.Forbidden;

            using DistributedTransactionResponse response = await this.ExecuteWriteAsync();

            Assert.IsTrue(response.IsSuccessStatusCode, response.StatusCode.ToString());
            AssertDispatches(this.handler.Dispatches, (false, false), (true, true));
            Assert.AreNotEqual(this.handler.Dispatches[0].Uri.Host, this.handler.Dispatches[1].Uri.Host,
                "The second dispatch must actually target a different region.");
        }

        private async Task<DistributedTransactionResponse> ExecuteWriteAsync()
        {
            string firstId = Guid.NewGuid().ToString("N");
            string secondId = Guid.NewGuid().ToString("N");
            return await this.client.CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(firstId), firstId, new { id = firstId, pk = firstId })
                .CreateItem(this.container, new PartitionKey(secondId), secondId, new { id = secondId, pk = secondId })
                .ExecuteTransactionAsync(CancellationToken.None);
        }

        private static void AssertDispatches(
            IReadOnlyList<Dispatch> actual,
            params (bool Retry, bool Redirect)[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Count, "Unexpected number of DTX wire dispatches.");
            Assert.IsTrue(Guid.TryParse(actual[0].Token, out _), "The first dispatch must carry an idempotency token.");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Retry ? bool.TrueString : bool.FalseString, actual[i].Retry,
                    $"Dispatch {i}: {DistributedTransactionConstants.IsDtxRetry}");
                Assert.AreEqual(expected[i].Redirect ? bool.TrueString : bool.FalseString, actual[i].Redirect,
                    $"Dispatch {i}: {DistributedTransactionConstants.IsDtxCrossRegionRedirect}");
                Assert.AreEqual(actual[0].Token, actual[i].Token, $"Dispatch {i} must reuse the idempotency token.");
            }
        }

        private sealed class Dispatch
        {
            public Uri Uri { get; set; }

            public string Token { get; set; }

            public string Retry { get; set; }

            public string Redirect { get; set; }
        }

        private sealed class HeaderCaptureHandler : DelegatingHandler
        {
            public HeaderCaptureHandler()
                : base(new HttpClientHandler())
            {
            }

            public HttpStatusCode? InjectOnce { get; set; }

            public List<Dispatch> Dispatches { get; } = new List<Dispatch>();

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                if (request.Method == HttpMethod.Post
                    && request.RequestUri.AbsolutePath.EndsWith("/operations/dtc", StringComparison.OrdinalIgnoreCase))
                {
                    this.Dispatches.Add(new Dispatch
                    {
                        Uri = request.RequestUri,
                        Token = GetHeader(request, HttpConstants.HttpHeaders.IdempotencyToken),
                        Retry = GetHeader(request, DistributedTransactionConstants.IsDtxRetry),
                        Redirect = GetHeader(request, DistributedTransactionConstants.IsDtxCrossRegionRedirect)
                    });

                    if (this.InjectOnce.HasValue)
                    {
                        HttpStatusCode status = this.InjectOnce.Value;
                        this.InjectOnce = null;
                        HttpResponseMessage failure = new HttpResponseMessage(status) { RequestMessage = request };
                        if (status == HttpStatusCode.Forbidden)
                        {
                            failure.Headers.Add(
                                WFConstants.BackendHeaders.SubStatus,
                                ((int)SubStatusCodes.WriteForbidden).ToString(CultureInfo.InvariantCulture));
                        }

                        return Task.FromResult(failure);
                    }
                }

                return base.SendAsync(request, cancellationToken);
            }

            private static string GetHeader(HttpRequestMessage request, string name)
            {
                return request.Headers.TryGetValues(name, out IEnumerable<string> values)
                    ? values.Single()
                    : null;
            }
        }
    }
}
