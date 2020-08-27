//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;
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
        private bool disposed = false;

        private readonly ReaderWriterLockSlim stateLock =
            new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private State state = State.New;  // Guarded by stateLock.
        private Task initializationTask = null;  // Guarded by stateLock.

        private ChannelOpenArguments openArguments;

        public Channel(Guid activityId, Uri serverUri, ChannelProperties channelProperties)
        {
            Debug.Assert(channelProperties != null);
            this.dispatcher = new Dispatcher(serverUri,
                channelProperties.UserAgent,
                channelProperties.ConnectionStateListener,
                channelProperties.CertificateHostNameOverride,
                channelProperties.ReceiveHangDetectionTime,
                channelProperties.SendHangDetectionTime,
                channelProperties.IdleTimerPool,
                channelProperties.IdleTimeout);
            this.timerPool = channelProperties.RequestTimerPool;
            this.requestTimeoutSeconds = (int) channelProperties.RequestTimeout.TotalSeconds;
            this.serverUri = serverUri;
            this.openArguments = new ChannelOpenArguments(
                activityId, new ChannelOpenTimeline(),
                (int)channelProperties.OpenTimeout.TotalSeconds,
                channelProperties.PortReuseMode,
                channelProperties.UserPortPool,
                channelProperties.CallerId);
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

        public void Initialize()
        {
            this.ThrowIfDisposed();
            this.stateLock.EnterWriteLock();
            try
            {
                Debug.Assert(this.state == State.New);
                this.state = State.Opening;
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
                });
            }
            finally
            {
                this.stateLock.ExitWriteLock();
            }
        }

        public async Task<StoreResponse> RequestAsync(
            DocumentServiceRequest request, Uri physicalAddress,
            ResourceOperation resourceOperation, Guid activityId)
        {
            this.ThrowIfDisposed();
            Task initTask = null;
            this.stateLock.EnterReadLock();
            try
            {
                if (this.state != State.Open)
                {
                    Debug.Assert(this.initializationTask != null);
                    initTask = this.initializationTask;
                }
            }
            finally
            {
                this.stateLock.ExitReadLock();
            }
            if (initTask != null)
            {
                DefaultTrace.TraceInformation(
                    "Awaiting RNTBD channel initialization. Request URI: {0}",
                    physicalAddress);
                await initTask;
            }

            // Ideally, we would set up a timer here, and then hand off the rest of the work
            // to the dispatcher. In practice, additional constraints force the interaction to
            // be chattier:
            // - Serialization errors are handled differently from channel errors.
            // - Timeouts only apply to the call (send+recv), not to everything preceding it.
            ChannelCallArguments callArguments = new ChannelCallArguments(activityId);
            try
            {
                callArguments.PreparedCall = this.dispatcher.PrepareCall(
                    request, physicalAddress, resourceOperation, activityId);
            }
            catch (DocumentClientException e)
            {
                e.Headers.Add(HttpConstants.HttpHeaders.RequestValidationFailure, "1");
                throw;
            }
            catch (Exception e)
            {
                DefaultTrace.TraceError(
                    "Failed to serialize request. Assuming malformed request payload: {0}", e);
                DocumentClientException clientException = new BadRequestException(e);
                clientException.Headers.Add(
                    HttpConstants.HttpHeaders.RequestValidationFailure, "1");
                throw clientException;
            }

            PooledTimer timer = this.timerPool.GetPooledTimer(this.requestTimeoutSeconds);
            Task[] tasks = new Task[2];
            tasks[0] = timer.StartTimerAsync();
            Task<StoreResponse> dispatcherCall = this.dispatcher.CallAsync(callArguments);
            TransportClient.GetTransportPerformanceCounters().LogRntbdBytesSentCount(resourceOperation.resourceType, resourceOperation.operationType, callArguments.PreparedCall?.SerializedRequest?.Length);
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
                this.dispatcher.CancelCall(callArguments.PreparedCall);
                Channel.HandleTaskTimeout(tasks[1], activityId);
                Exception ex = completedTask.Exception?.InnerException;
                DefaultTrace.TraceWarning("RNTBD call timed out on channel {0}. Error: {1}",
                    this, timeoutCode);
                Debug.Assert(callArguments.CommonArguments.UserPayload);
                throw new TransportException(
                    timeoutCode, ex, activityId, physicalAddress, this.ToString(),
                    callArguments.CommonArguments.UserPayload, payloadSent);
            }
            else
            {
                // Request completed.
                Debug.Assert(object.ReferenceEquals(completedTask, tasks[1]));
                timer.CancelTimer();

                if (completedTask.IsFaulted)
                {
                    await completedTask;
                }
            }

            StoreResponse storeResponse = dispatcherCall.Result;
            TransportClient.GetTransportPerformanceCounters().LogRntbdBytesReceivedCount(resourceOperation.resourceType, resourceOperation.operationType, storeResponse?.ResponseBody?.Length);
            return storeResponse;
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
            DefaultTrace.TraceInformation("Disposing RNTBD Channel {0}", this);

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
                        "{0} initialization failed. Consuming the task " +
                        "exception in {1}. Server URI: {2}. Exception: {3}",
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
            try
            {
                PooledTimer timer = this.timerPool.GetPooledTimer(
                    this.openArguments.OpenTimeoutSeconds);
                Task[] tasks = new Task[2];
                tasks[0] = timer.StartTimerAsync();
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
                        this.openArguments.CommonArguments.ActivityId);
                    Exception ex = completedTask.Exception?.InnerException;
                    DefaultTrace.TraceWarning(
                        "RNTBD open timed out on channel {0}. Error: {1}", this,
                        timeoutCode);
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
            catch (DocumentClientException e)
            {
                this.FinishInitialization(State.Closed);

                e.Headers.Set(
                    HttpConstants.HttpHeaders.ActivityId,
                    this.openArguments.CommonArguments.ActivityId.ToString());
                DefaultTrace.TraceWarning(
                    "Channel.InitializeAsync failed. Channel: {0}. DocumentClientException: {1}",
                    this, e);

                throw;
            }
            catch (TransportException e)
            {
                this.FinishInitialization(State.Closed);

                DefaultTrace.TraceWarning(
                    "Channel.InitializeAsync failed. Channel: {0}. TransportException: {1}",
                    this, e);

                throw;
            }
            catch (Exception e)
            {
                this.FinishInitialization(State.Closed);

                DefaultTrace.TraceWarning(
                    "Channel.InitializeAsync failed. Wrapping exception in " +
                    "TransportException. Channel: {0}. Inner exception: {1}",
                    this, e);

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
            }

            this.TestOnInitializeComplete?.Invoke();
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
                Debug.Assert(this.state == State.Opening || this.state == State.Closed);
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
                        "{0} initialization failed. Consuming the task " +
                        "exception asynchronously. Server URI: {1}. Exception: {2}",
                        nameof(Channel),
                        this.serverUri,
                        completedTask.Exception.InnerException?.Message);
                },
                TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        private static void HandleTaskTimeout(Task runawayTask, Guid activityId)
        {
            Task ignored = runawayTask.ContinueWith(task =>
            {
                Trace.CorrelationManager.ActivityId = activityId;
                Debug.Assert(task.IsFaulted);
                Debug.Assert(task.Exception != null);
                Exception e = task.Exception.InnerException;
                DefaultTrace.TraceInformation(
                    "Timed out task completed. Activity ID = {0}. HRESULT = {1:X}. Exception: {2}",
                    activityId, e.HResult, e);
            },
            TaskContinuationOptions.OnlyOnFaulted);
        }

        private enum State
        {
            New,
            Opening,
            Open,
            Closed,
        }
    }
}
