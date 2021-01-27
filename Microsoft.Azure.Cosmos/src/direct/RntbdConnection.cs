//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
#if COSMOSCLIENT
    using Microsoft.Azure.Cosmos.Rntbd;
#endif
    using Microsoft.Azure.Documents.Rntbd;

    internal class RntbdConnection : IConnection
    {
        //Upper/Lower bounds on the timeout after which client will treat the connection as expired
        private static readonly TimeSpan MaxIdleConnectionTimeout = TimeSpan.FromHours(1);
        private static readonly TimeSpan MinIdleConnectionTimeout = TimeSpan.FromSeconds(100); 
        private static readonly TimeSpan DefaultIdleConnectionTimeout = TimeSpan.FromSeconds(100);

        private static readonly TimeSpan DefaultUnauthenticatedTimeout = TimeSpan.FromSeconds(10);
        private const UInt32 MinimumUnauthenticatedTimeoutInSeconds = 1;

        // Similar Delta for Unauthenticated connection (as stated above) but shorter since UnauthenticatedTimeout
        // is also shorter in comparison. 
        private const UInt32 UnauthenticatedTimeoutBufferInSeconds = 5;

        // SSL-related constants
        private const SslProtocols EnabledTLSProtocols = SslProtocols.Tls12;

        // Path format
        private static readonly char[] UrlTrim = new char[] { '/' };

        // TCP-related constants
        private static readonly byte[] KeepAliveOn = BitConverter.GetBytes((UInt32)1);
        private static readonly byte[] KeepAliveTimeInMilliseconds = BitConverter.GetBytes((UInt32)30 * 1000);
        private static readonly byte[] KeepAliveIntervalInMilliseconds = BitConverter.GetBytes((UInt32)1 * 1000);

        // Embedded in GoneExceptions, only in Gateway
        internal static string LocalIpv4Address;
        private static bool AddSourceIpAddressInNetworkExceptionMessagePrivate = false;

        private const int MaxContextResponse = 8000;
        private const int MaxResponse = int.MaxValue;

        // Hostname and port of the target backend. Note that the
        // URI probably contains a path, but it may not be the same path as
        // is used for the current request. Only the hostname and port
        // from this URI are used during initial Open call.
        private readonly Uri initialOpenUri;

        // The string that should be used as the key for pooling connections - each unique "poolKey"
        // corresponds to a separate pool. Currently, the poolKey is just "host:port"
        private readonly string poolKey;

        // URI with hostname, port AND path to the target. Valid only when
        // connection is in use (and not when it is
        // in the connection pool in idle state).
        private Uri targetPhysicalAddress;

        private Stream stream;
        private Socket socket;
        private TcpClient tcpClient;

        private double requestTimeoutInSeconds;
        private bool isOpen;
        private string serverAgent;
        private string serverVersion;
        private TimeSpan idleTimeout;
        private TimeSpan unauthenticatedTimeout;
        private string overrideHostNameInCertificate;
        private double openTimeoutInSeconds;

        private DateTime lastUsed;
        private DateTime opened;
        private UserAgentContainer userAgent;
        private bool hasIssuedSuccessfulRequest;

        private RntbdConnectionOpenTimers connectionTimers;
        private readonly TimerPool timerPool;

        public RntbdConnection(
            Uri address, 
            double requestTimeoutInSeconds, 
            string overrideHostNameInCertificate, 
            double openTimeoutInSeconds, 
            double idleConnectionTimeoutInSeconds,
            string poolKey, 
            UserAgentContainer userAgent,
            TimerPool pool)
        {
            this.connectionTimers.CreationTimestamp = DateTimeOffset.Now;
            this.initialOpenUri = address;
            this.poolKey = poolKey;
            this.requestTimeoutInSeconds = requestTimeoutInSeconds;
            this.overrideHostNameInCertificate = overrideHostNameInCertificate;
            this.openTimeoutInSeconds = openTimeoutInSeconds;
            if(TimeSpan.FromSeconds(idleConnectionTimeoutInSeconds) < RntbdConnection.MaxIdleConnectionTimeout
                && TimeSpan.FromSeconds(idleConnectionTimeoutInSeconds) > RntbdConnection.MinIdleConnectionTimeout)
            {
                this.idleTimeout = TimeSpan.FromSeconds(idleConnectionTimeoutInSeconds);
            }
            else
            {
                this.idleTimeout = RntbdConnection.DefaultIdleConnectionTimeout;
            }

            this.BufferProvider = new BufferProvider();
            this.serverVersion = null;
            this.opened = DateTime.UtcNow;
            this.lastUsed = opened;
            this.userAgent = userAgent ?? new UserAgentContainer();
            this.timerPool = pool;
        }

        protected BufferProvider BufferProvider { get; }

        public string PoolKey
        {
            get
            {
                return this.poolKey;
            }
        }

        public RntbdConnectionOpenTimers ConnectionTimers
        {
            get
            {
                return this.connectionTimers;
            }
        }

        // This will default to false, to avoid privacy concerns in general. We can explicitly enable it
        // inside processes running in our datacenter, to get useful detail in our traces (paired source 
        // and target IP address have been needed in the past for CloudNet investigations, for example)
        public static bool AddSourceIpAddressInNetworkExceptionMessage
        {
            get
            {
                return RntbdConnection.AddSourceIpAddressInNetworkExceptionMessagePrivate;
            }
            set
            {
                if (value && !RntbdConnection.AddSourceIpAddressInNetworkExceptionMessagePrivate)
                {
                    // From false to true, reset the IP address for logging
                    RntbdConnection.LocalIpv4Address = NetUtil.GetNonLoopbackIpV4Address() ?? string.Empty;
                }

                RntbdConnection.AddSourceIpAddressInNetworkExceptionMessagePrivate = value;
            }
        }

        public void Close()
        {
            if (this.stream != null)
            {
                DefaultTrace.TraceVerbose("Closing connection stream for TargetAddress: {0}, creationTime: {1}, lastUsed: {2}, poolKey: {3}",
                    this.targetPhysicalAddress,
                    this.connectionTimers.CreationTimestamp.ToString("o", CultureInfo.InvariantCulture),
                    this.lastUsed.ToString("o", CultureInfo.InvariantCulture),
                    this.poolKey);

                this.stream.Close();
                this.stream = null;
            }

            if (this.tcpClient != null)
            {
                this.tcpClient.Close();
            }

            if (this.socket != null)
            {
                this.socket = null;
            }

            if (this.isOpen)
            {
                this.isOpen = false;
            }
        }

        public async Task Open(Guid activityId, Uri fullTargetAddress)
        {
            this.targetPhysicalAddress = fullTargetAddress;
            DateTimeOffset openStartTime = DateTimeOffset.Now;
            Task[] awaitTasks = new Task[2];

            // Optimized version of Task.Delay for timeout scenarios
            PooledTimer delayTaskTimer;
            if(this.openTimeoutInSeconds != 0)
            {
                delayTaskTimer = this.timerPool.GetPooledTimer((int)this.openTimeoutInSeconds);
            }
            else
            {
                delayTaskTimer = this.timerPool.GetPooledTimer((int)this.requestTimeoutInSeconds);
            }

            // Starts the timer which returns a Task that you await on
            Task delayTaskOpen = delayTaskTimer.StartTimerAsync();

            awaitTasks[0] = delayTaskOpen;
            awaitTasks[1] = this.OpenSocket(activityId);

            // Any exception during Open becomes a GoneException.
            Task completedTask = await Task.WhenAny(awaitTasks);
            if (completedTask == awaitTasks[0])
            {
                CleanupWorkTask(awaitTasks[1], activityId, openStartTime);
                if (!awaitTasks[0].IsFaulted)
                {
                    throw RntbdConnection.GetGoneException(fullTargetAddress, activityId);
                }
                else
                {
                    throw RntbdConnection.GetGoneException(fullTargetAddress, activityId, completedTask.Exception.InnerException);
                }
            }
            else
            {
                if (completedTask.IsFaulted)
                {
                    // Cancels the timer as it's no longer needed
                    delayTaskTimer.CancelTimer();

                    if (completedTask.Exception.InnerException is DocumentClientException)
                    {
                        ((DocumentClientException)completedTask.Exception.InnerException)
                            .Headers.Set(HttpConstants.HttpHeaders.ActivityId, activityId.ToString());
                        await completedTask;
                    }
                    else
                    {
                        throw RntbdConnection.GetGoneException(fullTargetAddress, activityId, completedTask.Exception.InnerException);
                    }
                }
            }

            this.connectionTimers.TcpConnectCompleteTimestamp = DateTimeOffset.Now;

            RntbdResponseState state = new RntbdResponseState();
            awaitTasks[1] = this.PerformHandshakes(activityId, state);

            // Any exception during Open becomes a GoneException.
            completedTask = await Task.WhenAny(awaitTasks);
            if (completedTask == awaitTasks[0])
            {
                CleanupWorkTask(awaitTasks[1], activityId, openStartTime);

                if (!awaitTasks[0].IsFaulted)
                {
                    throw RntbdConnection.GetGoneException(fullTargetAddress, activityId);
                }
                else
                {
                    throw RntbdConnection.GetGoneException(fullTargetAddress, activityId, completedTask.Exception.InnerException);
                }
            }
            else
            {
                // Cancels the timer as it's no longer needed
                delayTaskTimer.CancelTimer();

                if (completedTask.IsFaulted)
                {
                    if (completedTask.Exception.InnerException is DocumentClientException)
                    {
                        ((DocumentClientException)completedTask.Exception.InnerException)
                            .Headers.Set(HttpConstants.HttpHeaders.ActivityId, activityId.ToString());
                        await completedTask;
                    }
                    else
                    {
                        throw RntbdConnection.GetGoneException(fullTargetAddress, activityId, completedTask.Exception.InnerException);
                    }
                }
            }
        }

        /// <summary>
        ///  Async method to makes request to backend using the rntbd protocol
        /// </summary>
        /// <param name="request"> a DocumentServiceRequest object that has the state for all the headers </param>
        /// <param name="physicalAddress"> physical address of the replica </param>
        /// <param name="resourceOperation"> Resource Type + Operation Type Pair </param>
        /// <param name="activityId"> ActivityId of the request </param>
        /// <returns> StoreResponse </returns>
        /// <exception cref="DocumentClientException"> exception that was caught while building the request byte buffer for sending over wire </exception>
        /// <exception cref="BadRequestException"> any other exception encountered while building the request </exception>
        /// <exception cref="GoneException"> All timeouts for read-only requests are converted to Gone exception </exception>
        /// <exception cref="RequestTimeoutException"> Request Times out (if there's no response until the timeout duration </exception>
        /// <exception cref="ServiceUnavailableException"> Any other exception that is not from our code (exception SocketException, or Connection Close 
        /// </exception>
        public async Task<StoreResponse> RequestAsync(
            DocumentServiceRequest request,
            Uri physicalAddress,
            ResourceOperation resourceOperation,
            Guid activityId)
        {
            // No races as there is no request multiplexing.
            this.targetPhysicalAddress = physicalAddress;

            // build the request byte payload
            BufferProvider.DisposableBuffer requestPayload = default;
            int headerAndMetadataSize = 0;
            int bodySize = 0;
            try
            {
                requestPayload = this.BuildRequest(request, physicalAddress.PathAndQuery.TrimEnd(RntbdConnection.UrlTrim), resourceOperation, out headerAndMetadataSize, out bodySize, activityId);
            }
            catch (Exception ex)
            {
                requestPayload.Dispose();
                DocumentClientException clientException = ex as DocumentClientException;
                if (clientException != null)
                {
                    clientException.Headers.Add(HttpConstants.HttpHeaders.RequestValidationFailure, "1");
                    throw;
                }
                else
                {
                    DefaultTrace.TraceError("RntbdConnection.BuildRequest failure due to assumed malformed request payload: {0}", ex);
                    clientException = new BadRequestException(ex);
                    clientException.Headers.Add(HttpConstants.HttpHeaders.RequestValidationFailure, "1");
                    throw clientException;
                }
            }

            using (requestPayload)
            {
                // Optimized version of Task.Delay for timeout scenarios
                PooledTimer delayTaskTimer = this.timerPool.GetPooledTimer((int)this.requestTimeoutInSeconds);

                //Starts the timer which returns a Task that you await on
                Task delayTaskRequest = delayTaskTimer.StartTimerAsync();

                DateTimeOffset requestStartTime = DateTimeOffset.Now;

                Task[] awaitTasks = new Task[2];
                awaitTasks[0] = delayTaskRequest;

                // Any cancellation from here needs to become RequestTimeoutException or GoneException.
                // For read requests, we throw GoneException to avoid transient timeouts due to BE down.
                // For other requests, we throw RequestTimeoutException.
                // Wrap any other exception that isn't ours (notably any SocketException or graceful connection closure) into 
                // a ServiceUnavailableException. If the server returns a malformed response, then make an 
                // InternalServerErrorException.
                awaitTasks[1] = this.SendRequestAsyncInternal(requestPayload.Buffer,
                    activityId);
                Task completedTask = await Task.WhenAny(awaitTasks);
                if (completedTask == awaitTasks[0])
                {
                    DateTimeOffset requestEndTime = DateTimeOffset.Now;

                    CleanupWorkTask(awaitTasks[1], activityId, requestStartTime);
                    DefaultTrace.TraceError("Throwing RequestTimeoutException while awaiting request send. Task start time {0}. Task end time {1}. Request message size: {2}", requestStartTime, requestEndTime, requestPayload.Buffer.Count);
                    if (!awaitTasks[0].IsFaulted)
                    {
                        if (request.IsReadOnlyRequest)
                        {
                            DefaultTrace.TraceVerbose("Converting RequestTimeout to GoneException for ReadOnlyRequest");
                            throw RntbdConnection.GetGoneException(physicalAddress, activityId);
                        }
                        else
                        {
                            throw RntbdConnection.GetRequestTimeoutException(physicalAddress, activityId);
                        }
                    }
                    else
                    {
                        if (request.IsReadOnlyRequest)
                        {
                            DefaultTrace.TraceVerbose("Converting RequestTimeout to GoneException for ReadOnlyRequest");
                            throw RntbdConnection.GetGoneException(physicalAddress, activityId, completedTask.Exception.InnerException);
                        }
                        else
                        {
                            throw RntbdConnection.GetRequestTimeoutException(physicalAddress, activityId, completedTask.Exception.InnerException);
                        }
                    }
                }
                else
                {
                    if (completedTask.IsFaulted)
                    {
                        // Cancels the timer as it's no longer needed
                        delayTaskTimer.CancelTimer();

                        if (completedTask.Exception.InnerException is DocumentClientException)
                        {
                            ((DocumentClientException)completedTask.Exception.InnerException)
                                .Headers.Set(HttpConstants.HttpHeaders.ActivityId, activityId.ToString());
                            await completedTask;
                        }
                        else
                        {
                            throw RntbdConnection.GetServiceUnavailableException(physicalAddress, activityId, completedTask.Exception.InnerException);
                        }
                    }
                }

                DateTimeOffset requestSendDoneTime = DateTimeOffset.Now;

                RntbdResponseState state = new RntbdResponseState();
                Task<StoreResponse> responseTask = this.GetResponseAsync(activityId, request.IsReadOnlyRequest, state);
                awaitTasks[1] = responseTask;

                completedTask = await Task.WhenAny(awaitTasks);
                if (completedTask == awaitTasks[0])
                {
                    DateTimeOffset requestEndTime = DateTimeOffset.Now;
                    CleanupWorkTask(awaitTasks[1], activityId, requestStartTime);

                    DefaultTrace.TraceError("Throwing RequestTimeoutException while awaiting response receive. " +
                                            "Task start time {0}. Request Send End time: {1}. Request header size: {2}. Request body size: {3}. Request size: {4}. Task end time {5}. State {6}.",
                        requestStartTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                        requestSendDoneTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                        headerAndMetadataSize,
                        bodySize,
                        requestPayload.Buffer.Count,
                        requestEndTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                        state.ToString());

                    if (!awaitTasks[0].IsFaulted)
                    {
                        if (request.IsReadOnlyRequest)
                        {
                            DefaultTrace.TraceVerbose("Converting RequestTimeout to GoneException for ReadOnlyRequest");
                            throw RntbdConnection.GetGoneException(physicalAddress, activityId);
                        }
                        else
                        {
                            throw RntbdConnection.GetRequestTimeoutException(physicalAddress, activityId);
                        }
                    }
                    else
                    {
                        if (request.IsReadOnlyRequest)
                        {
                            DefaultTrace.TraceVerbose("Converting RequestTimeout to GoneException for ReadOnlyRequest");
                            throw RntbdConnection.GetGoneException(physicalAddress, activityId, completedTask.Exception.InnerException);
                        }
                        else
                        {
                            throw RntbdConnection.GetRequestTimeoutException(physicalAddress, activityId, completedTask.Exception.InnerException);
                        }
                    }
                }
                else
                {
                    // Cancels the timer as it's no longer needed
                    delayTaskTimer.CancelTimer();

                    if (completedTask.IsFaulted)
                    {
                        if (completedTask.Exception.InnerException is DocumentClientException)
                        {
                            ((DocumentClientException)completedTask.Exception.InnerException)
                                .Headers.Set(HttpConstants.HttpHeaders.ActivityId, activityId.ToString());
                            await completedTask;
                        }
                        else
                        {

                            throw RntbdConnection.GetServiceUnavailableException(physicalAddress, activityId, completedTask.Exception.InnerException);
                        }
                    }
                }

                // Note that if we reach this point, we already awaited responseTask successfully, so we can examine its Result safely
                // (it will not incur a blocking wait)
                if (responseTask.Result.Status >= 200
                    && responseTask.Result.Status != 410 // 410 or Gone implies that the request was not sent to right replica, in such case, do not reuse 
                    && responseTask.Result.Status != 401 // 401 or 403 implies that the connection was never authenticated, and as a result, BE could have terminated the connection, do not reuse
                    && responseTask.Result.Status != 403)
                {
                    // for everything else, the connection is safe for reuse
                    this.hasIssuedSuccessfulRequest = true;
                }

                this.lastUsed = DateTime.UtcNow;

                return responseTask.Result;
            }
        }

        private void CleanupWorkTask(Task workTask, Guid activityId, DateTimeOffset requestStartTime)
        {
            // Fire-and-forget a task to handle the error (do NOT await it)
            Task ignoredTask = workTask.ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    // Filter out ObjectDisposedException on SslStream, since it is expected here with the way we implement cancellation.
                    // SslStream doesn't play nice when you call Close on it with an outstanding ReadAsync.
                    ObjectDisposedException ex = t.Exception.InnerException as ObjectDisposedException;
                    if (ex == null ||
                        (ex.ObjectName != null && string.Compare(ex.ObjectName, "SslStream", StringComparison.Ordinal) != 0))
                    {
                        DefaultTrace.TraceError("Ignoring exception {0} on ActivityId {1}. Task start time {2} Hresult {3}",
                            t.Exception, activityId.ToString(), requestStartTime, t.Exception.HResult);
                    }
                    else
                    {
                        DefaultTrace.TraceVerbose("Ignoring exception {0} on ActivityId {1}. Task start time {2} Hresult {3}",
                            ex, activityId.ToString(), requestStartTime, ex.HResult);
                    }
                }
            });
        }

        #region Connection Establishment
        private async Task OpenSocket(Guid activityId)
        {
            TcpClient client = null;
            try
            {
                IPAddress[] serverAddresses = await Dns.GetHostAddressesAsync(
                    this.initialOpenUri.DnsSafeHost);
                if (serverAddresses.Length > 1)
                {
                    DefaultTrace.TraceWarning(
                        "Found multiple addresses for host, choosing the first. " +
                        "Host: {0}. Addresses: {1}",
                        this.initialOpenUri.DnsSafeHost, serverAddresses);
                }
                IPAddress address = serverAddresses[0];
                client = new TcpClient(address.AddressFamily);

                client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                RntbdConnection.SetKeepAlive(client.Client);

                await client.ConnectAsync(address, this.initialOpenUri.Port);
            }
            catch (Exception ex)
            {
                // Don't forget to dispose the socket. Else the
                // Win32 handle to the underlying socket stays around
                // to be GCed increasing the handle count of the process.
                client?.Close();

                // Check for WSAETIMEDOUT - if the socket fails with that error code, we have a perf counter
                // we want to increment.
                SocketException socketEx = ex as SocketException;
                if (socketEx != null &&
                    socketEx.SocketErrorCode == SocketError.TimedOut)
                {
#if NETFX
                    if (PerfCounters.Counters.BackendConnectionOpenFailuresDueToSynRetransmitPerSecond != null)
                    {
                        PerfCounters.Counters.BackendConnectionOpenFailuresDueToSynRetransmitPerSecond.Increment();
                    }
#endif
                }

                throw RntbdConnection.GetGoneException(this.targetPhysicalAddress, activityId, ex);
            }

            // save TcpClient's stream to this.stream so we will close it explicitly at Close if SSL handshake fails
            Debug.Assert(client != null);
            this.tcpClient = client;
            this.socket = client.Client;
            this.stream = client.GetStream();
        }

        private async Task PerformHandshakes(Guid activityId, RntbdResponseState state)
        {
            string targetHost = this.overrideHostNameInCertificate != null ? this.overrideHostNameInCertificate : this.initialOpenUri.Host;
            SslStream sslStream = new SslStream(this.stream, false);

            try
            {
                await sslStream.AuthenticateAsClientAsync(targetHost, null, EnabledTLSProtocols, false);
            }
            catch (Exception ex)
            {
                throw RntbdConnection.GetGoneException(this.targetPhysicalAddress, activityId, ex);
            }

            this.connectionTimers.SslHandshakeCompleteTimestamp = DateTimeOffset.Now;

            // The stream we'll actually use directly is the SslStream that now wraps the socket stream.
            this.stream = sslStream;

            try
            {
                await this.NegotiateRntbdContextAsync(sslStream, activityId, state);
            }
            catch (Exception ex)
            {
                if (ex is DocumentClientException)
                {
                    throw;
                }
                else
                {
                    throw RntbdConnection.GetGoneException(this.targetPhysicalAddress, activityId, ex);
                }
            }

            this.connectionTimers.RntbdHandshakeCompleteTimestamp = DateTimeOffset.Now;

            this.isOpen = true;
        }

        public bool HasExpired()
        {
            // The backend enforces two timers which can expire a connection. This function enforces the same checks on the client/Gateway
            // to eliminate  the racing window where server-side connection closure races with client-side usage of the connection.
            // The timers are:
            // 1) The idle monitor, which fires after 2 minutes of idle time (counting from the last read or write on the connection by 
            //    the server). To close the window with this, we check whether the last time the connection was in use was >= 100 seconds 
            //    ago, and if so we consider it expired. //TODO: ankshah Revisit this since BE no longer termiantes the connection if idle
            // 2) The unauthenticated monitor, which fires 15 seconds after connection acceptance if the connection has yet to send a 
            //    request that passed authentication. Requests may get a valid response (always with a failed status code) without 
            //    performing authentication, so to eliminate the window here, we check whether there have been no successful requests 
            //    on the conneciton and whether it's been at least 10 seconds since the connection was opened.
            TimeSpan timeSinceUse = DateTime.UtcNow - this.lastUsed;
            TimeSpan timeSinceOpen = DateTime.UtcNow - this.opened;

            return (timeSinceUse > this.idleTimeout ||
                    (!this.hasIssuedSuccessfulRequest && timeSinceOpen > this.unauthenticatedTimeout));
        }

        public bool ConfirmOpen()
        {
            return CustomTypeExtensions.ConfirmOpen(this.socket);
        }

        protected virtual byte[] BuildContextRequest(Guid activityId)
        {
            return Rntbd.TransportSerialization.BuildContextRequest(activityId, this.userAgent, RntbdConstants.CallerId.Anonymous);
        }

        private async Task NegotiateRntbdContextAsync(Stream negotiatingStream, Guid activityId, RntbdResponseState state)
        {
            byte[] contextMessage = this.BuildContextRequest(activityId);
            await negotiatingStream.WriteAsync(contextMessage, 0, contextMessage.Length);

            // Read the response.
            Tuple<byte[], byte[]> headerAndMetadata;
            headerAndMetadata = await this.ReadHeaderAndMetadata(RntbdConnection.MaxContextResponse, true, activityId, state);

            byte[] header = headerAndMetadata.Item1;
            byte[] metadata = headerAndMetadata.Item2;

            // Full header and metadata are read now. Parse out more fields and handle them.
            StatusCodes status = (StatusCodes)BitConverter.ToUInt32(header, 4);
            byte[] responseActivityIdBytes = new byte[16];
            Buffer.BlockCopy(header, 8, responseActivityIdBytes, 0, 16);

            // Server should just be echoing back the ActivityId from the connection request, but retrieve it 
            // from the wire and use it from here on, to be absolutely certain we have the same ActivityId 
            // the server is using
            Guid responseActivityId = new Guid(responseActivityIdBytes);
            RntbdConstants.ConnectionContextResponse response = null;
            BytesDeserializer deserializer = new BytesDeserializer(metadata, metadata.Length);
            response = new RntbdConstants.ConnectionContextResponse();
            response.ParseFrom(ref deserializer);

            this.serverAgent = BytesSerializer.GetStringFromBytes(response.serverAgent.value.valueBytes);
            this.serverVersion = BytesSerializer.GetStringFromBytes(response.serverVersion.value.valueBytes);

            this.SetIdleTimers(response);

            if ((UInt32)status < 200 || (UInt32)status >= 400)
            {
                byte[] errorResponse;
                errorResponse = await this.ReadBody(true, responseActivityId, state);

                using (MemoryStream errorReadStream = new MemoryStream(errorResponse))
                {
                    Error error = Resource.LoadFrom<Error>(errorReadStream);

                    Trace.CorrelationManager.ActivityId = responseActivityId;
                    DocumentClientException exception = new DocumentClientException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            error.ToString()),
                        null,
                        (HttpStatusCode)status,
                        this.targetPhysicalAddress);

                    if (response.clientVersion.isPresent)
                    {
                        exception.Headers.Add("RequiredClientVersion", BytesSerializer.GetStringFromBytes(response.clientVersion.value.valueBytes));
                    }

                    if (response.protocolVersion.isPresent)
                    {
                        exception.Headers.Add("RequiredProtocolVersion", response.protocolVersion.value.valueULong.ToString());
                    }

                    if (response.serverAgent.isPresent)
                    {
                        exception.Headers.Add("ServerAgent", BytesSerializer.GetStringFromBytes(response.serverAgent.value.valueBytes));
                    }

                    if (response.serverVersion.isPresent)
                    {
                        exception.Headers.Add(HttpConstants.HttpHeaders.ServerVersion, BytesSerializer.GetStringFromBytes(response.serverVersion.value.valueBytes));
                    }

                    throw exception;
                }
            }
        }

        private void SetIdleTimers(RntbdConstants.ConnectionContextResponse response)
        {
            if (response.unauthenticatedTimeoutInSeconds.isPresent)
            {
                UInt32 unauthenticatedTimeoutInSeconds;
                if (response.unauthenticatedTimeoutInSeconds.value.valueULong > RntbdConnection.UnauthenticatedTimeoutBufferInSeconds)
                {
                    unauthenticatedTimeoutInSeconds = response.unauthenticatedTimeoutInSeconds.value.valueULong - RntbdConnection.UnauthenticatedTimeoutBufferInSeconds;
                }
                else
                {
                    unauthenticatedTimeoutInSeconds = RntbdConnection.MinimumUnauthenticatedTimeoutInSeconds;
                }

                this.unauthenticatedTimeout = TimeSpan.FromSeconds(unauthenticatedTimeoutInSeconds);
            }
            else
            {
                this.unauthenticatedTimeout = RntbdConnection.DefaultUnauthenticatedTimeout;
            }
        }

        #endregion

        #region Send Request
        private async Task SendRequestAsyncInternal(
            ArraySegment<byte> requestPayload,
            Guid activityId)
        {
            // Beyond this point, any IO exception has to be mapped to an indication that the request may have been sent (in particular, no GoneException)
            try
            {
                await this.stream.WriteAsync(requestPayload.Array, requestPayload.Offset, requestPayload.Count);
            }
            catch (SocketException ex)
            {
                throw RntbdConnection.GetServiceUnavailableException(this.targetPhysicalAddress, activityId, ex);
            }
            catch (IOException ex)
            {
                throw RntbdConnection.GetServiceUnavailableException(this.targetPhysicalAddress, activityId, ex);
            }
        }

        /// <summary>
        /// Given DocumentServiceRequest object, creates a byte array of the request that is to be sent over wire to
        /// backend per rntbd protocol
        /// </summary>
        /// <param name="request"> DocumentService Request</param>
        /// <param name="replicaPath"> path to the replica, as extracted from the replica uri </param>
        /// <param name="resourceOperation"> ResourceType + OperationType pair </param>
        /// <param name="headerAndMetadataSize"></param>
        /// <param name="bodySize"></param>
        /// <param name="activityId"></param>
        /// <exception cref="InternalServerErrorException"> 
        /// This is of type DocumentClientException. Thrown 
        /// if there is a bug in rntbd token serialization 
        /// </exception>
        /// <returns> byte array that is the request body to be sent over wire </returns>
        protected virtual BufferProvider.DisposableBuffer BuildRequest(
            DocumentServiceRequest request,
            string replicaPath,
            ResourceOperation resourceOperation,
            out int headerAndMetadataSize,
            out int bodySize,
            Guid activityId)
        {
            return Rntbd.TransportSerialization.BuildRequest(request, replicaPath, resourceOperation,
                activityId, this.BufferProvider, out headerAndMetadataSize, out bodySize);
        }
        #endregion

        #region Read Response
        private async Task<StoreResponse> GetResponseAsync(Guid requestActivityId, bool isReadOnlyRequest, RntbdResponseState state)
        {
            state.SetState(RntbdResponseStateEnum.Called);

            // Read the response.
            Tuple<byte[], byte[]> headerAndMetadata = await this.ReadHeaderAndMetadata(
                RntbdConnection.MaxResponse,
                isReadOnlyRequest, // throw gone if readonly request so that we would retry on channel failure
                requestActivityId, state);

            byte[] header = headerAndMetadata.Item1;
            byte[] metadata = headerAndMetadata.Item2;

            // Parse out fields and handle them.
            StatusCodes status = (StatusCodes)BitConverter.ToUInt32(header, 4);
            byte[] responseActivityIdBytes = new byte[16];
            Buffer.BlockCopy(header, 8, responseActivityIdBytes, 0, 16);
            // Server should just be echoing back the ActivityId from the request, but retrieve it from the wire
            // and use it from here on, to be absolutely certain we have the ActivityId the server is using
            Guid responseActivityId = new Guid(responseActivityIdBytes);

            RntbdConstants.Response response = null;

            BytesDeserializer deserializer = new BytesDeserializer(metadata, metadata.Length);
            response = new RntbdConstants.Response();
            response.ParseFrom(ref deserializer);

            MemoryStream bodyStream = null;
            if (response.payloadPresent.value.valueByte != (byte)0x00)
            {
                byte[] body;
                try
                {
                    body = await this.ReadBody(false, responseActivityId, state);
                }
                catch (Exception ex)
                {
                    if (ex is DocumentClientException)
                    {
                        throw;
                    }

                    throw RntbdConnection.GetServiceUnavailableException(this.targetPhysicalAddress, responseActivityId, ex);
                }

                bodyStream = new MemoryStream(body);
            }

            state.SetState(RntbdResponseStateEnum.Done);

            return Rntbd.TransportSerialization.MakeStoreResponse(status, responseActivityId, response, bodyStream, this.serverVersion);
        }

        private async Task<Tuple<byte[], byte[]>> ReadHeaderAndMetadata(int maxAllowed,
            bool throwGoneOnChannelFailure, Guid activityId, RntbdResponseState state)
        {
            state.SetState(RntbdResponseStateEnum.StartHeader);

            byte[] header = new byte[24];
            int headerRead = 0;
            while (headerRead < header.Length)
            {
                int read = 0;
                try
                {
                    read = await this.stream.ReadAsync(header, headerRead, header.Length - headerRead);
                }
                catch (IOException ex)
                {
                    DefaultTrace.TraceError("Hit IOException while reading header on connection with last used time {0}", this.lastUsed.ToString("o", CultureInfo.InvariantCulture));
                    ThrowOnFailure(throwGoneOnChannelFailure, activityId, ex);
                }

                if (read == 0)
                {
                    DefaultTrace.TraceError("Read 0 bytes while reading header");
                    ThrowOnFailure(throwGoneOnChannelFailure, activityId);
                }

                state.SetState(RntbdResponseStateEnum.BufferingHeader);
                state.AddHeaderMetadataRead(read);

                headerRead += read;
            }

            if (state != null)
            {
                state.SetState(RntbdResponseStateEnum.DoneBufferingHeader);
            }

            UInt32 totalLength = BitConverter.ToUInt32(header, 0);
            if (totalLength > maxAllowed)
            {
                DefaultTrace.TraceCritical("RNTBD header length says {0} but expected at most {1} bytes", totalLength, maxAllowed);

                throw RntbdConnection.GetInternalServerErrorException(this.targetPhysicalAddress, activityId);
            }

            if (totalLength < header.Length)
            {
                DefaultTrace.TraceCritical("RNTBD header length says {0} but expected at least {1} bytes and read {2} bytes from wire", totalLength, header.Length, headerRead);

                throw RntbdConnection.GetInternalServerErrorException(this.targetPhysicalAddress, activityId);
            }

            int metadataLength = (int)totalLength - header.Length;
            byte[] metadata = new byte[metadataLength];
            int responseMetadataRead = 0;
            while (responseMetadataRead < metadataLength)
            {
                int read = 0;
                try
                {
                    read = await this.stream.ReadAsync(metadata, responseMetadataRead, metadataLength - responseMetadataRead);
                }
                catch (IOException ex)
                {
                    DefaultTrace.TraceError("Hit IOException while reading metadata on connection with last used time {0}", this.lastUsed.ToString("o", CultureInfo.InvariantCulture));
                    ThrowOnFailure(throwGoneOnChannelFailure, activityId, ex);
                }

                if (read == 0)
                {
                    DefaultTrace.TraceError("Read 0 bytes while reading metadata");
                    ThrowOnFailure(throwGoneOnChannelFailure, activityId);
                }

                state.SetState(RntbdResponseStateEnum.BufferingMetadata);
                state.AddHeaderMetadataRead(read);

                responseMetadataRead += read;
            }

            state.SetState(RntbdResponseStateEnum.DoneBufferingMetadata);

            return new Tuple<byte[], byte[]>(header, metadata);
        }

        private async Task<byte[]> ReadBody(bool throwGoneOnChannelFailure, Guid activityId, RntbdResponseState state)
        {
            byte[] bodyLengthHeader = new byte[4];
            int bodyLengthRead = 0;
            while (bodyLengthRead < 4)
            {
                int read = 0;
                try
                {
                    read = await this.stream.ReadAsync(bodyLengthHeader, bodyLengthRead, bodyLengthHeader.Length - bodyLengthRead);
                }
                catch (IOException ex)
                {
                    DefaultTrace.TraceError("Hit IOException while reading BodyLengthHeader on connection with last used time {0}", this.lastUsed.ToString("o", CultureInfo.InvariantCulture));
                    ThrowOnFailure(throwGoneOnChannelFailure, activityId, ex);
                }

                if (read == 0)
                {
                    DefaultTrace.TraceError("Read 0 bytes while reading BodyLengthHeader");
                    ThrowOnFailure(throwGoneOnChannelFailure, activityId);
                }

                state.SetState(RntbdResponseStateEnum.BufferingBodySize);
                state.AddBodyRead(read);

                bodyLengthRead += read;
            }

            state.SetState(RntbdResponseStateEnum.DoneBufferingBodySize);

            UInt32 bodyRead = 0;
            UInt32 length = BitConverter.ToUInt32(bodyLengthHeader, 0);
            byte[] body = new byte[length];
            while (bodyRead < length)
            {
                int read = 0;
                try
                {
                    read = await this.stream.ReadAsync(body, (int)bodyRead, body.Length - (int)bodyRead);
                }
                catch (IOException ex)
                {
                    DefaultTrace.TraceError("Hit IOException while reading Body on connection with last used time {0}", this.lastUsed.ToString("o", CultureInfo.InvariantCulture));
                    ThrowOnFailure(throwGoneOnChannelFailure, activityId, ex);
                }

                if (read == 0)
                {
                    DefaultTrace.TraceError("Read 0 bytes while reading Body");
                    ThrowOnFailure(throwGoneOnChannelFailure, activityId);
                }

                state.SetState(RntbdResponseStateEnum.BufferingBody);
                state.AddBodyRead(read);

                bodyRead += (UInt32)read;
            }

            state.SetState(RntbdResponseStateEnum.DoneBufferingBody);

            return body;
        }

        private void ThrowOnFailure(bool throwGoneOnChannelFailure, Guid activityId, Exception innerException = null)
        {
            if (throwGoneOnChannelFailure)
            {
                throw RntbdConnection.GetGoneException(this.targetPhysicalAddress, activityId, innerException);
            }
            else
            {
                throw RntbdConnection.GetServiceUnavailableException(this.targetPhysicalAddress, activityId, innerException);
            }
        }

        #endregion

        #region Helpers
        private static GoneException GetGoneException(
            Uri fullTargetAddress, Guid activityId, Exception inner = null)
        {
            return Rntbd.TransportExceptions.GetGoneException(fullTargetAddress, activityId, inner);
        }

        private static RequestTimeoutException GetRequestTimeoutException(
            Uri fullTargetAddress, Guid activityId, Exception inner = null)
        {
            return Rntbd.TransportExceptions.GetRequestTimeoutException(fullTargetAddress, activityId, inner);
        }

        private static ServiceUnavailableException GetServiceUnavailableException(
            Uri fullTargetAddress, Guid activityId, Exception inner = null)
        {
            return Rntbd.TransportExceptions.GetServiceUnavailableException(fullTargetAddress, activityId, inner);
        }

        private static InternalServerErrorException GetInternalServerErrorException(
            Uri fullTargetAddress, Guid activityId, Exception inner = null)
        {
            return Rntbd.TransportExceptions.GetInternalServerErrorException(fullTargetAddress, activityId, inner);
        }

        private static void SetKeepAlive(Socket socket)
        {
            // Socket.IOControl isn't supported fully on .NET Standard yet.
            // Use the default keep-alive settings.
            // See also: RuntimeInformation.IsOSPlatform(OSPlatform),
            // https://github.com/dotnet/corefx/issues/25040.
#if NETFX
            //  struct tcp_keepalive
            //  {
            //      u_long  onoff;
            //      u_long  keepalivetime;
            //      u_long  keepaliveinterval;
            //  };
            byte[] keepAliveValues = new byte[3 * sizeof(UInt32)];
            Buffer.BlockCopy(RntbdConnection.KeepAliveOn, 0, keepAliveValues, 0, 4);
            Buffer.BlockCopy(RntbdConnection.KeepAliveTimeInMilliseconds, 0, keepAliveValues, 4, 4);
            Buffer.BlockCopy(RntbdConnection.KeepAliveIntervalInMilliseconds, 0, keepAliveValues, 8, 4);

            byte[] keepAliveOutput = new byte[sizeof(UInt32)];
            socket.IOControl(IOControlCode.KeepAliveValues, keepAliveValues, keepAliveOutput);
#endif
        }

        #endregion
    }
}
