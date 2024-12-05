//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Rntbd;
    using static Microsoft.Azure.Documents.ConnectionStateListener;

    /// <summary>
    /// ConnectionStateListener listens to the connection reset event notification fired by the transport client
    /// and refreshes the Document client's address cache
    /// </summary>

    internal sealed class ConnectionStateListener : IConnectionStateListener
    {
        // Unbounded on number of endpoints
        readonly bool enableTcpConnectionEndpointRediscovery;
        readonly ConcurrentDictionary<ServerKey, EventHandler<ServerKey>> serverKeyEventHandlers = new ConcurrentDictionary<ServerKey, EventHandler<ServerKey>>();

        public ConnectionStateListener(bool enableTcpConnectionEndpointRediscovery)
        {
            this.enableTcpConnectionEndpointRediscovery = enableTcpConnectionEndpointRediscovery;
        }

        public void Register(ServerKey serverKey, 
            EventHandler<ServerKey> serverKeyEventHandler)
        {
            if (!this.enableTcpConnectionEndpointRediscovery) return;

            if (serverKey == null || serverKeyEventHandler == null) throw new ArgumentNullException(serverKeyEventHandler != null ? nameof(serverKeyEventHandler) : nameof(serverKey));

            this.serverKeyEventHandlers.AddOrUpdate(serverKey,
                addValueFactory: serverKey => new EventHandler<ServerKey>(serverKeyEventHandler),
                updateValueFactory: (serverKey, value) =>
                    {
                        value += serverKeyEventHandler;
                        return value;
                    });
        }

        public void UnRegister(ServerKey serverKey,
            EventHandler<ServerKey> serverKeyEventHandler)
        {
            if (!this.enableTcpConnectionEndpointRediscovery) return;

            if (serverKey == null || serverKeyEventHandler == null) throw new ArgumentNullException(serverKeyEventHandler != null ? nameof(serverKeyEventHandler) : nameof(serverKey));

            if (this.serverKeyEventHandlers.TryGetValue(serverKey, out EventHandler<ServerKey> handler))
            {
                handler -= serverKeyEventHandler;
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
                if (this.serverKeyEventHandlers.TryGetValue(serverKey, out EventHandler<ServerKey> stateHandler))
                {
                    Task updateCacheTask = Task.Run(
                        () => stateHandler.Invoke(this, serverKey));
                }
            }
        }

////        internal sealed class AddressResolverConnectionStateListener : IDisposable
////        {
////            private readonly IAddressResolver addressResolver;
////            private readonly ConnectionStateListener connectionStateListener;
////            private readonly ConcurrentDictionary<ServerKey, EventHandler<ServerKey>> eventHandlers = new ConcurrentDictionary<ServerKey, EventHandler<ServerKey>>();

////            public AddressResolverConnectionStateListener(IAddressResolver addressResolver,
////                ConnectionStateListener connectionStateListener)
////            {
////                this.addressResolver = addressResolver;
////                this.connectionStateListener = connectionStateListener;
////            }

////            public void Register(ServerKey serverKey)
////            {
////#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
////                EventHandler<ServerKey> handler = new EventHandler<ServerKey>(async (sender, args) => await this.OnConnectionEventAsync(sender, args));
////#pragma warning restore VSTHRD101 // Avoid unsupported async delegates

////                this.eventHandlers.Add(handler);
////                this.connectionStateListener.Register(serverKey, handler);
////            }

////            public void Dispose()
////            {
////                if (this.eventHandlers.Any())
////                {
////                    foreach(KeyValuePair<ServerKey, EventHandler<ServerKey>> entry in this.eventHandlers)
////                    {
////                        this.connectionStateListener.UnRegister(entry.Key, entry.Value);
////                    }

////                    this.eventHandlers.Clear();
////                }
////            }

////            private async Task OnConnectionEventAsync(object? _, ServerKey serverKey)
////            {
////                try
////                {
////                    await this.addressResolver.UpdateAsync(serverKey);
////                }
////                catch (Exception ex)
////                {
////                    DefaultTrace.TraceWarning("AddressCache update failed: {0}", ex.InnerException);
////                }
////            }
////        }
    }
}
