//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.FaultInjection;

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

        public IChannel GetChannel(Uri requestUri, bool localRegionRequest)
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
                localRegionRequest);
            if (this.channels.TryAdd(key, value))
            {
                return value;
            }
            bool found = this.channels.TryGetValue(key, out value);
            Debug.Assert(found);
            Debug.Assert(value != null);
            return value;
        }

        public List<Channel> GetAllChannels()
        {
            this.ThrowIfDisposed();
            return ((List<LoadBalancingChannel>)this.channels.Values).SelectMany(loadBalancingChannel => loadBalancingChannel.GetAllChannels()).ToList();
        }

        public bool TryGetChannel(Uri requestUri, out IChannel channel)
        {
            this.ThrowIfDisposed();
            ServerKey key = new ServerKey(requestUri);
            return this.channels.TryGetValue(key, out channel);
        }

        /// <summary>
        /// Opens the Rntbd context negotiation channel to the backend replica node, using the server's physical uri.
        /// </summary>
        /// <param name="physicalAddress">An instance of <see cref="Uri"/> containing the backend server URI.</param>
        /// <param name="localRegionRequest">A boolean flag indicating if the request is targeting the local region.</param>
        /// <param name="activityId">An unique identifier indicating the current activity id.</param>
        /// <returns>An instance of <see cref="Task"/> indicating the channel has opened successfully.</returns>
        public Task OpenChannelAsync(
            Uri physicalAddress,
            bool localRegionRequest,
            Guid activityId, 
            RntbdServerErrorInjector serverErrorInjector)
        {
            // Do not open a new channel, if the channel is
            // already a part of the concurrent dictionary.
            if (!this.channels.ContainsKey(
                new ServerKey(physicalAddress)))
            {
                this.ThrowIfDisposed();
                IChannel channel = this.GetChannel(
                    physicalAddress,
                    localRegionRequest);

                return channel.OpenChannelAsync(activityId, serverErrorInjector);
            }

            return Task.FromResult(0);
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
