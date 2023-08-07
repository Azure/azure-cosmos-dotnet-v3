//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;
    using System.Net.NetworkInformation;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

#if NETSTANDARD15 || NETSTANDARD16
    using Trace = Microsoft.Azure.Documents.Trace;
#endif

    // The RNTBD RPC channel. Supports multiple parallel requests and timeouts.
    internal sealed class Channel : IChannel, IDisposable
    {
        private readonly Dispatcher dispatcher;
        private readonly TimerPool timerPool;
        private readonly int requestTimeoutSeconds;
        private readonly Uri serverUri;
        private readonly bool localRegionRequest;
        private bool disposed = false;

        private readonly ReaderWriterLockSlim stateLock =
            new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private State state = State.New;  // Guarded by stateLock.
        private Task initializationTask = null;  // Guarded by stateLock.
        private volatile bool isInitializationComplete = false;

        private ChannelOpenArguments openArguments;
        private readonly SemaphoreSlim openingSlim;

        public Channel(Guid activityId, Uri serverUri, ChannelProperties channelProperties, bool localRegionRequest, SemaphoreSlim openingSlim)
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
                channelProperties.DnsResolutionFunction);
            this.timerPool = channelProperties.RequestTimerPool;
            this.requestTimeoutSeconds = (int) channelProperties.RequestTimeout.TotalSeconds;
            this.serverUri = serverUri;
            this.localRegionRequest = localRegionRequest;

            TimeSpan openTimeout = localRegionRequest ? channelProperties.LocalRegionOpenTimeout : channelProperties.OpenTimeout;

            this.openArguments = new ChannelOpenArguments(
                activityId, new ChannelOpenTimeline(),
                openTimeout,
                channelProperties.PortReuseMode,
                channelProperties.UserPortPool,
                channelProperties.CallerId);

            this.openingSlim = openingSlim;
            this.Initialize();
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

        private void Initialize()
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
                    await this.InitializeAsync();
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
            using ChannelCallArguments callArguments = new ChannelCallArguments(activityId);
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
                    "[RNTBD Channel {0}] Failed to serialize request. Assuming malformed request payload: {1}", this.ConnectionCorrelationId, e);
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
                Channel.HandleTaskTimeout(tasks[1], activityId, this.ConnectionCorrelationId);
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
            this.ThrowIfDisposed();
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
                    initTask.Wait();
                }
                catch (Exception e)
                {
                    DefaultTrace.TraceWarning(
                        "[RNTBD Channel {0}] {1} initialization failed. Consuming the task " +
                        "exception in {2}. Server URI: {3}. Exception: {4}",
                        this.ConnectionCorrelationId,
                        nameof(Channel),
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
                throw new ObjectDisposedException(nameof(Channel));
            }
        }

        private async Task InitializeAsync()
        {
            bool slimAcquired = false;
            try
            {
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
                        Channel.HandleTaskTimeout(
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
                    this.ConnectionCorrelationId, this, e);

                throw;
            }
            catch (TransportException e)
            {
                this.FinishInitialization(State.Closed);

                DefaultTrace.TraceWarning(
                    "[RNTBD Channel {0}] Channel.InitializeAsync failed. Channel: {1}. TransportException: {2}",
                    this.ConnectionCorrelationId, this, e);

                throw;
            }
            catch (Exception e)
            {
                this.FinishInitialization(State.Closed);

                DefaultTrace.TraceWarning(
                    "[RNTBD Channel {0}] Channel.InitializeAsync failed. Wrapping exception in " +
                    "TransportException. Channel: {1}. Inner exception: {2}",
                    this.ConnectionCorrelationId, this, e);

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
                initTask.ContinueWith(completedTask =>
                {
                    Debug.Assert(completedTask.IsFaulted);
                    Debug.Assert(this.serverUri != null);
                    Debug.Assert(completedTask.Exception != null);
                    DefaultTrace.TraceWarning(
                        "[RNTBD Channel {0}] {1} initialization failed. Consuming the task " +
                        "exception asynchronously. Server URI: {2}. Exception: {3}",
                        this.ConnectionCorrelationId,
                        nameof(Channel),
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
                Debug.Assert(task.IsFaulted);
                Debug.Assert(task.Exception != null);
                Exception e = task.Exception.InnerException;
                DefaultTrace.TraceInformation(
                    "[RNTBD Channel {0}] Timed out task completed. Activity ID = {1}. HRESULT = {2:X}. Exception: {3}",
                    connectionCorrelationId, activityId, e.HResult, e);
            },
            TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <inheritdoc/>
        public void SetHealthState(
            bool isHealthy)
        {
            // Do Nothing.
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
