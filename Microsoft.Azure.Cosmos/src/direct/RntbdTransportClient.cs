﻿namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class RntbdTransportClient : TransportClient
    {

        private readonly ConnectionPoolManager rntbdConnectionManager;

        public RntbdTransportClient(int requestTimeout,
            int maxConcurrentConnectionOpenRequests,
            UserAgentContainer userAgent = null,
            string overrideHostNameInCertificate = null,
            int openTimeoutInSeconds = 0,
            int idleTimeoutInSeconds = 100,
            int timerPoolGranularityInSeconds = 0)
        {
            this.rntbdConnectionManager = new ConnectionPoolManager(
                new RntbdConnectionDispenser(requestTimeout,
                    overrideHostNameInCertificate,
                    openTimeoutInSeconds,
                    idleTimeoutInSeconds,
                    timerPoolGranularityInSeconds,
                    userAgent),
                    maxConcurrentConnectionOpenRequests);
        }

        public override void Dispose()
        {
            base.Dispose();

            this.rntbdConnectionManager?.Dispose();
        }

        internal override Task<StoreResponse> InvokeStoreAsync(
            Uri physicalAddress,
            ResourceOperation resourceOperation,
            DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(new TransportAddressUri(physicalAddress), resourceOperation, request);
        }

        internal override async Task<StoreResponse> InvokeStoreAsync(
            TransportAddressUri physicalAddress,
            ResourceOperation resourceOperation,
            DocumentServiceRequest request)
        {
            Guid activityId = Trace.CorrelationManager.ActivityId;
            Debug.Assert(activityId != Guid.Empty);

            if (!request.IsBodySeekableClonableAndCountable)
            {
                throw new InternalServerErrorException();
            }

            StoreResponse storeResponse = null;
            IConnection connection;
            try
            {
                connection = await this.rntbdConnectionManager.GetOpenConnection(activityId, physicalAddress.Uri);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceInformation("GetOpenConnection failed: RID: {0}, ResourceType {1}, Op: {2}, Address: {3}, Exception: {4}",
                    request.ResourceAddress,
                    request.ResourceType,
                    resourceOperation,
                    physicalAddress,
                    ex);

                throw;
            }

            try
            {
#if NETFX
                if (PerfCounters.Counters.BackendActiveRequests != null)
                {
                    PerfCounters.Counters.BackendActiveRequests.Increment();
                }

                if (PerfCounters.Counters.BackendRequestsPerSec != null)
                {
                    PerfCounters.Counters.BackendRequestsPerSec.Increment();
                }
#endif

                storeResponse = await connection.RequestAsync(request, physicalAddress.Uri, resourceOperation, activityId);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceInformation("RequestAsync failed: RID: {0}, ResourceType {1}, Op: {2}, Address: {3}, Exception: {4}",
                    request.ResourceAddress,
                    request.ResourceType,
                    resourceOperation,
                    physicalAddress,
                    ex);

                connection.Close();

                throw;
            }
            finally
            {
#if NETFX
                if (PerfCounters.Counters.BackendActiveRequests != null)
                {
                    PerfCounters.Counters.BackendActiveRequests.Decrement();
                }
#endif
            }

            // Even in case of a response with a failed status code (any failure), we can 
            // maintain the connection since the connection is bound to a physical process rather
            // than a replica - even if the replica is gone, as long as the process is responding,
            // the connection to the process is still good and can be pooled.
            this.rntbdConnectionManager.ReturnToPool(connection);

            // Throw an appropriate exception if we got a response message that was tagged with a failed status code
            TransportClient.ThrowServerException(request.ResourceAddress, storeResponse, physicalAddress.Uri, activityId, request);

            return storeResponse;
        }
    }
}
