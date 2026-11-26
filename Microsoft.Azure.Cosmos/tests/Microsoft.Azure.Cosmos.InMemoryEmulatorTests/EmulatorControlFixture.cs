//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.InMemoryEmulatorTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    internal sealed class EmulatorControlFixture : IAsyncDisposable
    {
        internal const string DatabaseId = "complexdb";
        internal const string ContainerId = "items";
        private readonly InMemoryEmulator host;
        private readonly HttpClient management;
        private readonly List<IDisposable> clients = new List<IDisposable>();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        private EmulatorControlFixture(InMemoryEmulator host)
        {
            this.host = host;
            this.management = new HttpClient(new HttpClientHandler { UseProxy = false })
            {
                BaseAddress = host.ManagementEndpoint,
                Timeout = TimeSpan.FromSeconds(10),
            };
        }

        internal CancellationToken CancellationToken => this.cancellation.Token;
        internal IReadOnlyDictionary<string, Uri> Regions { get; private set; }

        internal static async Task<EmulatorControlFixture> StartAsync()
        {
            string executable = Environment.GetEnvironmentVariable(InMemoryEmulator.ExecutableVariable);
            if (string.IsNullOrEmpty(executable))
            {
                Assert.Inconclusive($"Set {InMemoryEmulator.ExecutableVariable} to run the owned in-memory host.");
            }

            InMemoryEmulator host = await InMemoryEmulator.StartAsync(
                executable, Path.Combine(AppContext.BaseDirectory, "emulator-complex-config.json"));
            EmulatorControlFixture fixture = new EmulatorControlFixture(host);
            try
            {
                JObject capabilities = await fixture.SendAsync(HttpMethod.Get, "capabilities");
                foreach (string action in new[] { "partitionSplit", "partitionMerge", "replicationPauseResume" })
                {
                    Assert.IsTrue(capabilities["managementActions"].Values<string>().Contains(action),
                        $"The pinned host must support {action}.");
                }

                JObject account = await fixture.SendAsync(HttpMethod.Get, "account");
                Assert.AreEqual("single", (string)account["writeMode"]);
                Assert.AreEqual("Session", (string)account["consistency"]);
                using JsonDocument document = JsonDocument.Parse(account.ToString());
                Dictionary<string, Uri> regions = new Dictionary<string, Uri>();
                foreach (JsonElement region in document.RootElement.GetProperty("regions").EnumerateArray())
                {
                    regions.Add(region.GetProperty("name").GetString(),
                        InMemoryEmulator.ReadLoopbackEndpoint(region, "gatewayEndpoint"));
                }

                Assert.IsTrue(regions.ContainsKey("East US") && regions.ContainsKey("West US"));
                Assert.AreEqual(2, regions.Count);
                fixture.Regions = regions;
                return fixture;
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
        }

        internal (CosmosClient Client, ObservingHttpHandler Observer) CreateClient(
            Uri endpoint = null, CosmosClientOptions options = null, bool limitToEndpoint = true)
        {
            endpoint ??= this.host.AccountEndpoint;
            Assert.IsTrue(this.Regions.Values.Contains(endpoint), "Clients must stay on owned regional endpoints.");
            ObservingHttpHandler observer = new ObservingHttpHandler();
            HttpClient http = new HttpClient(observer);
            this.clients.Add(http);
            options ??= new CosmosClientOptions();
            options.ConnectionMode = ConnectionMode.Gateway;
            options.LimitToEndpoint = limitToEndpoint;
            options.RequestTimeout = TimeSpan.FromSeconds(10);
            options.HttpClientFactory = () => http;
            CosmosClient client = new CosmosClient(endpoint.ToString(), Convert.ToBase64String(new byte[64]), options);
            this.clients.Add(client);
            return (client, observer);
        }

        internal async Task<JObject> SendAsync(
            HttpMethod method, string path, JObject body = null, HttpStatusCode expected = HttpStatusCode.OK)
        {
            using HttpRequestMessage request = new HttpRequestMessage(method, path);
            if (body != null)
            {
                request.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
            }

            using HttpResponseMessage response = await this.management.SendAsync(request, this.CancellationToken);
            string content = await response.Content.ReadAsStringAsync();
            Assert.AreEqual(expected, response.StatusCode, content);
            return JObject.Parse(content);
        }

        internal Task<JObject> AdvanceAsync(string operationId)
        {
            return this.SendAsync(HttpMethod.Post, $"operations/{Uri.EscapeDataString(operationId)}/advance");
        }

        internal async Task<JObject> WaitForSuccessAsync(string operationId)
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.Elapsed < TimeSpan.FromSeconds(10))
            {
                JObject operation = await this.SendAsync(HttpMethod.Get, $"operations/{Uri.EscapeDataString(operationId)}");
                string status = (string)operation["status"];
                if (status == "Succeeded")
                {
                    Assert.AreEqual("Succeeded", (string)operation["phase"]);
                    return operation;
                }

                Assert.AreEqual("Running", status, operation.ToString());
                await Task.Delay(10, this.CancellationToken);
            }

            throw new TimeoutException($"The owned host operation {operationId} did not complete.");
        }

        internal Task<JObject> PauseWestAsync()
        {
            return this.SendAsync(HttpMethod.Post, "regions/West%20US/replication/pause");
        }

        internal Task<JObject> ResumeWestAsync()
        {
            return this.SendAsync(HttpMethod.Post, "regions/West%20US/replication/resume");
        }

        internal async Task<JArray> ReadTopologyAsync()
        {
            using HttpClient http = new HttpClient(new HttpClientHandler { UseProxy = false });
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get,
                new Uri(this.host.AccountEndpoint, $"dbs/{DatabaseId}/colls/{ContainerId}/pkranges"));
            request.Headers.Add("x-ms-version", "2018-12-31");
            using HttpResponseMessage response = await http.SendAsync(request, this.CancellationToken);
            response.EnsureSuccessStatusCode();
            JObject body = JObject.Parse(await response.Content.ReadAsStringAsync());
            return (JArray)body["PartitionKeyRanges"];
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                this.cancellation.Cancel();
                for (int index = this.clients.Count - 1; index >= 0; index--)
                {
                    this.clients[index].Dispose();
                }
            }
            finally
            {
                try
                {
                    this.management.Dispose();
                    await this.host.DisposeAsync();
                }
                finally
                {
                    this.cancellation.Dispose();
                }
            }
        }
    }

    internal sealed record ObservedRequest(Uri Endpoint, HttpStatusCode Status, int SubStatus, string SessionToken);

    internal sealed class ObservingHttpHandler : DelegatingHandler
    {
        private readonly ConcurrentQueue<ObservedRequest> requests = new ConcurrentQueue<ObservedRequest>();

        internal ObservingHttpHandler() : base(new HttpClientHandler { UseProxy = false })
        {
        }

        internal IReadOnlyList<ObservedRequest> ItemReads(string id)
        {
            return this.requests.Where(request => request.Endpoint.AbsolutePath.EndsWith("/docs/" + id)).ToArray();
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string session = request.Headers.TryGetValues("x-ms-session-token", out IEnumerable<string> values)
                ? values.Single() : null;
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            if (request.Method == HttpMethod.Get)
            {
                int subStatus = response.Headers.TryGetValues("x-ms-substatus", out IEnumerable<string> statuses)
                    ? int.Parse(statuses.Single(), System.Globalization.CultureInfo.InvariantCulture) : 0;
                this.requests.Enqueue(new ObservedRequest(request.RequestUri, response.StatusCode, subStatus, session));
            }

            return response;
        }
    }
}
