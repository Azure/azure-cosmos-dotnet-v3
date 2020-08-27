//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IStoreClient
    {
        Task<DocumentServiceResponse> ProcessMessageAsync(
            DocumentServiceRequest request,
            IRetryPolicy retryPolicy = null,
            Func<DocumentServiceRequest, Task> prepareRequestAsyncDelegate = null,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
