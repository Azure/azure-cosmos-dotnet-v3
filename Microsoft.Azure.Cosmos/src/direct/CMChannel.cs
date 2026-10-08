//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.FaultInjection;
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

#if NETSTANDARD15 || NETSTANDARD16
    using Trace = Microsoft.Azure.Documents.Trace;
#endif

    // The RNTBD RPC channel. Supports multiple parallel requests and timeouts.
    internal sealed class CMChannel : IChannel, IDisposable
    {
        private readonly Dispatcher dispatcher;
        private readonly TimerPool timerPool;
        private readonly int requestTimeoutSeconds;
        private readonly Uri serverUri;
        private readonly bool localRegionRequest;
        private readonly ReaderWriterLockSlim stateLock =
            new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private readonly SemaphoreSlim openingSlim;
        private readonly IChaosInterceptor chaosInterceptor;

        private bool disposed = false;
        private State state = State.New;  // Guarded by stateLock.
        private Task initializationTask = null;  // Guarded by stateLock.
        private volatile bool isInitializationComplete = false;

        private ChannelOpenArguments openArguments;
        // Cached reference to the open's timeline so the manager can read
        // per-stage TCP / TLS / RNTBD durations after the open completes.
        // openArguments is nulled in InitializeWithoutTimerAsync's finally;
        // this field outlives that and stays readable for the lifetime of
        // the CMChannel.
        private ChannelOpenTimeline openTimeline;

        /// <summary>
        /// Per-stage timing information captured during this CMChannel's open.
        /// Populated incrementally by the open path (TCP connect → TLS handshake
        /// → RNTBD context). Safe to read after the open completes (success or
        /// failure). Stage timestamps are null for any stage the open didn't
        /// reach — useful for deriving which stage a failed open got stuck on.
        /// </summary>
        internal ChannelOpenTimeline OpenTimeline => this.openTimeline;

        public CMChannel(
            Guid activityId,
            Uri serverUri,
            ChannelProperties channelProperties,
            bool localRegionRequest, SemaphoreSlim openingSlim,
            IChaosInterceptor chaosInterceptor = null,
            Func<Guid, Guid, Uri, CMChannel, Task> onChannelOpen = null,
            bool deferInitialize = false)
        {
            Debug.Assert(channelProperties != null);
            this.dispatcher = new Dispatcher(serverUri,
                channelProperties.UserAgent,
                channelProperties.ConnectionStateListener,
                channelProperties.CertificateHostNameOverride,
                channelProperties.ReceiveHangDetectionTime,
                channelProperties.SendHangDetectionTime,
                channelProperties.IdleTimerPool,
                channelProperties.IdleTimeout,
                channelProperties.EnableChannelMultiplexing,
                channelProperties.MemoryStreamPool,
                channelProperties.RemoteCertificateValidationCallback,
                channelProperties.ClientCertificateFunction,
                channelProperties.ClientCertificateFailureHandler,
                channelProperties.DnsResolutionFunction,
                chaosInterceptor);
            this.timerPool = channelProperties.RequestTimerPool;
            this.requestTimeoutSeconds = (int) channelProperties.RequestTimeout.TotalSeconds;
            this.serverUri = serverUri;
            this.localRegionRequest = localRegionRequest;
            this.chaosInterceptor = chaosInterceptor;

            TimeSpan openTimeout = localRegionRequest ? channelProperties.LocalRegionOpenTimeout : channelProperties.OpenTimeout;

            this.openTimeline = new ChannelOpenTimeline();
            this.openArguments = new ChannelOpenArguments(
                activityId, this.openTimeline,
                openTimeout,
                channelProperties.PortReuseMode,
                channelProperties.UserPortPool,
                channelProperties.CallerId);

            this.openingSlim = openingSlim;
            if (!deferInitialize)
            {
                this.Initialize(activityId, onChannelOpen);
            }
        }

        public void InjectFaultInjectionConnectionError(TransportException transportException)
        {
            if (!this.disposed)
            {
                this.dispatcher.InjectFaultInjectionConnectionError(transportException);
            }
        }

        public Uri GetServerUri()
        {
            this.ThrowIfDisposed();
            return this.serverUri;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool Healthy
        {
            get
            {
                this.ThrowIfDisposed();
                Dispatcher dispatcher = null;
                this.stateLock.EnterReadLock();
                try
                {
                    switch (this.state)
                    {
                    case State.Open:
                        dispatcher = this.dispatcher;
                        break;


                    case State.WaitingToOpen:
                    case State.Opening:
                        return true;

                    case State.Closed:
                        return false;

                    case State.New:
                        Debug.Assert(false,
                            "Channel.Healthy called before Initialize()");
                        return false;

                    default:
                        Debug.Assert(false, "Unhandled state");
                        return false;
                    }
                }
                finally
                {
                    this.stateLock.ExitReadLock();
                }
                Debug.Assert(dispatcher != null);
                return dispatcher.Healthy;
            }
        }

        private Guid ConnectionCorrelationId { get => this.dispatcher.ConnectionCorrelationId; }

        private void Initialize(Guid activityId, Func<Guid, Guid, Uri, CMChannel, Task> onChannelOpen = null)
        {
            this.ThrowIfDisposed();
            this.stateLock.EnterWriteLock();
            try
            {
                Debug.Assert(this.state == State.New);
                this.state = State.WaitingToOpen;
                Debug.Assert(this.initializationTask == null);

                // Initialization should use a task scheduler internal to the Cosmos DB
                // client or the default scheduler. Avoid using the current scheduler.
                // Some components use custom task schedulers for accounting or to
                // control and suspend task execution. It does not make sense
                // for channel initialization to be charged to the caller that created
                // the connection, and it can be dangerous to play scheduling games
                // with this task.
                this.initializationTask = Task.Run(async () =>
                {
                    Debug.Assert(this.openArguments != null);
                    Debug.Assert(this.openArguments.CommonArguments != null);
                    Trace.CorrelationManager.ActivityId = this.openArguments.CommonArguments.ActivityId;
                    await this.InitializeAsync(activityId, onChannelOpen);
                    this.isInitializationComplete = true;
                    this.TestOnInitializeComplete?.Invoke();
                });
            }
            finally
            {
                this.stateLock.ExitWriteLock();
            }
        }

        public async Task<StoreResponse> RequestAsync(
            DocumentServiceRequest request, TransportAddressUri physicalAddress,
            ResourceOperation resourceOperation, Guid activityId, TransportRequestStats transportRequestStats)
        {
            this.ThrowIfDisposed();

            if (!this.isInitializationComplete)
            {
                transportRequestStats.RequestWaitingForConnectionInitialization = true;
                DefaultTrace.TraceInformation(
                    "[RNTBD Channel {0}] Awaiting RNTBD channel initialization. Request URI: {1}",
                    this.ConnectionCorrelationId, physicalAddress);
                await this.initializationTask;
            }
            else
            {
                transportRequestStats.RequestWaitingForConnectionInitialization = false;
            }

            // Waiting for channel initialization to move to Pipelined stage
            transportRequestStats.RecordState(TransportRequestStats.RequestStage.Pipelined);

            // Ideally, we would set up a timer here, and then hand off the rest of the work
            // to the dispatcher. In practice, additional constraints force the interaction to
            // be chattier:
            // - Serialization errors are handled differently from channel errors.
            // - Timeouts only apply to the call (send+recv), not to everything preceding it.
            using ChannelCallArguments callArguments = this.chaosInterceptor == null
                ? new ChannelCallArguments(activityId)
                : new ChannelCallArguments(
                    activityId,
                    request.OperationType,
                    request.ResourceType,
                    request.RequestContext.ResolvedCollectionRid,
                    request.Headers,
                    request.RequestContext.LocationEndpointToRoute);
            try
            {
                callArguments.PreparedCall = this.dispatcher.PrepareCall(
                    request, physicalAddress, resourceOperation, activityId, transportRequestStats);
            }
            catch (DocumentClientException e)
            {
                e.Headers.Add(HttpConstants.HttpHeaders.RequestValidationFailure, "1");
                throw;
            }
            catch (Exception e)
            {
                DefaultTrace.TraceError(
                    "[RNTBD Channel {0}] Failed to serialize request. Assuming malformed request payload: {1}", this.ConnectionCorrelationId, e.Message);
                DocumentClientException clientException = new BadRequestException(e);
                clientException.Headers.Add(
                    HttpConstants.HttpHeaders.RequestValidationFailure, "1");
                throw clientException;
            }

            PooledTimer timer = this.timerPool.GetPooledTimer(this.requestTimeoutSeconds);
            Task[] tasks = new Task[2];
            tasks[0] = timer.StartTimerAsync();
            Task<StoreResponse> dispatcherCall = this.dispatcher.CallAsync(callArguments, transportRequestStats);
            TransportClient.GetTransportPerformanceCounters().LogRntbdBytesSentCount(resourceOperation.resourceType, resourceOperation.operationType, callArguments.PreparedCall?.SerializedRequest.RequestSize);
            tasks[1] = dispatcherCall;
            Task completedTask = await Task.WhenAny(tasks);
            if (object.ReferenceEquals(completedTask, tasks[0]))
            {
                // Timed out.
                TransportErrorCode timeoutCode;
                bool payloadSent;
                callArguments.CommonArguments.SnapshotCallState(
                    out timeoutCode, out payloadSent);
                Debug.Assert(TransportException.IsTimeout(timeoutCode));
                this.dispatcher.CancelCallAndNotifyConnectionOnTimeoutEvent(callArguments.PreparedCall, request.IsReadOnlyRequest);
                CMChannel.HandleTaskTimeout(tasks[1], activityId, this.ConnectionCorrelationId);
                Exception ex = completedTask.Exception?.InnerException;
                DefaultTrace.TraceWarning("[RNTBD Channel {0}] RNTBD call timed out on channel {1}. Error: {2}",
                    this.ConnectionCorrelationId, this, timeoutCode);
                Debug.Assert(callArguments.CommonArguments.UserPayload);
                throw new TransportException(
                    timeoutCode, ex, activityId, physicalAddress.Uri, this.ToString(),
                    callArguments.CommonArguments.UserPayload, payloadSent);
            }
            else
            {
                // Request completed.
                Debug.Assert(object.ReferenceEquals(completedTask, tasks[1]));
                timer.CancelTimer();

                this.dispatcher.NotifyConnectionOnSuccessEvent();
                if (completedTask.IsFaulted)
                {
                    await completedTask;
                }
            }

            physicalAddress.SetConnected();
            StoreResponse storeResponse = dispatcherCall.Result;
            TransportClient.GetTransportPerformanceCounters().LogRntbdBytesReceivedCount(resourceOperation.resourceType, resourceOperation.operationType, storeResponse?.ResponseBody?.Length);
            return storeResponse;
        }

        /// <summary>
        /// Exceptionless request path. Returns a <see cref="Result{T}"/>
        /// carrying either a <see cref="StoreResponse"/> or the exception
        /// that would have been thrown by <see cref="RequestAsync"/>.
        /// </summary>
        public async Task<Res<StoreResponse>> TryRequestAsync(
            DocumentServiceRequest request, TransportAddressUri physicalAddress,
            ResourceOperation resourceOperation, Guid activityId, TransportRequestStats transportRequestStats)
        {
            if (this.disposed)
            {
                return Res.FromException<StoreResponse>(new ObjectDisposedException(nameof(CMChannel)));
            }

            if (!this.isInitializationComplete)
            {
                transportRequestStats.RequestWaitingForConnectionInitialization = true;
                DefaultTrace.TraceInformation(
                    "[RNTBD Channel {0}] Awaiting RNTBD channel initialization. Request URI: {1}",
                    this.ConnectionCorrelationId, physicalAddress);

                Exception initEx = await Res.Wrap(this.initializationTask);
                if (initEx != null)
                {
                    return Res.FromException<StoreResponse>(initEx);
                }
            }
            else
            {
                transportRequestStats.RequestWaitingForConnectionInitialization = false;
            }

            // Waiting for channel initialization to move to Pipelined stage
            transportRequestStats.RecordState(TransportRequestStats.RequestStage.Pipelined);

            // Ideally, we would set up a timer here, and then hand off the rest of the work
            // to the dispatcher. In practice, additional constraints force the interaction to
            // be chattier:
            // - Serialization errors are handled differently from channel errors.
            // - Timeouts only apply to the call (send+recv), not to everything preceding it.
            using ChannelCallArguments callArguments = this.chaosInterceptor == null
                ? new ChannelCallArguments(activityId)
                : new ChannelCallArguments(
                    activityId,
                    request.OperationType,
                    request.ResourceType,
                    request.RequestContext.ResolvedCollectionRid,
                    request.Headers,
                    request.RequestContext.LocationEndpointToRoute);
            try
            {
                callArguments.PreparedCall = this.dispatcher.PrepareCall(
                    request, physicalAddress, resourceOperation, activityId, transportRequestStats);
            }
            catch (DocumentClientException e)
            {
                e.Headers.Add(HttpConstants.HttpHeaders.RequestValidationFailure, "1");
                return Res.FromException<StoreResponse>(e);
            }
            catch (Exception e)
            {
                DefaultTrace.TraceError(
                    "[RNTBD Channel {0}] Failed to serialize request. Assuming malformed request payload: {1}", this.ConnectionCorrelationId, e.Message);
                DocumentClientException clientException = new BadRequestException(e);
                clientException.Headers.Add(
                    HttpConstants.HttpHeaders.RequestValidationFailure, "1");
                return Res.FromException<StoreResponse>(clientException);
            }

            PooledTimer timer = this.timerPool.GetPooledTimer(this.requestTimeoutSeconds);
            Task[] tasks = new Task[2];
            tasks[0] = timer.StartTimerAsync();
            Task<StoreResponse> dispatcherCall = this.dispatcher.CallAsync(callArguments, transportRequestStats);
            TransportClient.GetTransportPerformanceCounters().LogRntbdBytesSentCount(resourceOperation.resourceType, resourceOperation.operationType, callArguments.PreparedCall?.SerializedRequest.RequestSize);
            tasks[1] = dispatcherCall;
            Task completedTask = await Task.WhenAny(tasks);
            if (object.ReferenceEquals(completedTask, tasks[0]))
            {
                // Timed out.
                TransportErrorCode timeoutCode;
                bool payloadSent;
                callArguments.CommonArguments.SnapshotCallState(
                    out timeoutCode, out payloadSent);
                Debug.Assert(TransportException.IsTimeout(timeoutCode));
                this.dispatcher.CancelCallAndNotifyConnectionOnTimeoutEvent(callArguments.PreparedCall, request.IsReadOnlyRequest);
                CMChannel.HandleTaskTimeout(tasks[1], activityId, this.ConnectionCorrelationId);
                Exception ex = completedTask.Exception?.InnerException;
                DefaultTrace.TraceWarning("[RNTBD Channel {0}] RNTBD call timed out on channel {1}. Error: {2}",
                    this.ConnectionCorrelationId, this, timeoutCode);
                Debug.Assert(callArguments.CommonArguments.UserPayload);
                return Res.FromException<StoreResponse>(new TransportException(
                    timeoutCode, ex, activityId, physicalAddress.Uri, this.ToString(),
                    callArguments.CommonArguments.UserPayload, payloadSent));
            }
            else
            {
                // Request completed.
                Debug.Assert(object.ReferenceEquals(completedTask, tasks[1]));
                timer.CancelTimer();

                this.dispatcher.NotifyConnectionOnSuccessEvent();
                if (completedTask.IsFaulted)
                {
                    return Res.FromException<StoreResponse>(completedTask.Exception);
                }

                if (completedTask.IsCanceled)
                {
                    return Res.FromException<StoreResponse>(new OperationCanceledException());
                }
            }

            physicalAddress.SetConnected();
            StoreResponse storeResponse = dispatcherCall.Result;
            TransportClient.GetTransportPerformanceCounters().LogRntbdBytesReceivedCount(resourceOperation.resourceType, resourceOperation.operationType, storeResponse?.ResponseBody?.Length);
            return Res.Success(storeResponse);
        }

        /// <summary>
        /// Returns the background channel initialization task.
        /// </summary>
        /// <returns>The initialization task.</returns>
        public Task OpenChannelAsync(Guid activityId)
        {
            if(this.initializationTask == null)
            {
                throw new InvalidOperationException("Channal Initialization Task Can't be null.");
            }

            return this.initializationTask;
        }

        /// <summary>
        /// Opens the channel under <see cref="RntbdConnectionManager"/> ownership:
        /// runs the same open body as <see cref="Initialize"/>, but with **no
        /// per-open timer**. The open completes only when the underlying protocol
        /// stack reports success or failure (TCP RST, TLS error, OS-level connect
        /// timeout, RNTBD context error).
        /// </summary>
        /// <param name="activityId">Activity id for correlation.</param>
        /// <param name="cancellationToken">
        /// Manager-scoped cancellation token. Cancels only the wait on the
        /// shared open-concurrency semaphore — never propagated into the
        /// dispatcher's open. The manager fires this only on disposal.
        /// </param>
        /// <param name="onChannelOpen">Optional chaos-interceptor hook.</param>
        /// <returns>
        /// A task that completes when the open resolves. The returned task is
        /// also stashed as <see cref="initializationTask"/> so that any caller
        /// invoking <see cref="RequestAsync"/> against this channel before
        /// the open completes still observes the legacy "await initialization
        /// task" semantics.
        /// </returns>
        /// <remarks>
        /// The owning <see cref="CMChannel"/> instance must have been constructed
        /// with <c>deferInitialize: true</c>; otherwise the constructor-driven
        /// <see cref="Initialize"/> already started a parallel init and this call
        /// will fail the state assertion.
        /// </remarks>
        internal Task OpenWithoutTimerAsync(
            Guid activityId,
            CancellationToken cancellationToken,
            Func<Guid, Guid, Uri, CMChannel, Task> onChannelOpen = null)
        {
            this.ThrowIfDisposed();
            this.stateLock.EnterWriteLock();
            try
            {
                if (this.state != State.New)
                {
                    throw new InvalidOperationException(
                        "OpenWithoutTimerAsync requires a CMChannel constructed with deferInitialize: true.");
                }
                Debug.Assert(this.initializationTask == null);
                this.state = State.WaitingToOpen;

                this.initializationTask = Task.Run(async () =>
                {
                    Debug.Assert(this.openArguments != null);
                    Debug.Assert(this.openArguments.CommonArguments != null);
                    Trace.CorrelationManager.ActivityId =
                        this.openArguments.CommonArguments.ActivityId;
                    await this.InitializeWithoutTimerAsync(
                        activityId, cancellationToken, onChannelOpen).ConfigureAwait(false);
                    this.isInitializationComplete = true;
                    this.TestOnInitializeComplete?.Invoke();
                });
                return this.initializationTask;
            }
            finally
            {
                this.stateLock.ExitWriteLock();
            }
        }

        public override string ToString()
        {
            return this.dispatcher.ToString();
        }

        public void Close()
        {
            ((IDisposable) this).Dispose();
        }

        void IDisposable.Dispose()
        {
            // Dispose must be idempotent and never throw on a second call
            // (hierarchical shutdown can race an explicit Close). Guard-and-
            // return rather than throwing ObjectDisposedException.
            if (this.disposed)
            {
                return;
            }
            this.chaosInterceptor?.OnChannelDispose(this.ConnectionCorrelationId);
            this.disposed = true;
            DefaultTrace.TraceInformation("[RNTBD Channel {0}] Disposing RNTBD Channel {1}", this.ConnectionCorrelationId, this);

            Task initTask = null;
            this.stateLock.EnterWriteLock();
            try
            {
                if (this.state != State.Closed)
                {
                    initTask = this.initializationTask;
                }
                this.state = State.Closed;
            }
            finally
            {
                this.stateLock.ExitWriteLock();
            }
            if (initTask != null)
            {
                try
                {
                    // Preserve synchronous disposal: initialization must finish before the dispatcher is disposed.
#pragma warning disable VSTHRD002
                    initTask.Wait();
#pragma warning restore VSTHRD002
                }
                catch (Exception e)
                {
                    DefaultTrace.TraceWarning(
                        "[RNTBD Channel {0}] {1} initialization failed. Consuming the task " +
                        "exception in {2}. Server URI: {3}. Exception: {4}",
                        this.ConnectionCorrelationId,
                        nameof(CMChannel),
                        nameof(IDisposable.Dispose),
                        this.serverUri,
                        e.Message);
                    // Intentionally swallowing the exception. The caller can't
                    // do anything useful with it.
                }
            }
            Debug.Assert(this.dispatcher != null);
            this.dispatcher.Dispose();
            this.stateLock.Dispose();
        }

        #region Test hook.

        internal event Action TestOnInitializeComplete;
        internal event Action TestOnConnectionClosed
        {
            add
            {
                this.dispatcher.TestOnConnectionClosed += value;
            }
            remove
            {
                this.dispatcher.TestOnConnectionClosed -= value;
            }
        }
        internal bool TestIsIdle
        {
            get
            {
                return this.dispatcher.TestIsIdle;
            }
        }
        #endregion

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(CMChannel));
            }
        }

        private async Task InitializeAsync(Guid activityId, Func<Guid, Guid, Uri, CMChannel, Task> onChannelOpen = null)
        {
            bool slimAcquired = false;
            try
            {
                if (this.chaosInterceptor != null)
                {
                    await onChannelOpen?.Invoke(activityId, this.ConnectionCorrelationId, this.serverUri, this);
                }

                this.openArguments.CommonArguments.SetTimeoutCode(TransportErrorCode.ChannelWaitingToOpenTimeout);
                slimAcquired = await this.openingSlim.WaitAsync(this.openArguments.OpenTimeout).ConfigureAwait(false);
                if (!slimAcquired)
                {
                    // Timed out.
                    TransportErrorCode timeoutCode;
                    bool payloadSent;
                    this.openArguments.CommonArguments.SnapshotCallState(
                        out timeoutCode, out payloadSent);
                    Debug.Assert(TransportException.IsTimeout(timeoutCode));
                    DefaultTrace.TraceWarning(
                        "[RNTBD Channel {0}] RNTBD waiting to open timed out on channel {1}. Error: {2}", this.ConnectionCorrelationId, this, timeoutCode);
                    throw new TransportException(
                        timeoutCode, null, this.openArguments.CommonArguments.ActivityId,
                        this.serverUri, this.ToString(),
                        this.openArguments.CommonArguments.UserPayload, payloadSent);
                }
                else
                {
                    this.openArguments.CommonArguments.SetTimeoutCode(TransportErrorCode.ChannelOpenTimeout);
                    this.state = State.Opening;

                    PooledTimer timer = this.timerPool.GetPooledTimer(
                        this.openArguments.OpenTimeout);
                    Task[] tasks = new Task[2];

                    // For local region requests the the OpenTimeout could be lower than the TimerPool minSupportedTimerDelayInSeconds,
                    // so use the lower value
                    if (this.localRegionRequest && this.openArguments.OpenTimeout < timer.MinSupportedTimeout)
                    {
                        tasks[0] = Task.Delay(this.openArguments.OpenTimeout);
                    }
                    else
                    {
                        tasks[0] = timer.StartTimerAsync();
                    }

                    tasks[1] = this.dispatcher.OpenAsync(this.openArguments);
                    Task completedTask = await Task.WhenAny(tasks);
                    if (object.ReferenceEquals(completedTask, tasks[0]))
                    {
                        // Timed out.
                        TransportErrorCode timeoutCode;
                        bool payloadSent;
                        this.openArguments.CommonArguments.SnapshotCallState(
                            out timeoutCode, out payloadSent);
                        Debug.Assert(TransportException.IsTimeout(timeoutCode));
                        CMChannel.HandleTaskTimeout(
                            tasks[1],
                            this.openArguments.CommonArguments.ActivityId,
                            this.ConnectionCorrelationId);
                        Exception ex = completedTask.Exception?.InnerException;
                        DefaultTrace.TraceWarning(
                            "[RNTBD Channel {0}] RNTBD open timed out on channel {1}. Error: {2}",
                            this.ConnectionCorrelationId, this, timeoutCode);
                        Debug.Assert(!this.openArguments.CommonArguments.UserPayload);
                        throw new TransportException(
                            timeoutCode, ex, this.openArguments.CommonArguments.ActivityId,
                            this.serverUri, this.ToString(),
                            this.openArguments.CommonArguments.UserPayload, payloadSent);
                    }
                    else
                    {
                        // Open completed.
                        Debug.Assert(object.ReferenceEquals(completedTask, tasks[1]));
                        timer.CancelTimer();

                        if (completedTask.IsFaulted)
                        {
                            await completedTask;
                        }
                    }

                    this.FinishInitialization(State.Open);
                }

            }
            catch (DocumentClientException e)
            {
                this.FinishInitialization(State.Closed);

                e.Headers.Set(
                    HttpConstants.HttpHeaders.ActivityId,
                    this.openArguments.CommonArguments.ActivityId.ToString());
                DefaultTrace.TraceWarning(
                    "[RNTBD Channel {0}] Channel.InitializeAsync failed. Channel: {1}. DocumentClientException: {2}",
                    this.ConnectionCorrelationId, this, e.Message);

                throw;
            }
            catch (TransportException e)
            {
                this.FinishInitialization(State.Closed);

                DefaultTrace.TraceWarning(
                    "[RNTBD Channel {0}] Channel.InitializeAsync failed. Channel: {1}. TransportException: {2}",
                    this.ConnectionCorrelationId, this, e.Message);

                throw;
            }
            catch (Exception e)
            {
                this.FinishInitialization(State.Closed);

                DefaultTrace.TraceWarning(
                    "[RNTBD Channel {0}] Channel.InitializeAsync failed. Wrapping exception in " +
                    "TransportException. Channel: {1}. Inner exception: {2}",
                    this.ConnectionCorrelationId, this, e.Message);

                Debug.Assert(!this.openArguments.CommonArguments.UserPayload);
                throw new TransportException(
                    TransportErrorCode.ChannelOpenFailed, e,
                    this.openArguments.CommonArguments.ActivityId,
                    this.serverUri, this.ToString(),
                    this.openArguments.CommonArguments.UserPayload,
                    this.openArguments.CommonArguments.PayloadSent);
            }
            finally
            {
                this.openArguments.OpenTimeline.WriteTrace();
                // The open arguments are no longer needed after this point.
                this.openArguments = null;
                if (slimAcquired)
                {
                    this.openingSlim.Release();
                }
            }
        }

        // Parallel to InitializeAsync, but with no per-open timer. Open
        // completion is driven solely by the protocol stack. The cancellation
        // token is observed only on the shared open-concurrency semaphore wait;
        // once that has been acquired the open runs to natural completion so
        // the work the first caller triggered is never wasted just because that
        // caller's request budget ran out. Mirrors the error-handling and
        // tracing of InitializeAsync so the rest of the channel state machine
        // and the existing telemetry pipeline behave identically.
        private async Task InitializeWithoutTimerAsync(
            Guid activityId,
            CancellationToken cancellationToken,
            Func<Guid, Guid, Uri, CMChannel, Task> onChannelOpen)
        {
            bool slimAcquired = false;
            try
            {
                if (this.chaosInterceptor != null && onChannelOpen != null)
                {
                    await onChannelOpen.Invoke(
                        activityId, this.ConnectionCorrelationId, this.serverUri, this);
                }

                this.openArguments.CommonArguments.SetTimeoutCode(
                    TransportErrorCode.ChannelWaitingToOpenTimeout);
                if (this.openingSlim != null)
                {
                    await this.openingSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
                    slimAcquired = true;
                }

                this.openArguments.CommonArguments.SetTimeoutCode(
                    TransportErrorCode.ChannelOpenTimeout);
                this.state = State.Opening;

                // No timer race. Just await the dispatcher's open and let the
                // OS / protocol stack decide when this completes.
                await this.dispatcher.OpenAsync(this.openArguments).ConfigureAwait(false);

                this.FinishInitialization(State.Open);
            }
            catch (DocumentClientException e)
            {
                this.FinishInitialization(State.Closed);

                e.Headers.Set(
                    HttpConstants.HttpHeaders.ActivityId,
                    this.openArguments.CommonArguments.ActivityId.ToString());
                DefaultTrace.TraceWarning(
                    "[RNTBD Channel {0}] CMChannel.InitializeWithoutTimerAsync failed. Channel: {1}. DocumentClientException: {2}",
                    this.ConnectionCorrelationId, this, e.Message);

                throw;
            }
            catch (TransportException e)
            {
                this.FinishInitialization(State.Closed);

                DefaultTrace.TraceWarning(
                    "[RNTBD Channel {0}] CMChannel.InitializeWithoutTimerAsync failed. Channel: {1}. TransportException: {2}",
                    this.ConnectionCorrelationId, this, e.Message);

                throw;
            }
            catch (Exception e)
            {
                this.FinishInitialization(State.Closed);

                DefaultTrace.TraceWarning(
                    "[RNTBD Channel {0}] CMChannel.InitializeWithoutTimerAsync failed. Wrapping exception in " +
                    "TransportException. Channel: {1}. Inner exception: {2}",
                    this.ConnectionCorrelationId, this, e.Message);

                Debug.Assert(!this.openArguments.CommonArguments.UserPayload);
                throw new TransportException(
                    TransportErrorCode.ChannelOpenFailed, e,
                    this.openArguments.CommonArguments.ActivityId,
                    this.serverUri, this.ToString(),
                    this.openArguments.CommonArguments.UserPayload,
                    this.openArguments.CommonArguments.PayloadSent);
            }
            finally
            {
                this.openArguments.OpenTimeline.WriteTrace();
                // The open arguments are no longer needed after this point.
                this.openArguments = null;
                if (slimAcquired)
                {
                    this.openingSlim.Release();
                }
            }
        }

        private void FinishInitialization(State nextState)
        {
            Debug.Assert(!Monitor.IsEntered(this.stateLock));
            Debug.Assert(nextState == State.Open || nextState == State.Closed);
            Task initTask = null;
            this.stateLock.EnterWriteLock();
            try
            {
                // this.state might have become Closed if Dispose was already called.
                Debug.Assert(this.state == State.WaitingToOpen || this.state == State.Opening || this.state == State.Closed);
                if (this.state != State.Closed)
                {
                    this.state = nextState;
                    initTask = this.initializationTask;
                }
            }
            finally
            {
                this.stateLock.ExitWriteLock();
            }
            if ((nextState == State.Closed) && (initTask != null))
            {
                // In the typical case, a channel is created, asynchronous initialization
                // starts, and then one or more callers await on the initialization task
                // (and thus consume its exception, if one is thrown).
                // This code defends against the rare case where a channel begins asynchronous
                // initialization which ends in error, but nothing consumes the exception.
                Task ignored = initTask.ContinueWith(completedTask =>
                {
                    Debug.Assert(completedTask.IsFaulted);
                    Debug.Assert(this.serverUri != null);
                    Debug.Assert(completedTask.Exception != null);
                    DefaultTrace.TraceWarning(
                        "[RNTBD Channel {0}] {1} initialization failed. Consuming the task " +
                        "exception asynchronously. Server URI: {2}. Exception: {3}",
                        this.ConnectionCorrelationId,
                        nameof(CMChannel),
                        this.serverUri,
                        completedTask.Exception.InnerException?.Message);
                },
                TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        private static void HandleTaskTimeout(Task runawayTask, Guid activityId, Guid connectionCorrelationId)
        {
            Task ignored = runawayTask.ContinueWith(task =>
            {
                Trace.CorrelationManager.ActivityId = activityId;

                if (task.IsFaulted && task.Exception != null)
                {
                    Exception e = task.Exception.InnerException;
                    DefaultTrace.TraceInformation(
                        "[RNTBD Channel {0}] Timed out task completed with fault. Activity ID = {1}. HRESULT = {2:X}. Exception: {3}",
                        connectionCorrelationId, activityId, e?.HResult, e?.Message);
                }
                else if (task.IsCanceled)
                {
                    DefaultTrace.TraceInformation(
                        "[RNTBD Channel {0}] Timed out task completed with cancellation. Activity ID = {1}.",
                        connectionCorrelationId, activityId);
                }
            },
            TaskContinuationOptions.NotOnRanToCompletion); // Captures both faulted and canceled states
        }

        private enum State
        {
            New,
            WaitingToOpen,
            Opening,
            Open,
            Closed,
        }
    }
}
