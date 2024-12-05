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
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents.Rntbd;
    using static Microsoft.Azure.Documents.ConnectionStateListener;

    /// <summary>
    /// ConnectionStateListener listens to the connection reset event notification fired by the transport client
    /// and refreshes the Document client's address cache
    /// </summary>

    internal sealed class ConnectionStateListener : IConnectionStateListener
    {
        readonly ConcurrentDictionary<IAddressResolver, IAddressResolver> addressResolvers;

        public ConnectionStateListener()
        {
        }

        public void Register(IAddressResolver globalAddressResolver)
        {
            if (globalAddressResolver == null) throw new ArgumentNullException(nameof(globalAddressResolver));

            this.addressResolvers.GetOrAdd(globalAddressResolver, globalAddressResolver);
        }

        public void UnRegister(IAddressResolver globalAddressResolver)
        {
            if (globalAddressResolver == null) throw new ArgumentNullException(nameof(globalAddressResolver));

            this.addressResolvers.TryRemove(globalAddressResolver, out _);
        }

        public void OnConnectionEvent(ConnectionEvent connectionEvent, 
            DateTime eventTime, 
            ServerKey serverKey)
        {
            DefaultTrace.TraceInformation("OnConnectionEventAsync fired, connectionEvent :{0}, eventTime: {1}, serverKey: {2}",
                connectionEvent,
                eventTime,
                serverKey.ToString());

            if (connectionEvent == ConnectionEvent.ReadEof || connectionEvent == ConnectionEvent.ReadFailure)
            {
                Task updateCacheTask = Task.Run(
                    async () =>
                    {
                        /// Number of resolvers are #accounts
                        /// Unless the registration is at the ServerKey level nested loop iteration is not avoidable
                        foreach (GlobalAddressResolver resolver in this.addressResolvers.Keys)
                        {
                            await resolver.UpdateAsync(serverKey, CancellationToken.None);
                        }
                    });
            }
        }
    }
}
