// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    internal sealed class CatchAllQueryPipelineStage : QueryPipelineStageBase
    {
        public CatchAllQueryPipelineStage(IQueryPipelineStage inputStage)
            : base(inputStage)
        {
        }

        protected override async Task<TryCatch<QueryPage>> GetNextPageAsync(CancellationToken cancellationToken)
        {
            try
            {
                await this.inputStage.MoveNextAsync();
                return this.inputStage.Current;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Per cancellationToken.ThrowIfCancellationRequested(); line above, this function should still throw OperationCanceledException.
                throw;
            }
            catch (Exception ex)
            {
                CosmosException cosmosException = ExceptionToCosmosException.CreateFromException(ex);
                return TryCatch<QueryPage>.FromException(cosmosException);
            }
        }
    }
}
