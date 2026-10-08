//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.FaultInjection;

    // Connection-manager parallel-path analogue of LoadBalancingChannel.
    //
    // Reached only when the RNTBD connection-manager feature flag is ON (the
    // single fork lives in CMTransportClient.GetChannel). Mirrors the legacy
    // fan-out for behavioral parity: it splits load across PartitionCount
    // CMLoadBalancingPartitions (round-robin by activity-id hash), each of which
    // owns exactly one RntbdConnectionManager. Total connections per server thus
    // match the legacy PartitionCount * MaxChannels shape.
    internal sealed class CMLoadBalancingChannel : IChannel, IDisposable
    {
        private readonly Uri serverUri;

        private readonly CMLoadBalancingPartition singlePartition;
        private readonly CMLoadBalancingPartition[] partitions;

        // Idempotent, best-effort dispose flag (0 = live, 1 = disposed).
        // Matches the CMLoadBalancingPartition / CMTransportClient convention so
        // hierarchical shutdown never throws on a double dispose. Written with
        // Interlocked.Exchange; read with Volatile.Read.
        private int disposed;

        public CMLoadBalancingChannel(
            Uri serverUri,
            ChannelProperties channelProperties,
            bool localRegionRequest,
            IChaosInterceptor chaosInterceptor = null,
            IRntbdConnectionManagerTelemetry telemetry = null,
            Func<bool> debugLogsResolver = null,
            Func<int> snapshotIntervalSecondsResolver = null)
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
                this.partitions = new CMLoadBalancingPartition[channelProperties.PartitionCount];
                for (int i = 0; i < this.partitions.Length; i++)
                {
                    this.partitions[i] = new CMLoadBalancingPartition(
                        serverUri,
                        partitionProperties,
                        localRegionRequest,
                        chaosInterceptor: chaosInterceptor,
                        telemetry: telemetry,
                        debugLogsResolver: debugLogsResolver,
                        snapshotIntervalSecondsResolver: snapshotIntervalSecondsResolver);
                }
            }
            else
            {
                Debug.Assert(channelProperties.PartitionCount == 1);
                this.singlePartition = new CMLoadBalancingPartition(
                    serverUri,
                    channelProperties,
                    localRegionRequest,
                    chaosInterceptor: chaosInterceptor,
                    telemetry: telemetry,
                    debugLogsResolver: debugLogsResolver,
                    snapshotIntervalSecondsResolver: snapshotIntervalSecondsResolver);
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
            CMLoadBalancingPartition partition = this.GetLoadBalancedPartition(activityId);
            return partition.RequestAsync(
                request, physicalAddress, resourceOperation, activityId, transportRequestStats);
        }

        /// <summary>
        /// Exceptionless variant of <see cref="RequestAsync"/>.
        /// Returns a <see cref="Res{T}"/> instead of throwing on transport errors.
        /// </summary>
        public Task<Res<StoreResponse>> TryRequestAsync(
            DocumentServiceRequest request,
            TransportAddressUri physicalAddress,
            ResourceOperation resourceOperation,
            Guid activityId,
            TransportRequestStats transportRequestStats)
        {
            ObjectDisposedException ex = this.GetDisposedException();
            if (ex != null)
            {
                return Res.TaskFromException<StoreResponse>(ex);
            }

            Debug.Assert(this.serverUri.IsBaseOf(physicalAddress.Uri),
                string.Format("Expected: {0}.{1}Actual: {2}",
                this.serverUri.GetLeftPart(UriPartial.Authority),
                Environment.NewLine,
                physicalAddress.Uri.GetLeftPart(UriPartial.Authority)));

            if (this.singlePartition != null)
            {
                Debug.Assert(this.partitions == null);
                return this.singlePartition.TryRequestAsync(
                    request, physicalAddress, resourceOperation, activityId, transportRequestStats);
            }

            Debug.Assert(this.partitions != null);
            CMLoadBalancingPartition partition = this.GetLoadBalancedPartition(activityId);
            return partition.TryRequestAsync(
                request, physicalAddress, resourceOperation, activityId, transportRequestStats);
        }

        /// <summary>
        /// Attempts to open the Rntbd channel to the backend replica nodes.
        /// </summary>
        /// <param name="activityId">An unique identifier indicating the current activity id.</param>
        /// <returns>A completed task once the channel is opened.</returns>
        public Task OpenChannelAsync(Guid activityId)
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
                CMLoadBalancingPartition partition = this.GetLoadBalancedPartition(activityId);
                return partition.OpenChannelAsync(activityId);
            }
        }

        /// <summary>
        /// Gets the load balanced partition from the hash key,
        /// generated from the current activity id.
        /// </summary>
        /// <param name="activityId">An unique identifier indicating the current activity id.</param>
        /// <returns>An instance of <see cref="CMLoadBalancingPartition"/>.</returns>
        private CMLoadBalancingPartition GetLoadBalancedPartition(Guid activityId)
        {
            // A missing activity id (Guid.Empty) hashes to a constant, which
            // would pin every request to partitions[0] and defeat the fan-out.
            // Use a random GUID for partition selection only; the request's
            // activityId (used for tracing/correlation elsewhere) is untouched.
            if (activityId == Guid.Empty)
            {
                activityId = Guid.NewGuid();
            }

            int hash = activityId.GetHashCode();
            // Drop the sign bit. Operator % can return negative values in C#.
            return this.partitions[
                (hash & 0x7FFFFFFF) % this.partitions.Length];
        }

        public void Close()
        {
            ((IDisposable)this).Dispose();
        }

#region IDisposable

        void IDisposable.Dispose()
        {
            // Idempotent: only the first caller runs teardown; subsequent
            // disposes (e.g. hierarchical shutdown racing an explicit Close)
            // are no-ops rather than throwing ObjectDisposedException.
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }

            if (this.singlePartition != null)
            {
                try
                {
                    this.singlePartition.Dispose();
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceWarning(
                        "CMLoadBalancingChannel: partition dispose threw during shutdown: {0}", ex.Message);
                }
            }
            if (this.partitions != null)
            {
                for (int i = 0; i < this.partitions.Length; i++)
                {
                    try
                    {
                        this.partitions[i].Dispose();
                    }
                    catch (Exception ex)
                    {
                        // Best-effort: one partition failing to dispose must not
                        // abort disposal of the remaining partitions.
                        DefaultTrace.TraceWarning(
                            "CMLoadBalancingChannel: partition dispose threw during shutdown: {0}", ex.Message);
                    }
                }
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException ex = this.GetDisposedException();
            if (ex != null)
            {
                throw ex;
            }
        }

        private ObjectDisposedException GetDisposedException()
        {
            if (Volatile.Read(ref this.disposed) != 0)
            {
                Debug.Assert(this.serverUri != null);
                return new ObjectDisposedException(string.Format("{0}:{1}",
                    nameof(CMLoadBalancingChannel), this.serverUri));
            }

            return null;
        }

        #endregion
    }
}
