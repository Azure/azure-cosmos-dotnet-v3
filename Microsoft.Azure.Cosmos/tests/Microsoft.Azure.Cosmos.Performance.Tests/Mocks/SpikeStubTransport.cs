//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Mocks
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Stage 2 spike: minimal <see cref="TransportClient"/> that returns a canned success
    /// <see cref="StoreResponse"/> for every RNTBD call. Delegates to the existing
    /// <see cref="MockRequestHelper.GetStoreResponse"/>, which is the same canned-response
    /// source that <c>MockedItemStreamBenchmark</c> already relies on.
    /// </summary>
    internal sealed class SpikeStubTransport : TransportClient
    {
        public int InvokeCount;
        public string LastResourceAddress;
        public OperationType LastOperationType;
        public int? LastReturnedStatus;

        internal override Task<StoreResponse> InvokeStoreAsync(
            Uri physicalAddress,
            ResourceOperation resourceOperation,
            DocumentServiceRequest request)
        {
            System.Threading.Interlocked.Increment(ref this.InvokeCount);
            this.LastResourceAddress = request.ResourceAddress;
            this.LastOperationType = request.OperationType;
            StoreResponse response = MockRequestHelper.GetStoreResponse(request);
            this.LastReturnedStatus = response?.Status;
            if (response == null)
            {
                throw new InvalidOperationException(
                    $"SpikeStubTransport: no canned response for {request.OperationType} {request.ResourceType} ({request.ResourceAddress})");
            }

            return Task.FromResult(response);
        }
    }
}
