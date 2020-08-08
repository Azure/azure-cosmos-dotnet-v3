//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.SkipTake
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;

    internal abstract partial class SkipDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private int skipCount;

        protected SkipDocumentQueryExecutionComponent(IDocumentQueryExecutionComponent source, long skipCount)
            : base(source)
        {
            if (skipCount > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(skipCount));
            }

            this.skipCount = (int)skipCount;
        }

        public static Task<TryCatch<IDocumentQueryExecutionComponent>> MonadicCreateAsync(
            ExecutionEnvironment executionEnvironment,
            int offsetCount,
            CosmosElement continuationToken,
            Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> monadicCreatePipelineStage)
        {
            return default;
        }
    }
}