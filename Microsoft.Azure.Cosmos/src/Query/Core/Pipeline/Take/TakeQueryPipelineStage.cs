// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Take
{
    using System;
    using System.Threading;
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

        public static TryCatch<IQueryPipelineStage> MonadicCreateLimitStage(
            ExecutionEnvironment executionEnvironment,
            int limitCount,
            CosmosElement requestContinuationToken,
            MonadicCreatePipelineStage monadicCreatePipelineStage) => executionEnvironment switch
            {
                ExecutionEnvironment.Client => ClientTakeQueryPipelineStage.MonadicCreateLimitStage(
                    limitCount,
                    requestContinuationToken,
                    monadicCreatePipelineStage),
                ExecutionEnvironment.Compute => ComputeTakeQueryPipelineStage.MonadicCreateLimitStage(
                    limitCount,
                    requestContinuationToken,
                    monadicCreatePipelineStage),
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(ExecutionEnvironment)}: {executionEnvironment}."),
            };

        public static TryCatch<IQueryPipelineStage> MonadicCreateTopStage(
            ExecutionEnvironment executionEnvironment,
            int limitCount,
            CosmosElement requestContinuationToken,
            MonadicCreatePipelineStage monadicCreatePipelineStage) => executionEnvironment switch
            {
                ExecutionEnvironment.Client => ClientTakeQueryPipelineStage.MonadicCreateTopStage(
                    limitCount,
                    requestContinuationToken,
                    monadicCreatePipelineStage),
                ExecutionEnvironment.Compute => ComputeTakeQueryPipelineStage.MonadicCreateTopStage(
                    limitCount,
                    requestContinuationToken,
                    monadicCreatePipelineStage),
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(ExecutionEnvironment)}: {executionEnvironment}."),
            };
    }
}
