// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class CatchAllQueryPipelineStage : QueryPipelineStageBase
    {
        public CatchAllQueryPipelineStage(IQueryPipelineStage inputStage, CancellationToken cancellationToken)
            : base(inputStage, cancellationToken)
        {
        }

        public override async ValueTask<bool> MoveNextAsync()
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!await this.inputStage.MoveNextAsync())
                {
                    this.Current = default;
                    return false;
                }

                this.Current = this.inputStage.Current;
                return true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && this.cancellationToken.IsCancellationRequested))
            {
                CosmosException cosmosException = ExceptionToCosmosException.CreateFromException(ex);
                this.Current = TryCatch<QueryPage>.FromException(cosmosException);
                return true;
            }
        }
    }
}
