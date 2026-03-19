// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace ConnectionStormTool;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using Microsoft.Azure.Cosmos;

/// <summary>
/// Connection storm stress test designed to reproduce port exhaustion scenarios.
/// 
/// Two modes:
///   Storm (original) - blast as many connections as possible.
///   SustainedRps     - maintain a fixed request rate. When downstream (Mux/SqlX) is
///                      slow or failing, retries + new requests pile up because the
///                      scheduler keeps firing at the target rate regardless of in-flight
///                      count. With connection pooling disabled, every request and every
///                      SDK retry opens a new TCP connection, organically creating the
///                      connection storm pattern seen in production.
/// </summary>
internal sealed class ConnectionStormJob : IDisposable
{
    private readonly AccountInfo accountInfo;
    private readonly ConnectionStormSettings stormSettings;
    private readonly string databaseName;
    private readonly string collectionName;

    // Metrics
    private long totalConnectionAttempts;
    private long successfulConnections;
    private long failedConnections;
    private long activeConnections;
    private long peakActiveConnections;
    private long retryAttempts;
    private long throttledByInFlightCap;
    private readonly ConcurrentDictionary<string, long> errorCounts = new();
    private readonly ConcurrentDictionary<string, long> errorLocationCounts = new(); // Client vs Server
    private readonly ConcurrentDictionary<HttpStatusCode, long> statusCodeCounts = new(); // HTTP status codes
    private readonly ConcurrentDictionary<HttpStatusCode, ConcurrentBag<double>> statusCodeLatencies = new(); // per-status latency samples (ms)
    private readonly Stopwatch globalStopwatch = new();

    // Port monitoring
    private int lastTimeWaitCount = 0;
    private int lastEstablishedCount = 0;
    private long portThrottlePauses = 0;

    // HTTP connection lifecycle metrics (via .NET 8 MeterListener)
    private readonly HttpConnectionMetrics httpConnectionMetrics = new(traceEnabled: false);

    // Seed document: written once before the storm so all reads return 200 (cache hit at SQLx, no RU charge)
    private const string SeedDocumentId = "stormtest_seed";

    // Client pool for connection reuse (SustainedRps mode with pooling enabled)
    private HttpClient[]? httpClientPool;
    private CosmosClient[]? cosmosClientPool;
    private Container[]? containerPool;
    private int clientPoolSize;

    public ConnectionStormJob(AccountInfo accountInfo, ConnectionStormSettings stormSettings)
    {
        this.accountInfo = accountInfo;
        this.stormSettings = stormSettings;
        this.databaseName = stormSettings.DatabaseName;
        this.collectionName = stormSettings.CollectionName;
    }

    /// <summary>
    /// Writes a single seed document before the storm starts. All subsequent reads target
    /// this document so they return 200. After the first read the document is cached at the
    /// downstream SQLx endpoint, making follow-up reads cache hits with zero RU cost.
    /// </summary>
    private async Task SeedDocumentAsync(CancellationToken cancellationToken)
    {
        Console.Write("Seeding document for cache-hit reads... ");

        using HttpClient httpClient = this.CreateNonPooledHttpClient();
        using CosmosClient client = this.CreateCosmosClientForMode(httpClient);

        Container container = client.GetDatabase(this.databaseName).GetContainer(this.collectionName);

        var seedDoc = new { id = SeedDocumentId, pk = SeedDocumentId, payload = "storm-seed" };

        try
        {
            await container.UpsertItemAsync(seedDoc, new PartitionKey(SeedDocumentId), cancellationToken: cancellationToken);
            Console.WriteLine($"OK (id={SeedDocumentId})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.Message}");
            Console.WriteLine("WARNING: reads will return 404 instead of 200. Continuing anyway...");
        }

        Console.WriteLine();
    }

