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
        /// A read-only instance of <see cref="SemaphoreSlim"/> for
        /// concurrency control.
        /// </summary>
        private readonly SemaphoreSlim semaphore;

        /// <summary>
        /// A read-only TimeSpan indicating the semephore timeout in minutes.
        /// The default timeout is 10 minutes.
        /// </summary>
        private static readonly TimeSpan SemaphoreAcquireTimeout = TimeSpan.FromMinutes(10);

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

            // The semaphore arguments `initialCount` and `maxCount` are set to match the number of cpu cores, to keep the
            // implementation similar to the Java counterpart.
            this.semaphore = new SemaphoreSlim(
                initialCount: Environment.ProcessorCount,
                maxCount: Environment.ProcessorCount);
        }

        /// <inheritdoc/>
        public async Task TryOpenRntbdChannelsAsync(IEnumerable<TransportAddressUri> addresses)
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
                        .WaitAsync(RntbdOpenConnectionHandler.SemaphoreAcquireTimeout)
                        .ConfigureAwait(false);

                    if (slimAcquired)
                    {
                        await this.transportClient.OpenConnectionAsync(
                            physicalAddress: address);
                        address.SetConnected();
                    }
                    else
                    {
                        DefaultTrace.TraceWarning("Failed to open Rntbd connection to backend uri: {0} because" +
                            "the semaphore couldn't be acquired within the given timeout: {1} minutes. '{2}'",
                            address.Uri,
                            RntbdOpenConnectionHandler.SemaphoreAcquireTimeout.TotalMinutes,
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
                        this.semaphore.Release();
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!this.disposed)
            {
                this.semaphore.Dispose();
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
