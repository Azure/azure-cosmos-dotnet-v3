//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class EnumerableStage : IAsyncEnumerable<TryCatch<QueryPage>>
    {
        private readonly IQueryPipelineStage stage;

        private readonly ITrace trace;

        public EnumerableStage(IQueryPipelineStage stage, ITrace trace)
        {
            this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
            this.trace = trace ?? throw new ArgumentNullException(nameof(trace));
        }

        public IAsyncEnumerator<TryCatch<QueryPage>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new TracingAsyncEnumerator<TryCatch<QueryPage>>(this.stage, this.trace, cancellationToken);
        }
    }
}