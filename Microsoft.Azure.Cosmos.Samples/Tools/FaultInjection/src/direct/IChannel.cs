//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.FaultInjection;

    internal interface IChannel
    {
        Task<StoreResponse> RequestAsync(
            DocumentServiceRequest request, 
            TransportAddressUri physicalAddress,
            ResourceOperation resourceOperation, 
            Guid activityId,
            TransportRequestStats transportRequestStats);

        /// <summary>
        /// Opens the Rntbd context negotiation channel to
        /// the backend replica node.
        /// </summary>
        /// <param name="activityId">An unique identifier indicating the current activity id.</param>
        /// <param name="serverErrorInjector">a server error injector for fault injection, can be null if not suing fault injection.</param>
        /// <returns>A completed task indicating oncw the channel is opened.</returns>
        public Task OpenChannelAsync(
            Guid activityId,
            RntbdServerErrorInjector serverErrorInjector);

        bool Healthy { get; }

        void Close();
    }
}