// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System.Threading;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal delegate TryCatch<IQueryPipelineStage> MonadicCreatePipelineStage(CosmosElement continuationToken, CancellationToken cancellationToken);
}
