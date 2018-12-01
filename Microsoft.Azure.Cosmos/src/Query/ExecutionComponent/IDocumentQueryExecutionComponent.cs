//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.ExecutionComponent
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    
    internal interface IDocumentQueryExecutionComponent : IDisposable
    {
        bool IsDone { get; }

        Task<FeedResponse<object>> DrainAsync(int maxElements, CancellationToken token);

        void Stop();

        IReadOnlyDictionary<string, QueryMetrics> GetQueryMetrics();
    }
}
