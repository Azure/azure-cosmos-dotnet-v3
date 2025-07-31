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
#pragma warning disable SA1507 // Code should not contain multiple blank lines in a row


#pragma warning disable CS1570 // XML comment has badly formed XML
#pragma warning disable CS1584 // XML comment has syntactically incorrect cref attribute
    /// <summary>
    /// ConnectionStateListener listens to the connection reset event notification fired by the transport client
    /// and refreshes the Document client's address cache
    /// </summary>
    /// <remarks>
    /// <see cref="enableTcpConnectionEndpointRediscovery"/> controls enable/disable for the listener
    ///     - Enable: Will accept registrations and will notify the registered handlers
    ///     - Disable: Will not accept registrations and will not notify the registered handlers
    ///     - Immutable and changes need to re-create the listener => sometimes practically a process restart
    ///     
    /// <see cref="notificationConcurrency"/>control the number of concurrent notifications or calles to the registered handlers
    ///     - Default: Environment.ProcessorCount
    ///     - Can be set through <see cref="IStoreClientFactory.GetConnectionStateListener().SetConnectionEventConcurrency(int notificationConcurrency)"/>
    ///     - ZERO: no notifications will be sent
    /// 
    /// <see cref="Microsoft.Azure.Documents.Client.ConnectionPolicy.EnableTcpConnectionEndpointRediscovery"/> can be used in conjunction with this listener
    /// to control at the account level with the above combinations (AND clause)
    /// 
    /// A safe phased roll-out for service might be: 
    /// - <see cref="enableTcpConnectionEndpointRediscovery"/>=False
    /// - (Restart) <see cref="enableTcpConnectionEndpointRediscovery"/>=True & <see cref="notificationConcurrency"/>=0 (Subscription only non notifications)
    /// - <see cref="enableTcpConnectionEndpointRediscovery"/>=True & <see cref="notificationConcurrency"/> > 0 
    ///     - When upgrade resiliency will be fully ON
    ///     
    /// Live site turning off: (from final state)
    ///     - (! Restart) <see cref="enableTcpConnectionEndpointRediscovery"/>=True & <see cref="notificationConcurrency"/>=0 (Subscription only non notifications)
    ///     - (Restaret) <see cref="enableTcpConnectionEndpointRediscovery"/>=False
    ///     
    /// Monitoring:
    ///     - TCP direct connections in TIMED_WAIT state
    ///     - Task scheduler contention
    /// </remarks>
    internal sealed class ConnectionStateMuxListener : IConnectionStateListener
#pragma warning restore CS1584 // XML comment has syntactically incorrect cref attribute
#pragma warning restore SA1507 // Code should not contain multiple blank lines in a row
#pragma warning restore CS1570 // XML comment has badly formed XML
    {
        readonly internal bool enableTcpConnectionEndpointRediscovery;
        readonly internal ConcurrentDictionary<ServerKey, ConcurrentDictionary<Func<ServerKey, Task>, object>> serverKeyEventHandlers = new();
        volatile internal int notificationConcurrency;
        volatile internal SemaphoreSlim notificationSemaphore;

        public ConnectionStateMuxListener(bool enableTcpConnectionEndpointRediscovery) 
        {
#pragma warning disable CS1587 // XML comment has badly formed XML
            /// Default to the processor count 
            this.notificationConcurrency = Environment.ProcessorCount;
            this.notificationSemaphore = new SemaphoreSlim(this.notificationConcurrency);
            this.enableTcpConnectionEndpointRediscovery = enableTcpConnectionEndpointRediscovery;
#pragma warning restore CS1587 // XML comment has badly formed XML
        }

        public void SetConnectionEventConcurrency(int notificationConcurrency)
        {
            this.notificationConcurrency = notificationConcurrency;
            this.notificationSemaphore = new SemaphoreSlim(this.notificationConcurrency);
        }

        public void Register(ServerKey serverKey,
            Func<ServerKey, Task> serverKeyEventHandler) 
        {
            if (!this.enableTcpConnectionEndpointRediscovery) return;

            if (serverKey == null) throw new ArgumentNullException(nameof(serverKey));
            if (serverKeyEventHandler == null) throw new ArgumentNullException(nameof(serverKeyEventHandler));

            this.serverKeyEventHandlers.AddOrUpdate(serverKey,
                addValueFactory: serverKey => 
                {
                    ConcurrentDictionary<Func<ServerKey, Task>, object> entry = new();
                    entry[serverKeyEventHandler] = serverKeyEventHandler;
                    return entry;
                },
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

            if (serverKey == null) throw new ArgumentNullException(nameof(serverKey));
            if (serverKeyEventHandler == null) throw new ArgumentNullException(nameof(serverKeyEventHandler));

            if (this.serverKeyEventHandlers.TryGetValue(serverKey, out ConcurrentDictionary<Func<ServerKey, Task>, object> handlers)) 
            {
                bool removed = handlers.TryRemove(serverKeyEventHandler, out _);
                if (!removed)
                {
                    DefaultTrace.TraceInformation("UnRegister({0}) didn't match any registrations, possible duplicate un-registration?", serverKey.ToString());
                }

                if (handlers.IsEmpty)
                {
                    // Try to remove the entry from the outer dictionary
                    if (this.serverKeyEventHandlers.TryRemove(serverKey, out ConcurrentDictionary<Func<ServerKey, Task>, object> removedHandlers))
                    {
                        if (!removedHandlers.IsEmpty)
                        {
                            // Concurrency: new registrations showed-up
                            // Re-add them back (even if they are un-registered in-between dispose handling will take care of them
                            foreach (KeyValuePair<Func<ServerKey, Task>, object> entry in removedHandlers)
                            {
                                this.Register(serverKey, entry.Key);
                            }
                        }
                    }
                }
            }
        }

        public async Task OnConnectionEventAsync(ConnectionEvent connectionEvent,
            DateTime eventTime,
            ServerKey serverKey) 
        {
            if (!this.enableTcpConnectionEndpointRediscovery || this.notificationConcurrency == 0) return;

            if (serverKey == null) throw new ArgumentNullException(nameof(serverKey));

            DefaultTrace.TraceInformation("OnConnectionEventAsync fired, connectionEvent :{0}, eventTime: {1}, serverKey: {2}",
                connectionEvent,
                eventTime,
                serverKey.ToString());

            if (connectionEvent == ConnectionEvent.ReadEof || connectionEvent == ConnectionEvent.ReadFailure) 
            {
                if (this.serverKeyEventHandlers.TryGetValue(serverKey, out ConcurrentDictionary<Func<ServerKey, Task>, object> handlers)) 
                {
                    // Dispatcher is already calling it on an async Task and observing the failures
                    await this.NotifyAsync(serverKey, handlers);
                }
            }
        }

        private async Task NotifyAsync(ServerKey serverKey, ConcurrentDictionary<Func<ServerKey, Task>, object> allCallback) 
        {
            await this.notificationSemaphore.WaitAsync();

            try 
            {
                foreach (Func<ServerKey, Task> entry in allCallback.Keys) 
                {
                    try 
                    {
                        await entry(serverKey);
                    }
                    catch (ObjectDisposedException) 
                    {
                        this.UnRegister(serverKey, entry);
                    }
                }
            }
            finally 
            {
                this.notificationSemaphore.Release();
            }
        }
    }
}
