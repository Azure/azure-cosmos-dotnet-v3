//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class LoadBalancingPartition : IDisposable
    {
        private readonly Uri serverUri;
        private readonly ChannelProperties channelProperties;
        private readonly bool localRegionRequest;
        private readonly int maxCapacity;  // maxChannels * maxRequestsPerChannel

        private int requestsPending = 0;  // Atomic.
        // Clock hand.
        private readonly SequenceGenerator sequenceGenerator =
            new SequenceGenerator();

        private readonly ReaderWriterLockSlim capacityLock =
            new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        // capacity == openChannels.Count * maxRequestsPerChannel.
        private int capacity = 0;  // Guarded by capacityLock.
        private readonly List<LbChannelState> openChannels =
            new List<LbChannelState>();  // Guarded by capacityLock.

        public LoadBalancingPartition(Uri serverUri, ChannelProperties channelProperties, bool localRegionRequest)
        {
            Debug.Assert(serverUri != null);
            this.serverUri = serverUri;
            Debug.Assert(channelProperties != null);
            this.channelProperties = channelProperties;
            this.localRegionRequest = localRegionRequest;

            this.maxCapacity = checked(channelProperties.MaxChannels *
                channelProperties.MaxRequestsPerChannel);
        }

        public async Task<StoreResponse> RequestAsync(
            DocumentServiceRequest request,
            Uri physicalAddress,
            ResourceOperation resourceOperation,
            Guid activityId)
        {
            int currentPending = Interlocked.Increment(
                ref this.requestsPending);
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

                while (true)
                {
                    LbChannelState channelState = null;
                    bool addCapacity = false;

                    uint sequenceNumber = this.sequenceGenerator.Next();

                    this.capacityLock.EnterReadLock();
                    try
                    {
                        if (currentPending <= this.capacity)
                        {
                            // Enough capacity is available, pick a channel.
                            int channelIndex = (int) (sequenceNumber % this.openChannels.Count);
                            LbChannelState candidateChannel = this.openChannels[channelIndex];
                            if (candidateChannel.Enter())
                            {
                                // Do not check the health status yet. Do it
                                // without holding the capacity lock.
                                channelState = candidateChannel;
                            }
                        }
                        else
                        {
                            addCapacity = true;
                        }
                    }
                    finally
                    {
                        this.capacityLock.ExitReadLock();
                    }

                    if (channelState != null)
                    {
                        bool healthy = false;
                        try
                        {
                            healthy = channelState.DeepHealthy;
                            if (healthy)
                            {
                                return await channelState.Channel.RequestAsync(
                                    request,
                                    physicalAddress,
                                    resourceOperation,
                                    activityId);
                            }

                            // Unhealthy channel
                            this.capacityLock.EnterWriteLock();
                            try
                            {
                                // Other callers might have noticed the channel
                                // fail at the same time. Do not assume that
                                // this caller is the only one trying to remove
                                // it.
                                if (this.openChannels.Remove(channelState))
                                {
                                    this.capacity -= this.channelProperties.MaxRequestsPerChannel;
                                }
                                Debug.Assert(
                                    this.capacity ==
                                    this.openChannels.Count *
                                    this.channelProperties.MaxRequestsPerChannel);
                            }
                            finally
                            {
                                this.capacityLock.ExitWriteLock();
                            }
                        }
                        finally
                        {
                            bool lastCaller = channelState.Exit();
                            if (lastCaller && !channelState.ShallowHealthy)
                            {
                                channelState.Dispose();
                                DefaultTrace.TraceInformation(
                                    "Closed unhealthy channel {0}",
                                    channelState.Channel);
                            }
                        }
                    }
                    else if (addCapacity)
                    {
                        int targetCapacity = MathUtils.CeilingMultiple(
                            currentPending,
                            this.channelProperties.MaxRequestsPerChannel);
                        Debug.Assert(targetCapacity % this.channelProperties.MaxRequestsPerChannel == 0);
                        int targetChannels = targetCapacity / this.channelProperties.MaxRequestsPerChannel;
                        int channelsCreated = 0;

                        this.capacityLock.EnterWriteLock();
                        try
                        {
                            if (this.openChannels.Count < targetChannels)
                            {
                                channelsCreated = targetChannels - this.openChannels.Count;
                            }
                            while (this.openChannels.Count < targetChannels)
                            {
                                Channel newChannel = new Channel(activityId, this.serverUri, this.channelProperties, this.localRegionRequest);
                                newChannel.Initialize();
                                this.openChannels.Add(new LbChannelState(newChannel, this.channelProperties.MaxRequestsPerChannel));
                                this.capacity += this.channelProperties.MaxRequestsPerChannel;
                            }
                            Debug.Assert(
                                this.capacity ==
                                this.openChannels.Count *
                                this.channelProperties.MaxRequestsPerChannel);
                        }
                        finally
                        {
                            this.capacityLock.ExitWriteLock();
                        }
                        if (channelsCreated > 0)
                        {
                            DefaultTrace.TraceInformation(
                                "Opened {0} channels to server {1}",
                                channelsCreated, this.serverUri);
                        }
                    }
                }
            }
            finally
            {
                currentPending = Interlocked.Decrement(
                    ref this.requestsPending);
                Debug.Assert(currentPending >= 0);
            }
        }

        public void Dispose()
        {
            this.capacityLock.EnterWriteLock();
            try
            {
                foreach (LbChannelState channelState in this.openChannels)
                {
                    channelState.Dispose();
                }
            }
            finally
            {
                this.capacityLock.ExitWriteLock();
            }
            this.capacityLock.Dispose();
        }

        // Thread safe sequence number generator. It should probably be a
        // separate utility class of its own.
        private sealed class SequenceGenerator
        {
            private int current = 0;

            // Returns the next positive integer in the sequence. Wraps around
            // when it reaches UInt32.MaxValue.
            public uint Next()
            {
                // Interlocked.Increment only works with signed integer.
                // Map the returned value to the unsigned int32 range.
                return (uint) (
                    ((uint) int.MaxValue) + 1 +
                    Interlocked.Increment(ref this.current));
            }
        }
    }
}