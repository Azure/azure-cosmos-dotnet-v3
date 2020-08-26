//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.SkipTake
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal abstract partial class TakeDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private int takeCount;

        protected TakeDocumentQueryExecutionComponent(
            IDocumentQueryExecutionComponent source,
            int takeCount)
            : base(source)
        {
            this.takeCount = takeCount;
        }

        public static Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateLimitDocumentQueryExecutionComponentAsync(
            ExecutionEnvironment executionEnvironment,
            int limitCount,
            CosmosElement requestContinuationToken,
            Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync)
        {
            Task<TryCatch<IDocumentQueryExecutionComponent>> tryCreateComponentAsync = executionEnvironment switch
            {
                ExecutionEnvironment.Client => ClientTakeDocumentQueryExecutionComponent.TryCreateLimitDocumentQueryExecutionComponentAsync(
                                       limitCount,
                                       requestContinuationToken,
                                       tryCreateSourceAsync),
                ExecutionEnvironment.Compute => ComputeTakeDocumentQueryExecutionComponent.TryCreateAsync(
limitCount,
requestContinuationToken,
tryCreateSourceAsync),
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(ExecutionEnvironment)}: {executionEnvironment}."),
            };
            return tryCreateComponentAsync;
        }

        public static Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateTopDocumentQueryExecutionComponentAsync(
            ExecutionEnvironment executionEnvironment,
            int topCount,
            CosmosElement requestContinuationToken,
            Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync)
        {
            Task<TryCatch<IDocumentQueryExecutionComponent>> tryCreateComponentAsync = executionEnvironment switch
            {
                ExecutionEnvironment.Client => ClientTakeDocumentQueryExecutionComponent.TryCreateTopDocumentQueryExecutionComponentAsync(
                                       topCount,
                                       requestContinuationToken,
                                       tryCreateSourceAsync),
                ExecutionEnvironment.Compute => ComputeTakeDocumentQueryExecutionComponent.TryCreateAsync(
topCount,
requestContinuationToken,
tryCreateSourceAsync),
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(ExecutionEnvironment)}: {executionEnvironment}."),
            };
            return tryCreateComponentAsync;
        }

        public override bool IsDone => this.Source.IsDone || this.takeCount <= 0;
    }
}