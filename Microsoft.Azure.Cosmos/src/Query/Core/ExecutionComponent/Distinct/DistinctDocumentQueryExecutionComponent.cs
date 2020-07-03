//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Distinct
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;

    internal abstract partial class DistinctDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        /// <summary>
        /// An DistinctMap that efficiently stores the documents that we have already seen.
        /// </summary>
        private readonly DistinctMap distinctMap;

        protected DistinctDocumentQueryExecutionComponent(
            DistinctMap distinctMap,
            IDocumentQueryExecutionComponent source)
            : base(source)
        {
            this.distinctMap = distinctMap ?? throw new ArgumentNullException(nameof(distinctMap));
        }

        public static Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateAsync(
            ExecutionEnvironment executionEnvironment,
            CosmosElement requestContinuation,
            Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync,
            DistinctQueryType distinctQueryType)
        {
            return default;
        }
    }
}
