// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal interface IQueryPipelineStage : IAsyncEnumerator<TryCatch<QueryPage>>
    {
        void SetCancellationToken(CancellationToken cancellationToken);
    }
}
