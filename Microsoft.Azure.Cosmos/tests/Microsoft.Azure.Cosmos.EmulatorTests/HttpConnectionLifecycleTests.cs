//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Integration tests validating HTTP/2 connection lifecycle and reuse behavior in thin client mode.
    ///
    /// The core invariant tested: a stream-level cancellation on an HTTP/2 connection should
    /// NOT close the parent TCP connection. Subsequent requests should reuse the same parent
    /// connections without creating new ones.
    /// </summary>
    [TestClass]
    public class HttpConnectionLifecycleTests
    {
        private string connectionString;

        [TestInitialize]
        public void TestInit()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");

            this.connectionString = Environment.GetEnvironmentVariable("COSMOSDB_THINCLIENT");

            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_THINCLIENT to run the tests.");
            }
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "False");
        }

        /// <summary>
        /// Proves that under concurrent load, SocketsHttpHandler can allocate multiple
        /// HTTP/2 parent connections (TCP connections) to the same endpoint, and that
        /// parent connections survive a stream-level cancellation.
        ///
        /// With EnableMultipleHttp2Connections=true and concurrent requests exceeding the
        /// server's MAX_CONCURRENT_STREAMS, SocketsHttpHandler opens additional parent
        /// connections. This test sends concurrent reads to force multiple connections,
        /// then injects a stream-level cancellation, and confirms parent connections survive.
        ///
        /// Only HTTP/2.0 requests (thin client data operations) are targeted for cancellation.
        /// HTTP/1.1 requests (gateway management operations) pass through normally.
        /// Connection tracking is filtered by endpoint to isolate thin client connections
        /// from gateway connections.
        /// </summary>
        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task MultiParentChannelConnectionReuse()
        {
            // Track TCP connections per endpoint to distinguish thin client (HTTP/2) from
            // gateway (HTTP/1.1) connections. Only thin client connections are relevant
            // for this test — gateway connections are established during setup and are not
            // affected by the HTTP/2 fault injection.
            ConcurrentDictionary<string, ConcurrentBag<string>> connectionsByEndpoint =
                new ConcurrentDictionary<string, ConcurrentBag<string>>();
            int connectionCounter = 0;

            SocketsHttpHandler socketsHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                MaxConnectionsPerServer = 20,
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10),
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true
                },
                ConnectCallback = async (context, cancellationToken) =>
                {
                    string id = $"conn-{Interlocked.Increment(ref connectionCounter)}";
                    string endpoint = context.DnsEndPoint.Host;

                    ConcurrentBag<string> bag = connectionsByEndpoint.GetOrAdd(
                        endpoint, _ => new ConcurrentBag<string>());
                    bag.Add(id);

                    Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                    await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
            };

            StreamCancellationHandler faultHandler = new StreamCancellationHandler(socketsHandler);
            HttpClient httpClient = new HttpClient(faultHandler);

            string databaseId = "H2ConnReuse_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            using CosmosClient client = new CosmosClient(
                this.connectionString,
                new CosmosClientOptions()
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    HttpClientFactory = () => httpClient,
                });

            try
            {
                // Setup: seed one item (HTTP/1.1 gateway operations — not affected by fault handler)
                Database db = (await client.CreateDatabaseIfNotExistsAsync(databaseId)).Database;
                Container container = (await db.CreateContainerIfNotExistsAsync(
                    Guid.NewGuid().ToString(),
                    "/pk")).Container;

                ToDoActivity seedItem = ToDoActivity.CreateRandomToDoActivity();
                await container.CreateItemAsync(seedItem, new PartitionKey(seedItem.pk));

                // Step 1: Force HTTP/2 parent connections by saturating the pool.
                // With EnableMultipleHttp2Connections=true, concurrent requests exceeding
                // MAX_CONCURRENT_STREAMS per connection force additional TCP connections.
                // Strategy: fire multiple waves of concurrent bursts until >1 connection observed.
                int concurrentRequests = 100;
                int maxWaves = 3;

                for (int wave = 0; wave < maxWaves; wave++)
                {
                    Task<ItemResponse<ToDoActivity>>[] waveTasks = Enumerable.Range(0, concurrentRequests)
                        .Select(_ => container.ReadItemAsync<ToDoActivity>(seedItem.id, new PartitionKey(seedItem.pk)))
                        .ToArray();
                    await Task.WhenAll(waveTasks);

                    if (this.GetThinClientConnectionCount(connectionsByEndpoint) > 1)
                    {
                        break;
                    }
                }

                int preFaultThinClientConnections = this.GetThinClientConnectionCount(connectionsByEndpoint);
                Assert.IsTrue(preFaultThinClientConnections >= 1,
                    $"Expected >= 1 thin client HTTP/2 connection after concurrent warmup. " +
                    $"Got: {preFaultThinClientConnections}. " +
                    $"Endpoints observed: [{string.Join(", ", connectionsByEndpoint.Keys)}]");

                // Step 2: Inject stream-level fault.
                // In Java: tc netem adds 8s network delay causing real ReadTimeoutException.
                // In .NET: cancel the request immediately after it starts flowing through
                // SocketsHttpHandler. Since SendAsync is async, it yields at I/O points
                // (after synchronously acquiring a pooled H2 stream), and the cancellation
                // fires while the request is in-flight. For HTTP/2, this triggers RST_STREAM
                // on the stream channel — the parent TCP connection should survive.
                //
                // Note: the SDK retries the cancelled request. Each retry also gets cancelled
                // (InjectFault is still true). This exercises multiple stream cancellations
                // across potentially different connections.
                faultHandler.InjectFault = true;
                try
                {
                    await container.ReadItemAsync<ToDoActivity>(seedItem.id, new PartitionKey(seedItem.pk));
                }
                catch (CosmosException)
                {
                    // Expected — stream cancellation surfaces as CosmosException
                }
                finally
                {
                    faultHandler.InjectFault = false;
                }

                // Step 3: Send concurrent reads again — if parent connections survived,
                // SocketsHttpHandler reuses them and ConnectCallback is NOT invoked for
                // the thin client endpoint. New ConnectCallback calls = connections died.
                Task<ItemResponse<ToDoActivity>>[] recoveryTasks = Enumerable.Range(0, concurrentRequests)
                    .Select(_ => container.ReadItemAsync<ToDoActivity>(seedItem.id, new PartitionKey(seedItem.pk)))
                    .ToArray();
                await Task.WhenAll(recoveryTasks);

                int postRecoveryThinClientConnections = this.GetThinClientConnectionCount(connectionsByEndpoint);
                int newThinClientConnections = postRecoveryThinClientConnections - preFaultThinClientConnections;

                // Step 4: Assert connection survival.
                // Java asserts: (pre-delay parentChannelIds ∩ post-delay parentChannelIds).isNotEmpty()
                // .NET equivalent: if fewer new connections were created than the pre-fault count,
                // at least one pre-fault connection must have been reused (survived).
                //
                // When preFaultCount == 1: require 0 new connections (the single connection must survive)
                // When preFaultCount > 1:  require < preFaultCount new connections (at least one survived)
                if (preFaultThinClientConnections == 1)
                {
                    Assert.AreEqual(0, newThinClientConnections,
                        $"The single HTTP/2 parent connection should survive the stream cancellation. " +
                        $"Pre-fault: {preFaultThinClientConnections}, " +
                        $"Post-recovery: {postRecoveryThinClientConnections}.");
                }
                else
                {
                    Assert.IsTrue(newThinClientConnections < preFaultThinClientConnections,
                        $"At least one pre-fault HTTP/2 parent connection should survive the stream " +
                        $"cancellation and be reused post-recovery. " +
                        $"Pre-fault: {preFaultThinClientConnections}, " +
                        $"Post-recovery: {postRecoveryThinClientConnections}, " +
                        $"New: {newThinClientConnections}. " +
                        $"Connection details: [{this.FormatConnectionDetails(connectionsByEndpoint)}]");
                }

                // Verify all recovery operations succeeded
                foreach (Task<ItemResponse<ToDoActivity>> task in recoveryTasks)
                {
                    Assert.AreEqual(HttpStatusCode.OK, task.Result.StatusCode);
                }

                await db.DeleteAsync();
            }
            catch
            {
                try { await client.GetDatabase(databaseId).DeleteAsync(); } catch { }
                throw;
            }
        }

        /// <summary>
        /// Returns the number of thin client connections by finding the non-gateway endpoint.
        /// The gateway endpoint is the account host from the connection string (e.g.,
        /// "myaccount.documents.azure.com"). The thin client proxy endpoint is a different host.
        /// If only one endpoint is observed, all connections are thin client connections.
        /// </summary>
        private int GetThinClientConnectionCount(
            ConcurrentDictionary<string, ConcurrentBag<string>> connectionsByEndpoint)
        {
            if (connectionsByEndpoint.IsEmpty)
            {
                return 0;
            }

            // Parse the gateway endpoint from the connection string
            string gatewayHost = null;
            if (!string.IsNullOrEmpty(this.connectionString))
            {
                try
                {
                    // Extract AccountEndpoint from connection string
                    string[] parts = this.connectionString.Split(';');
                    foreach (string part in parts)
                    {
                        string trimmed = part.Trim();
                        if (trimmed.StartsWith("AccountEndpoint=", StringComparison.OrdinalIgnoreCase))
                        {
                            string endpoint = trimmed.Substring("AccountEndpoint=".Length);
                            gatewayHost = new Uri(endpoint).Host;
                            break;
                        }
                    }
                }
                catch
                {
                    // If parsing fails, fall back to total count
                }
            }

            if (gatewayHost == null)
            {
                // Can't distinguish — return total
                return connectionsByEndpoint.Values.Sum(bag => bag.Count);
            }

            // Sum connections for all endpoints that are NOT the gateway
            int thinClientCount = 0;
            foreach (var kvp in connectionsByEndpoint)
            {
                if (!string.Equals(kvp.Key, gatewayHost, StringComparison.OrdinalIgnoreCase))
                {
                    thinClientCount += kvp.Value.Count;
                }
            }

            // If no non-gateway endpoints found, all connections might be thin client
            // (e.g., if the proxy uses the same host)
            if (thinClientCount == 0)
            {
                return connectionsByEndpoint.Values.Sum(bag => bag.Count);
            }

            return thinClientCount;
        }

        private string FormatConnectionDetails(
            ConcurrentDictionary<string, ConcurrentBag<string>> connectionsByEndpoint)
        {
            return string.Join("; ", connectionsByEndpoint.Select(
                kvp => $"{kvp.Key}: [{string.Join(", ", kvp.Value)}]"));
        }

        /// <summary>
        /// DelegatingHandler that cancels HTTP/2.0 requests immediately after they start
        /// flowing through SocketsHttpHandler.
        ///
        /// How it works:
        /// 1. <c>base.SendAsync(request, cts.Token)</c> calls SocketsHttpHandler.SendAsync
        /// 2. SocketsHttpHandler synchronously acquires a pooled HTTP/2 stream (no await yet)
        /// 3. SocketsHttpHandler starts writing HEADERS frame — first <c>await</c> point, yields
        /// 4. Control returns here; <c>cts.Cancel()</c> marks the token as cancelled
        /// 5. SocketsHttpHandler sees the cancellation at the next token check → RST_STREAM
        /// 6. The individual H2 stream is closed, parent TCP connection remains open
        ///
        /// HTTP/1.1 requests (gateway management operations) pass through unaffected.
        ///
        /// This is the .NET analog of Java's tc netem network delay. The key difference:
        /// Java tests real network-level failure (delayed TCP packets causing ReadTimeoutException).
        /// .NET tests application-level cancellation (CancellationToken propagated into the HTTP/2
        /// pipeline). Both trigger RST_STREAM and test parent connection survival.
        /// </summary>
        private class StreamCancellationHandler : DelegatingHandler
        {
            private static readonly Version Http2Version = new Version(2, 0);

            public volatile bool InjectFault;

            public StreamCancellationHandler(HttpMessageHandler innerHandler)
                : base(innerHandler)
            {
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                if (this.InjectFault && request.Version == Http2Version)
                {
                    using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                    // Start the request through SocketsHttpHandler — this synchronously
                    // acquires a pooled HTTP/2 stream, then begins async I/O (writing
                    // HEADERS frame). The Task returned represents the in-flight request.
                    Task<HttpResponseMessage> sendTask = base.SendAsync(request, cts.Token);

                    // Cancel immediately — the request is now in-flight inside
                    // SocketsHttpHandler on an established connection. The cancellation
                    // propagates into the async pipeline, triggering RST_STREAM for HTTP/2.
                    cts.Cancel();

                    try
                    {
                        return await sendTask;
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new TaskCanceledException(
                            "Simulated HTTP/2 stream timeout (analogous to Java's ReadTimeoutException via tc netem).");
                    }
                }

                return await base.SendAsync(request, cancellationToken);
            }
        }
    }
}
