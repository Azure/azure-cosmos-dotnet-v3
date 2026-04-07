//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    ///   Gateway Mode:
    ///     Operation                       Revocation
    ///     ------------------------------------------
    ///     Data-plane (doc CRUD, queries)   YES
    ///     Container read/create/delete     YES
    ///     Database read/create/delete      YES
    ///     Account read (client init)       YES
    ///     Collection metadata cache        YES
    ///     Partition key ranges             YES
    ///
    ///   Direct Mode:
    ///     Operation                       Revocation
    ///     ------------------------------------------
    ///     Data-plane (doc CRUD, queries)   NO
    ///     Container read/create/delete     YES
    ///     Database read/create/delete      YES
    ///     Account read (client init)       YES
    ///     Address resolution               YES
    ///     Collection metadata cache        YES
    ///     Partition key ranges             YES
    ///
    ///   ThinClient Mode:
    ///     Operation                        Revocation
    ///     -------------------------------- ----------
    ///     Data-plane (doc CRUD, queries)    NO
    ///     Container read/create/delete      YES
    ///     Database read/create/delete       YES
    ///     Account read (client init)        YES
    ///     Address resolution                YES
    ///     Collection metadata cache         YES
    ///     Partition key ranges              YES
    /// </summary>
    [TestClass]
    public class CosmosAadTokenRevocationE2ETests
    {
        private CosmosClient cosmosClient;
        private Cosmos.Database database;
        private Container container;

        private static readonly string DatabaseId = string.Concat("RevocationTestDb_", Guid.NewGuid().ToString("N").AsSpan(0, 8));
        private static readonly string ContainerId = "RevocationTestContainer";

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient();
            this.database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseId);
            this.container = await this.database.CreateContainerIfNotExistsAsync(ContainerId, "/id");
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.database != null)
            {
                await this.database.DeleteStreamAsync();
            }

            this.cosmosClient?.Dispose();
        }

        /// <summary>
        /// Gateway data-plane: CreateItemAsync goes through GatewayStoreModel → GatewayStoreClient (HTTP).
        /// Handler fakes 401 on first POST /docs → SDK extracts claims → retries with fresh token → 201.
        /// </summary>
        [TestMethod]
        public async Task Revocation_Gateway_DataPlane_ShouldRetryWithFreshToken()
        {
            await this.RunRevocationRetryTest(
                connectionMode: ConnectionMode.Gateway,
                targetPathContains: "/docs",
                targetMethod: HttpMethod.Post,
                executeOperation: async (c) =>
                {
                    ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                    ItemResponse<ToDoActivity> r = await c.CreateItemAsync(item, new Cosmos.PartitionKey(item.id));
                    return r.StatusCode;
                },
                expectedStatusCode: HttpStatusCode.Created);
        }

        /// <summary>
        /// Gateway mode: ReadContainerAsync (GET /colls/) is a container metadata read.
        /// Always routes through GatewayStoreModel, even in direct mode.
        /// Handler fakes 401 on first GET /colls/ → SDK extracts claims → retries → 200.
        /// </summary>
        [TestMethod]
        public async Task Revocation_Gateway_ContainerRead_ShouldRetryWithFreshToken()
        {
            await this.RunRevocationRetryTest(
                connectionMode: ConnectionMode.Gateway,
                targetPathContains: "/colls/",
                targetMethod: HttpMethod.Get,
                executeOperation: async (c) =>
                {
                    ContainerResponse r = await c.ReadContainerAsync();
                    return r.StatusCode;
                },
                expectedStatusCode: HttpStatusCode.OK);
        }

        /// <summary>
        /// Account read (GET /) during client init — outside the handler pipeline.
        /// GatewayAccountReader has its own catch-when revocation retry.
        /// If init succeeds, revocation retry worked.
        /// </summary>
        [TestMethod]
        public async Task Revocation_AccountRead_ShouldRetryWithFreshToken()
        {
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            int tokenCallCount = 0;
            List<string> claimsList = new List<string>();

            LocalEmulatorTokenCredential credential = new LocalEmulatorTokenCredential(
                expectedScope: "https://127.0.0.1/.default",
                masterKey: authKey,
                getTokenCallback: (ctx, ct) => { tokenCallCount++; claimsList.Add(ctx.Claims); });

            RevocationSimulatingHandler handler = new RevocationSimulatingHandler(
                new HttpClientHandler(),
                targetPathEquals: "/",
                targetMethod: HttpMethod.Get);

            using CosmosClient aadClient = new CosmosClient(endpoint, credential,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    HttpClientFactory = () => new HttpClient(handler),
                });

            // Init succeeded — account read revocation retry worked
            Container c = aadClient.GetContainer(DatabaseId, ContainerId);
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await c.CreateItemAsync(item, new Cosmos.PartitionKey(item.id));

            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.IsTrue(handler.SimulatedRevocationCount >= 1, "Should have simulated 401 on GET /.");
            Assert.IsTrue(claimsList.Any(c2 => !string.IsNullOrEmpty(c2) && c2.Contains("nbf")),
                "Retry token must include nbf claims.");
        }

        /// <summary>
        /// Direct mode: container metadata read (GET /colls/) goes through gateway even in direct mode.
        /// Handler fakes 401 → SDK retries with fresh token → 200.
        /// </summary>
        [TestMethod]
        public async Task Revocation_Direct_ContainerRead_ShouldRetryWithFreshToken()
        {
            await this.RunRevocationRetryTest(
                connectionMode: ConnectionMode.Direct,
                targetPathContains: "/colls/",
                targetMethod: HttpMethod.Get,
                executeOperation: async (c) =>
                {
                    ContainerResponse r = await c.ReadContainerAsync();
                    return r.StatusCode;
                },
                expectedStatusCode: HttpStatusCode.OK);
        }

        /// <summary>
        /// Direct mode data-plane: CreateItemAsync goes via RNTBD directly to replicas.
        /// The HTTP handler never sees the request, so no 401 is simulated.
        /// This test confirms that direct data-plane is NOT subject to gateway revocation.
        /// </summary>
        [TestMethod]
        public async Task Revocation_Direct_DataPlane_NotSubjectToGatewayRevocation()
        {
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();

            LocalEmulatorTokenCredential credential = new LocalEmulatorTokenCredential(
                expectedScope: "https://127.0.0.1/.default",
                masterKey: authKey);

            RevocationSimulatingHandler handler = new RevocationSimulatingHandler(
                new HttpClientHandler(),
                targetPathContains: "/docs",
                targetMethod: HttpMethod.Post);

            using CosmosClient aadClient = new CosmosClient(endpoint, credential,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct,
                    ConnectionProtocol = Protocol.Tcp,
                    HttpClientFactory = () => new HttpClient(handler),
                });

            Container c = aadClient.GetContainer(DatabaseId, ContainerId);
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await c.CreateItemAsync(item, new Cosmos.PartitionKey(item.id));

            // Request succeeds because RNTBD bypasses the HTTP handler entirely
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.AreEqual(0, handler.SimulatedRevocationCount,
                "Direct data-plane should NOT hit the HTTP handler — RNTBD bypasses the gateway.");
        }

        /// <summary>
        /// Retry exhaustion: handler always returns 401 on every doc POST.
        /// SDK retries once with fresh token (max retry = 1), then gives up.
        /// Verifies no infinite retry loops.
        /// </summary>
        [TestMethod]
        [DataRow(ConnectionMode.Gateway)]
        public async Task Revocation_RetryExhausted_ShouldFailAfterOneRetry(ConnectionMode connectionMode)
        {
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            int tokenCallCount = 0;
            List<string> claimsList = new List<string>();

            LocalEmulatorTokenCredential credential = new LocalEmulatorTokenCredential(
                expectedScope: "https://127.0.0.1/.default",
                masterKey: authKey,
                getTokenCallback: (ctx, ct) => { tokenCallCount++; claimsList.Add(ctx.Claims); });

            AlwaysRevokingHandler handler = new AlwaysRevokingHandler(new HttpClientHandler());

            using CosmosClient aadClient = new CosmosClient(endpoint, credential,
                new CosmosClientOptions
                {
                    ConnectionMode = connectionMode,
                    ConnectionProtocol = connectionMode == ConnectionMode.Direct ? Protocol.Tcp : Protocol.Https,
                    HttpClientFactory = () => new HttpClient(handler),
                });

            Container c = aadClient.GetContainer(DatabaseId, ContainerId);
            int tokenCallsAfterInit = tokenCallCount;

            try
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                await c.CreateItemAsync(item, new Cosmos.PartitionKey(item.id));
                Assert.Fail("Should have thrown after retry exhaustion.");
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.Unauthorized, ex.StatusCode);
                Assert.AreEqual(2, handler.SimulatedRevocationCount,
                    "Exactly 2 simulated 401s: original + one retry, no more.");
                Assert.IsTrue(claimsList.Skip(tokenCallsAfterInit)
                    .Any(c2 => !string.IsNullOrEmpty(c2) && c2.Contains("nbf")),
                    "Should have requested fresh token with claims before giving up.");
            }
        }

        private async Task RunRevocationRetryTest(
            ConnectionMode connectionMode,
            string targetPathContains,
            HttpMethod targetMethod,
            Func<Container, Task<HttpStatusCode>> executeOperation,
            HttpStatusCode expectedStatusCode)
        {
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            int tokenCallCount = 0;
            List<string> claimsList = new List<string>();

            LocalEmulatorTokenCredential credential = new LocalEmulatorTokenCredential(
                expectedScope: "https://127.0.0.1/.default",
                masterKey: authKey,
                getTokenCallback: (ctx, ct) => { tokenCallCount++; claimsList.Add(ctx.Claims); });

            RevocationSimulatingHandler handler = new RevocationSimulatingHandler(
                new HttpClientHandler(),
                targetPathContains: targetPathContains,
                targetMethod: targetMethod);

            using CosmosClient aadClient = new CosmosClient(endpoint, credential,
                new CosmosClientOptions
                {
                    ConnectionMode = connectionMode,
                    ConnectionProtocol = connectionMode == ConnectionMode.Direct ? Protocol.Tcp : Protocol.Https,
                    HttpClientFactory = () => new HttpClient(handler),
                });

            Container c = aadClient.GetContainer(DatabaseId, ContainerId);
            int tokenCallsAfterInit = tokenCallCount;

            HttpStatusCode actualStatusCode = await executeOperation(c);
            int retryTokenCalls = tokenCallCount - tokenCallsAfterInit;

            Console.WriteLine($"  StatusCode: {actualStatusCode}");
            Console.WriteLine($"  Simulated 401s: {handler.SimulatedRevocationCount}");
            Console.WriteLine($"  Token calls during test: {retryTokenCalls}");
            for (int i = 0; i < handler.RequestLog.Count; i++)
            {
                (string method, string path, bool simulated) = handler.RequestLog[i];
                Console.WriteLine($"  Request[{i}]: {method} {path} {(simulated ? "→ SIMULATED 401" : "→ passthrough")}");
            }

            Assert.AreEqual(expectedStatusCode, actualStatusCode, "Request should succeed after revocation retry.");
            Assert.IsTrue(handler.SimulatedRevocationCount >= 1,
                "Handler should have intercepted and returned a fake 401/5013 with WWW-Authenticate.");
            Assert.IsTrue(retryTokenCalls >= 1,
                "SDK should have reset token cache and called credential again for a fresh token.");
            Assert.IsTrue(claimsList.Skip(tokenCallsAfterInit)
                .Any(c2 => !string.IsNullOrEmpty(c2) && c2.Contains("nbf")),
                "Fresh token request should contain merged claims: nbf (from server challenge) + xms_cc (SDK cp1).");
        }

        private class RevocationSimulatingHandler : DelegatingHandler
        {
            private readonly string targetPathContains;
            private readonly string targetPathEquals;
            private readonly HttpMethod targetMethod;
            private bool hasSimulated401;

            public int SimulatedRevocationCount { get; private set; }
            public List<(string method, string path, bool simulated)> RequestLog { get; }
                = new List<(string, string, bool)>();

            public RevocationSimulatingHandler(
                HttpMessageHandler innerHandler,
                HttpMethod targetMethod,
                string targetPathContains = null,
                string targetPathEquals = null)
                : base(innerHandler)
            {
                this.targetMethod = targetMethod;
                this.targetPathContains = targetPathContains;
                this.targetPathEquals = targetPathEquals;
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string path = request.RequestUri?.AbsolutePath ?? "";
                bool match = request.Method == this.targetMethod
                    && (this.targetPathEquals != null
                        ? (path == this.targetPathEquals || path == this.targetPathEquals + "/")
                        : this.targetPathContains != null && path.Contains(this.targetPathContains));

                if (match && !this.hasSimulated401)
                {
                    this.hasSimulated401 = true;
                    this.SimulatedRevocationCount++;
                    this.RequestLog.Add((request.Method.ToString(), path, true));
                    return CreateFake401Response();
                }

                this.RequestLog.Add((request.Method.ToString(), path, false));
                return await base.SendAsync(request, cancellationToken);
            }
        }

        private class AlwaysRevokingHandler : DelegatingHandler
        {
            public int SimulatedRevocationCount { get; private set; }

            public AlwaysRevokingHandler(HttpMessageHandler innerHandler)
                : base(innerHandler) { }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string path = request.RequestUri?.AbsolutePath ?? "";
                if (request.Method == HttpMethod.Post && path.Contains("/docs"))
                {
                    this.SimulatedRevocationCount++;
                    return CreateFake401Response();
                }

                return await base.SendAsync(request, cancellationToken);
            }
        }

        private static HttpResponseMessage CreateFake401Response()
        {
            long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string claimsJson = "{\"access_token\":{\"nbf\":{\"essential\":false,\"value\":\"" + ts + "\"}}}";
            string base64Claims = Convert.ToBase64String(Encoding.UTF8.GetBytes(claimsJson));
            string wwwAuth = "Bearer realm=\"\", authorization_uri=\"\", error=\"insufficient_claims\", claims=\"" + base64Claims + "\"";

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            response.Headers.TryAddWithoutValidation("x-ms-substatus", "5013");
            response.Headers.TryAddWithoutValidation("x-ms-activity-id", Guid.NewGuid().ToString());
            response.Content = new StringContent("{\"code\":\"Unauthorized\",\"message\":\"Provided AAD token has been revoked.\"}");
            response.Headers.TryAddWithoutValidation("WWW-Authenticate", wwwAuth);
            return response;
        }
    }
}
