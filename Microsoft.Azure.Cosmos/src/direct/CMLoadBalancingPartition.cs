//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.FaultInjection;

    // Connection-manager parallel-path analogue of LoadBalancingPartition.
    //
    // This type is only ever reached when the RNTBD connection-manager feature
    // flag is ON (the single fork lives in CMTransportClient.GetChannel). It is
    // a thin IChannel wrapper: connection establishment and pooling are owned by
    // exactly one RntbdConnectionManager per partition, created lazily on first
    // use. Request dispatch simply acquires a connection from the manager and
    // runs the request on the channel the manager hands back.
    internal sealed class CMLoadBalancingPartition : IChannel, IDisposable
    {
        private readonly Uri serverUri;
        private readonly ChannelProperties channelProperties;
        private readonly bool localRegionRequest;
        private readonly int maxCapacity;  // maxChannels * maxRequestsPerChannel

        // Per-request bound on connection acquisition. The manager itself is
        // timeout-agnostic (it waits on the caller's token); the requester owns
        // the open-timeout policy. Derived from the local- vs cross-region open
        // timeout, matching the legacy Channel open path. When a connection
        // cannot be acquired within this window the request fails with
        // ChannelOpenTimeout so the store reader retries another replica,
        // instead of waiting forever.
        private readonly TimeSpan openTimeout;

        private readonly IChaosInterceptor chaosInterceptor;
        private readonly IRntbdConnectionManagerTelemetry telemetry;
        private readonly Func<bool> debugLogsResolver;
        private readonly Func<int> snapshotIntervalSecondsResolver;

        // The one manager this partition owns. Created lazily so partitions that
        // never see traffic never allocate a manager (and its snapshot timer).
        private readonly Lazy<RntbdConnectionManager> connectionManager;

        private int requestsPending = 0;  // Atomic.
        private int disposed = 0;

        public CMLoadBalancingPartition(
            Uri serverUri,
            ChannelProperties channelProperties,
            bool localRegionRequest,
            IChaosInterceptor chaosInterceptor = null,
            IRntbdConnectionManagerTelemetry telemetry = null,
            Func<bool> debugLogsResolver = null,
            Func<int> snapshotIntervalSecondsResolver = null)
        {
            this.serverUri = serverUri ?? throw new ArgumentNullException(nameof(serverUri));
            this.channelProperties = channelProperties ?? throw new ArgumentNullException(nameof(channelProperties));
            this.localRegionRequest = localRegionRequest;
            this.openTimeout = localRegionRequest
                ? channelProperties.LocalRegionOpenTimeout
                : channelProperties.OpenTimeout;
            this.chaosInterceptor = chaosInterceptor;
            this.telemetry = telemetry;
            this.debugLogsResolver = debugLogsResolver;
            this.snapshotIntervalSecondsResolver = snapshotIntervalSecondsResolver;

            this.maxCapacity = checked(channelProperties.MaxChannels *
                channelProperties.MaxRequestsPerChannel);

            this.connectionManager = new Lazy<RntbdConnectionManager>(
                this.CreateConnectionManager,
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        private RntbdConnectionManager CreateConnectionManager()
        {
            RntbdConnectionManager manager = new RntbdConnectionManager(
                this.serverUri,
                this.channelProperties,
                this.localRegionRequest,
                this.chaosInterceptor,
                this.telemetry,
                this.debugLogsResolver,
                this.snapshotIntervalSecondsResolver);

            DefaultTrace.TraceInformation(
                "CMLoadBalancingPartition: lazily created RntbdConnectionManager for endpoint {0} (manager [{1}])",
                this.serverUri,
                manager.ConnectionManagerId);

            return manager;
        }

        public bool Healthy
        {
            get
            {
                this.ThrowIfDisposed();
                return true;
            }
        }

        public async Task<StoreResponse> RequestAsync(
            DocumentServiceRequest request,
            TransportAddressUri physicalAddress,
            ResourceOperation resourceOperation,
            Guid activityId,
            TransportRequestStats transportRequestStats)
        {
            this.ThrowIfDisposed();

            int currentPending = Interlocked.Increment(ref this.requestsPending);
            transportRequestStats.NumberOfInflightRequestsToEndpoint = currentPending;
            try
            {
                if (currentPending > this.maxCapacity)
                {
                    throw new RequestRateTooLargeException(
                        string.Format(
                            "All connections to {0} are fully utilized. Increase " +
                            "the maximum number of connections or the maximum number " +
                            "of requests per connection", this.serverUri),
                        SubStatusCodes.ClientTcpChannelFull);
                }

                transportRequestStats.RecordState(TransportRequestStats.RequestStage.ChannelAcquisitionStarted);

                // Bound connection acquisition by the open timeout. The manager
                // is timeout-agnostic and would otherwise wait indefinitely for
                // a slot (e.g. against a permanently-failing endpoint); the
                // requester owns the timeout. On expiry, surface
                // ChannelOpenTimeout so the store reader retries another replica.
                using (CancellationTokenSource openCts = this.CreateOpenTimeoutCts())
                {
                    try
                    {
                        using (RntbdConnectionManager.AcquiredConnection acquired = await this.connectionManager.Value
                            .AcquireAsync(activityId, openCts.Token)
                            .ConfigureAwait(false))
                        {
                            return await acquired.Channel.RequestAsync(
                                request,
                                physicalAddress,
                                resourceOperation,
                                activityId,
                                transportRequestStats).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException) when (openCts.IsCancellationRequested)
                    {
                        throw this.CreateOpenTimeoutException(activityId);
                    }
                }
            }
            finally
            {
                Interlocked.Decrement(ref this.requestsPending);
            }
        }

        /// <summary>
        /// Exceptionless variant of <see cref="RequestAsync"/>.
        /// Returns a <see cref="Res{T}"/> instead of throwing on transport errors.
        /// </summary>
        public async Task<Res<StoreResponse>> TryRequestAsync(
            DocumentServiceRequest request,
            TransportAddressUri physicalAddress,
            ResourceOperation resourceOperation,
            Guid activityId,
            TransportRequestStats transportRequestStats)
        {
            ObjectDisposedException disposedException = this.GetDisposedException();
            if (disposedException != null)
            {
                return Res.FromException<StoreResponse>(disposedException);
            }

            int currentPending = Interlocked.Increment(ref this.requestsPending);
            transportRequestStats.NumberOfInflightRequestsToEndpoint = currentPending;
            try
            {
                if (currentPending > this.maxCapacity)
                {
                    return Res.FromException<StoreResponse>(new RequestRateTooLargeException(
                        string.Format(
                            "All connections to {0} are fully utilized. Increase " +
                            "the maximum number of connections or the maximum number " +
                            "of requests per connection", this.serverUri),
                        SubStatusCodes.ClientTcpChannelFull));
                }

                transportRequestStats.RecordState(TransportRequestStats.RequestStage.ChannelAcquisitionStarted);

                // Exceptionless acquire, bounded by the open timeout. The
                // manager surfaces disposal / cancellation / open failures as a
                // faulted Res instead of throwing. A cancellation caused by our
                // open-timeout is translated into a ChannelOpenTimeout so the
                // store reader retries another replica instead of waiting
                // forever.
                using (CancellationTokenSource openCts = this.CreateOpenTimeoutCts())
                {
                    Res<RntbdConnectionManager.AcquiredConnection> acquireResult =
                        await this.connectionManager.Value
                            .TryAcquireAsync(activityId, openCts.Token)
                            .ConfigureAwait(false);
                    if (!acquireResult.IsSuccess)
                    {
                        if (openCts.IsCancellationRequested
                            && acquireResult.Exception is OperationCanceledException)
                        {
                            return Res.FromException<StoreResponse>(
                                this.CreateOpenTimeoutException(activityId));
                        }
                        return Res.FromException<StoreResponse>(acquireResult.Exception);
                    }

                    using (RntbdConnectionManager.AcquiredConnection acquired = acquireResult.Value)
                    {
                        return await acquired.Channel.TryRequestAsync(
                            request,
                            physicalAddress,
                            resourceOperation,
                            activityId,
                            transportRequestStats).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                Interlocked.Decrement(ref this.requestsPending);
            }
        }

        /// <summary>
        /// Creates a <see cref="CancellationTokenSource"/> that fires after the
        /// configured open timeout, bounding how long a request waits to acquire
        /// a connection. A non-positive timeout yields an unbounded source
        /// (no timer), preserving the manager's wait-until-cancelled behavior.
        /// </summary>
        private CancellationTokenSource CreateOpenTimeoutCts()
        {
            return this.openTimeout > TimeSpan.Zero
                ? new CancellationTokenSource(this.openTimeout)
                : new CancellationTokenSource();
        }

        /// <summary>
        /// Builds the <see cref="TransportException"/> surfaced when connection
        /// acquisition exceeds the open timeout. Uses
        /// <see cref="TransportErrorCode.ChannelOpenTimeout"/> so the store
        /// reader treats it as a transient open failure and retries another
        /// replica.
        /// </summary>
        private TransportException CreateOpenTimeoutException(Guid activityId)
        {
            return new TransportException(
                TransportErrorCode.ChannelOpenTimeout,
                innerException: null,
                activityId: activityId,
                requestUri: this.serverUri,
                sourceDescription: this.serverUri.ToString(),
                userPayload: false,
                payloadSent: false);
        }

        /// <summary>
        /// Routes replica-validation / pre-warm through the manager's
        /// idempotent join-or-start surface.
        /// </summary>
        /// <param name="activityId">An unique identifier indicating the current activity id.</param>
        public Task OpenChannelAsync(Guid activityId)
        {
            this.ThrowIfDisposed();
            return this.connectionManager.Value.WarmAsync(activityId);
        }

        public void Close()
        {
            ((IDisposable)this).Dispose();
        }

        void IDisposable.Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }

            if (this.connectionManager.IsValueCreated)
            {
                try
                {
                    this.connectionManager.Value.Dispose();
                }
                catch
                {
                    // Best-effort dispose during shutdown.
                }
            }
        }

        public void Dispose()
        {
            ((IDisposable)this).Dispose();
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException ex = this.GetDisposedException();
            if (ex != null)
            {
                throw ex;
            }
        }

        private ObjectDisposedException GetDisposedException()
        {
            if (Volatile.Read(ref this.disposed) != 0)
            {
                return new ObjectDisposedException(string.Format("{0}:{1}",
                    nameof(CMLoadBalancingPartition), this.serverUri));
            }

            return null;
        }
    }
}
