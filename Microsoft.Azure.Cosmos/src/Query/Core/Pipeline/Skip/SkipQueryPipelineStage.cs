// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Skip
{
    using System;
    using System.Collections.Generic;
    using System.Text;
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
            if (skipCount > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(skipCount));
            }

            this.skipCount = (int)skipCount;
        }

        public static Task<TryCatch<IQueryPipelineStage>> TryCreateAsync(
            ExecutionEnvironment executionEnvironment,
            int offsetCount,
            CosmosElement continuationToken,
            Func<CosmosElement, Task<TryCatch<IQueryPipelineStage>>> tryCreateSourceAsync)
        {
            Task<TryCatch<IQueryPipelineStage>> tryCreate = executionEnvironment switch
            {
                ExecutionEnvironment.Client => ClientSkipQueryPipelineStage.TryCreateAsync(
                    offsetCount,
                    continuationToken,
                    tryCreateSourceAsync),
                ExecutionEnvironment.Compute => ComputeSkipQueryPipelineStage.TryCreateAsync(
                    offsetCount,
                    continuationToken,
                    tryCreateSourceAsync),
                _ => throw new ArgumentException($"Unknown {nameof(ExecutionEnvironment)}: {executionEnvironment}"),
            };

            return tryCreate;
        }
    }
}
