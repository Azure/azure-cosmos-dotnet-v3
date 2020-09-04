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

        public LoadBalancingChannel(Uri serverUri, ChannelProperties channelProperties)
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
                    channelProperties.RequestTimeout,
                    channelProperties.OpenTimeout,
                    channelProperties.PortReuseMode,
                    channelProperties.UserPortPool,
                    MathUtils.CeilingMultiple(
                        channelProperties.MaxChannels,
                        channelProperties.PartitionCount) /
                        channelProperties.PartitionCount,
                    1,
                    channelProperties.MaxRequestsPerChannel,
                    channelProperties.ReceiveHangDetectionTime,
                    channelProperties.SendHangDetectionTime,
                    channelProperties.IdleTimeout,
                    channelProperties.CallerId);
                this.partitions = new LoadBalancingPartition[channelProperties.PartitionCount];
                for (int i = 0; i < this.partitions.Length; i++)
                {
                    this.partitions[i] = new LoadBalancingPartition(
                        serverUri, partitionProperties);
                }
            }
            else
            {
                Debug.Assert(channelProperties.PartitionCount == 1);
                this.singlePartition = new LoadBalancingPartition(
                    serverUri, channelProperties);
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
            Uri physicalAddress,
            ResourceOperation resourceOperation,
            Guid activityId)
        {
            this.ThrowIfDisposed();
            Debug.Assert(this.serverUri.IsBaseOf(physicalAddress),
                string.Format("Expected: {0}.{1}Actual: {2}",
                this.serverUri.GetLeftPart(UriPartial.Authority),
                Environment.NewLine,
                physicalAddress.GetLeftPart(UriPartial.Authority)));

            if (this.singlePartition != null)
            {
                Debug.Assert(this.partitions == null);
                return this.singlePartition.RequestAsync(
                    request, physicalAddress, resourceOperation, activityId);
            }

            Debug.Assert(this.partitions != null);
            int h = activityId.GetHashCode();
            // Drop the sign bit. Operator % can return negative values in C#.
            LoadBalancingPartition partition = this.partitions[
                (h & 0x8FFFFFFF) % this.partitions.Length];
            return partition.RequestAsync(
                request, physicalAddress, resourceOperation, activityId);
        }

        public void Close()
        {
            ((IDisposable) this).Dispose();
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
