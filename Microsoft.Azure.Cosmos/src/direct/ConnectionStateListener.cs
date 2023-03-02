//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Concurrent;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// ConnectionStateListener listens to the connection reset event notification fired by the transport client
    /// and refreshes the Document client's address cache
    /// </summary>
    internal sealed class ConnectionStateListener : IConnectionStateListener
    {
        /// <summary>
        /// transportAddressUris
        /// </summary>
        private readonly ConcurrentQueue<TransportAddressUri> transportAddressUris;

        /// <summary>
        /// blabla.
        /// </summary>
        public ConnectionStateListener()
        {
            this.transportAddressUris = new ();
        }

        /// <inheritdoc/>
        public void OnPrepareCallEvent(TransportAddressUri serverUri)
        {
            this.transportAddressUris.Enqueue(serverUri);
        }

        /// <inheritdoc/>
        public void OnConnectionEvent(ConnectionEvent connectionEvent, DateTime eventTime, ServerKey serverKey)
        {
            DefaultTrace.TraceInformation("OnConnectionEvent fired, connectionEvent :{0}, eventTime: {1}, serverKey: {2}",
                connectionEvent,
                eventTime,
                serverKey.ToString());

            if (connectionEvent == ConnectionEvent.ReadEof || connectionEvent == ConnectionEvent.ReadFailure)
            {
                foreach (TransportAddressUri addressUri in this.transportAddressUris)
                {
                    addressUri.SetUnhealthy();
                }
            }
        }
    }
}
