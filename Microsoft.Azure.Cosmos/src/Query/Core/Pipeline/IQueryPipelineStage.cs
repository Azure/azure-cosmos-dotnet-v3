// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tracing;

    internal interface IQueryPipelineStage : IAsyncEnumerator<TryCatch<QueryPage>>
    {
        void SetCancellationToken(CancellationToken cancellationToken);

        ValueTask<bool> MoveNextAsync(ITrace trace);
    }
}
