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

        private readonly SemaphoreSlim concurrentOpeningChannelSlim;

        public LoadBalancingPartition(Uri serverUri, ChannelProperties channelProperties, bool localRegionRequest)
        {
            Debug.Assert(serverUri != null);
            this.serverUri = serverUri;
            Debug.Assert(channelProperties != null);
            this.channelProperties = channelProperties;
            this.localRegionRequest = localRegionRequest;

            this.maxCapacity = checked(channelProperties.MaxChannels *
                channelProperties.MaxRequestsPerChannel);

            this.concurrentOpeningChannelSlim =
                new SemaphoreSlim(channelProperties.MaxConcurrentOpeningConnectionCount, channelProperties.MaxConcurrentOpeningConnectionCount);
        }

        public async Task<StoreResponse> RequestAsync(
            DocumentServiceRequest request,
            TransportAddressUri physicalAddress,
            ResourceOperation resourceOperation,
            Guid activityId,
            TransportRequestStats transportRequestStats)
        {
            int currentPending = Interlocked.Increment(
                ref this.requestsPending);
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

                while (true)
                {
                    LbChannelState channelState = null;
                    bool addCapacity = false;

                    uint sequenceNumber = this.sequenceGenerator.Next();

                    this.capacityLock.EnterReadLock();
                    try
                    {
                        transportRequestStats.NumberOfOpenConnectionsToEndpoint = this.openChannels.Count; // Lock has already been acquired for openChannels
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
                                    activityId,
                                    transportRequestStats);
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
                                await this.OpenChannelAndIncrementCapacity(
                                    activityId: activityId,
                                    waitForBackgroundInitializationComplete: false);
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

        /// <summary>
        /// Open and initializes the <see cref="Channel"/>.
        /// </summary>
        /// <param name="activityId">An unique identifier indicating the current activity id.</param>
        internal Task OpenChannelAsync(Guid activityId)
        {
            this.capacityLock.EnterWriteLock();
            if (this.capacity < this.maxCapacity)
            {
                try
                {
                    return this.OpenChannelAndIncrementCapacity(
                        activityId: activityId,
                        waitForBackgroundInitializationComplete: true);
                }
                finally
                {
                    this.capacityLock.ExitWriteLock();
                }
            }
            else
            {
                string errorMessage = $"Failed to open channels to server {this.serverUri} because the current channel capacity {this.capacity} has exceeded the maaximum channel capacity limit: {this.maxCapacity}";
                this.capacityLock.ExitWriteLock();

                DefaultTrace.TraceWarning(
                    message: errorMessage);

                // Converting the error into transport exception.
                throw new TransportException(
                    errorCode: TransportErrorCode.ChannelOpenFailed,
                    innerException: new Exception(errorMessage),
                    activityId: activityId,
                    requestUri: this.serverUri,
                    this.ToString(),
                    userPayload: false,
                    payloadSent: false);
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

        /// <summary>
        /// Open and initializes the <see cref="Channel"/> and adds
        /// the corresponding channel state to the openChannels pool
        /// and increment the currrent channel capacity.
        /// </summary>
        /// <param name="activityId">An unique identifier indicating the current activity id.</param>
        /// <param name="waitForBackgroundInitializationComplete">A boolean flag to indicate if the caller thread should
        /// wait until all the background tasks have finished.</param>
        private async Task OpenChannelAndIncrementCapacity(
            Guid activityId,
            bool waitForBackgroundInitializationComplete)
        {
            Debug.Assert(this.capacityLock.IsWriteLockHeld);
            Channel newChannel = new(
                activityId,
                this.serverUri,
                this.channelProperties,
                this.localRegionRequest,
                this.concurrentOpeningChannelSlim);

            if (waitForBackgroundInitializationComplete)
            {
                await newChannel.OpenChannelAsync(activityId);
            }

            this.openChannels.Add(
                new LbChannelState(
                    newChannel,
                    this.channelProperties.MaxRequestsPerChannel));
            this.capacity += this.channelProperties.MaxRequestsPerChannel;
        }

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