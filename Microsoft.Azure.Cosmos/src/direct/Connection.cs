//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

#if COSMOSCLIENT
    using Microsoft.Azure.Cosmos.Rntbd;
#endif

#if NETSTANDARD15 || NETSTANDARD16
    using Trace = Microsoft.Azure.Documents.Trace;
#endif

    // Connection encapsulates the TCP connection to one back-end, and all surrounding
    // mechanisms (SSL stream, connection state).
    internal sealed class Connection : IDisposable
    {
        private const int ResponseLengthByteLimit = int.MaxValue;
        private const SslProtocols TlsProtocols = SslProtocols.Tls12;
        private const uint TcpKeepAliveIntervalSocketOptionEnumValue = 17;
        private const uint TcpKeepAliveTimeSocketOptionEnumValue = 3;
        private const uint DefaultSocketOptionTcpKeepAliveInterval = 1;
        private const uint DefaultSocketOptionTcpKeepAliveTime = 30;
        private static readonly uint SocketOptionTcpKeepAliveInterval = GetUInt32FromEnvironmentVariableOrDefault(
            Constants.EnvironmentVariables.SocketOptionTcpKeepAliveIntervalName,
            minValue: 1,
            maxValue: 100,
            defaultValue: DefaultSocketOptionTcpKeepAliveInterval);
        private static readonly uint SocketOptionTcpKeepAliveTime = GetUInt32FromEnvironmentVariableOrDefault(
            Constants.EnvironmentVariables.SocketOptionTcpKeepAliveTimeName,
            minValue: 1,
            maxValue: 100,
            defaultValue: DefaultSocketOptionTcpKeepAliveTime);

        private static readonly Lazy<ConcurrentPrng> rng =
            new Lazy<ConcurrentPrng>(LazyThreadSafetyMode.ExecutionAndPublication);
        private static readonly Lazy<byte[]> keepAliveConfiguration =
            new Lazy<byte[]>(Connection.GetWindowsKeepAliveConfiguration,
                LazyThreadSafetyMode.ExecutionAndPublication);
        private static readonly Lazy<bool> isKeepAliveCustomizationSupported =
            new Lazy<bool>(Connection.IsKeepAliveCustomizationSupported,
                LazyThreadSafetyMode.ExecutionAndPublication);

        private static readonly TimeSpan recentReceiveWindow = TimeSpan.FromSeconds(1.0);

        private readonly Uri serverUri;
        private readonly string hostNameCertificateOverride;

        // Recyclable memory stream pool
        private readonly MemoryStreamPool memoryStreamPool;

        // Used only for integration tests
        private readonly RemoteCertificateValidationCallback remoteCertificateValidationCallback;

        private bool disposed = false;

        private TcpClient tcpClient;
        private UserPortPool portPool;
        private IPEndPoint localEndPoint;
        private IPEndPoint remoteEndPoint;

        /// <summary>
        /// a connection is defined as idle if (now - lastReceiveTime >= idleConnectionTimeout)
        /// </summary>
        private readonly TimeSpan idleConnectionTimeout;

        /// <summary>
        /// Due to race condition, requests may enter a connection when it's evaluated as idle
        /// The value is idleConnectionTimeout plus a reasonably adequate buffer for pending requests to complete send and receive.
        /// </summary>
        private readonly TimeSpan idleConnectionClosureTimeout;

        private readonly Func<string, Task<IPAddress>> dnsResolutionFunction;

        // Only one task may write to the stream at once. Reads don't need
        // mutual exclusion because only one thread consumes from the stream.
        private readonly SemaphoreSlim writeSemaphore = new SemaphoreSlim(1);
        private Stream stream;
        private RntbdStreamReader streamReader;

        private readonly object timestampLock = new object();
        private DateTime lastSendAttemptTime;  // Guarded by timestampLock.
        private DateTime lastSendTime;  // Guarded by timestampLock.
        private DateTime lastReceiveTime;  // Guarded by timestampLock.
        private long numberOfSendsSinceLastReceive = 0;  // Guarded by timestampLock.
        private DateTime firstSendSinceLastReceive;  // Guarded by timestampLock.

        private readonly object nameLock = new object();  // Acquired after timestampLock.
        private string name;  // Guarded by nameLock.

        private static int numberOfOpenTcpConnections;

        private readonly bool timeoutDetectionEnabled;
        private readonly ConnectionHealthChecker healthChecker;

        private int transitTimeoutCounter;
        private int transitTimeoutWriteCounter;

        public Connection(
            Uri serverUri,
            string hostNameCertificateOverride,
            TimeSpan receiveHangDetectionTime,
            TimeSpan sendHangDetectionTime,
            TimeSpan idleTimeout,
            MemoryStreamPool memoryStreamPool,
            RemoteCertificateValidationCallback remoteCertificateValidationCallback,
            Func<string, Task<IPAddress>> dnsResolutionFunction)
        {
            Debug.Assert(serverUri.PathAndQuery.Equals("/", StringComparison.Ordinal), serverUri.AbsoluteUri,
                "The server URI must not specify a path and query");
            this.serverUri = serverUri;
            this.hostNameCertificateOverride = hostNameCertificateOverride;
            this.BufferProvider = new BufferProvider();
            this.dnsResolutionFunction = dnsResolutionFunction ?? Connection.ResolveHostAsync;
            this.lastSendAttemptTime = DateTime.MinValue;
            this.lastSendTime = DateTime.MinValue;
            this.lastReceiveTime = DateTime.MinValue;
            
            if (idleTimeout > TimeSpan.Zero)
            {
                // idle timeout is enabled
                // Due to race condition, requests may enter a connection when it's evaluated as idle.
                // The race condition is resolved by making the value of idleConnectionClosureTimeout as idleConnectionTimeout plus a reasonably adequate buffer for pending requests to complete send and receive.
                this.idleConnectionTimeout = idleTimeout;
                this.idleConnectionClosureTimeout = this.idleConnectionTimeout
                    + TimeSpan.FromTicks(2 * (sendHangDetectionTime.Ticks + receiveHangDetectionTime.Ticks));
            }

            this.name = string.Format(CultureInfo.InvariantCulture,
                "<not connected> -> {0}", this.serverUri);

            this.memoryStreamPool = memoryStreamPool;
            this.remoteCertificateValidationCallback = remoteCertificateValidationCallback;

            this.timeoutDetectionEnabled = Helpers.GetEnvironmentVariable(
                    name: Constants.EnvironmentVariables.TimeoutDetectionEnabled,
                    defaultValue: true);
            this.transitTimeoutCounter = 0;
            this.transitTimeoutWriteCounter = 0;

            this.healthChecker = new (
                sendDelayLimit: sendHangDetectionTime,
                receiveDelayLimit: receiveHangDetectionTime,
                idleConnectionTimeout: idleTimeout,
                timeoutDetectionEnabled: this.timeoutDetectionEnabled);
        }

        public static int NumberOfOpenTcpConnections { get { return Connection.numberOfOpenTcpConnections; } }

        public BufferProvider BufferProvider { get; }

        public Uri ServerUri { get { return this.serverUri; } }

        public bool Healthy
        {
            get
            {
                this.ThrowIfDisposed();
                if (this.tcpClient == null)
                {
                    return false;
                }

                DateTime currentTime = DateTime.UtcNow;
                int transitTimeoutCounter = -1, transitTimeoutWriteCounter = -1;

                this.SnapshotConnectionTimestamps(
                    out DateTime lastSendAttempt,
                    out DateTime lastSend,
                    out DateTime lastReceive,
                    out DateTime? firstSendSinceLastReceive,
                    out long numberOfSendsSinceLastReceive);

                if (this.timeoutDetectionEnabled)
                {
                    this.SnapshotTransitTimeoutCounters(
                        out transitTimeoutCounter,
                        out transitTimeoutWriteCounter);
                }

                return this.healthChecker.IsHealthy(
                    currentTime: currentTime,
                    lastSendAttempt: lastSendAttempt,
                    lastSend: lastSend,
                    lastReceive: lastReceive,
                    firstSendSinceLastReceive: firstSendSinceLastReceive,
                    numberOfSendsSinceLastReceive: numberOfSendsSinceLastReceive,
                    transitTimeoutCounter: transitTimeoutCounter,
                    transitTimeoutWriteCounter: transitTimeoutWriteCounter,
                    socket: this.tcpClient.Client);
            }
        }

        public bool Disposed { get { return this.disposed; } }

        public sealed class ResponseMetadata : IDisposable
        {
            private bool disposed;

            private BufferProvider.DisposableBuffer header;
            private BufferProvider.DisposableBuffer metadata;

            public ResponseMetadata(BufferProvider.DisposableBuffer header, BufferProvider.DisposableBuffer metadata)
            {
                this.header = header;
                this.metadata = metadata;
                this.disposed = false;
            }

            public ArraySegment<byte> Header => this.header.Buffer;
            public ArraySegment<byte> Metadata => this.metadata.Buffer;

            /// <inheritdoc />
            public void Dispose()
            {
                if (!this.disposed)
                {
                    this.header.Dispose();
                    this.metadata.Dispose();
                    this.disposed = true;
                }
            }
        }

        public async Task OpenAsync(ChannelOpenArguments args)
        {
            this.ThrowIfDisposed();
            await this.OpenSocketAsync(args);
            await this.NegotiateSslAsync(args);
        }

        // This method is thread safe.
        public async Task WriteRequestAsync(ChannelCommonArguments args, 
                    TransportSerialization.SerializedRequest messagePayload,
                    TransportRequestStats transportRequestStats)
        {
            this.ThrowIfDisposed();

            if (transportRequestStats != null)
            {
                this.SnapshotConnectionTimestamps(
                    out DateTime lastSendAttempt,
                    out DateTime lastSend,
                    out DateTime lastReceive,
                    out DateTime? firstSendSinceLastReceive,
                    out long numberOfSendsSinceLastReceive);
                transportRequestStats.ConnectionLastSendAttemptTime = lastSendAttempt;
                transportRequestStats.ConnectionLastSendTime = lastSend;
                transportRequestStats.ConnectionLastReceiveTime = lastReceive;
            }

            args.SetTimeoutCode(TransportErrorCode.SendLockTimeout);
            await this.writeSemaphore.WaitAsync();
            try
            {
                args.SetTimeoutCode(TransportErrorCode.SendTimeout);
                args.SetPayloadSent();
                this.UpdateLastSendAttemptTime();
                await messagePayload.CopyToStreamAsync(this.stream);
            }
            finally
            {
                this.writeSemaphore.Release();
            }
            this.UpdateLastSendTime();
            // Do not update the last receive timestamp here. The fact that sending
            // the request succeeded means nothing until the response comes back.
        }

        /// <summary>
        /// Increments Transit Timeout counter.
        /// </summary>
        /// <param name="isReadOnly">is readonly flag.</param>
        public void IncrementTransitTimeoutCounter(
            bool isReadOnly)
        {
            Interlocked.Increment(ref this.transitTimeoutCounter);
            if (!isReadOnly)
            {
                Interlocked.Increment(ref this.transitTimeoutWriteCounter);
            }
        }

        /// <summary>
        /// Reset transit timeout.
        /// </summary>
        public void ResetTransitTimeout()
        {
            Debug.Assert(!Monitor.IsEntered(this.timestampLock));
            lock (this.timestampLock)
            {
                this.transitTimeoutCounter = 0;
                this.transitTimeoutWriteCounter = 0;
            }
        }

        // This method is not thread safe. ReadResponseMetadataAsync and
        // ReadResponseBodyAsync must be called in sequence, from a single thread.
        [SuppressMessage("", "AvoidMultiLineComments", Justification = "Multi line business logic")]
        public async Task<ResponseMetadata> ReadResponseMetadataAsync(ChannelCommonArguments args)
        {
            this.ThrowIfDisposed();

            Trace.CorrelationManager.ActivityId = args.ActivityId;
            int metadataHeaderLength = sizeof(UInt32) /* totalLength */ + sizeof(UInt32) /* status */ +
                           16;
            BufferProvider.DisposableBuffer header = this.BufferProvider.GetBuffer(metadataHeaderLength);
            await this.ReadPayloadAsync(header.Buffer.Array, metadataHeaderLength /* sizeof(Guid) */, "header", args);

            UInt32 totalLength = BitConverter.ToUInt32(header.Buffer.Array, 0);
            if (totalLength > Connection.ResponseLengthByteLimit)
            {
                header.Dispose();
                DefaultTrace.TraceCritical("RNTBD header length says {0} but expected at most {1} bytes. Connection: {2}",
                    totalLength, Connection.ResponseLengthByteLimit, this);
                throw TransportExceptions.GetInternalServerErrorException(
                    this.serverUri,
                    string.Format(
                        CultureInfo.CurrentUICulture,
                        RMResources.ServerResponseHeaderTooLargeError,
                        totalLength, this));
            }

            if (totalLength < metadataHeaderLength)
            {
                DefaultTrace.TraceCritical(
                    "Invalid RNTBD header length {0} bytes. Expected at least {1} bytes. Connection: {2}",
                    totalLength, metadataHeaderLength, this);
                throw TransportExceptions.GetInternalServerErrorException(
                    this.serverUri,
                    string.Format(
                        CultureInfo.CurrentUICulture,
                        RMResources.ServerResponseInvalidHeaderLengthError,
                        metadataHeaderLength, totalLength, this));
            }

            int metadataLength = (int)totalLength - metadataHeaderLength;
            BufferProvider.DisposableBuffer metadata = this.BufferProvider.GetBuffer(metadataLength);
            await this.ReadPayloadAsync(metadata.Buffer.Array, metadataLength, "metadata", args);
            return new ResponseMetadata(header, metadata);
        }

        // This method is not thread safe. ReadResponseMetadataAsync and
        // ReadResponseBodyAsync must be called in sequence, from a single thread.
        public async Task<MemoryStream> ReadResponseBodyAsync(ChannelCommonArguments args)
        {
            this.ThrowIfDisposed();

            Trace.CorrelationManager.ActivityId = args.ActivityId;
            using BufferProvider.DisposableBuffer bodyLengthHeader = this.BufferProvider.GetBuffer(sizeof(uint));
            await this.ReadPayloadAsync(bodyLengthHeader.Buffer.Array, sizeof(uint),
                "body length header", args);

            uint length = BitConverter.ToUInt32(bodyLengthHeader.Buffer.Array, 0);
            // This check can also validate "length" against the expected total
            // response size.
            if (length > Connection.ResponseLengthByteLimit)
            {
                DefaultTrace.TraceCritical("Invalid RNTBD response body length {0} bytes. Connection: {1}", length, this);
                throw TransportExceptions.GetInternalServerErrorException(
                    this.serverUri,
                    string.Format(
                        CultureInfo.CurrentUICulture,
                        RMResources.ServerResponseBodyTooLargeError,
                        length, this));
            }

            MemoryStream memoryStream = null;
            if (this.memoryStreamPool?.TryGetMemoryStream((int)length, out memoryStream) ?? false)
            {
                await this.ReadPayloadAsync(memoryStream, (int)length, "body", args);
                memoryStream.Position = 0;
                return memoryStream;
            }
            else
            {
                byte[] body = new byte[length];
                await this.ReadPayloadAsync(body, (int)length, "body", args);
                return StreamExtension.CreateExportableMemoryStream(body);
            }
        }

        public override string ToString()
        {
            lock (this.nameLock)
            {
                return this.name;
            }
        }

        public void Dispose()
        {
            this.ThrowIfDisposed();
            this.disposed = true;

            string connectionTimestampsText = this.GetConnectionTimestampsText();
            if (this.tcpClient != null)
            {
                Debug.Assert(this.tcpClient.Client != null);
                DefaultTrace.TraceInformation(
                    "Disposing RNTBD connection {0} -> {1} to server {2}. {3}",
                    this.localEndPoint,
                    this.remoteEndPoint,
                    this.serverUri, connectionTimestampsText);
                string newName = string.Format(
                    CultureInfo.InvariantCulture,
                    "<disconnected> {0} -> {1}",
                    this.localEndPoint,
                    this.remoteEndPoint);
                lock (this.nameLock)
                {
                    this.name = newName;
                }
            }
            else
            {
                DefaultTrace.TraceInformation(
                    "Disposing unused RNTBD connection to server {0}. {1}",
                    this.serverUri, connectionTimestampsText);
            }

            if (this.tcpClient != null)
            {
                if (this.portPool != null)
                {
                    this.portPool.RemoveReference(this.localEndPoint.AddressFamily, checked((ushort)this.localEndPoint.Port));
                }
                this.tcpClient.Close();

                Interlocked.Decrement(ref Connection.numberOfOpenTcpConnections);

                this.tcpClient = null;
                Debug.Assert(this.stream != null);
                this.stream.Close();
                this.streamReader?.Dispose();
                TransportClient.GetTransportPerformanceCounters().IncrementRntbdConnectionClosedCount();
            }
        }

        // Returns (true, timeToIdle) for active connections and (false, idleTimeout)
        // for idle connections.
        // timeToIdle is the minimum amount of time that will pass until the
        // connection might become idle.
        public bool IsActive(out TimeSpan timeToIdle)
        {
            this.ThrowIfDisposed();

            // IsActive should not be called if idle timeout is disabled
            Debug.Assert(this.idleConnectionTimeout > TimeSpan.Zero);

            this.SnapshotConnectionTimestamps(
                out DateTime lastSendAttempt,
                out DateTime lastSend,
                out DateTime lastReceive,
                out DateTime? firstSendSinceLastReceive,
                out long numberOfSendsSinceLastReceive);
            DateTime now = DateTime.UtcNow;

            if (now - lastReceive > this.idleConnectionTimeout)
            {
                // idle
                timeToIdle = this.idleConnectionClosureTimeout;
                return false;
            }
            else
            {
                // 'not idle' guarantees lastReceiveTime is a non-default value.
                Debug.Assert(lastReceive != DateTime.MinValue);

                timeToIdle = lastReceive + this.idleConnectionClosureTimeout - now;

                // 'not idle' guarantees 'now < lastReceive + idleConnectionTimeout', so that the new timeToIdle is guaranteed to be positive
                Debug.Assert(timeToIdle > TimeSpan.Zero);

                return true;
            }
        }

        #region Test hook.

        internal TimeSpan TestIdleConnectionClosureTimeout => this.idleConnectionClosureTimeout;
        internal void TestSetLastReceiveTime(DateTime lrt)
        {
            lock (this.timestampLock)
            {
                this.lastReceiveTime = lrt;
            }
        }

        #endregion

        private static uint GetUInt32FromEnvironmentVariableOrDefault(
            string name,
            uint minValue,
            uint maxValue,
            uint defaultValue)
        {
            string envVariableValueText = Environment.GetEnvironmentVariable(name);

            if (String.IsNullOrEmpty(envVariableValueText)  ||
                !UInt32.TryParse(
                    envVariableValueText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out uint envVariableValue))
            {
                return defaultValue;
            }

            if (envVariableValue > maxValue || envVariableValue < minValue)
            {
                throw new ArgumentOutOfRangeException(
                    name,
                    $"Value for environment variable '{name}' is outside expected range of {minValue} - {maxValue}.");
            }

            return envVariableValue;
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                Debug.Assert(this.serverUri != null);
                throw new ObjectDisposedException(
                    string.Format("{0}:{1}", nameof(Connection), this.serverUri));
            }
        }

        private async Task OpenSocketAsync(ChannelOpenArguments args)
        {
            if (this.tcpClient != null)
            {
                throw new InvalidOperationException(
                    $"Attempting to call Connection.OpenSocketAsync on an " +
                    $"already initialized connection {this}");
            }

            TcpClient tcpClient = null;
            TransportErrorCode errorCode = TransportErrorCode.Unknown;
            try
            {
                errorCode = TransportErrorCode.DnsResolutionFailed;
                args.CommonArguments.SetTimeoutCode(
                    TransportErrorCode.DnsResolutionTimeout);
                IPAddress address = await this.dnsResolutionFunction(this.serverUri.DnsSafeHost);

                errorCode = TransportErrorCode.ConnectFailed;
                args.CommonArguments.SetTimeoutCode(TransportErrorCode.ConnectTimeout);

                this.UpdateLastSendAttemptTime();

                DefaultTrace.TraceInformation("Port reuse mode: {0}. Connection: {1}", args.PortReuseMode, this);
                switch (args.PortReuseMode)
                {
                    case PortReuseMode.ReuseUnicastPort:
                        tcpClient = await Connection.ConnectUnicastPortAsync(this.serverUri, address);
                        break;

                    case PortReuseMode.PrivatePortPool:
                        Tuple<TcpClient, bool> result = await Connection.ConnectUserPortAsync(this.serverUri, address, args.PortPool, this.ToString());
                        tcpClient = result.Item1;
                        bool portPoolUsed = result.Item2;
                        if (portPoolUsed)
                        {
                            Debug.Assert(this.portPool == null);
                            this.portPool = args.PortPool;
                        }
                        else
                        {
                            DefaultTrace.TraceInformation("PrivatePortPool: Configured but actually not used. Connection: {0}", this);
                        }

                        break;

                    default:
                        throw new ArgumentException(
                            string.Format(
                                "Unsupported port reuse policy {0}",
                                args.PortReuseMode.ToString()));
                }

                this.UpdateLastSendTime();
                this.UpdateLastReceiveTime();
                args.OpenTimeline.RecordConnectFinishTime();

                DefaultTrace.TraceInformation("RNTBD connection established {0} -> {1}",
                    tcpClient.Client.LocalEndPoint, tcpClient.Client.RemoteEndPoint);
                TransportClient.GetTransportPerformanceCounters().IncrementRntbdConnectionEstablishedCount();
                string newName = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} -> {1}",
                    tcpClient.Client.LocalEndPoint,
                    tcpClient.Client.RemoteEndPoint);
                lock (this.nameLock)
                {
                    this.name = newName;
                }
            }
            catch (Exception ex)
            {
                // Dispose the socket eagerly to avoid keeping the underlying
                // handle around until finalization.
                tcpClient?.Close();

#if NETFX
                SocketException socketEx = ex as SocketException;
                if (socketEx != null && socketEx.SocketErrorCode == SocketError.TimedOut)
                {
                    if (PerfCounters.Counters.BackendConnectionOpenFailuresDueToSynRetransmitPerSecond != null)
                    {
                        PerfCounters.Counters.BackendConnectionOpenFailuresDueToSynRetransmitPerSecond.Increment();
                    }
                }
#endif

                DefaultTrace.TraceInformation(
                    "Connection.OpenSocketAsync failed. Converting to TransportException. " +
                    "Connection: {0}. Inner exception: {1}", this, ex);
                Debug.Assert(errorCode != TransportErrorCode.Unknown);
                Debug.Assert(!args.CommonArguments.UserPayload);
                Debug.Assert(!args.CommonArguments.PayloadSent);
                throw new TransportException(errorCode, ex,
                    args.CommonArguments.ActivityId, this.serverUri,
                    this.ToString(), args.CommonArguments.UserPayload,
                    args.CommonArguments.PayloadSent);
            }

            Debug.Assert(tcpClient != null);
            this.localEndPoint = (IPEndPoint)tcpClient.Client.LocalEndPoint;
            this.remoteEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
            this.tcpClient = tcpClient;
            this.stream = tcpClient.GetStream();

            Interlocked.Increment(ref Connection.numberOfOpenTcpConnections);

            // Per MSDN, "The Blocking property has no effect on asynchronous methods"
            // (https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socket.blocking),
            // but we also try to get the health status of the socket with a 
            // non-blocking, zero-byte Send.
            this.tcpClient.Client.Blocking = false;
        }

        private async Task NegotiateSslAsync(ChannelOpenArguments args)
        {
            string host = this.hostNameCertificateOverride ?? this.serverUri.DnsSafeHost;
            Debug.Assert(this.stream != null);
            SslStream sslStream = new SslStream(this.stream, leaveInnerStreamOpen: false, userCertificateValidationCallback: this.remoteCertificateValidationCallback);
            try
            {
                args.CommonArguments.SetTimeoutCode(
                    TransportErrorCode.SslNegotiationTimeout);
                this.UpdateLastSendAttemptTime();

                await sslStream.AuthenticateAsClientAsync(host, clientCertificates: null,
                    enabledSslProtocols: Connection.TlsProtocols, checkCertificateRevocation: false);

                this.UpdateLastSendTime();
                this.UpdateLastReceiveTime();
                args.OpenTimeline.RecordSslHandshakeFinishTime();

                this.stream = sslStream;
                this.streamReader = new RntbdStreamReader(this.stream);
                Debug.Assert(this.tcpClient != null);
                DefaultTrace.TraceInformation("RNTBD SSL handshake complete {0} -> {1}",
                    this.localEndPoint, this.remoteEndPoint);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceInformation(
                    "Connection.NegotiateSslAsync failed. Converting to TransportException. " +
                    "Connection: {0}. Inner exception: {1}", this, ex);
                Debug.Assert(!args.CommonArguments.UserPayload);
                Debug.Assert(!args.CommonArguments.PayloadSent);
                throw new TransportException(
                    TransportErrorCode.SslNegotiationFailed, ex,
                    args.CommonArguments.ActivityId, this.serverUri,
                    this.ToString(), args.CommonArguments.UserPayload,
                    args.CommonArguments.PayloadSent);
            }
        }

        private async Task ReadPayloadAsync(
            byte[] payload,
            int length,
            string type,
            ChannelCommonArguments args)
        {
            Debug.Assert(length > 0);
            Debug.Assert(length <= Connection.ResponseLengthByteLimit);
            int bytesRead = 0;
            while (bytesRead < length)
            {
                int read = 0;
                try
                {
                    read = await this.streamReader.ReadAsync(payload, bytesRead,
                        length - bytesRead);
                }
                catch (IOException ex)
                {
                    this.TraceAndThrowReceiveFailedException(ex, type, args);
                }

                if (read == 0)
                {
                    this.TraceAndThrowEndOfStream(type, args);
                }
                this.UpdateLastReceiveTime();
                bytesRead += read;
            }
            Debug.Assert(bytesRead == length);
            Debug.Assert(length <= payload.Length);
        }

        private async Task ReadPayloadAsync(
            MemoryStream payload,
            int length,
            string type,
            ChannelCommonArguments args)
        {
            Debug.Assert(length > 0);
            Debug.Assert(length <= Connection.ResponseLengthByteLimit);
            int bytesRead = 0;
            while (bytesRead < length)
            {
                int read = 0;
                try
                {
                    read = await this.streamReader.ReadAsync(payload, length - bytesRead);
                }
                catch (IOException ex)
                {
                    this.TraceAndThrowReceiveFailedException(ex, type, args);
                }

                if (read == 0)
                {
                    this.TraceAndThrowEndOfStream(type, args);
                }
                this.UpdateLastReceiveTime();
                bytesRead += read;
            }
            Debug.Assert(bytesRead == length);
            Debug.Assert(length <= payload.Length);
        }

        private void TraceAndThrowReceiveFailedException(IOException e, string type, ChannelCommonArguments args)
        {
            DefaultTrace.TraceError(
                "Hit IOException {0} with HResult {1} while reading {2} on connection {3}. {4}",
                e.Message,
                e.HResult,
                type,
                this,
                this.GetConnectionTimestampsText());
            throw new TransportException(
                TransportErrorCode.ReceiveFailed, e, args.ActivityId,
                this.serverUri, this.ToString(), args.UserPayload, true);
        }

        private void TraceAndThrowEndOfStream(string type, ChannelCommonArguments args)
        {
            DefaultTrace.TraceError(
                        "Reached end of stream. Read 0 bytes while reading {0} " +
                        "on connection {1}. {2}",
                        type, this, this.GetConnectionTimestampsText());
            throw new TransportException(
                TransportErrorCode.ReceiveStreamClosed, null,
                args.ActivityId, this.serverUri, this.ToString(),
                args.UserPayload, true);
        }

        private void SnapshotConnectionTimestamps(
            out DateTime lastSendAttempt,
            out DateTime lastSend,
            out DateTime lastReceive,
            out DateTime? firstSendSinceLastReceive,
            out long numberOfSendsSinceLastReceive)
        {
            Debug.Assert(!Monitor.IsEntered(this.timestampLock));
            lock (this.timestampLock)
            {
                lastSendAttempt = this.lastSendAttemptTime;
                lastSend = this.lastSendTime;
                lastReceive = this.lastReceiveTime;
                firstSendSinceLastReceive = 
                    this.lastReceiveTime < this.firstSendSinceLastReceive ? this.firstSendSinceLastReceive : null;
                numberOfSendsSinceLastReceive = this.numberOfSendsSinceLastReceive;
            }
        }

        private void SnapshotTransitTimeoutCounters(
            out int transitTimeoutCounter,
            out int transitTimeoutWriteCounter)
        {
            Debug.Assert(!Monitor.IsEntered(this.timestampLock));
            lock (this.timestampLock)
            {
                transitTimeoutCounter = this.transitTimeoutCounter;
                transitTimeoutWriteCounter = this.transitTimeoutWriteCounter;
            }
        }

        private string GetConnectionTimestampsText()
        {
            this.SnapshotConnectionTimestamps(
                out DateTime lastSendAttempt,
                out DateTime lastSend,
                out DateTime lastReceive,
                out DateTime? firstSendSinceLastReceive,
                out long numberOfSendsSinceLastReceive);
            return string.Format(
                CultureInfo.InvariantCulture,
                "Last send attempt time: {0:o}. Last send time: {1:o}. " +
                "Last receive time: {2:o}. First sends since last receieve: {3:o}. " +
                "# of sends since last receive: {4}",
                lastSendAttempt, lastSend, lastReceive, firstSendSinceLastReceive, numberOfSendsSinceLastReceive);
        }

        private void UpdateLastSendAttemptTime()
        {
            Debug.Assert(!Monitor.IsEntered(this.timestampLock));
            lock (this.timestampLock)
            {
                this.lastSendAttemptTime = DateTime.UtcNow;
            }
        }

        private void UpdateLastSendTime()
        {
            Debug.Assert(!Monitor.IsEntered(this.timestampLock));
            lock (this.timestampLock)
            {
                this.lastSendTime = DateTime.UtcNow;

                if (this.numberOfSendsSinceLastReceive++ == 0)
                {
                    this.firstSendSinceLastReceive = this.lastSendTime;
                }
            }
        }

        private void UpdateLastReceiveTime()
        {
            Debug.Assert(!Monitor.IsEntered(this.timestampLock));
            lock (this.timestampLock)
            {
                this.numberOfSendsSinceLastReceive = 0;
                this.lastReceiveTime = DateTime.UtcNow;
            }
        }

        private static async Task<TcpClient> ConnectUnicastPortAsync(Uri serverUri, IPAddress resolvedAddress)
        {
            TcpClient tcpClient = new TcpClient(resolvedAddress.AddressFamily);

            Connection.SetCommonSocketOptions(tcpClient.Client);

            Connection.SetReuseUnicastPort(tcpClient.Client);

            DefaultTrace.TraceInformation("RNTBD: {0} connecting to {1} (address {2})",
                nameof(ConnectUnicastPortAsync), serverUri, resolvedAddress);

            await tcpClient.ConnectAsync(resolvedAddress, serverUri.Port);

            return tcpClient;
        }

        private static async Task<Tuple<TcpClient, bool>> ConnectReuseAddrAsync(
            Uri serverUri, IPAddress address, ushort candidatePort)
        {
            TcpClient candidateClient = new TcpClient(address.AddressFamily);
            TcpClient client = null;
            try
            {
                Connection.SetCommonSocketOptions(candidateClient.Client);

                candidateClient.Client.SetSocketOption(
                    SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                EndPoint bindEndpoint = null;
                switch (address.AddressFamily)
                {
                    case AddressFamily.InterNetwork:
                        bindEndpoint = new IPEndPoint(IPAddress.Any, candidatePort);
                        break;

                    case AddressFamily.InterNetworkV6:
                        bindEndpoint = new IPEndPoint(IPAddress.IPv6Any, candidatePort);
                        break;

                    default:
                        throw new NotSupportedException(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Address family {0} not supported",
                                address.AddressFamily));
                }

                DefaultTrace.TraceInformation(
                    "RNTBD: {0} binding local endpoint {1}",
                    nameof(ConnectReuseAddrAsync), bindEndpoint);

                try
                {
                    Debug.Assert(bindEndpoint != null);
                    candidateClient.Client.Bind(bindEndpoint);
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.AccessDenied)
                    {
                        Debug.Assert(candidateClient != null);
                        return Tuple.Create<TcpClient, bool>(null, false);
                    }
                    else
                    {
                        throw;
                    }
                }

                DefaultTrace.TraceInformation("RNTBD: {0} connecting to {1} (address {2})",
                    nameof(ConnectReuseAddrAsync), serverUri, address);

                try
                {
                    await candidateClient.ConnectAsync(address, serverUri.Port);
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        return Tuple.Create<TcpClient, bool>(null, true);
                    }
                    else
                    {
                        throw;
                    }
                }

                client = candidateClient;
                candidateClient = null;
            }
            finally
            {
                if (candidateClient != null)
                {
                    Debug.Assert(client == null);
                    candidateClient.Close();
                }
            }
            Debug.Assert(candidateClient == null);
            return Tuple.Create(client, true);
        }

        private static async Task<Tuple<TcpClient, bool>> ConnectUserPortAsync(
            Uri serverUri, IPAddress address, UserPortPool portPool, string connectionName)
        {
            ushort[] candidatePorts = portPool.GetCandidatePorts(address.AddressFamily);
            if (candidatePorts != null)
            {
                foreach (ushort candidatePort in candidatePorts)
                {
                    Debug.Assert(candidatePort != 0);
                    Tuple<TcpClient, bool> result =
                        await Connection.ConnectReuseAddrAsync(
                            serverUri, address, candidatePort);
                    TcpClient portReuseClient = result.Item1;
                    bool portUsable = result.Item2;
                    if (portReuseClient != null)
                    {
                        ushort localPort = checked((ushort)((IPEndPoint)portReuseClient.Client.LocalEndPoint).Port);
                        Debug.Assert(localPort == candidatePort);
                        portPool.AddReference(
                            address.AddressFamily, localPort);
                        return Tuple.Create(portReuseClient, true);
                    }
                    if (!portUsable)
                    {
                        portPool.MarkUnusable(address.AddressFamily, candidatePort);
                    }
                }

                DefaultTrace.TraceInformation("PrivatePortPool: All {0} candidate ports have been tried but none connects. Connection: {1}", candidatePorts.Length, connectionName);
            }

            Tuple<TcpClient, bool> wildcardResult = await Connection.ConnectReuseAddrAsync(serverUri, address, 0);
            TcpClient wildcardClient = wildcardResult.Item1;
            if (wildcardClient != null)
            {
                portPool.AddReference(
                    address.AddressFamily,
                    checked((ushort)((IPEndPoint)wildcardClient.Client.LocalEndPoint).Port));
                return Tuple.Create(wildcardClient, true);
            }

            DefaultTrace.TraceInformation(
                "PrivatePortPool: Not enough reusable ports in the system or pool. Have to connect unicast port. Pool status: {0}. Connection: {1}",
                portPool.DumpStatus(), connectionName);
            return Tuple.Create(await Connection.ConnectUnicastPortAsync(serverUri, address), false);
        }

        internal static async Task<IPAddress> ResolveHostAsync(string hostName)
        {
            IPAddress[] serverAddresses = await Dns.GetHostAddressesAsync(hostName);
            int addressIndex = 0;
            if (serverAddresses.Length > 1)
            {
                addressIndex = Connection.rng.Value.Next(serverAddresses.Length);
            }
            return serverAddresses[addressIndex];
        }

        private static void SetCommonSocketOptions(Socket clientSocket)
        {
            clientSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            Connection.EnableTcpKeepAlive(clientSocket);
        }

        private static void EnableTcpKeepAlive(Socket clientSocket)
        {
            clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

#if !NETSTANDARD15 && !NETSTANDARD16
            // This code should use RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
            // but the feature is unavailable on .NET Framework 4.5.1.
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                try
                {
                    clientSocket.IOControl(
                        IOControlCode.KeepAliveValues,
                        Connection.keepAliveConfiguration.Value,
                        null);
                }
                catch (Exception e)
                {
                    DefaultTrace.TraceWarning("IOControl(KeepAliveValues) failed: {0}", e);
                    // Ignore the exception.
                }
            }
            else
            {
                Connection.SetKeepAliveSocketOptions(clientSocket);
            }
