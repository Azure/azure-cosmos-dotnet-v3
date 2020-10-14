// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Take
{
    using System;
    using System.Threading;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal abstract partial class TakeQueryPipelineStage : QueryPipelineStageBase
    {
        private int takeCount;

        protected TakeQueryPipelineStage(
            IQueryPipelineStage source,
            CancellationToken cancellationToken,
            int takeCount)
            : base(source, cancellationToken)
        {
            this.takeCount = takeCount;
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreateLimitStage(
            ExecutionEnvironment executionEnvironment,
            int limitCount,
            CosmosElement requestContinuationToken,
            CancellationToken cancellationToken,
            MonadicCreatePipelineStage monadicCreatePipelineStage) => executionEnvironment switch
            {
                ExecutionEnvironment.Client => ClientTakeQueryPipelineStage.MonadicCreateLimitStage(
                    limitCount,
                    requestContinuationToken,
                    cancellationToken,
                    monadicCreatePipelineStage),
                ExecutionEnvironment.Compute => ComputeTakeQueryPipelineStage.MonadicCreateLimitStage(
                    limitCount,
                    requestContinuationToken,
                    cancellationToken,
                    monadicCreatePipelineStage),
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(ExecutionEnvironment)}: {executionEnvironment}."),
            };

        public static TryCatch<IQueryPipelineStage> MonadicCreateTopStage(
            ExecutionEnvironment executionEnvironment,
            int limitCount,
            CosmosElement requestContinuationToken,
            CancellationToken cancellationToken,
            MonadicCreatePipelineStage monadicCreatePipelineStage) => executionEnvironment switch
            {
                ExecutionEnvironment.Client => ClientTakeQueryPipelineStage.MonadicCreateTopStage(
                    limitCount,
                    requestContinuationToken,
                    cancellationToken,
                    monadicCreatePipelineStage),
                ExecutionEnvironment.Compute => ComputeTakeQueryPipelineStage.MonadicCreateTopStage(
                    limitCount,
                    requestContinuationToken,
                    cancellationToken,
                    monadicCreatePipelineStage),
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(ExecutionEnvironment)}: {executionEnvironment}."),
            };
    }
}
