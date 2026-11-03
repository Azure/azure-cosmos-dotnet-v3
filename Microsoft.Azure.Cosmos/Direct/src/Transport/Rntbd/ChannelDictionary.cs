//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using Microsoft.Azure.Documents.FaultInjection;

    // ChannelDictionary maps server keys to load-balanced channels. There is
    // one load-balanced channel per back-end server.
    internal sealed class ChannelDictionary : IChannelDictionary, IDisposable
    {
        private readonly ChannelProperties channelProperties;
        private bool disposed = false;

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
            this.ThrowIfDisposed();
            this.disposed = true;
            foreach (IChannel channel in this.channels.Values)
            {
                channel.Close();
            }
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(ChannelDictionary));
            }
        }
    }
}
