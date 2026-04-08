//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.FaultInjection;

    // ChannelDictionary maps server keys to load-balanced channels. There is
    // one load-balanced channel per back-end server.
    internal sealed class ChannelDictionary : IChannelDictionary, IDisposable, IAsyncDisposable
    {
        private readonly ChannelProperties channelProperties;
        private int disposed;

        private ConcurrentDictionary<ServerKey, IChannel> channels =
            new ConcurrentDictionary<ServerKey, IChannel>();

        private readonly IChaosInterceptor chaosInterceptor;

        public ChannelDictionary(ChannelProperties channelProperties, IChaosInterceptor chaosInterceptor = null)
        {
            Debug.Assert(channelProperties != null);
            this.channelProperties = channelProperties;
            this.chaosInterceptor = chaosInterceptor;
        }

        /// <summary>
        /// Creates or gets an instance of <see cref="LoadBalancingChannel"/> using the server's physical uri.
        /// </summary>
        /// <param name="requestUri">An instance of <see cref="Uri"/> containing the backend server URI.</param>
        /// <param name="localRegionRequest">A boolean flag indicating if the request is targeting the local region.</param>
        /// <returns>An instance of <see cref="IChannel"/> containing the <see cref="LoadBalancingChannel"/>.</returns>
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
            value = new LoadBalancingChannel(
                new Uri(requestUri.GetLeftPart(UriPartial.Authority)),
                this.channelProperties,
                localRegionRequest,
                this.chaosInterceptor);

            if (this.channels.TryAdd(key, value))
            {
                return value;
            }
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
            if (Interlocked.CompareExchange(ref this.disposed, 1, 0) != 0)
            {
                return;
            }

            GC.SuppressFinalize(this);
            foreach (IChannel channel in this.channels.Values)
            {
                channel.Close();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref this.disposed, 1, 0) != 0)
            {
                return;
            }

            GC.SuppressFinalize(this);

            List<Task> closeTasks = new List<Task>(this.channels.Count);
            foreach (IChannel channel in this.channels.Values)
            {
                closeTasks.Add(channel.CloseAsync());
            }

            Task whenAllTask = Task.WhenAll(closeTasks);
            try
            {
                await whenAllTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                foreach (Exception inner in whenAllTask.Exception.Flatten().InnerExceptions)
                {
                    DefaultTrace.TraceWarning(
                        "[RNTBD ChannelDictionary] Async dispose encountered error during channel closure: {0}",
                        inner.Message);
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed != 0)
            {
                throw new ObjectDisposedException(nameof(ChannelDictionary));
            }
        }
    }
}
