//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Distinct
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Newtonsoft.Json;

    /// <summary>
    /// Distinct queries return documents that are distinct with a page.
    /// This means that documents are not guaranteed to be distinct across continuations and partitions.
    /// The reasoning for this is because the backend treats each continuation of a query as a separate request
    /// and partitions are not aware of each other.
    /// The solution is that the client keeps a running hash set of all the documents it has already seen,
    /// so that when it encounters a duplicate document from another continuation it will not be emitted to the user.
    /// The only problem is that if the user chooses to go through the continuation token API for DocumentQuery instead
    /// of while(HasMoreResults) ExecuteNextAsync, then will see duplicates across continuations.
    /// There is no workaround for that use case, since the continuation token will have to include all the documents seen.
    /// </summary>
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

        public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateAsync(
            ExecutionEnvironment executionEnvironment,
            CosmosElement requestContinuation,
            Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync,
            DistinctQueryType distinctQueryType)
        {
            if (tryCreateSourceAsync == null)
            {
                throw new ArgumentNullException(nameof(tryCreateSourceAsync));
            }

            TryCatch<IDocumentQueryExecutionComponent> tryCreateDistinctDocumentQueryExecutionComponent;
            switch (executionEnvironment)
            {
                case ExecutionEnvironment.Client:
                    tryCreateDistinctDocumentQueryExecutionComponent = await ClientDistinctDocumentQueryExecutionComponent.TryCreateAsync(
                        requestContinuation,
                        tryCreateSourceAsync,
                        distinctQueryType);
                    break;

                case ExecutionEnvironment.Compute:
                    tryCreateDistinctDocumentQueryExecutionComponent = await ComputeDistinctDocumentQueryExecutionComponent.TryCreateAsync(
                        requestContinuation,
                        tryCreateSourceAsync,
                        distinctQueryType);
                    break;

                default:
                    throw new ArgumentException($"Unknown {nameof(ExecutionEnvironment)}: {executionEnvironment}.");
            }

            return tryCreateDistinctDocumentQueryExecutionComponent;
        }
    }
}
