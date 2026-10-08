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
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.FaultInjection;
    using Microsoft.Azure.Documents.Telemetry;
#if NETSTANDARD15 || NETSTANDARD16
    using Trace = Microsoft.Azure.Documents.Trace;
#endif

    internal class TransportClient :
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

        // Retained so the CMTransportClient parallel path can build its cloned
        // CMChannelDictionary from the exact same ChannelProperties (timer pools,
        // timeouts, port pool, TLS callbacks) the legacy path uses. Behavior-neutral
        // for the legacy path: this is the same object already passed to
        // channelDictionary above.
        protected readonly ChannelProperties channelProperties;
        private bool disposed = false;

        private readonly DistributedTracingOptions DistributedTracingOptions;

        #region RNTBD Transition

        // Transitional state while migrating SDK users from the old RNTBD stack
        // to the new version. Delete these fields when the old stack is deleted.
        private readonly object disableRntbdChannelLock = new object();
        // Guarded by disableRntbdChannelLock
        private bool disableRntbdChannel = false;

#endregion

        public TransportClient(Options clientOptions, IChaosInterceptor chaosInterceptor = null)
        {
            if (clientOptions == null)
            {
                throw new ArgumentNullException(nameof(clientOptions));
            }

            // The only permitted subclass is CMTransportClient (the RNTBD
            // connection-manager parallel path). This class is unsealed solely
            // to support that single fork; any other derivation is a bug. The
            // check is behavior-neutral for the legacy path (this.GetType() is
            // exactly TransportClient) and preserves the "flag-off is identical
            // to pre-feature" guarantee.
            if (this.GetType() != typeof(TransportClient) &&
                this.GetType() != typeof(CMTransportClient))
            {
                throw new InvalidOperationException(
                    "Rntbd.TransportClient may only be derived by Rntbd.CMTransportClient.");
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

            this.channelProperties = new ChannelProperties(
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
                    clientOptions.ClientCertificateFunction,
                    clientOptions.ClientCertificateFailureHandler,
                    clientOptions.DnsResolutionFunction);

            this.channelDictionary = new ChannelDictionary(
                this.channelProperties,
                chaosInterceptor);
        }

        /// <summary>
        /// Single seam through which all three request/prewarm paths obtain an
        /// <see cref="IChannel"/>. The base implementation delegates to the
        /// legacy per-endpoint <see cref="ChannelDictionary"/>; this is
        /// behavior-identical to the previous inline
        /// <c>channelDictionary.GetChannel(...)</c> call. <see cref="CMTransportClient"/>
        /// overrides this method to fork to the connection-manager graph when
        /// the feature flag is on. Keeping the fork here (rather than overriding
        /// whole request methods) means both flag states still run the base
        /// request-method body — protocol-downgrade, exception translation,
        /// perf counters and OpenTelemetry are inherited identically.
        /// </summary>
        protected virtual IChannel GetChannel(Uri requestUri, bool localRegionRequest)
        {
            return this.channelDictionary.GetChannel(requestUri, localRegionRequest);
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
            try
            {
                TransportClient.IncrementCounters();

                operation = "GetChannel";
                // Treat all retries as out of region request for open timeout. This is to prevent too many retries because of the shorter time duration.
                bool localRegionRequest = request.RequestContext.IsRetry ? false : request.RequestContext.LocalRegionRequest;
                IChannel channel = this.GetChannel(physicalAddress.Uri, localRegionRequest);

                TransportClient.GetTransportPerformanceCounters().IncrementRntbdRequestCount(resourceOperation.resourceType, resourceOperation.operationType);

                operation = "RequestAsync";
                storeResponse = await channel.RequestAsync(request, physicalAddress,
                    resourceOperation, activityId, transportRequestStats);
                transportRequestStats.RecordState(TransportRequestStats.RequestStage.Completed);
                storeResponse.TransportRequestStats = transportRequestStats;
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
                    resourceOperation, physicalAddress, ex.Message);
                if (request.IsReadOnlyRequest)
                {
                    DefaultTrace.TraceInformation("Converting to Gone (read-only request)");
                    GoneException goneExcepetion = TransportExceptions.GetGoneException(
                        physicalAddress.Uri, activityId, ex, transportRequestStats);
#if NETSTANDARD2_0_OR_GREATER
                    recorder?.Record(physicalAddress.Uri, exception: goneExcepetion);
#endif
                    throw goneExcepetion;
                }
                if (!ex.UserRequestSent)
                {
                    DefaultTrace.TraceInformation("Converting to Gone (write request, not sent)");
                    GoneException goneExcepetion = TransportExceptions.GetGoneException(
                        physicalAddress.Uri, activityId, ex, transportRequestStats);
#if NETSTANDARD2_0_OR_GREATER
                    recorder?.Record(physicalAddress.Uri, exception: goneExcepetion);
#endif
                    throw goneExcepetion;
                }
                if (TransportException.IsTimeout(ex.ErrorCode))
                {
                    DefaultTrace.TraceInformation("Converting to RequestTimeout");
                    RequestTimeoutException requestTimeoutException = TransportExceptions.GetRequestTimeoutException(
                        physicalAddress.Uri, activityId, ex, transportRequestStats);
#if NETSTANDARD2_0_OR_GREATER
                    recorder?.Record(physicalAddress.Uri, exception: requestTimeoutException);
#endif
                    throw requestTimeoutException;
                }
                DefaultTrace.TraceInformation("Converting to ServiceUnavailable");
                ServiceUnavailableException serviceUnavailableException = TransportExceptions.GetServiceUnavailableException(
                    physicalAddress.Uri, activityId, ex, transportRequestStats);
#if NETSTANDARD2_0_OR_GREATER
                recorder?.Record(physicalAddress.Uri, exception: serviceUnavailableException);
#endif
                throw serviceUnavailableException;

            }
            catch (DocumentClientException ex)
            {
                transportResponseStatusCode = (int)TransportResponseStatusCode.DocumentClientException;
                DefaultTrace.TraceInformation("{0} failed: RID: {1}, Resource Type: {2}, Op: {3}, Address: {4}, " +
                                              "Exception: {5}", operation, request.ResourceAddress, request.ResourceType, resourceOperation,
                    physicalAddress, ex.Message);
                transportRequestStats.RecordState(TransportRequestStats.RequestStage.Failed);
                ex.TransportRequestStats = transportRequestStats;
#if NETSTANDARD2_0_OR_GREATER
                recorder?.Record(physicalAddress.Uri, exception: ex);
#endif
                throw;
            }
            catch (Exception ex)
            {
                transportResponseStatusCode = (int)TransportResponseStatusCode.UnknownException;
                DefaultTrace.TraceInformation("{0} failed: RID: {1}, Resource Type: {2}, Op: {3}, Address: {4}, " +
                    "Exception: {5}", operation, request.ResourceAddress, request.ResourceType, resourceOperation,
                    physicalAddress, ex.Message);
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
                recorder?.Record(physicalAddress.Uri, exception: exception);
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

        /// <summary>
        /// Exceptionless variant of <see cref="InvokeStoreAsync(TransportAddressUri, ResourceOperation, DocumentServiceRequest)"/>.
        /// Calls <see cref="Channel.TryRequestAsync"/> and converts <see cref="TransportException"/>
        /// to the same <see cref="DocumentClientException"/> wrappers as the throwing path, but
        /// returns them inside <see cref="Result{T}"/> instead of throwing.
        /// </summary>
        internal override async Task<Res<StoreResponse>> TryInvokeStoreAsync(
            TransportAddressUri physicalAddress,
            ResourceOperation resourceOperation,
            DocumentServiceRequest request)
        {
            this.ThrowIfDisposed();
            Guid activityId = Trace.CorrelationManager.ActivityId;

            if (!request.IsBodySeekableClonableAndCountable)
            {
                return Res.FromException<StoreResponse>(new InternalServerErrorException());
            }

            StoreResponse storeResponse = null;
            TransportRequestStats transportRequestStats = new TransportRequestStats();
            DateTime requestStartTime = DateTime.UtcNow;
            int transportResponseStatusCode = (int)TransportResponseStatusCode.Success;

            try
            {
                TransportClient.IncrementCounters();

                // Treat all retries as out of region request for open timeout. This is to prevent too many retries because of the shorter time duration.
                bool localRegionRequest = request.RequestContext.IsRetry ? false : request.RequestContext.LocalRegionRequest;
                IChannel channel = this.GetChannel(physicalAddress.Uri, localRegionRequest);

                TransportClient.GetTransportPerformanceCounters().IncrementRntbdRequestCount(resourceOperation.resourceType, resourceOperation.operationType);

                Res<StoreResponse> result = await channel.TryRequestAsync(request, physicalAddress,
                    resourceOperation, activityId, transportRequestStats);

                if (!result.IsSuccess)
                {
                    Exception ex = result.Exception;
                    if (ex is TransportException transportEx)
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
                        transportResponseStatusCode = (int)transportEx.ErrorCode;
                        transportEx.RequestStartTime = requestStartTime;
                        transportEx.RequestEndTime = DateTime.UtcNow;
                        transportEx.OperationType = resourceOperation.operationType;
                        transportEx.ResourceType = resourceOperation.resourceType;
                        TransportClient.GetTransportPerformanceCounters().IncrementRntbdResponseCount(resourceOperation.resourceType,
                            resourceOperation.operationType, (int)transportEx.ErrorCode);

                        DocumentClientException wrappedException;
                        if (request.IsReadOnlyRequest || !transportEx.UserRequestSent)
                        {
                            wrappedException = TransportExceptions.GetGoneException(
                                physicalAddress.Uri, activityId, transportEx, transportRequestStats);
                        }
                        else if (TransportException.IsTimeout(transportEx.ErrorCode))
                        {
                            wrappedException = TransportExceptions.GetRequestTimeoutException(
                                physicalAddress.Uri, activityId, transportEx, transportRequestStats);
                        }
                        else
                        {
                            wrappedException = TransportExceptions.GetServiceUnavailableException(
                                physicalAddress.Uri, activityId, transportEx, transportRequestStats);
                        }

                        return Res.FromException<StoreResponse>(wrappedException);
                    }

                    if (ex is DocumentClientException dce)
                    {
                        transportResponseStatusCode = (int)TransportResponseStatusCode.DocumentClientException;
                        transportRequestStats.RecordState(TransportRequestStats.RequestStage.Failed);
                        dce.TransportRequestStats = transportRequestStats;
                    }

                    return Res.FromException<StoreResponse>(ex);
                }

                storeResponse = result.Value;
                transportRequestStats.RecordState(TransportRequestStats.RequestStage.Completed);
                storeResponse.TransportRequestStats = transportRequestStats;
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
            catch (DocumentClientException ex)
            {
                return Res.FromException<StoreResponse>(ex);
            }

            return Res.Success(storeResponse);
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
            IChannel channel = this.GetChannel(
                requestUri: physicalAddress,
                localRegionRequest: false);

            return channel.OpenChannelAsync(
                    activityId: Trace.CorrelationManager.ActivityId);
        }

#region RNTBD Transition

        public event Action OnDisableRntbdChannel;

        public Func<bool> IsDisableRntbdChannelActionRegistered
        {
            get
            {
                return () =>
                {
                    return (this.OnDisableRntbdChannel != null);
                };
            }
        }

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
                        failedTask.Exception?.Message);
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

            public Func<string, X509Certificate2> ClientCertificateFunction { get; internal set; }

            public Action<string, Exception> ClientCertificateFailureHandler { get; internal set; }

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