#else
            Connection.SetKeepAliveSocketOptions(clientSocket);
#endif
        }

        private static void SetKeepAliveSocketOptions(Socket clientSocket)
        {
            if (Connection.isKeepAliveCustomizationSupported.Value)
            {
                //SocketOptionName.TcpKeepAliveInterval
                clientSocket.SetSocketOption(SocketOptionLevel.Tcp, 
                                            (SocketOptionName)TcpKeepAliveIntervalSocketOptionEnumValue, 
                                            SocketOptionTcpKeepAliveInterval);

                //SocketOptionName.TcpKeepAliveTime
                clientSocket.SetSocketOption(SocketOptionLevel.Tcp, 
                                            (SocketOptionName)TcpKeepAliveTimeSocketOptionEnumValue,
                                            SocketOptionTcpKeepAliveTime);
            }
#if DEBUG
            int tcpKeepAliveInterval = (int)clientSocket.GetSocketOption(SocketOptionLevel.Tcp,
                                                                        (SocketOptionName)TcpKeepAliveIntervalSocketOptionEnumValue);
            int tcpKeepAliveTime = (int)clientSocket.GetSocketOption(SocketOptionLevel.Tcp, 
                                                                    (SocketOptionName)TcpKeepAliveTimeSocketOptionEnumValue);
            Debug.Equals(tcpKeepAliveInterval, SocketOptionTcpKeepAliveInterval);
            Debug.Equals(tcpKeepAliveTime, SocketOptionTcpKeepAliveTime);
#endif
        }

        private static bool IsKeepAliveCustomizationSupported()
        {
            // Check to see if the SetSocketOptionsMethod does not throw
            try
            {
                using (Socket dummySocket = new Socket(SocketType.Stream, ProtocolType.Tcp))
                {
                    dummySocket.SetSocketOption(SocketOptionLevel.Tcp,
                           (SocketOptionName)TcpKeepAliveIntervalSocketOptionEnumValue,
                           SocketOptionTcpKeepAliveInterval);
                    dummySocket.SetSocketOption(SocketOptionLevel.Tcp,
                                                (SocketOptionName)TcpKeepAliveTimeSocketOptionEnumValue,
                                                SocketOptionTcpKeepAliveTime);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static byte[] GetWindowsKeepAliveConfiguration()
        {
            const uint EnableKeepAlive = 1;
            const uint KeepAliveIntervalMs = 30 * 1000;
            const uint KeepAliveRetryIntervalMs = 1 * 1000;

            //  struct tcp_keepalive
            //  {
            //      u_long  onoff;
            //      u_long  keepalivetime;
            //      u_long  keepaliveinterval;
            //  };
            byte[] keepAliveConfig = new byte[3 * sizeof(uint)];
            BitConverter.GetBytes(EnableKeepAlive).CopyTo(keepAliveConfig, 0);
            BitConverter.GetBytes(KeepAliveIntervalMs).CopyTo(keepAliveConfig, sizeof(uint));
            BitConverter.GetBytes(KeepAliveRetryIntervalMs).CopyTo(keepAliveConfig, 2 * sizeof(uint));
            return keepAliveConfig;
        }

        private static void SetReuseUnicastPort(Socket clientSocket)
        {
#if !NETSTANDARD15 && !NETSTANDARD16
            // This code should use RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
            // but the feature is unavailable on .NET Framework 4.5.1.
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                try
                {
                    Debug.Assert(!clientSocket.IsBound);
                    // SocketOptionName.ReuseUnicastPort is only present in .NET Framework 4.6.1 and newer.
                    // Use the numeric value for as long as this code needs to target earlier versions.
                    const int SO_REUSE_UNICASTPORT = 0x3007;
                    clientSocket.SetSocketOption(SocketOptionLevel.Socket, (SocketOptionName)SO_REUSE_UNICASTPORT, true);
                }
                catch (Exception e)
                {
                    DefaultTrace.TraceWarning("SetSocketOption(Socket, ReuseUnicastPort) failed: {0}", e);
                    // Ignore the exception.
                }
            }
#endif  // !NETSTANDARD15 && !NETSTANDARD16
        }
    }
}
