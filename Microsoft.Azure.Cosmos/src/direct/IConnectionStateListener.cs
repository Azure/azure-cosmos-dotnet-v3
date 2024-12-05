//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Rntbd;

    internal interface IConnectionStateListener
    {
        /// <summary>
        /// Consumer: upstream caches registers
        /// </summary>
        /// <param name="serverKey"></param>
        /// <param name="serverKeyEventHandler"></param>
        void Register(ServerKey serverKey, Func<ServerKey, Task> serverKeyEventHandler);

        /// <summary>
        /// Consumer: upstream caches un-registers
        /// </summary>
        /// <param name="serverKey"></param>
        /// <param name="serverKeyEventHandler"></param>
        void UnRegister(ServerKey serverKey, Func<ServerKey, Task> serverKeyEventHandler);

        /// <summary>
        /// Producer: Downstram transport initiates
        /// </summary>
        /// <param name="connectionEvent"></param>
        /// <param name="eventTime"></param>
        /// <param name="serverKey"></param>
        void OnConnectionEvent(ConnectionEvent connectionEvent, DateTime eventTime, ServerKey serverKey);
    }
}