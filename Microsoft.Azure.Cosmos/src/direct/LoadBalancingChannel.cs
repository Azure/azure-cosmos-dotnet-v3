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
    using Microsoft.Azure.Documents.FaultInjection;

    // LoadBalancingChannel encapsulates the management of channels that connect to a single
    // back-end server. It assigns load to each channel, decides when to open more
    // channels, and when to close some channels.
    // To assign load, this channel uses a simple round-robin approach. It examines
    // the next channel available internally, and uses it if it's healthy and has
    // request slots available.
    internal sealed class LoadBalancingChannel : IChannel, IDisposable, IAsyncDisposable
    {
        private readonly Uri serverUri;

        private readonly LoadBalancingPartition singlePartition;
        private readonly LoadBalancingPartition[] partitions;

        private int disposed;

        public LoadBalancingChannel(
            Uri serverUri,
            ChannelProperties channelProperties,
            bool localRegionRequest,
            IChaosInterceptor chaosInterceptor = null)
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
                    channelProperties.ClientCertificateFunction,
                    channelProperties.ClientCertificateFailureHandler,
                    channelProperties.DnsResolutionFunction);
                this.partitions = new LoadBalancingPartition[channelProperties.PartitionCount];
                for (int i = 0; i < this.partitions.Length; i++)
                {
                    this.partitions[i] = new LoadBalancingPartition(
                        serverUri,
                        partitionProperties,
                        localRegionRequest,
                        chaosInterceptor: chaosInterceptor);
                }
            }
            else
            {
                Debug.Assert(channelProperties.PartitionCount == 1);
                this.singlePartition = new LoadBalancingPartition(
                    serverUri,
                    channelProperties,
                    localRegionRequest,
                    chaosInterceptor: chaosInterceptor);
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

        public Task CloseAsync() => this.DisposeAsync().AsTask();

#region IDisposable

        void IDisposable.Dispose()
        {
            if (Interlocked.CompareExchange(ref this.disposed, 1, 0) != 0)
            {
                return;
            }

            GC.SuppressFinalize(this);
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

        // Keep in sync with Dispose().
        // TODO(#4393): Wire upstream callers (IChannelDictionary) to call DisposeAsync
        // to fully address Path 2 (mass disposal starvation).
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref this.disposed, 1, 0) != 0)
            {
                return;
            }

            GC.SuppressFinalize(this);
            int capacity = (this.singlePartition != null ? 1 : 0) + (this.partitions?.Length ?? 0);
            List<Task> disposeTasks = new List<Task>(capacity);
            if (this.singlePartition != null)
            {
                disposeTasks.Add(this.singlePartition.DisposeAsync().AsTask());
            }
            if (this.partitions != null)
            {
                for (int i = 0; i < this.partitions.Length; i++)
                {
                    disposeTasks.Add(this.partitions[i].DisposeAsync().AsTask());
                }
            }
            try
            {
                await Task.WhenAll(disposeTasks).ConfigureAwait(false);
            }
            catch (AggregateException ae)
            {
                foreach (Exception inner in ae.Flatten().InnerExceptions)
                {
                    DefaultTrace.TraceWarning(
                        "[RNTBD LoadBalancingChannel] Async dispose encountered error during partition disposal: {0}",
                        inner.Message);
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed != 0)
            {
                Debug.Assert(this.serverUri != null);
                throw new ObjectDisposedException(string.Format("{0}:{1}",
                    nameof(LoadBalancingChannel), this.serverUri));
            }
        }

#endregion
    }
}
