//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Resource.Settings;

    internal interface IDocumentClientInternal : IDocumentClient
    {
        Task<AccountProperties> GetDatabaseAccountInternalAsync(Uri serviceEndpoint, CancellationToken cancellationToken = default);
        Task RefreshDatabaseAccountClientConfigInternalAsync();
    }
}
