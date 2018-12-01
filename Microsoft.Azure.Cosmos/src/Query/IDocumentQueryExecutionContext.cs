//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IDocumentQueryExecutionContext : IDisposable
    {
        bool IsDone { get; }

        Task<FeedResponse<dynamic>> ExecuteNextAsync(CancellationToken token);
    }
}
