//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Documents;

    internal sealed class NullRequestSigner : IRequestSigner
    {
        public Task SignRequestAsync(DocumentServiceRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }
    }
}
