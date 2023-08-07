//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Threading.Tasks;

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
        /// <returns>A completed task indicating oncw the channel is opened.</returns>
        public Task OpenChannelAsync(
            Guid activityId);

        /// <summary>
        /// Sets the health state.
        /// </summary>
        /// <param name="isHealthy">A boolean flag indicating the health state to be set.</param>
        public void SetHealthState(
            bool isHealthy);

        bool Healthy { get; }

        void Close();
    }
}