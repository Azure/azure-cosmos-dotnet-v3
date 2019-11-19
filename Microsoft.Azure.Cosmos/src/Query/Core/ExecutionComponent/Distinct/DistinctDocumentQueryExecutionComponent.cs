//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Distinct
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
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
            string requestContinuation,
            Func<string, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync,
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

        /// <summary>
        /// Continuation token for distinct queries.
        /// </summary>
        private sealed class DistinctContinuationToken
        {
            public DistinctContinuationToken(string sourceToken, string distinctMapToken)
            {
                this.SourceToken = sourceToken;
                this.DistinctMapToken = distinctMapToken;
            }

            public string SourceToken { get; }

            public string DistinctMapToken { get; }

            /// <summary>
            /// Tries to parse a DistinctContinuationToken from a string.
            /// </summary>
            /// <param name="value">The value to parse.</param>
            /// <param name="distinctContinuationToken">The output DistinctContinuationToken.</param>
            /// <returns>True if we successfully parsed the DistinctContinuationToken, else false.</returns>
            public static bool TryParse(
                string value,
                out DistinctContinuationToken distinctContinuationToken)
            {
                distinctContinuationToken = default;
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                try
                {
                    distinctContinuationToken = JsonConvert.DeserializeObject<DistinctContinuationToken>(value);
                    return true;
                }
                catch (JsonException)
                {
                    return false;
                }
            }

            /// <summary>
            /// Gets the serialized form of DistinctContinuationToken
            /// </summary>
            /// <returns>The serialized form of DistinctContinuationToken</returns>
            public override string ToString()
            {
                return JsonConvert.SerializeObject(this);
            }
        }
    }
}
