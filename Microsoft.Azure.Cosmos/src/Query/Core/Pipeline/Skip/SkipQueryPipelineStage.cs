// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Skip
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal abstract partial class SkipQueryPipelineStage : QueryPipelineStageBase
    {
        private int skipCount;

        protected SkipQueryPipelineStage(
            IQueryPipelineStage source,
            long skipCount)
            : base(source)
        {
            if (skipCount > int.MaxValue || skipCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(skipCount));
            }

            this.skipCount = (int)skipCount;
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            int offsetCount,
            CosmosElement continuationToken,
            MonadicCreatePipelineStage monadicCreatePipelineStage)
        {
            TryCatch<IQueryPipelineStage> tryCreate = ClientSkipQueryPipelineStage.MonadicCreate(
                    offsetCount,
                    continuationToken,
                    monadicCreatePipelineStage);

            return tryCreate;
        }
    }
}
