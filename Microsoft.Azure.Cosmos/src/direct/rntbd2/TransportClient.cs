//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net.Security;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Telemetry;
#if NETSTANDARD15 || NETSTANDARD16
    using Trace = Microsoft.Azure.Documents.Trace;
#endif

    internal sealed class TransportClient :
        Microsoft.Azure.Documents.TransportClient, IDisposable
    {
        private enum TransportResponseStatusCode
        {
            Success = 0,
            DocumentClientException = -1,
            UnknownException = -2
        }
        private static TransportPerformanceCounters transportPerformanceCounters = new TransportPerformanceCounters();

        private readonly TimerPool TimerPool;
        private readonly TimerPool IdleTimerPool;
        private readonly ChannelDictionary channelDictionary;
        private bool disposed = false;

        private readonly DistributedTracingOptions DistributedTracingOptions;

        #region RNTBD Transition

        // Transitional state while migrating SDK users from the old RNTBD stack
        // to the new version. Delete these fields when the old stack is deleted.
        private readonly object disableRntbdChannelLock = new object();
        // Guarded by disableRntbdChannelLock
        private bool disableRntbdChannel = false;

#endregion

        public TransportClient(Options clientOptions)
        {
            if (clientOptions == null)
            {
                throw new ArgumentNullException(nameof(clientOptions));
            }

            TransportClient.LogClientOptions(clientOptions);

            UserPortPool userPortPool = null;
            if (clientOptions.PortReuseMode == PortReuseMode.PrivatePortPool)
            {
                userPortPool = new UserPortPool(
                    clientOptions.PortPoolReuseThreshold,
                    clientOptions.PortPoolBindAttempts);
            }

            this.TimerPool = new TimerPool((int)clientOptions.TimerPoolResolution.TotalSeconds);
            if (clientOptions.IdleTimeout > TimeSpan.Zero)
            {
                this.IdleTimerPool = new TimerPool(minSupportedTimerDelayInSeconds: 30);
            }
            else
            {
                this.IdleTimerPool = null;
            }

            this.DistributedTracingOptions = clientOptions.DistributedTracingOptions;

            this.channelDictionary = new ChannelDictionary(
                new ChannelProperties(
                    clientOptions.UserAgent,
                    clientOptions.CertificateHostNameOverride,
                    clientOptions.ConnectionStateListener,
                    this.TimerPool,
                    clientOptions.RequestTimeout,
                    clientOptions.OpenTimeout,
                    clientOptions.LocalRegionOpenTimeout,
                    clientOptions.PortReuseMode,
                    userPortPool,
                    clientOptions.MaxChannels,
                    clientOptions.PartitionCount,
                    clientOptions.MaxRequestsPerChannel,
                    clientOptions.MaxConcurrentOpeningConnectionCount,
                    clientOptions.ReceiveHangDetectionTime,
                    clientOptions.SendHangDetectionTime,
                    clientOptions.IdleTimeout,
                    this.IdleTimerPool,
                    clientOptions.CallerId,
                    clientOptions.EnableChannelMultiplexing,
                    clientOptions.MemoryStreamPool,
                    clientOptions.RemoteCertificateValidationCallback,
                    clientOptions.DnsResolutionFunction));
        }

        internal override Task<StoreResponse> InvokeStoreAsync(
            Uri physicalAddress,
            ResourceOperation resourceOperation,
            DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(new TransportAddressUri(physicalAddress), resourceOperation, request);
        }

        internal override async Task<StoreResponse> InvokeStoreAsync(
            TransportAddressUri physicalAddress, ResourceOperation resourceOperation,
            DocumentServiceRequest request)
        {
            this.ThrowIfDisposed();
            Guid activityId = Trace.CorrelationManager.ActivityId;

            if (!request.IsBodySeekableClonableAndCountable)
            {
                throw new InternalServerErrorException();
            }

            StoreResponse storeResponse = null;
            TransportRequestStats transportRequestStats = new TransportRequestStats();
            string operation = "Unknown operation";
            DateTime requestStartTime = DateTime.UtcNow;
            int transportResponseStatusCode = (int)TransportResponseStatusCode.Success;

#if NETSTANDARD2_0_OR_GREATER
            using OpenTelemetryRecorder recorder = OpenTelemetryRecorderFactory.CreateRecorder(
                                                                                    options: this.DistributedTracingOptions,
                                                                                    request: request);
#endif
            IChannel channel = null;
            try
            {
                TransportClient.IncrementCounters();

                operation = "GetChannel";
                // Treat all retries as out of region request for open timeout. This is to prevent too many retries because of the shorter time duration.
                bool localRegionRequest = request.RequestContext.IsRetry ? false : request.RequestContext.LocalRegionRequest;
                channel = this.channelDictionary.GetChannel(physicalAddress.Uri, localRegionRequest);

                TransportClient.GetTransportPerformanceCounters().IncrementRntbdRequestCount(resourceOperation.resourceType, resourceOperation.operationType);

                operation = "RequestAsync";
                storeResponse = await channel.RequestAsync(request, physicalAddress,
                    resourceOperation, activityId, transportRequestStats);
                transportRequestStats.RecordState(TransportRequestStats.RequestStage.Completed);
                storeResponse.TransportRequestStats = transportRequestStats;
                channel?.SetHealthState(
                    isHealthy: true);
            }
            catch (TransportException ex)
            {
                // App-compat shim: On transport failure, TransportClient callers
                // expect one of:
                // - GoneException - widely abused to mean "refresh the address
                //   cache and try again".
                // - RequestTimeoutException - means what it says, but it's a
                //   non-retriable error.
                // - ServiceUnavailableException - abused to mean "non-retriable
                //   error other than timeout". Endless source of customer
                //   confusion.
                //
                // Traditionally, the transport client has converted timeouts to
                // RequestTimeoutException or GoneException based on whether
                // the request was a write (non-retriable) or a read (retriable).
                // This design leads to a low-level piece of code driving a
                // component much higher in the stack (the retry loop)
                // based on high-level information (request types).
                // Low-level code should only return errors describing what went
                // wrong at its level and provide enough information that callers
                // can decide what to do.
                //
                // Until the retry loop can be fixed so that it handles
                // TransportException directly, don't allow TransportException
                // to escape, and wrap it in an expected DocumentClientException
                // instead. Tracked in backlog item 303368.

                transportRequestStats.RecordState(TransportRequestStats.RequestStage.Failed);
                transportResponseStatusCode = (int) ex.ErrorCode;
                ex.RequestStartTime = requestStartTime;
                ex.RequestEndTime = DateTime.UtcNow;
                ex.OperationType = resourceOperation.operationType;
                ex.ResourceType = resourceOperation.resourceType;
                TransportClient.GetTransportPerformanceCounters().IncrementRntbdResponseCount(resourceOperation.resourceType,
                    resourceOperation.operationType, (int) ex.ErrorCode);

                DefaultTrace.TraceInformation(
                    "{0} failed: RID: {1}, Resource Type: {2}, Op: {3}, Address: {4}, " +
                    "Exception: {5}",
                    operation, request.ResourceAddress, request.ResourceType,
                    resourceOperation, physicalAddress, ex);

                channel?.SetHealthState(
                    isHealthy: false);

                if (request.IsReadOnlyRequest)
                {
                    DefaultTrace.TraceInformation("Converting to Gone (read-only request)");
                    GoneException goneExcepetion = TransportExceptions.GetGoneException(
                        physicalAddress.Uri, activityId, ex, transportRequestStats);
#if NETSTANDARD2_0_OR_GREATER
                    recorder?.Record(physicalAddress.Uri, documentClientException: goneExcepetion);
#endif
                    throw goneExcepetion;
                }
                if (!ex.UserRequestSent)
                {
                    DefaultTrace.TraceInformation("Converting to Gone (write request, not sent)");
                    GoneException goneExcepetion = TransportExceptions.GetGoneException(
                        physicalAddress.Uri, activityId, ex, transportRequestStats);
#if NETSTANDARD2_0_OR_GREATER
                    recorder?.Record(physicalAddress.Uri, documentClientException: goneExcepetion);
#endif
                    throw goneExcepetion;
                }
                if (TransportException.IsTimeout(ex.ErrorCode))
                {
                    DefaultTrace.TraceInformation("Converting to RequestTimeout");
                    RequestTimeoutException requestTimeoutException = TransportExceptions.GetRequestTimeoutException(
                        physicalAddress.Uri, activityId, ex, transportRequestStats);
#if NETSTANDARD2_0_OR_GREATER
                    recorder?.Record(physicalAddress.Uri, documentClientException: requestTimeoutException);
#endif
                    throw requestTimeoutException;
                }
                DefaultTrace.TraceInformation("Converting to ServiceUnavailable");
                ServiceUnavailableException serviceUnavailableException = TransportExceptions.GetServiceUnavailableException(
                    physicalAddress.Uri, activityId, ex, transportRequestStats);
#if NETSTANDARD2_0_OR_GREATER
                recorder?.Record(physicalAddress.Uri, documentClientException: serviceUnavailableException);
#endif
                throw serviceUnavailableException;

            }
            catch (DocumentClientException ex)
            {
                channel?.SetHealthState(
                    isHealthy: false);

                transportResponseStatusCode = (int)TransportResponseStatusCode.DocumentClientException;
                DefaultTrace.TraceInformation("{0} failed: RID: {1}, Resource Type: {2}, Op: {3}, Address: {4}, " +
                                              "Exception: {5}", operation, request.ResourceAddress, request.ResourceType, resourceOperation,
                    physicalAddress, ex);
                transportRequestStats.RecordState(TransportRequestStats.RequestStage.Failed);
                ex.TransportRequestStats = transportRequestStats;
#if NETSTANDARD2_0_OR_GREATER
                recorder?.Record(physicalAddress.Uri, documentClientException: ex);
#endif
                throw;
            }
            catch (Exception ex)
            {
                channel?.SetHealthState(
                    isHealthy: false);

                transportResponseStatusCode = (int)TransportResponseStatusCode.UnknownException;
                DefaultTrace.TraceInformation("{0} failed: RID: {1}, Resource Type: {2}, Op: {3}, Address: {4}, " +
                    "Exception: {5}", operation, request.ResourceAddress, request.ResourceType, resourceOperation,
                    physicalAddress, ex);
#if NETSTANDARD2_0_OR_GREATER
                recorder?.Record(physicalAddress.Uri, exception: ex);
#endif
                throw;
            }
            finally
            {
                TransportClient.DecrementCounters();
                TransportClient.GetTransportPerformanceCounters().IncrementRntbdResponseCount(resourceOperation.resourceType,
                    resourceOperation.operationType, transportResponseStatusCode);
                this.RaiseProtocolDowngradeRequest(storeResponse);
            }

            try
            {
                TransportClient.ThrowServerException(request.ResourceAddress, storeResponse, physicalAddress.Uri, activityId, request);
            }
#if NETSTANDARD2_0_OR_GREATER
            catch (DocumentClientException exception) 
            {
                recorder?.Record(physicalAddress.Uri, documentClientException: exception);
                throw;
            }

            // Record the information of the sucessfull response in the end, it also make sure it is not getting called twice.
            recorder?.Record(physicalAddress.Uri, storeResponse: storeResponse);
#else
            catch (DocumentClientException)
            {
                throw;
            }
#endif
            return storeResponse;
        }

        public override void Dispose()
        {
            this.ThrowIfDisposed();
            this.disposed = true;
            this.channelDictionary.Dispose();

            if (this.IdleTimerPool != null)
            {
                this.IdleTimerPool.Dispose();
            }

            this.TimerPool.Dispose();

            base.Dispose();

            DefaultTrace.TraceInformation("Rntbd.TransportClient disposed.");
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(TransportClient));
            }
        }

        private static void LogClientOptions(Options clientOptions)
        {
            DefaultTrace.TraceInformation("Creating RNTBD TransportClient with options {0}", clientOptions.ToString());
        }

        private static void IncrementCounters()
        {
#if NETFX
            if (PerfCounters.Counters.BackendActiveRequests != null)
            {
                PerfCounters.Counters.BackendActiveRequests.Increment();
            }
            if (PerfCounters.Counters.BackendRequestsPerSec != null)
            {
                PerfCounters.Counters.BackendRequestsPerSec.Increment();
            }
#endif
        }

        private static void DecrementCounters()
        {
#if NETFX
            if (PerfCounters.Counters.BackendActiveRequests != null)
            {
                PerfCounters.Counters.BackendActiveRequests.Decrement();
            }
#endif
        }

        /// <inheritdoc/>
        internal override Task OpenConnectionAsync(
            Uri physicalAddress)
        {
            Guid activityId = Trace.CorrelationManager.ActivityId;
            return this.channelDictionary.OpenChannelAsync(
                physicalAddress: physicalAddress,
                localRegionRequest: false,
                activityId: activityId);
        }

