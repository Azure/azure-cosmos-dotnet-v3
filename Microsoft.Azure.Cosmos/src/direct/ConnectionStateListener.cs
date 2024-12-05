//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
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
        // Unbounded on number of endpoints
        readonly bool enableTcpConnectionEndpointRediscovery;
        readonly ConcurrentDictionary<ServerKey, ConcurrentDictionary<Func<ServerKey, Task>, object>> serverKeyEventHandlers = new();
        readonly SemaphoreSlim notificationConcurrency;

        public ConnectionStateListener(bool enableTcpConnectionEndpointRediscovery)
        {
            this.enableTcpConnectionEndpointRediscovery = enableTcpConnectionEndpointRediscovery;

            // TODO: Control through environment variable 
            // Default to the processor count 
            this.notificationConcurrency = new SemaphoreSlim(Environment.ProcessorCount);
        }

        public void Register(ServerKey serverKey,
            Func<ServerKey, Task> serverKeyEventHandler)
        {
            if (!this.enableTcpConnectionEndpointRediscovery) return;

            if (serverKey == null || serverKeyEventHandler == null) throw new ArgumentNullException(serverKeyEventHandler != null ? nameof(serverKeyEventHandler) : nameof(serverKey));

            this.serverKeyEventHandlers.AddOrUpdate(serverKey,
                addValueFactory: serverKey => new (),
                updateValueFactory: (serverKey, value) =>
                    {
                        value.GetOrAdd(serverKeyEventHandler, serverKeyEventHandler);
                        return value;
                    });
        }

        public void UnRegister(ServerKey serverKey,
            Func<ServerKey, Task> serverKeyEventHandler)
        {
            if (!this.enableTcpConnectionEndpointRediscovery) return;

            if (serverKey == null || serverKeyEventHandler == null) throw new ArgumentNullException(serverKeyEventHandler != null ? nameof(serverKeyEventHandler) : nameof(serverKey));

            if (this.serverKeyEventHandlers.TryGetValue(serverKey, out ConcurrentDictionary<Func<ServerKey, Task>, object> handler))
            {
                handler.TryRemove(serverKeyEventHandler, out _);
            }
        }

        public void OnConnectionEvent(ConnectionEvent connectionEvent, 
            DateTime eventTime, 
            ServerKey serverKey)
        {
            if (!this.enableTcpConnectionEndpointRediscovery) return;

            DefaultTrace.TraceInformation("OnConnectionEventAsync fired, connectionEvent :{0}, eventTime: {1}, serverKey: {2}",
                connectionEvent,
                eventTime,
                serverKey.ToString());

            if (connectionEvent == ConnectionEvent.ReadEof || connectionEvent == ConnectionEvent.ReadFailure)
            {
                if (this.serverKeyEventHandlers.TryGetValue(serverKey, out ConcurrentDictionary<Func<ServerKey, Task>, object> handlers))
                {
                    // Tasks will be queued async but they will be blocked on the concurrency 
                    Task updateCacheTask = Task.Run(
                        async () => await this.NotifyAsync(serverKey, handlers));
                }
            }
        }

        private async Task NotifyAsync(ServerKey serverKey, ConcurrentDictionary<Func<ServerKey, Task>, object> allCallback)
        {
            this.notificationConcurrency.Wait();

            try
            {
                foreach (Func<ServerKey, Task> entry in allCallback.Keys)
                {
                    await entry(serverKey);
                }
            }
            finally
            {
                this.notificationConcurrency.Release();
            }
        }
    }
}
