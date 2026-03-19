// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace ConnectionStormTool;

using System.Collections.Concurrent;
using System.Diagnostics.Tracing;

/// <summary>
/// Collects HTTP connection and request lifecycle metrics via the <c>System.Net.Http</c>
/// EventSource using the enriched payloads available in .NET 10.
///
/// Tracked events:
///   1  = RequestStart            — scheme, host, port, pathAndQuery, versionMajor, versionMinor
///   2  = RequestStop             — statusCode
///   3  = RequestFailed           — exceptionMessage
///   4  = ConnectionEstablished   — versionMajor, versionMinor, connectionId, scheme, host, port, remoteAddress
///   5  = ConnectionClosed        — versionMajor, versionMinor, connectionId
///   6  = RequestLeftQueue        — timeOnQueueMilliseconds, versionMajor, versionMinor
///   15 = RequestFailedDetailed   — full exception toString (opt-in keyword)
///
/// Must be created BEFORE the first HttpClient is used so the EventListener
/// captures the EventSource enable.
/// </summary>
internal sealed class HttpConnectionMetrics : IDisposable
{
    private readonly HttpEventSourceListener eventSourceListener;

    // --- Connection counters ---
    private long connectionsCreated;
    private long connectionsClosed;
    private long connectionsFailed;       // app-level (RecordConnectionFailure)
    private long requestsFailed;          // EventSource RequestFailed (event 3)

    // Per-HTTP-version connection counters
    private long http11Created;
    private long http11Closed;
    private long http2Created;
    private long http2Closed;
    private long http3Created;
    private long http3Closed;

    // Active connection tracking by connectionId
    private readonly ConcurrentDictionary<long, ConnectionInfo> activeConnections = new();

    // Unique remote endpoints observed
    private readonly ConcurrentDictionary<string, byte> remoteEndpoints = new();

    // Recent failure messages (ring buffer for diagnostics)
    private const int MaxRecentFailures = 20;
    private readonly ConcurrentQueue<string> recentFailureMessages = new();

    // --- Request counters ---
    private long requestsStarted;
    private long requestsStopped;

    // Queue wait time tracking (microsecond-granularity from RequestLeftQueue)
    private long requestsDequeued;
    private double queueTimeTotalMs;
    private double queueTimeMaxMs;
    private readonly object queueTimeLock = new();

    // --- Snapshot state for interval delta reporting ---
    private long lastSnapshotCreated;
    private long lastSnapshotClosed;
    private long lastSnapshotFailed;
    private long lastSnapshotRequestsFailed;
    private long lastSnapshotRequestsStarted;
    private long lastSnapshotRequestsStopped;
    private long lastSnapshotRequestsDequeued;
    private double lastSnapshotQueueTimeTotalMs;

    private readonly bool traceEnabled = true;

    public HttpConnectionMetrics(bool traceEnabled = false)
    {
        this.traceEnabled = traceEnabled;
        this.eventSourceListener = new HttpEventSourceListener(this);
    }

    /// <summary>
    /// Starts listening for metrics. The EventSourceListener is already active from
    /// construction; this method is kept for API compatibility.
    /// </summary>
    public void Start()
    {
        // EventSourceListener is active from construction — nothing additional needed.
    }

