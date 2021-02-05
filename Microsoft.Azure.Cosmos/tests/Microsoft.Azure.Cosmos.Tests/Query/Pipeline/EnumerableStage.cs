//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;

    internal sealed class EnumerableStage : IAsyncEnumerable<TryCatch<QueryPage>>
    {
        private readonly IQueryPipelineStage stage;

        public EnumerableStage(IQueryPipelineStage stage)
        {
            this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
        }

        public IAsyncEnumerator<TryCatch<QueryPage>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return this.stage;
        }
    }
}
