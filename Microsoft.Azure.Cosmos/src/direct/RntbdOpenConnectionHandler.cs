// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Rntbd
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Encapsulates the reading from the network on a TCP connection.
    /// </summary>
    /// <remarks>
    /// RntbdStreamReader does not own the stream that it reads from.
    /// It is the callers responsibility to close the stream.
    /// </remarks>
    internal sealed class RntbdOpenConnectionHandler : IOpenConnectionsHandler
    {
        /// <summary>
        /// summary1
        /// </summary>
        private readonly TransportClient transportClient;

        /// <summary>
        /// summary2
        /// </summary>
        private readonly SemaphoreSlim semaphore;

        /// <summary>
        /// Default timeout is 10 mins.
        /// </summary>
        private readonly int SemaphoreAcquireTimeoutInMillis = 600000;

        /// <summary>
        /// test
        /// </summary>
        /// <param name="transportClient"></param>
        public RntbdOpenConnectionHandler(TransportClient transportClient)
        {
            this.transportClient = transportClient ?? throw new ArgumentNullException("Argument 'transportClient' can not be null");
            this.semaphore = new SemaphoreSlim(
                Environment.ProcessorCount,
                Environment.ProcessorCount);
        }

        /// <summary>
        /// Invokes the transport client delegate to open the Rntbd connection
        /// and establish Rntbd context negotiation to the backend replica nodes.
        /// </summary>
        /// <param name="addresses">An instance of <see cref="Tuple{T1, T2}"/> containing the partition key id
        /// and it's corresponding address information.</param>
        public async Task OpenRntbdChannelsAsync(IReadOnlyList<TransportAddressUri> addresses)
        {
            foreach (TransportAddressUri address in addresses)
            {
                bool slimAcquired = false;
                DefaultTrace.TraceVerbose("Attempting to open Rntbd connection to backend uri: {0}. '{1}'",
                    address.Uri,
                    Trace.CorrelationManager.ActivityId);
                try
                {
                    slimAcquired = await this.semaphore
                        .WaitAsync(this.SemaphoreAcquireTimeoutInMillis)
                        .ConfigureAwait(false);

                    if (slimAcquired)
                    {
                        await this.transportClient.OpenConnectionAsync(
                            physicalAddress: address.Uri);
                        address.SetConnected();
                    }
                    else
                    {
                        throw new Exception($"Error occurred while acquiring the semaphore within the timeout: {this.SemaphoreAcquireTimeoutInMillis}.");
                    }
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceWarning("Failed to open Rntbd connection to backend uri: {0} with exception: {1}. '{2}'",
                        address.Uri,
                        ex,
                        Trace.CorrelationManager.ActivityId);
                    address.SetUnhealthy();
                }
            }
        }
    }
}
