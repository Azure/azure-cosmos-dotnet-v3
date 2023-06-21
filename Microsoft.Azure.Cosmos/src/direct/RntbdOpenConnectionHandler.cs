//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Rntbd
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;

#if !NETSTANDARD16
    using System.Diagnostics;
#endif

    /// <summary>
    /// Handler class to open Rntbd connections to backend replica nodes.
    /// </summary>
    internal sealed class RntbdOpenConnectionHandler : IOpenConnectionsHandler, IDisposable
    {
        /// <summary>
        /// A read-only instance of <see cref="TransportClient"/>
        /// </summary>
        private readonly TransportClient transportClient;

        /// <summary>
        /// A booolean flag indicating if the current instance of RntbdOpenConnectionHandler
        /// has been disposed.
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Constructor to initialize the <see cref="RntbdOpenConnectionHandler"/>.
        /// </summary>
        /// <param name="transportClient">A reference to the <see cref="TransportClient"/>.</param>
        public RntbdOpenConnectionHandler(
            TransportClient transportClient)
        {
            this.disposed = false;
            this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient), $"Argument {nameof(transportClient)} can not be null");
        }

        /// <inheritdoc/>
        public async Task TryOpenRntbdChannelsAsync(
            IEnumerable<TransportAddressUri> addresses,
            SemaphoreSlim semaphore,
            TimeSpan semaphoreAcquireTimeout)
        {
            foreach (TransportAddressUri address in addresses)
            {
                bool slimAcquired = false;
                DefaultTrace.TraceVerbose("Attempting to open Rntbd connection to backend uri: {0}. '{1}'",
                    address.Uri,
                    Trace.CorrelationManager.ActivityId);
                try
                {
                    slimAcquired = await semaphore
                        .WaitAsync(semaphoreAcquireTimeout)
                        .ConfigureAwait(false);

                    if (slimAcquired)
                    {
                        await this.transportClient.OpenConnectionAsync(
                            physicalAddress: address.Uri);
                        address.SetConnected();
                    }
                    else
                    {
                        DefaultTrace.TraceWarning("Failed to open Rntbd connection to backend uri: {0} because" +
                            "the semaphore couldn't be acquired within the given timeout: {1} minutes. '{2}'",
                            address.Uri,
                            semaphoreAcquireTimeout.TotalMinutes,
                            Trace.CorrelationManager.ActivityId);
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
                finally
                {
                    if (slimAcquired)
                    {
                        semaphore.Release();
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!this.disposed)
            {
                this.transportClient.Dispose();
                this.disposed = true;
            }
            else
            {
                DefaultTrace.TraceVerbose("Failed to dispose the instance of: {0}, because it is already disposed. '{1}'",
                    nameof(RntbdOpenConnectionHandler),
                    Trace.CorrelationManager.ActivityId);
            }
        }
    }
}
