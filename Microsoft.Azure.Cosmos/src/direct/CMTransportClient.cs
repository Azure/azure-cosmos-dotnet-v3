//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.FaultInjection;
#if NETSTANDARD15 || NETSTANDARD16
    using Trace = Microsoft.Azure.Documents.Trace;
#endif

    /// <summary>
    /// RNTBD connection-manager parallel transport client and the single fork
    /// point for the connection-manager feature.
    ///
    /// It inherits the entire legacy <see cref="TransportClient"/> request
    /// pipeline unchanged and overrides only <see cref="GetChannel"/> to route
    /// channel acquisition through the cloned connection-manager graph
    /// (<see cref="CMChannelDictionary"/> -&gt; <see cref="CMLoadBalancingChannel"/>
    /// -&gt; <see cref="CMLoadBalancingPartition"/> -&gt;
    /// <see cref="RntbdConnectionManager"/>) when the feature-flag resolver
    /// returns true. When the resolver returns false the base legacy
    /// <c>channelDictionary</c> path runs verbatim, so flipping the flag off is
    /// provably identical to pre-feature behavior — no existing hot-path class is
    /// touched.
    ///
    /// This is the only permitted subclass of <see cref="TransportClient"/>
    /// (enforced by the base constructor guard).
    /// </summary>
    internal sealed class CMTransportClient : TransportClient
    {
        private readonly Func<bool> useConnectionManagerResolver;
        private readonly Func<bool> debugLogsResolver;
        private readonly Func<int> snapshotIntervalSecondsResolver;
        private readonly IChaosInterceptor chaosInterceptor;

        // The cloned graph is built lazily on the first manager-path GetChannel
        // call. Nothing is allocated while the flag stays off.
        private readonly Lazy<CMChannelDictionary> cmChannels;

        // Guards CMChannelDictionary disposal so it happens at most once.
        private int cmDisposed;

        public CMTransportClient(
            Options clientOptions,
            Func<bool> useConnectionManagerResolver,
            Func<bool> debugLogsResolver = null,
            Func<int> snapshotIntervalSecondsResolver = null,
            IChaosInterceptor chaosInterceptor = null)
            : base(clientOptions, chaosInterceptor)
        {
            this.useConnectionManagerResolver = useConnectionManagerResolver
                ?? throw new ArgumentNullException(nameof(useConnectionManagerResolver));
            this.debugLogsResolver = debugLogsResolver;
            this.snapshotIntervalSecondsResolver = snapshotIntervalSecondsResolver;
            this.chaosInterceptor = chaosInterceptor;

            this.cmChannels = new Lazy<CMChannelDictionary>(
                () => new CMChannelDictionary(
                    this.channelProperties,
                    this.chaosInterceptor,
                    telemetry: null,
                    debugLogsResolver: this.debugLogsResolver,
                    snapshotIntervalSecondsResolver: this.snapshotIntervalSecondsResolver),
                LazyThreadSafetyMode.ExecutionAndPublication);

            DefaultTrace.TraceInformation(
                "RntbdConnectionManager startup: CMTransportClient created. enabled(initial)={0}",
                this.IsConnectionManagerEnabled());
        }

        /// <summary>
        /// Single fork point. When the feature-flag resolver returns true the
        /// channel comes from the cloned connection-manager graph; otherwise the
        /// base legacy path is used unchanged.
        /// </summary>
        protected override IChannel GetChannel(Uri requestUri, bool localRegionRequest)
        {
            bool managerEnabled = this.IsConnectionManagerEnabled();

            // Per-request route-decision telemetry, gated behind the debug-logs
            // resolver so it is off by default and only enabled per-federation
            // during roll-out validation windows. Dashboards count these rows by
            // path/bin — no in-process accumulator is kept.
            if (this.AreDebugLogsEnabled())
            {
                // The request's activity id is ambient on the calling thread
                // (set by the RNTBD request pipeline before GetChannel is
                // invoked). Capturing it here lets a RouteDecision row be joined
                // to the ConnectionAcquisition / OpenStarted / OpenFailed rows
                // (which already carry the same activityId and the resolved cm
                // id), giving end-to-end per-request correlation across the CM
                // path without changing the GetChannel signature.
                Guid activityId = Trace.CorrelationManager.ActivityId;
                DefaultTraceConnectionManagerTelemetry.Instance.LogEvent(
                    RntbdConnectionManagerEvent.RouteDecision(
                        managerEnabled ? RntbdRoutePath.Manager : RntbdRoutePath.Legacy,
                        requestUri?.AbsoluteUri ?? string.Empty,
                        localRegionRequest,
                        activityId));
            }

            return managerEnabled
                ? this.cmChannels.Value.GetChannel(requestUri, localRegionRequest)
                : base.GetChannel(requestUri, localRegionRequest);
        }

        public override void Dispose()
        {
            // Dispose the cloned graph exactly once, before the base tears down
            // the shared timer pools it depends on. The guard makes CM disposal
            // idempotent while base.Dispose() preserves the legacy
            // throw-on-double-dispose semantics.
            if (Interlocked.Exchange(ref this.cmDisposed, 1) == 0 &&
                this.cmChannels.IsValueCreated)
            {
                try
                {
                    this.cmChannels.Value.Dispose();
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceWarning(
                        "RntbdConnectionManager CMChannelDictionary dispose threw: {0}", ex.Message);
                }
            }

            base.Dispose();
        }

        private bool IsConnectionManagerEnabled()
        {
            try
            {
                return this.useConnectionManagerResolver();
            }
            catch (Exception ex)
            {
                // A misbehaving resolver must never break request dispatch; fall
                // back to the legacy path (flag treated as off).
                DefaultTrace.TraceWarning(
                    "RntbdConnectionManager useConnectionManagerResolver threw; treating as disabled: {0}", ex.Message);
                return false;
            }
        }

        private bool AreDebugLogsEnabled()
        {
            if (this.debugLogsResolver == null)
            {
                return false;
            }

            try
            {
                return this.debugLogsResolver();
            }
            catch (Exception ex)
            {
                // A misbehaving resolver must never break request dispatch;
                // treat debug logging as disabled.
                DefaultTrace.TraceWarning(
                    "RntbdConnectionManager debugLogsResolver threw; treating as disabled: {0}", ex.Message);
                return false;
            }
        }
    }
}
