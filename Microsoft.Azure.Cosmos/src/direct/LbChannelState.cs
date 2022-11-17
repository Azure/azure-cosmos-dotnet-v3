//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    // This class is thread safe.
    sealed class LbChannelState : IDisposable
    {
        private readonly int maxRequestsPending;
        private readonly IChannel channel;
        private int requestsPending = 0;  // Atomic.
        // Atomic. Stays true for as long as IChannel.Healthy keeps returning
        // true. Transitions to false exactly once, when IChannel.Healthy starts
        // returning false.
        private bool cachedHealthy = true;

        public LbChannelState(IChannel channel, int maxRequestsPending)
        {
            Debug.Assert(channel != null);
            this.channel = channel;
            Debug.Assert(maxRequestsPending > 0);
            this.maxRequestsPending = maxRequestsPending;
        }

        // Returns true if the channel is below peak utilization and false
        // otherwise.
        // If the call returns true, the caller must ensure that Exit() is
        // called exactly once. Treat Enter() and Exit() like
        // AddRef() and Release().
        public bool Enter()
        {
            int currentRequests = Interlocked.Increment(ref this.requestsPending);
            if (currentRequests > this.maxRequestsPending)
            {
                currentRequests = Interlocked.Decrement(ref this.requestsPending);
                Debug.Assert(currentRequests >= 0);
                return false;
            }
            return true;
        }

        // Returns true if the caller held the last reference to the channel
        // state. Callers can use the return value to decide when to dispose
        // of unhealthy channels.
        public bool Exit()
        {
            int currentRequests = Interlocked.Decrement(ref this.requestsPending);
            Debug.Assert(currentRequests >= 0);
            return currentRequests == 0;
        }

        // Checks the health status of the channel and returns it. This is more
        // expensive than ShallowHealthy. Avoid calling it more than once per
        // request.
        public bool DeepHealthy
        {
            get
            {
                if (!this.ShallowHealthy)
                {
                    return false;
                }
                bool healthy = this.channel.Healthy;
                if (!healthy)
                {
                    this.cachedHealthy = false;
                    Interlocked.MemoryBarrier();
                }
                return healthy;
            }
        }

        // Returns a cached value that reflects the last health status returned
        // by the underlying channel. Very cheap compared to DeepHealthy. Can
        // be used multiple times per request.
        public bool ShallowHealthy
        {
            get
            {
                Interlocked.MemoryBarrier();
                return this.cachedHealthy;
            }
        }

        public IChannel Channel { get { return this.channel; } }

        public void Dispose()
        {
            this.channel.Close();
        }
    }
}