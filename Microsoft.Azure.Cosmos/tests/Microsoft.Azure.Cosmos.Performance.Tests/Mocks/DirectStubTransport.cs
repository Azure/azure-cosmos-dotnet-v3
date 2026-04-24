//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Mocks
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Minimal <see cref="TransportClient"/> for the Direct-mode end-to-end benchmark harness
    /// that returns a canned <see cref="StoreResponse"/> for every RNTBD call by delegating to
    /// <see cref="MockRequestHelper.GetStoreResponse"/> (the same canned-response source used
    /// by <c>MockedItemStreamBenchmark</c>). No socket / TCP / RNTBD frames involved.
    /// </summary>
    internal sealed class DirectStubTransport : TransportClient
    {
        public int InvokeCount;
        public string LastResourceAddress;
        public OperationType LastOperationType;
        public int? LastReturnedStatus;

        public void ResetCounters()
        {
            this.InvokeCount = 0;
            this.LastResourceAddress = null;
            this.LastReturnedStatus = null;
        }

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
                    $"DirectStubTransport: no canned response for {request.OperationType} {request.ResourceType} ({request.ResourceAddress})");
            }

            return Task.FromResult(response);
        }
    }
}
