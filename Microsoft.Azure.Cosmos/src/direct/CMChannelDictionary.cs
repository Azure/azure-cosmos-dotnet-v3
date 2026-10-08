//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.FaultInjection;

    // Connection-manager parallel-path analogue of ChannelDictionary.
    //
    // Reached only when the RNTBD connection-manager feature flag is ON (the
    // single fork lives in CMTransportClient.GetChannel). Maps server keys to
    // one CMLoadBalancingChannel per back-end server, exactly like the legacy
    // dictionary, but every channel it hands out is manager-backed.
    internal sealed class CMChannelDictionary : IChannelDictionary, IDisposable
    {
        private readonly ChannelProperties channelProperties;
        private readonly IChaosInterceptor chaosInterceptor;
        private readonly IRntbdConnectionManagerTelemetry telemetry;
        private readonly Func<bool> debugLogsResolver;
        private readonly Func<int> snapshotIntervalSecondsResolver;

        // Idempotent, best-effort dispose flag (0 = live, 1 = disposed).
        // Matches the CMLoadBalancingPartition / CMTransportClient convention so
        // hierarchical shutdown never throws on a double dispose. Written with
        // Interlocked.Exchange; read with Volatile.Read.
        private int disposed;

        private ConcurrentDictionary<ServerKey, IChannel> channels =
            new ConcurrentDictionary<ServerKey, IChannel>();

        public CMChannelDictionary(
            ChannelProperties channelProperties,
            IChaosInterceptor chaosInterceptor = null,
            IRntbdConnectionManagerTelemetry telemetry = null,
            Func<bool> debugLogsResolver = null,
            Func<int> snapshotIntervalSecondsResolver = null)
        {
            Debug.Assert(channelProperties != null);
            this.channelProperties = channelProperties;
            this.chaosInterceptor = chaosInterceptor;
            this.telemetry = telemetry;
            this.debugLogsResolver = debugLogsResolver;
            this.snapshotIntervalSecondsResolver = snapshotIntervalSecondsResolver;
        }

        /// <summary>
        /// Creates or gets an instance of <see cref="CMLoadBalancingChannel"/> using the server's physical uri.
        /// </summary>
        /// <param name="requestUri">An instance of <see cref="Uri"/> containing the backend server URI.</param>
        /// <param name="localRegionRequest">A boolean flag indicating if the request is targeting the local region.</param>
        /// <returns>An instance of <see cref="IChannel"/> containing the <see cref="CMLoadBalancingChannel"/>.</returns>
        public IChannel GetChannel(
            Uri requestUri,
            bool localRegionRequest)
        {
            this.ThrowIfDisposed();
            ServerKey key = new ServerKey(requestUri);
            IChannel value = null;
            if (this.channels.TryGetValue(key, out value))
            {
                Debug.Assert(value != null);
                return value;
            }
            value = new CMLoadBalancingChannel(
                new Uri(requestUri.GetLeftPart(UriPartial.Authority)),
                this.channelProperties,
                localRegionRequest,
                this.chaosInterceptor,
                this.telemetry,
                this.debugLogsResolver,
                this.snapshotIntervalSecondsResolver);

            if (this.channels.TryAdd(key, value))
            {
                return value;
            }

            // Lost the race: another thread added a channel for this key first.
            // Our just-created CMLoadBalancingChannel owns disposable resources
            // (lazy RntbdConnectionManagers, snapshot timers), so dispose it
            // rather than leaking it, then return the winner.
            (value as IDisposable)?.Dispose();
            bool found = this.channels.TryGetValue(key, out value);
            Debug.Assert(found);
            Debug.Assert(value != null);
            return value;
        }

        public bool TryGetChannel(Uri requestUri, out IChannel channel)
        {
            this.ThrowIfDisposed();
            ServerKey key = new ServerKey(requestUri);
            return this.channels.TryGetValue(key, out channel);
        }

        public void Dispose()
        {
            // Idempotent: only the first caller runs teardown; subsequent
            // disposes (e.g. hierarchical shutdown racing an explicit Close)
            // are no-ops rather than throwing ObjectDisposedException.
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }

            foreach (IChannel channel in this.channels.Values)
            {
                try
                {
                    channel.Close();
                }
                catch (Exception ex)
                {
                    // Best-effort close: one channel failing to close must not
                    // abort disposal of the remaining channels.
                    DefaultTrace.TraceWarning(
                        "CMChannelDictionary: channel close threw during dispose: {0}", ex.Message);
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref this.disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(CMChannelDictionary));
            }
        }
    }
}
