//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IDocumentClientInternal : IDocumentClient
    {
        Task<AccountProperties> GetDatabaseAccountInternalAsync(Uri serviceEndpoint, CancellationToken cancellationToken = default);
    }
}
