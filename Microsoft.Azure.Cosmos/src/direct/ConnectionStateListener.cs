//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// ConnectionStateListener listens to the connection reset event notification fired by the transport client
    /// and refreshes the Document client's address cache
    /// </summary>
    internal sealed class ConnectionStateListener : IConnectionStateListener
    {
        private readonly IAddressResolver addressResolver;

        public ConnectionStateListener(IAddressResolver addressResolver)
        {
            this.addressResolver = addressResolver;
        }

        public void OnConnectionEvent(ConnectionEvent connectionEvent, DateTime eventTime, ServerKey serverKey)
        {
            DefaultTrace.TraceInformation("OnConnectionEvent fired, connectionEvent :{0}, eventTime: {1}, serverKey: {2}",
                connectionEvent,
                eventTime,
                serverKey.ToString());

            if (connectionEvent == ConnectionEvent.ReadEof || connectionEvent == ConnectionEvent.ReadFailure)
            {
                Task updateCacheTask = Task.Run(
                    async () => await this.addressResolver.UpdateAsync(serverKey));

                updateCacheTask.ContinueWith(task =>
                {
                    DefaultTrace.TraceWarning("AddressCache update failed: {0}", task.Exception?.InnerException);
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }
    }
}
