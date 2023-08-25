// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class CatchAllQueryPipelineStage : QueryPipelineStageBase
    {
        public CatchAllQueryPipelineStage(IQueryPipelineStage inputStage, CancellationToken cancellationToken)
            : base(inputStage, cancellationToken)
        {
        }

        public override async ValueTask<bool> MoveNextAsync(ITrace trace)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            try
            {
                if (!await this.inputStage.MoveNextAsync(trace))
                {
                    this.Current = default;
                    return false;
                }

                this.Current = this.inputStage.Current;
                return true;
            }
            catch (Exception ex)
            {
                if (!ExceptionToCosmosException.TryCreateFromException(ex, trace, out CosmosException cosmosException))
                {
                    throw;
                }

                this.Current = TryCatch<QueryPage>.FromException(cosmosException);
                return true;
            }
        }
    }
}