#region RNTBD Transition

        public event Action OnDisableRntbdChannel;

        // Examines storeResponse and raises an event if this is the first time
        // this transport client sees the "disable RNTBD channel" header set to
        // true by the back-end.
        private void RaiseProtocolDowngradeRequest(StoreResponse storeResponse)
        {
            if (storeResponse == null)
            {
                return;
            }
            string disableRntbdChannelHeader = null;
            if (!storeResponse.TryGetHeaderValue(HttpConstants.HttpHeaders.DisableRntbdChannel, out disableRntbdChannelHeader))
            {
                return;
            }
            if (!string.Equals(disableRntbdChannelHeader, "true"))
            {
                return;
            }

            bool raiseRntbdChannelDisable = false;
            lock (this.disableRntbdChannelLock)
            {
                if (this.disableRntbdChannel)
                {
                    return;
                }
                this.disableRntbdChannel = true;
                raiseRntbdChannelDisable = true;
            }
            if (!raiseRntbdChannelDisable)
            {
                return;
            }

            // Schedule execution on current .NET task scheduler.
            // Compute gateway uses custom task scheduler to track tenant resource utilization.
            // Task.Run() switches to default task scheduler for entire sub-tree of tasks making compute gateway incapable of tracking resource usage accurately.
            // Task.Factory.StartNew() allows specifying task scheduler to use.
            Task.Factory.StartNewOnCurrentTaskSchedulerAsync(() =>
            {
                this.OnDisableRntbdChannel?.Invoke();
            })
            .ContinueWith(
                failedTask =>
                {
                    DefaultTrace.TraceError(
                        "RNTBD channel callback failed: {0}",
                        failedTask.Exception);
                },
                default(CancellationToken),
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Current);
        }

