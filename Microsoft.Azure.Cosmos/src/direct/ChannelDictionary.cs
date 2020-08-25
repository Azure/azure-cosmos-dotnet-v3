//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;

    // ChannelDictionary maps server keys to load-balanced channels. There is
    // one load-balanced channel per back-end server.
    internal sealed class ChannelDictionary : IDisposable
    {
        private readonly ChannelProperties channelProperties;
        private bool disposed = false;

        private ConcurrentDictionary<ServerKey, IChannel> channels =
            new ConcurrentDictionary<ServerKey, IChannel>();

        public ChannelDictionary(ChannelProperties channelProperties)
        {
            Debug.Assert(channelProperties != null);
            this.channelProperties = channelProperties;
        }

        public IChannel GetChannel(Uri requestUri)
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
                this.channelProperties);
            if (this.channels.TryAdd(key, value))
            {
                return value;
            }
            bool found = this.channels.TryGetValue(key, out value);
            Debug.Assert(found);
            Debug.Assert(value != null);
            return value;
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