    public void ReportMetrics()
    {
        HttpConnectionIntervalSnapshot httpSnap = this.GetIntervalSnapshot();
        Console.Write($"       HTTP Conns: created={httpSnap.Cumulative.Created:N0} ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"(+{httpSnap.DeltaCreated:N0})");
        Console.ResetColor();
        Console.Write($" | closed={httpSnap.Cumulative.Closed:N0} ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"(+{httpSnap.DeltaClosed:N0})");
        Console.ResetColor();
        Console.Write($" | failed={httpSnap.Cumulative.Failed:N0} ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"(+{httpSnap.DeltaFailed:N0})");
        Console.ResetColor();
        Console.Write($" | reqFailed={httpSnap.Cumulative.RequestsFailed:N0} ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"(+{httpSnap.DeltaRequestsFailed:N0})");
        Console.ResetColor();
        Console.WriteLine($" | open={httpSnap.Cumulative.CurrentlyOpen:N0}");

        var vb = httpSnap.Cumulative.VersionBreakdown;
        if (vb.Http11Total > 0 || vb.Http2Total > 0 || vb.Http3Total > 0)
        {
            Console.WriteLine($"                  versions: H1.1={vb.Http11Open:N0}/{vb.Http11Total:N0}  H2={vb.Http2Open:N0}/{vb.Http2Total:N0}  H3={vb.Http3Open:N0}/{vb.Http3Total:N0}  endpoints={httpSnap.Cumulative.UniqueRemoteEndpoints:N0}");
        }

        var rt = httpSnap.Cumulative.RequestTelemetry;
        Console.Write($"       HTTP Reqs:  started={rt.Started:N0} ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"(+{httpSnap.DeltaRequestsStarted:N0})");
        Console.ResetColor();
        Console.Write($" | stopped={rt.Stopped:N0} ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"(+{httpSnap.DeltaRequestsStopped:N0})");
        Console.ResetColor();
        Console.Write($" | inFlight={rt.InFlight:N0}");
        Console.WriteLine($" | queueAvg={httpSnap.IntervalAvgQueueTimeMs:F1}ms max={rt.MaxQueueTimeMs:F1}ms");
    }

    // ---- Connection callbacks ----

    internal void OnConnectionEstablished(byte versionMajor, byte versionMinor, long connectionId, string? remoteAddress)
    {
        if (this.traceEnabled)
            Console.WriteLine($"Connection established: {connectionId} ({versionMajor}.{versionMinor}) to {remoteAddress}");

        Interlocked.Increment(ref this.connectionsCreated);

        switch (versionMajor)
        {
            case 1: Interlocked.Increment(ref this.http11Created); break;
            case 2: Interlocked.Increment(ref this.http2Created); break;
            case 3: Interlocked.Increment(ref this.http3Created); break;
        }

        this.activeConnections[connectionId] = new ConnectionInfo(
            ConnectionId: connectionId,
            HttpVersion: $"{versionMajor}.{versionMinor}",
            RemoteAddress: remoteAddress,
            EstablishedAt: DateTime.UtcNow);

        if (!string.IsNullOrEmpty(remoteAddress))
        {
            this.remoteEndpoints.TryAdd(remoteAddress, 0);
        }
    }

    internal void OnConnectionClosed(byte versionMajor, long connectionId)
    {
        if (this.traceEnabled)
            Console.WriteLine($"Connection closed: {connectionId} ({versionMajor}.x)");

        Interlocked.Increment(ref this.connectionsClosed);

        switch (versionMajor)
        {
            case 1: Interlocked.Increment(ref this.http11Closed); break;
            case 2: Interlocked.Increment(ref this.http2Closed); break;
            case 3: Interlocked.Increment(ref this.http3Closed); break;
        }

        this.activeConnections.TryRemove(connectionId, out _);
    }

    internal void OnRequestFailed(string? exceptionMessage)
    {
        if (this.traceEnabled)
            Console.WriteLine($"Request failed: {exceptionMessage}");

        Interlocked.Increment(ref this.requestsFailed);

        if (!string.IsNullOrEmpty(exceptionMessage))
        {
            this.recentFailureMessages.Enqueue(exceptionMessage);
            while (this.recentFailureMessages.Count > MaxRecentFailures)
            {
                this.recentFailureMessages.TryDequeue(out _);
            }
        }
    }

    // ---- Request callbacks ----

    internal void OnRequestStarted()
    {
        if (this.traceEnabled)
            Console.WriteLine("Request started");

        Interlocked.Increment(ref this.requestsStarted);
    }

    internal void OnRequestStopped()
    {
        if (this.traceEnabled)
            Console.WriteLine("Request stopped");

        Interlocked.Increment(ref this.requestsStopped);
    }

    internal void OnRequestLeftQueue(double timeOnQueueMs)
    {
        if (this.traceEnabled)
            Console.WriteLine($"Request left queue: {timeOnQueueMs}ms");

        Interlocked.Increment(ref this.requestsDequeued);

        lock (this.queueTimeLock)
        {
            this.queueTimeTotalMs += timeOnQueueMs;
            if (timeOnQueueMs > this.queueTimeMaxMs)
            {
                this.queueTimeMaxMs = timeOnQueueMs;
            }
        }
    }

    // ---- Snapshots ----

    /// <summary>
    /// Returns cumulative totals for HTTP connection and request metrics.
    /// </summary>
    public HttpConnectionSnapshot GetCumulativeSnapshot()
    {
        long created = Interlocked.Read(ref this.connectionsCreated);
        long closed = Interlocked.Read(ref this.connectionsClosed);
        long failed = Interlocked.Read(ref this.connectionsFailed);
        long reqFailed = Interlocked.Read(ref this.requestsFailed);
        long currentlyOpen = created - closed;

        long reqStarted = Interlocked.Read(ref this.requestsStarted);
        long reqStopped = Interlocked.Read(ref this.requestsStopped);
        long reqDequeued = Interlocked.Read(ref this.requestsDequeued);
        double totalQueueMs;
        double maxQueueMs;
        lock (this.queueTimeLock)
        {
            totalQueueMs = this.queueTimeTotalMs;
            maxQueueMs = this.queueTimeMaxMs;
        }

        return new HttpConnectionSnapshot(
            Created: created,
            Closed: closed,
            Failed: failed,
            RequestsFailed: reqFailed,
            CurrentlyOpen: currentlyOpen,
            VersionBreakdown: this.GetVersionBreakdown(),
            UniqueRemoteEndpoints: this.remoteEndpoints.Count,
            RecentFailures: this.recentFailureMessages.ToArray(),
            RequestTelemetry: new RequestTelemetrySnapshot(
                Started: reqStarted,
                Stopped: reqStopped,
                InFlight: reqStarted - reqStopped,
                Dequeued: reqDequeued,
                AvgQueueTimeMs: reqDequeued > 0 ? totalQueueMs / reqDequeued : 0,
                MaxQueueTimeMs: maxQueueMs));
    }

    /// <summary>
    /// Returns interval deltas (changes since the last call to this method)
    /// along with cumulative totals.
    /// </summary>
    public HttpConnectionIntervalSnapshot GetIntervalSnapshot()
    {
        long created = Interlocked.Read(ref this.connectionsCreated);
        long closed = Interlocked.Read(ref this.connectionsClosed);
        long failed = Interlocked.Read(ref this.connectionsFailed);
        long reqFailed = Interlocked.Read(ref this.requestsFailed);
        long currentlyOpen = created - closed;

        long reqStarted = Interlocked.Read(ref this.requestsStarted);
        long reqStopped = Interlocked.Read(ref this.requestsStopped);
        long reqDequeued = Interlocked.Read(ref this.requestsDequeued);
        double totalQueueMs;
        double maxQueueMs;
        lock (this.queueTimeLock)
        {
            totalQueueMs = this.queueTimeTotalMs;
            maxQueueMs = this.queueTimeMaxMs;
        }

        long deltaCreated = created - this.lastSnapshotCreated;
        long deltaClosed = closed - this.lastSnapshotClosed;
        long deltaFailed = failed - this.lastSnapshotFailed;
        long deltaRequestsFailed = reqFailed - this.lastSnapshotRequestsFailed;
        long deltaReqStarted = reqStarted - this.lastSnapshotRequestsStarted;
        long deltaReqStopped = reqStopped - this.lastSnapshotRequestsStopped;
        long deltaDequeued = reqDequeued - this.lastSnapshotRequestsDequeued;
        double deltaQueueTotalMs = totalQueueMs - this.lastSnapshotQueueTimeTotalMs;

        this.lastSnapshotCreated = created;
        this.lastSnapshotClosed = closed;
        this.lastSnapshotFailed = failed;
        this.lastSnapshotRequestsFailed = reqFailed;
        this.lastSnapshotRequestsStarted = reqStarted;
        this.lastSnapshotRequestsStopped = reqStopped;
        this.lastSnapshotRequestsDequeued = reqDequeued;
        this.lastSnapshotQueueTimeTotalMs = totalQueueMs;

        var requestTelemetry = new RequestTelemetrySnapshot(
            Started: reqStarted,
            Stopped: reqStopped,
            InFlight: reqStarted - reqStopped,
            Dequeued: reqDequeued,
            AvgQueueTimeMs: reqDequeued > 0 ? totalQueueMs / reqDequeued : 0,
            MaxQueueTimeMs: maxQueueMs);

        return new HttpConnectionIntervalSnapshot(
            Cumulative: new HttpConnectionSnapshot(
                created, closed, failed, reqFailed, currentlyOpen,
                this.GetVersionBreakdown(), this.remoteEndpoints.Count,
                this.recentFailureMessages.ToArray(), requestTelemetry),
            DeltaCreated: deltaCreated,
            DeltaClosed: deltaClosed,
            DeltaFailed: deltaFailed,
            DeltaRequestsFailed: deltaRequestsFailed,
            DeltaRequestsStarted: deltaReqStarted,
            DeltaRequestsStopped: deltaReqStopped,
            DeltaDequeued: deltaDequeued,
            IntervalAvgQueueTimeMs: deltaDequeued > 0 ? deltaQueueTotalMs / deltaDequeued : 0);
    }

    /// <summary>
    /// Records a connection failure detected by application-level error handling.
    /// </summary>
    public void RecordConnectionFailure()
    {
        Interlocked.Increment(ref this.connectionsFailed);
    }

    private HttpVersionBreakdown GetVersionBreakdown()
    {
        return new HttpVersionBreakdown(
            Http11Open: Interlocked.Read(ref this.http11Created) - Interlocked.Read(ref this.http11Closed),
            Http2Open: Interlocked.Read(ref this.http2Created) - Interlocked.Read(ref this.http2Closed),
            Http3Open: Interlocked.Read(ref this.http3Created) - Interlocked.Read(ref this.http3Closed),
            Http11Total: Interlocked.Read(ref this.http11Created),
            Http2Total: Interlocked.Read(ref this.http2Created),
            Http3Total: Interlocked.Read(ref this.http3Created));
    }

    public void Dispose()
    {
        this.eventSourceListener.Dispose();
    }
}

/// <summary>
/// Listens to the <c>System.Net.Http</c> EventSource for connection and request lifecycle events.
/// Uses .NET 10 enriched payloads (connectionId, remoteAddress, exceptionMessage, queueTime).
///
/// Event IDs match <c>HttpTelemetry</c> in the .NET runtime:
///   1 = RequestStart, 2 = RequestStop, 3 = RequestFailed,
///   4 = ConnectionEstablished, 5 = ConnectionClosed,
///   6 = RequestLeftQueue, 15 = RequestFailedDetailed.
/// </summary>
internal sealed class HttpEventSourceListener : EventListener
{
    private const string HttpEventSourceName = "System.Net.Http";
    private const int RequestStartEventId = 1;
    private const int RequestStopEventId = 2;
    private const int RequestFailedEventId = 3;
    private const int ConnectionEstablishedEventId = 4;
    private const int ConnectionClosedEventId = 5;
    private const int RequestLeftQueueEventId = 6;

    private readonly HttpConnectionMetrics owner;

    public HttpEventSourceListener(HttpConnectionMetrics owner)
    {
        this.owner = owner;
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == HttpEventSourceName)
        {
            // EventLevel.Informational captures connection and request events.
            // Keywords value 1 = RequestFailedDetailed for richer error diagnostics.
            EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)1);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        switch (eventData.EventId)
        {
            case RequestStartEventId:
                this.owner.OnRequestStarted();
                break;

            case RequestStopEventId:
                this.owner.OnRequestStopped();
                break;

            case ConnectionEstablishedEventId:
                // .NET 10 payload: versionMajor(0), versionMinor(1), connectionId(2),
                //                  scheme(3), host(4), port(5), remoteAddress(6)
                this.owner.OnConnectionEstablished(
                    versionMajor: CastByte(eventData.Payload, 0),
                    versionMinor: CastByte(eventData.Payload, 1),
                    connectionId: CastLong(eventData.Payload, 2),
                    remoteAddress: eventData.Payload?.Count > 6 ? eventData.Payload[6] as string : null);
                break;

            case ConnectionClosedEventId:
                // .NET 10 payload: versionMajor(0), versionMinor(1), connectionId(2)
                this.owner.OnConnectionClosed(
                    versionMajor: CastByte(eventData.Payload, 0),
                    connectionId: CastLong(eventData.Payload, 2));
                break;

            case RequestLeftQueueEventId:
                // Payload: timeOnQueueMilliseconds(0), versionMajor(1), versionMinor(2)
                this.owner.OnRequestLeftQueue(
                    timeOnQueueMs: CastDouble(eventData.Payload, 0));
                break;

            case RequestFailedEventId:
                // Payload: exceptionMessage(0)
                this.owner.OnRequestFailed(
                    exceptionMessage: eventData.Payload?.Count > 0 ? eventData.Payload[0] as string : null);
                break;
        }
    }

    private static byte CastByte(IReadOnlyList<object?>? payload, int index)
    {
        if (payload == null || payload.Count <= index || payload[index] == null) return 0;
        return Convert.ToByte(payload[index]);
    }

    private static long CastLong(IReadOnlyList<object?>? payload, int index)
    {
        if (payload == null || payload.Count <= index || payload[index] == null) return 0;
        return Convert.ToInt64(payload[index]);
    }

    private static double CastDouble(IReadOnlyList<object?>? payload, int index)
    {
        if (payload == null || payload.Count <= index || payload[index] == null) return 0;
        return Convert.ToDouble(payload[index]);
    }
}

/// <summary>
/// Metadata for a tracked active connection.
/// </summary>
internal record ConnectionInfo(
    long ConnectionId,
    string HttpVersion,
    string? RemoteAddress,
    DateTime EstablishedAt);

/// <summary>
/// Per-HTTP-version connection counts.
/// </summary>
internal record HttpVersionBreakdown(
    long Http11Open,
    long Http2Open,
    long Http3Open,
    long Http11Total,
    long Http2Total,
    long Http3Total);

/// <summary>
/// Request-level telemetry from EventSource.
/// </summary>
internal record RequestTelemetrySnapshot(
    long Started,
    long Stopped,
    long InFlight,
    long Dequeued,
    double AvgQueueTimeMs,
    double MaxQueueTimeMs);

/// <summary>
/// Cumulative HTTP connection and request metrics snapshot.
/// </summary>
internal record HttpConnectionSnapshot(
    long Created,
    long Closed,
    long Failed,
    long RequestsFailed,
    long CurrentlyOpen,
    HttpVersionBreakdown VersionBreakdown,
    int UniqueRemoteEndpoints,
    string[] RecentFailures,
    RequestTelemetrySnapshot RequestTelemetry);

/// <summary>
/// Interval snapshot with deltas since last snapshot plus cumulative totals.
/// </summary>
internal record HttpConnectionIntervalSnapshot(
    HttpConnectionSnapshot Cumulative,
    long DeltaCreated,
    long DeltaClosed,
    long DeltaFailed,
    long DeltaRequestsFailed,
    long DeltaRequestsStarted,
    long DeltaRequestsStopped,
    long DeltaDequeued,
    double IntervalAvgQueueTimeMs);