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

#if NETSTANDARD15 || NETSTANDARD16
    using Trace = Microsoft.Azure.Documents.Trace;
#endif

    // Connection encapsulates the TCP connection to one back-end, and all surrounding
    // mechanisms (SSL stream, connection state).
    internal sealed class Connection : IDisposable
    {
        private const int ResponseLengthByteLimit = int.MaxValue;
        private const SslProtocols TlsProtocols = SslProtocols.Tls12;

        private static readonly Lazy<ConcurrentPrng> rng =
            new Lazy<ConcurrentPrng>(LazyThreadSafetyMode.ExecutionAndPublication);
        private static readonly Lazy<byte[]> keepAliveConfiguration =
            new Lazy<byte[]>(Connection.GetWindowsKeepAliveConfiguration,
                LazyThreadSafetyMode.ExecutionAndPublication);

        private static readonly byte[] healthCheckBuffer = new byte[1];
        private static readonly TimeSpan recentReceiveWindow = TimeSpan.FromSeconds(1.0);

        // The connection should not declare itself unhealthy if a send was
        // attempted very recently. As such, ignore
        // (lastSendAttemptTime - lastSendTime) gaps lower than sendHangGracePeriod.
        // The grace period should be large enough to accommodate slow sends.
        // In effect, a setting of 2s requires the client to be able to send
        // data at least at 1 MB/s for 2 MB documents.
        private static readonly TimeSpan sendHangGracePeriod = TimeSpan.FromSeconds(2.0);

        // The connection should not declare itself unhealthy if a send
        // succeeded very recently. As such, ignore
        // (lastSendTime - lastReceiveTime) gaps lower than receiveHangGracePeriod.
        // The grace period should be large enough to accommodate the round trip
        // time of the slowest server request. Assuming 1s of network RTT,
        // a 2 MB request, a 2 MB response, a connection that can sustain
        // 1 MB/s both ways, and a 5-second deadline at the server, 10 seconds
        // should be enough.
        private static readonly TimeSpan receiveHangGracePeriod = TimeSpan.FromSeconds(10.0);

        private readonly Uri serverUri;
        private readonly string hostNameCertificateOverride;

        // The connection will declare itself unhealthy if the
        // (lastSendTime - lastReceiveTime) gap grows beyond this value.
        // receiveDelayLimit must be greater than receiveHangGracePeriod.
        private readonly TimeSpan receiveDelayLimit;

        // The connection will declare itself unhealthy if the
        // (lastSendAttemptTime - lastSendTime) gap grows beyond this value.
        // sendDelayLimit must be greater than sendHangGracePeriod.
        private readonly TimeSpan sendDelayLimit;

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

        // Only one task may write to the stream at once. Reads don't need
        // mutual exclusion because only one thread consumes from the stream.
        private readonly SemaphoreSlim writeSemaphore = new SemaphoreSlim(1);
        private Stream stream;

        private readonly object timestampLock = new object();
        private DateTime lastSendAttemptTime;  // Guarded by timestampLock.
        private DateTime lastSendTime;  // Guarded by timestampLock.
        private DateTime lastReceiveTime;  // Guarded by timestampLock.

        private readonly object nameLock = new object();  // Acquired after timestampLock.
        private string name;  // Guarded by nameLock.

        public Connection(
            Uri serverUri,
            string hostNameCertificateOverride,
            TimeSpan receiveHangDetectionTime,
            TimeSpan sendHangDetectionTime,
            TimeSpan idleTimeout)
        {
            Debug.Assert(serverUri.PathAndQuery.Equals("/"), serverUri.AbsoluteUri,
                "The server URI must not specify a path and query");
            this.serverUri = serverUri;
            this.hostNameCertificateOverride = hostNameCertificateOverride;

            if (receiveHangDetectionTime <= Connection.receiveHangGracePeriod)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(receiveHangDetectionTime),
                    receiveHangDetectionTime,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} must be greater than {1} ({2})",
                        nameof(receiveHangDetectionTime),
                        nameof(Connection.receiveHangGracePeriod),
                        Connection.receiveHangGracePeriod));
            }
            this.receiveDelayLimit = receiveHangDetectionTime;
            if (sendHangDetectionTime <= Connection.sendHangGracePeriod)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sendHangDetectionTime),
                    sendHangDetectionTime,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} must be greater than {1} ({2})",
                        nameof(sendHangDetectionTime),
                        nameof(Connection.sendHangGracePeriod),
                        Connection.sendHangGracePeriod));
            }
            this.sendDelayLimit = sendHangDetectionTime;

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
        }

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
                DateTime lastSendAttempt, lastSend, lastReceive, now;
                this.SnapshotConnectionTimestamps(
                    out lastSendAttempt, out lastSend, out lastReceive);
                now = DateTime.UtcNow;
                // Assume that the connection is healthy if data was received
                // recently.
                if (now - lastReceive < Connection.recentReceiveWindow)
                {
                    return true;
                }
                // Black hole detection, part 1:
                // Treat the connection as unhealthy if the gap between the last
                // attempted send and the last successful send grew beyond
                // acceptable limits, unless a send was attempted very recently.
                // This is a sign of a hung send().
                if ((lastSendAttempt - lastSend > this.sendDelayLimit) &&
                    (now - lastSendAttempt > Connection.sendHangGracePeriod))
                {
                    DefaultTrace.TraceWarning(
                        "Unhealthy RNTBD connection: Hung send: {0}. " +
                        "Last send attempt: {1:o}. Last send: {2:o}. " +
                        "Tolerance {3:c}",
                        this, lastSendAttempt, lastSend, this.sendDelayLimit);
                    return false;
                }
                // Black hole detection, part 2:
                // Treat the connection as unhealthy if the gap between the last
                // successful send and the last successful receive grew beyond
                // acceptable limits, unless a send succeeded very recently.
                if ((lastSend - lastReceive > this.receiveDelayLimit) &&
                    (now - lastSend > Connection.receiveHangGracePeriod))
                {
                    DefaultTrace.TraceWarning(
                        "Unhealthy RNTBD connection: Replies not getting back: {0}. " +
                        "Last send: {1:o}. Last receive: {2:o}. Tolerance: {3:c}",
                        this, lastSend, lastReceive, this.receiveDelayLimit);
                    return false;
                }

                if (this.idleConnectionTimeout > TimeSpan.Zero)
                {
                    // idle timeout is enabled
                    if (now - lastReceive > this.idleConnectionTimeout)
                    {
                        return false;
                    }
                }

                // See https://aka.ms/zero-byte-send.
                // Socket.Send is expensive. Keep this operation last in the chain
                try
                {
                    Socket socket = this.tcpClient.Client;
                    if (socket == null || !socket.Connected)
                    {
                        return false;
                    }
                    Debug.Assert(!socket.Blocking);
                    socket.Send(Connection.healthCheckBuffer, 0, 0);
                    return true;
                }
                catch (SocketException e)
                {
                    bool healthy = (e.SocketErrorCode == SocketError.WouldBlock);
                    if (!healthy)
                    {
                        DefaultTrace.TraceWarning(
                            "Unhealthy RNTBD connection. Socket error code: {0}",
                            e.SocketErrorCode.ToString());
                    }
                    return healthy;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }
        }

        public bool Disposed { get { return this.disposed; } }

        public struct ResponseMetadata
        {
            public byte[] Header;
            public byte[] Metadata;

            public ResponseMetadata(byte[] header, byte[] metadata)
            {
                this.Header = header;
                this.Metadata = metadata;
            }
        }

        public async Task OpenAsync(ChannelOpenArguments args)
        {
            this.ThrowIfDisposed();
            await this.OpenSocketAsync(args);
            await this.NegotiateSslAsync(args);
        }

        // This method is thread safe.
        public async Task WriteRequestAsync(ChannelCommonArguments args, byte[] messagePayload)
        {
            this.ThrowIfDisposed();

            args.SetTimeoutCode(TransportErrorCode.SendLockTimeout);
            await this.writeSemaphore.WaitAsync();
            try
            {
                args.SetTimeoutCode(TransportErrorCode.SendTimeout);
                args.SetPayloadSent();
                this.UpdateLastSendAttemptTime();
                await this.stream.WriteAsync(messagePayload, 0, messagePayload.Length);
            }
            finally
            {
                this.writeSemaphore.Release();
            }
            this.UpdateLastSendTime();
            // Do not update the last receive timestamp here. The fact that sending
            // the request succeeded means nothing until the response comes back.
        }

        // This method is not thread safe. ReadResponseMetadataAsync and
        // ReadResponseBodyAsync must be called in sequence, from a single thread.
        [SuppressMessage("", "AvoidMultiLineComments", Justification = "Multi line business logic")]
        public async Task<ResponseMetadata> ReadResponseMetadataAsync(ChannelCommonArguments args)
        {
            this.ThrowIfDisposed();

            Trace.CorrelationManager.ActivityId = args.ActivityId;
            byte[] header = await this.ReadPayloadAsync(
                sizeof(UInt32) /* totalLength */ + sizeof(UInt32) /* status */ +
                16 /* sizeof(Guid) */, "header", args);

            UInt32 totalLength = BitConverter.ToUInt32(header, 0);
            if (totalLength > Connection.ResponseLengthByteLimit)
            {
                DefaultTrace.TraceCritical("RNTBD header length says {0} but expected at most {1} bytes. Connection: {2}",
                    totalLength, Connection.ResponseLengthByteLimit, this);
                throw TransportExceptions.GetInternalServerErrorException(
                    this.serverUri,
                    string.Format(
                        CultureInfo.CurrentUICulture,
                        RMResources.ServerResponseHeaderTooLargeError,
                        totalLength, this));
            }

            if (totalLength < header.Length)
            {
                DefaultTrace.TraceCritical(
                    "Invalid RNTBD header length {0} bytes. Expected at least {1} bytes. Connection: {2}",
                    totalLength, header.Length, this);
                throw TransportExceptions.GetInternalServerErrorException(
                    this.serverUri,
                    string.Format(
                        CultureInfo.CurrentUICulture,
                        RMResources.ServerResponseInvalidHeaderLengthError,
                        header.Length, totalLength, this));
            }

            int metadataLength = (int) totalLength - header.Length;
            byte[] metadata = await this.ReadPayloadAsync(metadataLength, "metadata", args);
            return new ResponseMetadata(header, metadata);
        }

        // This method is not thread safe. ReadResponseMetadataAsync and
        // ReadResponseBodyAsync must be called in sequence, from a single thread.
        public async Task<byte[]> ReadResponseBodyAsync(ChannelCommonArguments args)
        {
            this.ThrowIfDisposed();

            Trace.CorrelationManager.ActivityId = args.ActivityId;
            byte[] bodyLengthHeader = await this.ReadPayloadAsync(sizeof(uint),
                "body length header", args);

            uint length = BitConverter.ToUInt32(bodyLengthHeader, 0);
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
            byte[] body = await this.ReadPayloadAsync((int) length, "body", args);
            return body;
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
                this.tcpClient = null;
                Debug.Assert(this.stream != null);
                this.stream.Close();
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

            DateTime lastSendAttempt, lastSend, lastReceive;
            this.SnapshotConnectionTimestamps(out lastSendAttempt, out lastSend, out lastReceive);
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
                IPAddress address = await Connection.ResolveHostAsync(this.serverUri.DnsSafeHost);

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
            SslStream sslStream = new SslStream(this.stream, leaveInnerStreamOpen: false);
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

        private async Task<byte[]> ReadPayloadAsync(
            int length, string type, ChannelCommonArguments args)
        {
            Debug.Assert(length > 0);
            Debug.Assert(length <= Connection.ResponseLengthByteLimit);
            byte[] payload = new byte[length];
            int bytesRead = 0;
            while (bytesRead < length)
            {
                int read = 0;
                try
                {
                    read = await this.stream.ReadAsync(payload, bytesRead,
                        length - bytesRead);
                }
                catch (IOException ex)
                {
                    DefaultTrace.TraceError(
                        "Hit IOException while reading {0} on connection {1}. " +
                        "{2}",
                        type, this, this.GetConnectionTimestampsText());
                    throw new TransportException(
                        TransportErrorCode.ReceiveFailed, ex, args.ActivityId,
                        this.serverUri, this.ToString(), args.UserPayload, true);
                }

                if (read == 0)
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
                this.UpdateLastReceiveTime();
                bytesRead += read;
            }
            Debug.Assert(bytesRead == length);
            Debug.Assert(length == payload.Length);
            return payload;
        }

        private void SnapshotConnectionTimestamps(
            out DateTime lastSendAttempt, out DateTime lastSend,
            out DateTime lastReceive)
        {
            Debug.Assert(!Monitor.IsEntered(this.timestampLock));
            lock (this.timestampLock)
            {
                lastSendAttempt = this.lastSendAttemptTime;
                lastSend = this.lastSendTime;
                lastReceive = this.lastReceiveTime;
            }
        }

        private string GetConnectionTimestampsText()
        {
            DateTime lastSendAttempt, lastSend, lastReceive;
            this.SnapshotConnectionTimestamps(
                out lastSendAttempt, out lastSend, out lastReceive);
            return string.Format(
                CultureInfo.InvariantCulture,
                "Last send attempt time: {0:o}. Last send time: {1:o}. " +
                "Last receive time: {2:o}",
                lastSendAttempt, lastSend, lastReceive);
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
            }
        }

        private void UpdateLastReceiveTime()
        {
            Debug.Assert(!Monitor.IsEntered(this.timestampLock));
            lock (this.timestampLock)
            {
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

        private static async Task<IPAddress> ResolveHostAsync(string hostName)
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
#endif  // !NETSTANDARD15 && !NETSTANDARD16
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
