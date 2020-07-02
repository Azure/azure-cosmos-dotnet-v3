// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Take
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal abstract partial class TakeQueryPipelineStage : QueryPipelineStageBase
    {
        private int takeCount;

        protected TakeQueryPipelineStage(
            IQueryPipelineStage source,
            int takeCount)
            : base(source)
        {
            this.takeCount = takeCount;
        }

        public static Task<TryCatch<IQueryPipelineStage>> TryCreateLimitStageAsync(
            ExecutionEnvironment executionEnvironment,
            int limitCount,
            CosmosElement requestContinuationToken,
            Func<CosmosElement, Task<TryCatch<IQueryPipelineStage>>> tryCreateSourceAsync) => executionEnvironment switch
            {
                ExecutionEnvironment.Client => ClientTakeQueryPipelineStage.TryCreateLimitStageAsync(
                    limitCount,
                    requestContinuationToken,
                    tryCreateSourceAsync),
                ExecutionEnvironment.Compute => ComputeTakeQueryPipelineStage.TryCreateLimitStageAsync(
                    limitCount,
                    requestContinuationToken,
                    tryCreateSourceAsync),
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(ExecutionEnvironment)}: {executionEnvironment}."),
            };

        public static Task<TryCatch<IQueryPipelineStage>> TryCreateTopStageAsync(
            ExecutionEnvironment executionEnvironment,
            int limitCount,
            CosmosElement requestContinuationToken,
            Func<CosmosElement, Task<TryCatch<IQueryPipelineStage>>> tryCreateSourceAsync) => executionEnvironment switch
            {
                ExecutionEnvironment.Client => ClientTakeQueryPipelineStage.TryCreateTopStageAsync(
                    limitCount,
                    requestContinuationToken,
                    tryCreateSourceAsync),
                ExecutionEnvironment.Compute => ComputeTakeQueryPipelineStage.TryCreateTopStageAsync(
                    limitCount,
                    requestContinuationToken,
                    tryCreateSourceAsync),
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(ExecutionEnvironment)}: {executionEnvironment}."),
            };
    }
}
