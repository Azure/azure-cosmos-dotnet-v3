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
        void Register(ServerKey serverKey, Func<ServerKey, Task> serverKeyEventHandler);

        /// <summary>
        /// Consumer: upstream caches un-registers
        /// </summary>
        void UnRegister(ServerKey serverKey, Func<ServerKey, Task> serverKeyEventHandler);

        /// <summary>
        /// Producer: Downstream transport initiates
        /// This task will be executed inline, caller needs to start it async
        /// 
        /// ex: <seealso cref="Dispatcher"> does call it on an sync Task</seealso>
        /// </summary>
        Task OnConnectionEventAsync(ConnectionEvent connectionEvent, DateTime eventTime, ServerKey serverKey);

        /// <summary>
        /// Concurrent connection events to process concurrently. 
        /// Others will wait (Task will be queued to TaskSchedular but will block on semaphore)
        /// </summary>
        void SetConnectionEventConcurrency(int notificationConcurrency);
    }
}