//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Common
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal interface IRequestSigner
    {
        Task SignRequestAsync(DocumentServiceRequest request, CancellationToken cancellationToken);
    }
}