    public async Task RunStormAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"=== CONNECTION STORM TEST ({this.stormSettings.Mode} mode) ===");
        Console.WriteLine($"Target      : {this.accountInfo.AccountEndpoint}");
        Console.WriteLine($"Duration    : {this.stormSettings.DurationInSeconds} seconds");
        Console.WriteLine($"Connection Pooling: {(this.stormSettings.DisableConnectionPooling ? "DISABLED" : "Enabled")}");
        Console.WriteLine($"HTTP Version: {(this.stormSettings.ForceHttp11 ? "HTTP/1.1" : "HTTP/2")}");
        Console.WriteLine($"StormMode   : {this.stormSettings.Mode}");

        if (this.stormSettings.Mode == StormMode.SustainedRps)
        {
            int effectiveRps = this.stormSettings.TargetRequestsPerSecond > 0
                ? this.stormSettings.TargetRequestsPerSecond
                : this.stormSettings.TargetConnectionsPerSecond;
            Console.WriteLine($"Target Read RPS : {effectiveRps:N0}");
            Console.WriteLine($"Connection Reuse: {(!this.stormSettings.DisableConnectionPooling ? $"ENABLED ({this.stormSettings.NumberOfDistinctClients} clients, {this.stormSettings.MaxConnectionsPerClient} conns each)" : "DISABLED (new TCP per request)")}");
            Console.WriteLine($"SDK Retries     : {this.stormSettings.SdkMaxRetryCount} (max wait: {this.stormSettings.SdkMaxRetryWaitTimeSeconds}s)");
            Console.WriteLine($"Max In-Flight   : {(this.stormSettings.MaxInFlightRequests == 0 ? "Unlimited" : this.stormSettings.MaxInFlightRequests.ToString("N0"))}");
            Console.WriteLine($"Request Timeout : {this.stormSettings.RequestTimeoutMs}ms");
        }
        else
        {
            Console.WriteLine($"Concurrent Attempts: {this.stormSettings.ConcurrentConnectionAttempts}");
            Console.WriteLine($"Target Rate    : {(this.stormSettings.TargetConnectionsPerSecond == 0 ? "Unlimited" : $"{this.stormSettings.TargetConnectionsPerSecond}/sec")}");
            Console.WriteLine($"Fire-and-Forget: {this.stormSettings.FireAndForget}");
        }

        if (this.stormSettings.EnablePortAvailabilityCheck)
        {
            Console.WriteLine($"Port Availability Check: ENABLED (max TIME_WAIT: {this.stormSettings.MaxTimeWaitConnections:N0})");
        }
        Console.WriteLine();

        // Show initial port state
        this.PrintPortStatus("Initial");
        Console.WriteLine();

        // Start HTTP connection metrics listener before any storm HttpClients are created
        this.httpConnectionMetrics.Start();

        this.httpConnectionMetrics.ReportMetrics();

        // Seed a document so all reads return 200. Subsequent reads hit the SQLx cache (no RU charge).
        Console.WriteLine($"Seeding document and warming cache in ...");
        await this.SeedDocumentAsync(cancellationToken);

        // Print initial Http metrics after seeding
        this.httpConnectionMetrics.ReportMetrics();

        this.globalStopwatch.Start();

        using CancellationTokenSource durationCts = new(TimeSpan.FromSeconds(this.stormSettings.DurationInSeconds));
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, durationCts.Token);

        // Start metrics reporting task
        Task metricsTask = this.ReportMetricsAsync(linkedCts.Token);

        if (this.stormSettings.Mode == StormMode.SustainedRps)
        {
            try { await this.RunSustainedRpsAsync(this.httpConnectionMetrics, linkedCts.Token); }
            catch (OperationCanceledException) { }
        }
        else
        {
            try { await this.RunStormModeAsync(linkedCts.Token); }
            catch (OperationCanceledException) { }
        }

        this.globalStopwatch.Stop();

        // Wait for final metrics
        try { await metricsTask; } catch (OperationCanceledException) { }

        this.PrintFinalSummary();
    }

    private async Task RunStormModeAsync(CancellationToken cancellationToken)
    {
        Task[] stormTasks = new Task[this.stormSettings.ConcurrentConnectionAttempts];
        for (int i = 0; i < this.stormSettings.ConcurrentConnectionAttempts; i++)
        {
            stormTasks[i] = this.ConnectionStormWorkerAsync(i, cancellationToken);
        }

        try { await Task.WhenAll(stormTasks); } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Sustained RPS mode: a timer fires read requests at a fixed rate representing the
    /// client application's steady-state demand. Each request is fire-and-forget from the
    /// scheduler's perspective — if downstream is slow, requests pile up in-flight because
    /// the application's demand doesn't change.
    ///
    /// With DisableConnectionPooling=true: every request opens a new TCP connection,
    /// creating organic connection amplification.
    ///
    /// With DisableConnectionPooling=false: requests reuse pooled connections from
    /// NumberOfDistinctClients pre-created CosmosClient instances. Connection count is
    /// bounded but in-flight request count still grows when downstream is slow.
    /// </summary>
    private async Task RunSustainedRpsAsync(HttpConnectionMetrics httpConnectionMetrics, CancellationToken cancellationToken)
    {
        // Use TargetRequestsPerSecond if set, otherwise fall back to TargetConnectionsPerSecond
        int targetRps = this.stormSettings.TargetRequestsPerSecond > 0
            ? this.stormSettings.TargetRequestsPerSecond
            : this.stormSettings.TargetConnectionsPerSecond;

        if (targetRps <= 0)
        {
            Console.WriteLine("ERROR: TargetRequestsPerSecond (or TargetConnectionsPerSecond) must be > 0 for SustainedRps mode.");
            return;
        }

        bool useClientPool = !this.stormSettings.DisableConnectionPooling;

        if (useClientPool)
        {
            this.InitializeClientPool();
        }

        try
        {
            await this.RunSustainedRpsDispatchLoopAsync(httpConnectionMetrics, targetRps, useClientPool, cancellationToken);
        }
        finally
        {
            if (useClientPool)
            {
                this.DisposeClientPool();
            }
        }
    }

    private async Task RunSustainedRpsDispatchLoopAsync(HttpConnectionMetrics httpConnectionMetrics, int targetRps, bool useClientPool, CancellationToken cancellationToken)
    {

        // Calculate interval between request dispatches
        double intervalMs = 1000.0 / targetRps;
        List<Task> inFlightTasks = new();
        Stopwatch pacer = Stopwatch.StartNew();
        long requestsDispatched = 0;

        Console.WriteLine($"Dispatching at {targetRps:N0} read requests/sec (1 every {intervalMs:F2}ms)");
        if (useClientPool)
        {
            Console.WriteLine($"Connection reuse: ENABLED ({this.clientPoolSize} pooled clients, round-robin)");
        }
        else
        {
            Console.WriteLine("Connection reuse: DISABLED (new TCP connection per request)");
        }
        Console.WriteLine("If downstream is slow/failing, in-flight requests will pile up at this rate...");
        Console.WriteLine();

        while (!cancellationToken.IsCancellationRequested)
        {
            // Calculate how many requests should have been dispatched by now
            double elapsedMs = pacer.Elapsed.TotalMilliseconds;
            long expectedRequests = (long)(elapsedMs / intervalMs);

            // Dispatch requests to catch up to the target rate
            while (requestsDispatched < expectedRequests && !cancellationToken.IsCancellationRequested)
            {
                // Check in-flight cap
                long currentInFlight = Interlocked.Read(ref this.activeConnections);
                if (this.stormSettings.MaxInFlightRequests > 0 && currentInFlight >= this.stormSettings.MaxInFlightRequests)
                {
                    Interlocked.Increment(ref this.throttledByInFlightCap);
                    break; // Wait for next tick
                }

                // Check max total connections limit
                if (this.stormSettings.MaxTotalConnections > 0 &&
                    Interlocked.Read(ref this.totalConnectionAttempts) >= this.stormSettings.MaxTotalConnections)
                {
                    return;
                }

                requestsDispatched++;
                Interlocked.Increment(ref this.totalConnectionAttempts);
                Interlocked.Increment(ref this.activeConnections);
                this.UpdatePeakInFlight();

                // Capture the index for the closure
                long dispatchIndex = requestsDispatched;

                // Dispatch as a pure async call — no Task.Run, no ThreadPool thread needed.
                // The async HTTP call yields at await, so the calling thread is freed during I/O.
                // This avoids thousands of ThreadPool threads and the context-switching overhead.
                Task requestTask = DispatchRequestAsync(useClientPool, dispatchIndex, cancellationToken);

                // Periodically clean up completed tasks to avoid list growth
                inFlightTasks.Add(requestTask);
                if (inFlightTasks.Count > 5000)
                {
                    inFlightTasks.RemoveAll(t => t.IsCompleted);
                }
            }

            // Small yield to avoid busy-spinning
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            // Report metrics after every iteration of RPS
            //httpConnectionMetrics.ReportMetrics();
            //Console.WriteLine("PRESS ENTER TO CONTINUE");
            //Console.ReadLine();
        }

        // Wait for remaining in-flight requests to complete (with a short timeout)
        if (inFlightTasks.Count > 0)
        {
            Console.WriteLine($"Waiting for {inFlightTasks.Count} in-flight requests to complete...");
            await Task.WhenAny(
                Task.WhenAll(inFlightTasks),
                Task.Delay(TimeSpan.FromSeconds(10)));
        }
    }

    /// <summary>
    /// Wraps a single request dispatch as a pure async method (no Task.Run).
    /// Handles success/error tracking and in-flight count decrement.
    /// </summary>
    private async Task DispatchRequestAsync(bool useClientPool, long dispatchIndex, CancellationToken cancellationToken)
    {
        try
        {
            if (useClientPool)
            {
                await this.ExecuteSustainedRpsRequestPooledAsync(
                    (int)(dispatchIndex % this.clientPoolSize), cancellationToken);
            }
            else
            {
                await this.ExecuteSustainedRpsRequestAsync(cancellationToken);
            }
            Interlocked.Increment(ref this.successfulConnections);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            this.RecordError(ex);
        }
        finally
        {
            Interlocked.Decrement(ref this.activeConnections);
        }
    }

    /// <summary>
    /// Execute a single request in SustainedRps mode. SDK retries are enabled,
    /// so a single failed request can open multiple TCP connections.
    /// </summary>
    private async Task ExecuteSustainedRpsRequestAsync(CancellationToken cancellationToken)
    {
        using HttpClient httpClient = this.CreateNonPooledHttpClient();
        using CosmosClient client = this.CreateCosmosClientForMode(httpClient);

        Container container = client.GetDatabase(this.databaseName).GetContainer(this.collectionName);

        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            ResponseMessage response = await container.ReadItemStreamAsync(
                SeedDocumentId,
                new PartitionKey(SeedDocumentId),
                cancellationToken: cancellationToken);
            sw.Stop();

            this.RecordStatusCode(response.StatusCode);
            this.RecordLatency(response.StatusCode, sw.Elapsed.TotalMilliseconds);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Unexpected status: {response.StatusCode} - {response.ErrorMessage}");
            }
        }
        catch (CosmosException ex)
        {
            sw.Stop();
            this.RecordStatusCode(ex.StatusCode);
            this.RecordLatency(ex.StatusCode, sw.Elapsed.TotalMilliseconds);

            // Track retries — the SDK already retried internally, each opening new connections
            if (ex.Headers?.AllKeys()?.Contains("x-ms-retry-after-ms") == true ||
                ex.RetryAfter.HasValue)
            {
                Interlocked.Increment(ref this.retryAttempts);
            }

            throw;
        }
    }

    /// <summary>
    /// Creates a CosmosClient with retry settings based on current mode.
    /// Storm mode: retries disabled to see raw failure rate.
    /// SustainedRps mode: retries enabled to amplify connection pressure.
    /// </summary>
    private CosmosClient CreateCosmosClientForMode(HttpClient httpClient)
    {
        CosmosClientOptions options = new()
        {
            ConnectionMode = ConnectionMode.Gateway,
            HttpClientFactory = () => httpClient,
            LimitToEndpoint = true,
            RequestTimeout = TimeSpan.FromMilliseconds(this.stormSettings.RequestTimeoutMs),
        };

        if (this.stormSettings.Mode == StormMode.SustainedRps)
        {
            // Enable retries — each retry with pooling disabled = new TCP connection
            options.MaxRetryAttemptsOnRateLimitedRequests = this.stormSettings.SdkMaxRetryCount;
            options.MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(this.stormSettings.SdkMaxRetryWaitTimeSeconds);
        }
        else
        {
            // Original storm mode: no retries
            options.MaxRetryAttemptsOnRateLimitedRequests = 0;
            options.MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.Zero;
        }

        if (this.accountInfo.UseAAD)
        {
            return new CosmosClient(
                this.accountInfo.AccountEndpoint,
                new Azure.Identity.DefaultAzureCredential(),
                options);
        }
        else
        {
            return new CosmosClient(
                this.accountInfo.AccountEndpoint,
                this.accountInfo.AccountKey,
                options);
        }
    }

    private void UpdatePeakInFlight()
    {
        long current = Interlocked.Read(ref this.activeConnections);
        long peak;
        do
        {
            peak = Interlocked.Read(ref this.peakActiveConnections);
            if (current <= peak) break;
        } while (Interlocked.CompareExchange(ref this.peakActiveConnections, current, peak) != peak);
    }

    private async Task ConnectionStormWorkerAsync(int workerId, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Check if we've hit the max connections limit
            if (this.stormSettings.MaxTotalConnections > 0 &&
                Interlocked.Read(ref this.totalConnectionAttempts) >= this.stormSettings.MaxTotalConnections)
            {
                return;
            }

            // Check port availability before attempting connection
            if (this.stormSettings.EnablePortAvailabilityCheck && this.stormSettings.MaxTimeWaitConnections > 0)
            {
                if (await this.WaitForPortAvailabilityAsync(cancellationToken))
                {
                    // If we had to wait, continue to re-check conditions
                    continue;
                }
            }

            // Rate limiting if target is specified
            if (this.stormSettings.TargetConnectionsPerSecond > 0)
            {
                double elapsedSeconds = this.globalStopwatch.Elapsed.TotalSeconds;
                double expectedConnections = elapsedSeconds * this.stormSettings.TargetConnectionsPerSecond;
                long currentConnections = Interlocked.Read(ref this.totalConnectionAttempts);

                if (currentConnections >= expectedConnections)
                {
                    await Task.Delay(1, cancellationToken);
                    continue;
                }
            }

            Interlocked.Increment(ref this.totalConnectionAttempts);
            Interlocked.Increment(ref this.activeConnections);

            try
            {
                if (this.stormSettings.FireAndForget)
                {
                    // Fire-and-forget: don't await, just launch
                    _ = this.ExecuteSingleConnectionAsync(cancellationToken)
                        .ContinueWith(t =>
                        {
                            Interlocked.Decrement(ref this.activeConnections);
                            if (t.IsFaulted)
                            {
                                this.RecordError(t.Exception?.InnerException);
                            }
                            else
                            {
                                Interlocked.Increment(ref this.successfulConnections);
                            }
                        }, TaskScheduler.Default);
                }
                else
                {
                    await this.ExecuteSingleConnectionAsync(cancellationToken);
                    Interlocked.Increment(ref this.successfulConnections);
                    Interlocked.Decrement(ref this.activeConnections);
                }
            }
            catch (Exception ex)
            {
                Interlocked.Decrement(ref this.activeConnections);
                this.RecordError(ex);
            }
        }
    }

    private async Task ExecuteSingleConnectionAsync(CancellationToken cancellationToken)
    {
        // Create a NEW client for each request to force new TCP connections
        using HttpClient httpClient = this.CreateNonPooledHttpClient();
        using CosmosClient client = this.CreateCosmosClientForMode(httpClient);

        Container container = client.GetDatabase(this.databaseName).GetContainer(this.collectionName);

        // Read the seed document (cached at SQLx, no RU charge)
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            ResponseMessage response = await container.ReadItemStreamAsync(
                SeedDocumentId,
                new PartitionKey(SeedDocumentId),
                cancellationToken: cancellationToken);
            sw.Stop();

            this.RecordStatusCode(response.StatusCode);
            this.RecordLatency(response.StatusCode, sw.Elapsed.TotalMilliseconds);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Unexpected status: {response.StatusCode} - {response.ErrorMessage}");
            }
        }
        catch (CosmosException ex)
        {
            sw.Stop();
            this.RecordStatusCode(ex.StatusCode);
            this.RecordLatency(ex.StatusCode, sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    private void RecordStatusCode(HttpStatusCode statusCode)
    {
        this.statusCodeCounts.AddOrUpdate(statusCode, 1, (_, count) => count + 1);
    }

    private void RecordLatency(HttpStatusCode statusCode, double latencyMs)
    {
        var bag = this.statusCodeLatencies.GetOrAdd(statusCode, _ => new ConcurrentBag<double>());
        bag.Add(latencyMs);
    }

    private (double avg, double p99) GetLatencyStats(HttpStatusCode statusCode)
    {
        if (!this.statusCodeLatencies.TryGetValue(statusCode, out var bag) || bag.IsEmpty)
            return (0, 0);

        double[] samples = bag.ToArray();
        double avg = samples.Average();
        Array.Sort(samples);
        int p99Index = Math.Min((int)(samples.Length * 0.99), samples.Length - 1);
        return (avg, samples[p99Index]);
    }

    private HttpClient CreateNonPooledHttpClient()
    {
        SocketsHttpHandler handler;

        if (this.stormSettings.DisableConnectionPooling)
        {
            handler = new SocketsHttpHandler
            {
                // Disable connection pooling - each request gets a new connection
                PooledConnectionLifetime = TimeSpan.Zero,
                PooledConnectionIdleTimeout = TimeSpan.Zero,
                MaxConnectionsPerServer = 1,

                // Short timeouts to accumulate TIME_WAIT connections faster
                ConnectTimeout = TimeSpan.FromMilliseconds(this.stormSettings.ConnectionTimeoutMs),
            };
        }
        else
        {
            handler = new SocketsHttpHandler();
        }

        // Accept self-signed certs for test environments
        handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;

        HttpClient client = new(handler)
        {
            Timeout = TimeSpan.FromMilliseconds(this.stormSettings.RequestTimeoutMs)
        };

        // Force HTTP/1.1 to prevent multiplexing (uses more connections)
        if (this.stormSettings.ForceHttp11)
        {
            client.DefaultRequestVersion = HttpVersion.Version11;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
        }

        if (!string.IsNullOrEmpty(this.accountInfo.AccountDnsName))
        {
            client.DefaultRequestHeaders.Host = this.accountInfo.AccountDnsName;
        }

        return client;
    }

    // CreateCosmosClientForMode is defined above in the SustainedRps section

    /// <summary>
    /// Creates an HttpClient with connection pooling enabled for multi-client mode.
    /// Each client maintains its own pool of up to MaxConnectionsPerClient connections.
    /// </summary>
    private HttpClient CreatePooledHttpClient()
    {
        SocketsHttpHandler handler = new()
        {
            MaxConnectionsPerServer = this.stormSettings.MaxConnectionsPerClient,
            ConnectTimeout = TimeSpan.FromMilliseconds(this.stormSettings.ConnectionTimeoutMs),
        };

        handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;

        HttpClient client = new(handler)
        {
            Timeout = TimeSpan.FromMilliseconds(this.stormSettings.RequestTimeoutMs)
        };

        if (this.stormSettings.ForceHttp11)
        {
            client.DefaultRequestVersion = HttpVersion.Version11;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
        }

        if (!string.IsNullOrEmpty(this.accountInfo.AccountDnsName))
        {
            client.DefaultRequestHeaders.Host = this.accountInfo.AccountDnsName;
        }

        return client;
    }

    /// <summary>
    /// Pre-creates a pool of long-lived CosmosClient instances for connection reuse.
    /// Each client simulates a distinct "customer" and maintains its own connection pool.
    /// Requests are round-robined across clients.
    /// Total connections = NumberOfDistinctClients * MaxConnectionsPerClient.
    /// </summary>
    private void InitializeClientPool()
    {
        this.clientPoolSize = Math.Max(1, this.stormSettings.NumberOfDistinctClients);
        this.httpClientPool = new HttpClient[this.clientPoolSize];
        this.cosmosClientPool = new CosmosClient[this.clientPoolSize];
        this.containerPool = new Container[this.clientPoolSize];

        for (int i = 0; i < this.clientPoolSize; i++)
        {
            this.httpClientPool[i] = this.CreatePooledHttpClient();
            this.cosmosClientPool[i] = this.CreateCosmosClientForMode(this.httpClientPool[i]);
            this.containerPool[i] = this.cosmosClientPool[i]
                .GetDatabase(this.databaseName)
                .GetContainer(this.collectionName);
        }

        int totalConns = this.clientPoolSize * this.stormSettings.MaxConnectionsPerClient;
        Console.WriteLine($"Initialized {this.clientPoolSize} pooled CosmosClient instances");
        Console.WriteLine($"  MaxConnectionsPerClient: {this.stormSettings.MaxConnectionsPerClient}");
        Console.WriteLine($"  Total pooled connections: up to {totalConns:N0}");
        Console.WriteLine();
    }

    /// <summary>
    /// Disposes all pooled clients and their underlying HTTP connections.
    /// </summary>
    private void DisposeClientPool()
    {
        if (this.cosmosClientPool != null)
        {
            foreach (var client in this.cosmosClientPool)
            {
                client?.Dispose();
            }
            this.cosmosClientPool = null;
        }

        if (this.httpClientPool != null)
        {
            foreach (var client in this.httpClientPool)
            {
                client?.Dispose();
            }
            this.httpClientPool = null;
        }

        this.containerPool = null;
    }

    /// <summary>
    /// Execute a single read request using a pre-created pooled CosmosClient.
    /// The Container is shared and thread-safe. Connections are reused from the pool.
    /// </summary>
    private async Task ExecuteSustainedRpsRequestPooledAsync(int clientIndex, CancellationToken cancellationToken)
    {
        Container container = this.containerPool![clientIndex % this.clientPoolSize];

        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            ResponseMessage response = await container.ReadItemStreamAsync(
                SeedDocumentId,
                new PartitionKey(SeedDocumentId),
                cancellationToken: cancellationToken);
            sw.Stop();

            this.RecordStatusCode(response.StatusCode);
            this.RecordLatency(response.StatusCode, sw.Elapsed.TotalMilliseconds);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Unexpected status: {response.StatusCode} - {response.ErrorMessage}");
            }
        }
        catch (CosmosException ex)
        {
            sw.Stop();
            this.RecordStatusCode(ex.StatusCode);
            this.RecordLatency(ex.StatusCode, sw.Elapsed.TotalMilliseconds);

            if (ex.Headers?.AllKeys()?.Contains("x-ms-retry-after-ms") == true ||
                ex.RetryAfter.HasValue)
            {
                Interlocked.Increment(ref this.retryAttempts);
            }

            throw;
        }
    }

    private void RecordError(Exception? ex)
    {
        Interlocked.Increment(ref this.failedConnections);

        string errorType = ex?.GetType().Name ?? "Unknown";
        string errorLocation = "Unknown";

        // Dig into the exception to find SocketException
        Exception? current = ex;
        SocketException? socketEx = null;
        while (current != null)
        {
            if (current is SocketException se)
            {
                socketEx = se;
                break;
            }
            current = current.InnerException;
        }

        if (socketEx != null)
        {
            // Analyze SocketException to determine if it's client-side or server-side
            var errorInfo = ClassifySocketError(socketEx);
            errorType = errorInfo.errorType;
            errorLocation = errorInfo.location;
        }
        else if (ex?.Message != null)
        {
            // Fallback to message-based classification
            var errorInfo = ClassifyByMessage(ex.Message);
            errorType = errorInfo.errorType;
            errorLocation = errorInfo.location;
        }

        this.errorCounts.AddOrUpdate(errorType, 1, (_, count) => count + 1);
        this.errorLocationCounts.AddOrUpdate(errorLocation, 1, (_, count) => count + 1);
    }

    private static (string errorType, string location) ClassifySocketError(SocketException socketEx)
    {
        // SocketErrorCode gives us precise information about the failure
        // Reference: https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socketerror
        
        return socketEx.SocketErrorCode switch
        {
            // CLIENT-SIDE PORT EXHAUSTION indicators
            // These occur when the LOCAL machine runs out of ephemeral ports
            SocketError.AddressAlreadyInUse => ("CLIENT:AddressInUse", "CLIENT"),
            SocketError.AddressNotAvailable => ("CLIENT:NoLocalAddress", "CLIENT"),
            SocketError.TooManyOpenSockets => ("CLIENT:TooManySockets", "CLIENT"),
            SocketError.NoBufferSpaceAvailable => ("CLIENT:NoBufferSpace", "CLIENT"),
            
            // SERVER-SIDE / NETWORK issues
            // These indicate the server rejected or can't handle the connection
            SocketError.ConnectionRefused => ("SERVER:ConnectionRefused", "SERVER"),
            SocketError.ConnectionReset => ("SERVER:ConnectionReset", "SERVER"),
            SocketError.ConnectionAborted => ("SERVER:ConnectionAborted", "SERVER"),
            SocketError.HostDown => ("SERVER:HostDown", "SERVER"),
            SocketError.HostUnreachable => ("SERVER:HostUnreachable", "SERVER"),
            SocketError.NetworkDown => ("NETWORK:NetworkDown", "NETWORK"),
            SocketError.NetworkUnreachable => ("NETWORK:NetworkUnreachable", "NETWORK"),
            
            // TIMEOUT issues (could be either side)
            SocketError.TimedOut => ("TIMEOUT:SocketTimeout", "UNKNOWN"),
            
            // Other errors
            _ => ($"Socket:{socketEx.SocketErrorCode}", "UNKNOWN")
        };
    }

    private static (string errorType, string location) ClassifyByMessage(string message)
    {
        // CLIENT-SIDE port exhaustion patterns
        if (message.Contains("address already in use", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Only one usage of each socket address", StringComparison.OrdinalIgnoreCase))
        {
            return ("CLIENT:PortExhaustion", "CLIENT");
        }
        
        if (message.Contains("No ephemeral ports", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("cannot assign requested address", StringComparison.OrdinalIgnoreCase))
        {
            return ("CLIENT:NoEphemeralPorts", "CLIENT");
        }

        if (message.Contains("too many open files", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("EMFILE", StringComparison.OrdinalIgnoreCase))
        {
            return ("CLIENT:TooManyFileDescriptors", "CLIENT");
        }

        // SERVER-SIDE patterns
        if (message.Contains("refused", StringComparison.OrdinalIgnoreCase))
        {
            return ("SERVER:ConnectionRefused", "SERVER");
        }
        
        if (message.Contains("reset", StringComparison.OrdinalIgnoreCase))
        {
            return ("SERVER:ConnectionReset", "SERVER");
        }

        if (message.Contains("503", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("service unavailable", StringComparison.OrdinalIgnoreCase))
        {
            return ("SERVER:ServiceUnavailable", "SERVER");
        }

        if (message.Contains("429", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
        {
            return ("SERVER:Throttled", "SERVER");
        }

        // TIMEOUT patterns
        if (message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return ("TIMEOUT:RequestTimeout", "UNKNOWN");
        }

        return ("Other", "UNKNOWN");
    }

    private (int timeWait, int established, int closeWait, int finWait) GetTcpConnectionStats()
    {
        try
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var connections = properties.GetActiveTcpConnections();
            
            int timeWait = 0;
            int established = 0;
            int closeWait = 0;
            int finWait = 0;

            foreach (var conn in connections)
            {
                switch (conn.State)
                {
                    case TcpState.TimeWait:
                        timeWait++;
                        break;
                    case TcpState.Established:
                        established++;
                        break;
                    case TcpState.CloseWait:
                        closeWait++;
                        break;
                    case TcpState.FinWait1:
                    case TcpState.FinWait2:
                        finWait++;
                        break;
                }
            }

            return (timeWait, established, closeWait, finWait);
        }
        catch
        {
            return (0, 0, 0, 0);
        }
    }

    /// <summary>
    /// Checks if local ports are available. Returns true if we had to pause (caller should re-check conditions).
    /// </summary>
    private async Task<bool> WaitForPortAvailabilityAsync(CancellationToken cancellationToken)
    {
        var stats = GetTcpConnectionStats();
        
        if (stats.timeWait >= this.stormSettings.MaxTimeWaitConnections)
        {
            // Too many TIME_WAIT connections - pause to let them expire
            Interlocked.Increment(ref this.portThrottlePauses);
            await Task.Delay(this.stormSettings.PortExhaustionPauseMs, cancellationToken);
            return true; // We paused, caller should re-check
        }

        return false; // No pause needed
    }

    private void PrintPortStatus(string label)
    {
        var stats = GetTcpConnectionStats();
        Console.WriteLine($"[{label}] TCP Connections - TIME_WAIT: {stats.timeWait:N0} | ESTABLISHED: {stats.established:N0} | CLOSE_WAIT: {stats.closeWait:N0} | FIN_WAIT: {stats.finWait:N0}");
    }

    private async Task ReportMetricsAsync(CancellationToken cancellationToken)
    {
        int intervalSeconds = this.stormSettings.MetricReportingIntervalSeconds;
        long lastTotalConnections = 0;

        var props = IPGlobalProperties.GetIPGlobalProperties();
        TcpStatistics? lastTcpV4Stats = props.GetTcpIPv4Statistics();
        TcpStatistics? lastTcpV6Stats = props.GetTcpIPv4Statistics();

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);

            long currentTotal = Interlocked.Read(ref this.totalConnectionAttempts);
            long currentSuccess = Interlocked.Read(ref this.successfulConnections);
            long currentFailed = Interlocked.Read(ref this.failedConnections);
            long currentActive = Interlocked.Read(ref this.activeConnections);
            double elapsed = this.globalStopwatch.Elapsed.TotalSeconds;

            long recentConnections = currentTotal - lastTotalConnections;
            double recentRate = recentConnections / (double)intervalSeconds;
            double overallRate = currentTotal / elapsed;

            // Get TCP stats

            var tcpStats = GetTcpConnectionStats();
            var properties = IPGlobalProperties.GetIPGlobalProperties();

            Console.WriteLine($"[{elapsed:F1}s] Total: {currentTotal:N0} | InFlight: {currentActive:N0} (peak: {Interlocked.Read(ref this.peakActiveConnections):N0}) | " +
                            $"Success: {currentSuccess:N0} | Failed: {currentFailed:N0} | " +
                            $"Rate: {recentRate:N0}/s (avg: {overallRate:N0}/s)");
            
            Console.WriteLine($"       TCP: TIME_WAIT={tcpStats.timeWait:N0} ESTABLISHED={tcpStats.established:N0} CLOSE_WAIT={tcpStats.closeWait:N0}");

            // HTTP connection lifecycle metrics (via EventSource)
            this.httpConnectionMetrics.ReportMetrics();

            var tcpV4Stats= properties.GetTcpIPv4Statistics();
            Console.WriteLine("IPV4 -> " + string.Join(", ", typeof(TcpStatistics).GetProperties()
              .Select(p => (name: p.Name, current: Convert.ToInt64(p.GetValue(tcpV4Stats)), prev: Convert.ToInt64(p.GetValue(lastTcpV4Stats))))
              .Select(x => $"{x.name}= ({(x.current - x.prev >= 0 ? "+" : "")}{x.current - x.prev})")));

            var tcpV6Stats = properties.GetTcpIPv6Statistics();
            Console.WriteLine("IPV6 -> " + string.Join(", ", typeof(TcpStatistics).GetProperties()
              .Select(p => (name: p.Name, current: Convert.ToInt64(p.GetValue(tcpV6Stats)), prev: Convert.ToInt64(p.GetValue(lastTcpV6Stats))))
              .Select(x => $"{x.name}= ({(x.current - x.prev >= 0 ? "+" : "")}{x.current - x.prev})")));

            lastTcpV4Stats = tcpV4Stats;
            lastTcpV6Stats = tcpV6Stats;

            // Show SustainedRps-specific metrics
            if (this.stormSettings.Mode == StormMode.SustainedRps)
            {
                long retries = Interlocked.Read(ref this.retryAttempts);
                long throttled = Interlocked.Read(ref this.throttledByInFlightCap);
                if (retries > 0 || throttled > 0)
                {
                    Console.WriteLine($"       SustainedRps: failed-after-retry={retries:N0} | in-flight-capped={throttled:N0}");
                }

                // Amplification factor: how many more connections are open than the target RPS
                int effectiveRps = this.stormSettings.TargetRequestsPerSecond > 0
                    ? this.stormSettings.TargetRequestsPerSecond
                    : this.stormSettings.TargetConnectionsPerSecond;
                double amplification = effectiveRps > 0
                    ? currentActive / (double)effectiveRps
                    : 0;
                if (amplification > 1.5)
                {
                    Console.WriteLine($"       ⚡ AMPLIFICATION: {amplification:F1}x (in-flight vs target RPS — downstream bottleneck detected)");
                }
            }

            // Show port throttle status if enabled
            if (this.stormSettings.EnablePortAvailabilityCheck)
            {
                long pauses = Interlocked.Read(ref this.portThrottlePauses);
                if (pauses > 0 || tcpStats.timeWait >= this.stormSettings.MaxTimeWaitConnections * 0.8)
                {
                    Console.WriteLine($"       ⏸️  Port Throttle: {pauses:N0} pauses (threshold: {this.stormSettings.MaxTimeWaitConnections:N0})");
                }
            }

            if (this.statusCodeCounts.Count > 0)
            {
                long intervalTotal = this.statusCodeCounts.Values.Sum();
                double intervalElapsed = this.globalStopwatch.Elapsed.TotalSeconds;
                foreach (var kv in this.statusCodeCounts.OrderByDescending(kv => kv.Value))
                {
                    double rate = intervalElapsed > 0 ? kv.Value / intervalElapsed : 0;
                    var (avg, p99) = this.GetLatencyStats(kv.Key);
                    Console.WriteLine($"       {(int)kv.Key} {kv.Key}: {kv.Value:N0} ({rate:N0}/s)  avg={avg:F1}ms  P99={p99:F1}ms");
                }
            }

            if (this.errorLocationCounts.Count > 0)
            {
                Console.WriteLine($"       Error Location: {string.Join(", ", this.errorLocationCounts.Select(kv => $"{kv.Key}={kv.Value:N0}"))}");
            }

            if (this.errorCounts.Count > 0)
            {
                Console.WriteLine($"       Error Types: {string.Join(", ", this.errorCounts.Select(kv => $"{kv.Key}={kv.Value:N0}"))}");
            }

            // Track changes in TIME_WAIT
            int timeWaitDelta = tcpStats.timeWait - this.lastTimeWaitCount;
            if (Math.Abs(timeWaitDelta) > 100)
            {
                Console.WriteLine($"       ⚠️  TIME_WAIT delta: {(timeWaitDelta > 0 ? "+" : "")}{timeWaitDelta:N0} (high churn indicates client-side port pressure)");
            }

            this.lastTimeWaitCount = tcpStats.timeWait;
            this.lastEstablishedCount = tcpStats.established;
            lastTotalConnections = currentTotal;
        }
    }

    private void PrintFinalSummary()
    {
        Console.WriteLine();
        Console.WriteLine($"=== FINAL SUMMARY ({this.stormSettings.Mode} mode) ===");
        Console.WriteLine($"Duration: {this.globalStopwatch.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($"Total Connection Attempts: {this.totalConnectionAttempts:N0}");
        Console.WriteLine($"Successful Connections: {this.successfulConnections:N0}");
        Console.WriteLine($"Failed Connections: {this.failedConnections:N0}");
        Console.WriteLine($"Success Rate: {(this.totalConnectionAttempts > 0 ? (double)this.successfulConnections / this.totalConnectionAttempts * 100 : 0):F1}%");
        Console.WriteLine($"Average Rate: {this.totalConnectionAttempts / this.globalStopwatch.Elapsed.TotalSeconds:N0} connections/second");
        Console.WriteLine($"Projected Rate per Minute: {this.totalConnectionAttempts / this.globalStopwatch.Elapsed.TotalSeconds * 60:N0} connections/minute");
        Console.WriteLine($"Peak In-Flight: {Interlocked.Read(ref this.peakActiveConnections):N0}");

        if (this.stormSettings.Mode == StormMode.SustainedRps)
        {
            long retries = Interlocked.Read(ref this.retryAttempts);
            long throttled = Interlocked.Read(ref this.throttledByInFlightCap);
            Console.WriteLine();
            Console.WriteLine("--- SustainedRps Amplification Analysis ---");
            int effectiveTargetRps = this.stormSettings.TargetRequestsPerSecond > 0
                ? this.stormSettings.TargetRequestsPerSecond
                : this.stormSettings.TargetConnectionsPerSecond;
            Console.WriteLine($"Target RPS: {effectiveTargetRps:N0}");
            Console.WriteLine($"Actual avg RPS: {this.totalConnectionAttempts / this.globalStopwatch.Elapsed.TotalSeconds:N0}");
            Console.WriteLine($"Requests Failed After SDK Retries: {retries:N0}");
            Console.WriteLine($"Throttled by In-Flight Cap: {throttled:N0}");
            double peakAmplification = effectiveTargetRps > 0
                ? Interlocked.Read(ref this.peakActiveConnections) / (double)effectiveTargetRps
                : 0;
            Console.WriteLine($"Peak Amplification Factor: {peakAmplification:F1}x");
            if (peakAmplification > 2)
            {
                Console.WriteLine("⚡ Significant connection amplification detected — downstream bottleneck caused request pile-up.");
            }
        }
        
        if (this.stormSettings.EnablePortAvailabilityCheck)
        {
            long pauses = Interlocked.Read(ref this.portThrottlePauses);
            Console.WriteLine($"Port Throttle Pauses: {pauses:N0}");
        }
        Console.WriteLine();

        // Final port status
        this.PrintPortStatus("Final");
        Console.WriteLine();

        // HTTP Connection Lifecycle metrics
        var httpFinal = this.httpConnectionMetrics.GetCumulativeSnapshot();
        Console.WriteLine("=== HTTP CONNECTION METRICS ===");
        Console.WriteLine($"  Connections Created  : {httpFinal.Created:N0}");
        Console.WriteLine($"  Connections Closed   : {httpFinal.Closed:N0}");
        Console.WriteLine($"  Connections Failed   : {httpFinal.Failed:N0}  (app-level SocketException)");
        Console.WriteLine($"  Requests Failed      : {httpFinal.RequestsFailed:N0}  (EventSource)");
        Console.WriteLine($"  Currently Open       : {httpFinal.CurrentlyOpen:N0}");
        Console.WriteLine($"  Unique Endpoints     : {httpFinal.UniqueRemoteEndpoints:N0}");
        var vbFinal = httpFinal.VersionBreakdown;
        Console.WriteLine($"  HTTP/1.1             : {vbFinal.Http11Open:N0} open / {vbFinal.Http11Total:N0} total");
        Console.WriteLine($"  HTTP/2               : {vbFinal.Http2Open:N0} open / {vbFinal.Http2Total:N0} total");
        Console.WriteLine($"  HTTP/3               : {vbFinal.Http3Open:N0} open / {vbFinal.Http3Total:N0} total");
        var rtFinal = httpFinal.RequestTelemetry;
        Console.WriteLine($"  Requests Started     : {rtFinal.Started:N0}");
        Console.WriteLine($"  Requests Completed   : {rtFinal.Stopped:N0}");
        Console.WriteLine($"  Requests In-Flight   : {rtFinal.InFlight:N0}");
        Console.WriteLine($"  Queue Wait (avg)     : {rtFinal.AvgQueueTimeMs:F2} ms");
        Console.WriteLine($"  Queue Wait (max)     : {rtFinal.MaxQueueTimeMs:F2} ms");
        if (httpFinal.RecentFailures.Length > 0)
        {
            Console.WriteLine($"  Recent Failures ({httpFinal.RecentFailures.Length}):");
            foreach (var msg in httpFinal.RecentFailures.TakeLast(5))
            {
                Console.WriteLine($"    - {msg[..Math.Min(msg.Length, 120)]}");
            }
        }
        Console.WriteLine();

        // HTTP Status Code breakdown
        if (this.statusCodeCounts.Count > 0)
        {
            double totalElapsed = this.globalStopwatch.Elapsed.TotalSeconds;
            long totalRequests = this.statusCodeCounts.Values.Sum();
            Console.WriteLine("=== HTTP STATUS CODE BREAKDOWN ===");
            Console.WriteLine($"  {"Status",-26} {"Count",10}   {"  %",5}   {"Rate/s",8}   {"Avg ms",8}   {"P99 ms",8}   Description");
            Console.WriteLine($"  {new string('-', 100)}");
            foreach (var kvp in this.statusCodeCounts.OrderByDescending(kv => kv.Value))
            {
                double percentage = totalRequests > 0 ? (double)kvp.Value / totalRequests * 100 : 0;
                double rate = totalElapsed > 0 ? kvp.Value / totalElapsed : 0;
                var (avg, p99) = this.GetLatencyStats(kvp.Key);
                string statusDescription = GetStatusCodeDescription(kvp.Key);
                Console.WriteLine($"  {(int)kvp.Key} {kvp.Key,-22} {kvp.Value,10:N0}  {percentage,5:F1}%  {rate,8:N0}  {avg,8:F1}  {p99,8:F1}   {statusDescription}");
            }
            Console.WriteLine();
        }

        if (this.errorLocationCounts.Count > 0)
        {
            Console.WriteLine("=== ERROR LOCATION ANALYSIS ===");
            long clientErrors = 0;
            long serverErrors = 0;
            long networkErrors = 0;
            long unknownErrors = 0;

            foreach (var kvp in this.errorLocationCounts)
            {
                switch (kvp.Key)
                {
                    case "CLIENT":
                        clientErrors = kvp.Value;
                        break;
                    case "SERVER":
                        serverErrors = kvp.Value;
                        break;
                    case "NETWORK":
                        networkErrors = kvp.Value;
                        break;
                    default:
                        unknownErrors = kvp.Value;
                        break;
                }
            }

            long totalErrors = this.failedConnections;
            if (totalErrors > 0)
            {
                Console.WriteLine($"  CLIENT-SIDE errors:  {clientErrors:N0} ({(double)clientErrors / totalErrors * 100:F1}%)");
                Console.WriteLine($"  SERVER-SIDE errors:  {serverErrors:N0} ({(double)serverErrors / totalErrors * 100:F1}%)");
                Console.WriteLine($"  NETWORK errors:      {networkErrors:N0} ({(double)networkErrors / totalErrors * 100:F1}%)");
                Console.WriteLine($"  UNKNOWN location:    {unknownErrors:N0} ({(double)unknownErrors / totalErrors * 100:F1}%)");
            }
            Console.WriteLine();

            if (clientErrors > serverErrors && clientErrors > 0)
            {
                Console.WriteLine("*** CLIENT-SIDE PORT EXHAUSTION DETECTED! ***");
                Console.WriteLine("    The client machine is running out of ephemeral ports.");
                Console.WriteLine("    This is NOT a Mux/server issue - it's the test client hitting local limits.");
                Console.WriteLine();
                Console.WriteLine("    To reduce client-side exhaustion:");
                Console.WriteLine("    1. Reduce ConcurrentConnectionAttempts");
                Console.WriteLine("    2. Increase connection reuse (DisableConnectionPooling=false)");
                Console.WriteLine("    3. Run from multiple client machines");
                Console.WriteLine("    4. Increase ephemeral port range: netsh int ipv4 set dynamic tcp start=1025 num=64510");
                Console.WriteLine("    5. Reduce TIME_WAIT: reg add HKLM\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters /v TcpTimedWaitDelay /t REG_DWORD /d 30");
            }
            else if (serverErrors > clientErrors && serverErrors > 0)
            {
                Console.WriteLine("*** SERVER-SIDE ISSUES DETECTED! ***");
                Console.WriteLine("    The server (Mux/CosmosDB) is rejecting or dropping connections.");
                Console.WriteLine("    This indicates the target is under stress.");
            }
        }

        if (this.errorCounts.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Error Type Breakdown:");
            foreach (var kvp in this.errorCounts.OrderByDescending(x => x.Value))
            {
                double percentage = this.failedConnections > 0 ? (double)kvp.Value / this.failedConnections * 100 : 0;
                Console.WriteLine($"  {kvp.Key}: {kvp.Value:N0} ({percentage:F1}%)");
            }
        }
    }

    private static string GetStatusCodeDescription(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.OK => "Success",
            HttpStatusCode.Created => "Created",
            HttpStatusCode.NoContent => "No Content",
            HttpStatusCode.NotModified => "Not Modified (cached)",
            HttpStatusCode.BadRequest => "Bad Request - client error",
            HttpStatusCode.Unauthorized => "Unauthorized - auth failure",
            HttpStatusCode.Forbidden => "Forbidden - access denied",
            HttpStatusCode.NotFound => "Not Found - seed document missing?",
            HttpStatusCode.RequestTimeout => "Request Timeout - server didn't respond",
            HttpStatusCode.Conflict => "Conflict - write conflict",
            HttpStatusCode.Gone => "Gone - resource deleted",
            HttpStatusCode.PreconditionFailed => "Precondition Failed - ETag mismatch",
            HttpStatusCode.RequestEntityTooLarge => "Payload Too Large",
            (HttpStatusCode)429 => "Too Many Requests - THROTTLED",
            HttpStatusCode.InternalServerError => "Internal Server Error",
            HttpStatusCode.BadGateway => "Bad Gateway - upstream failure",
            HttpStatusCode.ServiceUnavailable => "Service Unavailable - SERVER OVERLOADED",
            HttpStatusCode.GatewayTimeout => "Gateway Timeout - upstream timeout",
            _ => $"HTTP {(int)statusCode}"
        };
    }

    public void Dispose()
    {
        this.DisposeClientPool();
        this.httpConnectionMetrics.Dispose();
    }
}