#endregion

        public sealed class Options
        {
            private UserAgentContainer userAgent = null;
            private TimeSpan openTimeout = TimeSpan.Zero;
            private TimeSpan localRegionOpenTimeout = TimeSpan.Zero;
            private TimeSpan timerPoolResolution = TimeSpan.Zero;

            public Options(TimeSpan requestTimeout)
            {
                Debug.Assert(requestTimeout > TimeSpan.Zero);
                this.RequestTimeout = requestTimeout;
                this.MaxChannels = ushort.MaxValue;
                this.PartitionCount = 1;
                this.MaxRequestsPerChannel = 30;
                this.PortReuseMode = PortReuseMode.ReuseUnicastPort;
                this.PortPoolReuseThreshold = 256;
                this.PortPoolBindAttempts = 5;
                this.ReceiveHangDetectionTime = TimeSpan.FromSeconds(65.0);
                this.SendHangDetectionTime = TimeSpan.FromSeconds(10.0);
                this.IdleTimeout = TimeSpan.FromSeconds(1800);
                this.CallerId = RntbdConstants.CallerId.Anonymous;
                this.EnableChannelMultiplexing = false;
                this.MaxConcurrentOpeningConnectionCount = ushort.MaxValue;
                this.DnsResolutionFunction = null;
                this.DistributedTracingOptions = null;
            }

            public TimeSpan RequestTimeout { get; private set; }
            public int MaxChannels { get; set; }
            public int PartitionCount { get; set; }
            public int MaxRequestsPerChannel { get; set; }
            public TimeSpan ReceiveHangDetectionTime { get; set; }
            public TimeSpan SendHangDetectionTime { get; set; }
            public TimeSpan IdleTimeout { get; set; }
            public RntbdConstants.CallerId CallerId { get; set; }
            public bool EnableChannelMultiplexing { get; set; }

            public Microsoft.Azure.Documents.MemoryStreamPool MemoryStreamPool { get; set; }

            public UserAgentContainer UserAgent
            {
                get
                {
                    if (this.userAgent != null)
                    {
                        return this.userAgent;
                    }
                    this.userAgent = new UserAgentContainer();
                    return this.userAgent;
                }
                set { this.userAgent = value; }
            }

            public string CertificateHostNameOverride { get; set; }

            public IConnectionStateListener ConnectionStateListener { get; set; }

            public TimeSpan OpenTimeout
            {
                get
                {
                    if (this.openTimeout > TimeSpan.Zero)
                    {
                        return this.openTimeout;
                    }
                    return this.RequestTimeout;
                }
                set { this.openTimeout = value; }
            }

            public TimeSpan LocalRegionOpenTimeout
            {
                get
                {
                    if (this.localRegionOpenTimeout > TimeSpan.Zero)
                    {
                        return this.localRegionOpenTimeout;
                    }
                    return this.OpenTimeout;
                }
                set { this.localRegionOpenTimeout = value; }
            }

            public PortReuseMode PortReuseMode { get; set; }

            public int PortPoolReuseThreshold { get; internal set; }

            public int PortPoolBindAttempts { get; internal set; }

            public TimeSpan TimerPoolResolution
            {
                get
                {
                    return Options.GetTimerPoolResolutionSeconds(
                        this.timerPoolResolution, this.RequestTimeout, this.openTimeout);
                }
                set { this.timerPoolResolution = value; }
            }

            public int MaxConcurrentOpeningConnectionCount { get; set; }

            public RemoteCertificateValidationCallback RemoteCertificateValidationCallback { get; internal set; }

            /// <summary>
            /// Override for DNS resolution callbacks for RNTBD connections.
            /// </summary>
            public Func<string, Task<System.Net.IPAddress>> DnsResolutionFunction { get; internal set; }

            /// <summary>
            /// Distributed Tracing Options
            /// </summary>
            public DistributedTracingOptions DistributedTracingOptions { get; set; }

            public override string ToString()
            {
                StringBuilder s = new StringBuilder();
                s.AppendLine("Rntbd.TransportClient.Options");
                s.Append("  OpenTimeout: ");
                s.AppendLine(this.OpenTimeout.ToString("c"));
                s.Append("  RequestTimeout: ");
                s.AppendLine(this.RequestTimeout.ToString("c"));
                s.Append("  TimerPoolResolution: ");
                s.AppendLine(this.TimerPoolResolution.ToString("c"));
                s.Append("  MaxChannels: ");
                s.AppendLine(this.MaxChannels.ToString(CultureInfo.InvariantCulture));
                s.Append("  PartitionCount: ");
                s.AppendLine(this.PartitionCount.ToString(CultureInfo.InvariantCulture));
                s.Append("  MaxRequestsPerChannel: ");
                s.AppendLine(this.MaxRequestsPerChannel.ToString(CultureInfo.InvariantCulture));
                s.Append("  ReceiveHangDetectionTime: ");
                s.AppendLine(this.ReceiveHangDetectionTime.ToString("c"));
                s.Append("  SendHangDetectionTime: ");
                s.AppendLine(this.SendHangDetectionTime.ToString("c"));
                s.Append("  IdleTimeout: ");
                s.AppendLine(this.IdleTimeout.ToString("c"));
                s.Append("  UserAgent: ");
                s.Append(this.UserAgent.UserAgent);
                s.Append(" Suffix: ");
                s.AppendLine(this.UserAgent.Suffix);
                s.Append("  CertificateHostNameOverride: ");
                s.AppendLine(this.CertificateHostNameOverride);
                s.Append("  LocalRegionTimeout: ");
                s.AppendLine(this.LocalRegionOpenTimeout.ToString("c"));
                s.Append("  EnableChannelMultiplexing: ");
                s.AppendLine(this.EnableChannelMultiplexing.ToString());
                s.Append("  MaxConcurrentOpeningConnectionCount: ");
                s.AppendLine(this.MaxConcurrentOpeningConnectionCount.ToString(CultureInfo.InvariantCulture));
                s.Append("  Use_RecyclableMemoryStream: ");
                s.AppendLine(this.MemoryStreamPool != null ? bool.TrueString : bool.FalseString);
                s.Append("  Use_CustomDnsResolution: ");
                s.AppendLine(this.DnsResolutionFunction != null ? bool.TrueString : bool.FalseString);
                s.Append("  IsDistributedTracingEnabled: ");
#if NETSTANDARD2_0_OR_GREATER
                s.AppendLine(this.DistributedTracingOptions?.IsDistributedTracingEnabled.ToString());
#else
                s.AppendLine("false");
#endif
                return s.ToString();
            }

            private static TimeSpan GetTimerPoolResolutionSeconds(
                TimeSpan timerPoolResolution, TimeSpan requestTimeout, TimeSpan openTimeout)
            {
                Debug.Assert(timerPoolResolution > TimeSpan.Zero ||
                    openTimeout > TimeSpan.Zero ||
                    requestTimeout > TimeSpan.Zero);
                if (timerPoolResolution > TimeSpan.Zero &&
                    timerPoolResolution < openTimeout &&
                    timerPoolResolution < requestTimeout)
                {
                    return timerPoolResolution;
                }
                if (openTimeout > TimeSpan.Zero && requestTimeout > TimeSpan.Zero)
                {
                    return openTimeout < requestTimeout ? openTimeout : requestTimeout;
                }
                return openTimeout > TimeSpan.Zero ? openTimeout : requestTimeout;
            }

        }

        internal static void SetTransportPerformanceCounters(TransportPerformanceCounters transportPerformanceCounters)
        {
            if (transportPerformanceCounters == null)
            {
                throw new ArgumentNullException(nameof(transportPerformanceCounters));
            }

            TransportClient.transportPerformanceCounters = transportPerformanceCounters;
        }

        internal static TransportPerformanceCounters GetTransportPerformanceCounters()
        {
            return transportPerformanceCounters;
        }
    }
}
