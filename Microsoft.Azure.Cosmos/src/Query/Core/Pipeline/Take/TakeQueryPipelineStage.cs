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

        private bool ReturnedFinalPage => this.takeCount <= 0;

        protected TakeQueryPipelineStage(
            IQueryPipelineStage source,
            uint takeCount)
            : base(source)
        {
            if (takeCount > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(takeCount));
            }

            this.takeCount = (int)takeCount;
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreateLimitStage(
            uint limitCount,
            CosmosElement requestContinuationToken,
            MonadicCreatePipelineStage monadicCreatePipelineStage)
        {
            return ClientTakeQueryPipelineStage.MonadicCreateLimitStage(
                limitCount,
                requestContinuationToken,
                monadicCreatePipelineStage);
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreateTopStage(
            uint limitCount,
            CosmosElement requestContinuationToken,
            MonadicCreatePipelineStage monadicCreatePipelineStage)
        {
            return ClientTakeQueryPipelineStage.MonadicCreateTopStage(
                limitCount,
                requestContinuationToken,
                monadicCreatePipelineStage);
        }
    }
}
