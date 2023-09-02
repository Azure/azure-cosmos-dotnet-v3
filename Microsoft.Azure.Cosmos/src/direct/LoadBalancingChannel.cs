//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    // LoadBalancingChannel encapsulates the management of channels that connect to a single
    // back-end server. It assigns load to each channel, decides when to open more
    // channels, and when to close some channels.
    // To assign load, this channel uses a simple round-robin approach. It examines
    // the next channel available internally, and uses it if it's healthy and has
    // request slots available.
    internal sealed class LoadBalancingChannel : IChannel, IDisposable
    {
        private readonly Uri serverUri;

        private readonly LoadBalancingPartition singlePartition;
        private readonly LoadBalancingPartition[] partitions;

        private bool disposed = false;

        public LoadBalancingChannel(
            Uri serverUri,
            ChannelProperties channelProperties,
            bool localRegionRequest,
            LoadBalancingPartition singleLoadBalancedPartitionForTest = null)
        {
            this.serverUri = serverUri;

            if ((channelProperties.PartitionCount < 1) ||
                (channelProperties.PartitionCount > 8))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(channelProperties.PartitionCount),
                    channelProperties.PartitionCount,
                    "The partition count must be between 1 and 8");
            }
            if (channelProperties.PartitionCount > 1)
            {
                ChannelProperties partitionProperties = new ChannelProperties(
                    channelProperties.UserAgent,
                    channelProperties.CertificateHostNameOverride,
                    channelProperties.ConnectionStateListener,
                    channelProperties.RequestTimerPool,
                    channelProperties.RequestTimeout,
                    channelProperties.OpenTimeout,
                    channelProperties.LocalRegionOpenTimeout,
                    channelProperties.PortReuseMode,
                    channelProperties.UserPortPool,
                    MathUtils.CeilingMultiple(
                        channelProperties.MaxChannels,
                        channelProperties.PartitionCount) /
                        channelProperties.PartitionCount,
                    1,
                    channelProperties.MaxRequestsPerChannel,
                    channelProperties.MaxConcurrentOpeningConnectionCount,
                    channelProperties.ReceiveHangDetectionTime,
                    channelProperties.SendHangDetectionTime,
                    channelProperties.IdleTimeout,
                    channelProperties.IdleTimerPool,
                    channelProperties.CallerId,
                    channelProperties.EnableChannelMultiplexing,
                    channelProperties.MemoryStreamPool,
                    channelProperties.RemoteCertificateValidationCallback,
                    channelProperties.DnsResolutionFunction);
                this.partitions = new LoadBalancingPartition[channelProperties.PartitionCount];
                for (int i = 0; i < this.partitions.Length; i++)
                {
                    this.partitions[i] = new LoadBalancingPartition(
                        serverUri, partitionProperties, localRegionRequest);
                }
            }
            else
            {
                Debug.Assert(channelProperties.PartitionCount == 1);
                this.singlePartition = singleLoadBalancedPartitionForTest ?? new LoadBalancingPartition(
                    serverUri, channelProperties, localRegionRequest);
            }
        }

        public bool Healthy
        {
            get
            {
                this.ThrowIfDisposed();
                return true;
            }
        }

        public Task<StoreResponse> RequestAsync(
            DocumentServiceRequest request,
            TransportAddressUri physicalAddress,
            ResourceOperation resourceOperation,
            Guid activityId,
            TransportRequestStats transportRequestStats)
        {
            this.ThrowIfDisposed();
            Debug.Assert(this.serverUri.IsBaseOf(physicalAddress.Uri),
                string.Format("Expected: {0}.{1}Actual: {2}",
                this.serverUri.GetLeftPart(UriPartial.Authority),
                Environment.NewLine,
                physicalAddress.Uri.GetLeftPart(UriPartial.Authority)));

            if (this.singlePartition != null)
            {
                Debug.Assert(this.partitions == null);
                return this.singlePartition.RequestAsync(
                    request, physicalAddress, resourceOperation, activityId, transportRequestStats);
            }

            Debug.Assert(this.partitions != null);
            LoadBalancingPartition partition = this.GetLoadBalancedPartition(activityId);
            return partition.RequestAsync(
                request, physicalAddress, resourceOperation, activityId, transportRequestStats);
        }

        /// <summary>
        /// Attempts to open the Rntbd channel to the backend replica nodes.
        /// </summary>
        /// <param name="activityId">An unique identifier indicating the current activity id.</param>
        /// <returns>A completed task once the channel is opened.</returns>
        public Task OpenChannelAsync(
            Guid activityId)
        {
            this.ThrowIfDisposed();
            if (this.singlePartition != null)
            {
                Debug.Assert(this.partitions == null);
                return this.singlePartition.OpenChannelAsync(activityId);
            }
            else
            {
                Debug.Assert(this.partitions != null);
                LoadBalancingPartition partition = this.GetLoadBalancedPartition(activityId);
                return partition.OpenChannelAsync(
                    activityId);
            }
        }

        /// <summary>
        /// Gets the load balanced partition from the hash key,
        /// generated from the current activity id.
        /// </summary>
        /// <param name="activityId">An unique identifier indicating the current activity id.</param>
        /// <returns>An instance of <see cref="LoadBalancingPartition"/>.</returns>
        private LoadBalancingPartition GetLoadBalancedPartition(
            Guid activityId)
        {
            int hash = activityId.GetHashCode();
            // Drop the sign bit. Operator % can return negative values in C#.
            return this.partitions[
            (hash & 0x8FFFFFFF) % this.partitions.Length];
        }

        public void Close()
        {
            ((IDisposable)this).Dispose();
        }

#region IDisposable

        void IDisposable.Dispose()
        {
            this.ThrowIfDisposed();
            this.disposed = true;
            if (this.singlePartition != null)
            {
                this.singlePartition.Dispose();
            }
            if (this.partitions != null)
            {
                for (int i = 0; i < this.partitions.Length; i++)
                {
                    this.partitions[i].Dispose();
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                Debug.Assert(this.serverUri != null);
                throw new ObjectDisposedException(string.Format("{0}:{1}",
                    nameof(LoadBalancingChannel), this.serverUri));
            }
        }

#endregion
    }
}
