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
            DocumentServiceRequest request, Uri physicalAddress,
            ResourceOperation resourceOperation, Guid activityId);

        bool Healthy { get; }

        void Close();
    }
}