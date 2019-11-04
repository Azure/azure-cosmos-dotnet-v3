//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
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
            if (distinctMap == null)
            {
                throw new ArgumentNullException(nameof(distinctMap));
            }

            this.distinctMap = distinctMap;
        }

        /// <summary>
        /// Creates an DistinctDocumentQueryExecutionComponent
        /// </summary>
        /// <param name="executionEnvironment">The environment to execute on.</param>
        /// <param name="queryClient">The query client</param>
        /// <param name="requestContinuation">The continuation token.</param>
        /// <param name="createSourceCallback">The callback to create the source to drain from.</param>
        /// <param name="distinctQueryType">The type of distinct query.</param>
        /// <returns>A task to await on and in return </returns>
        public static async Task<IDocumentQueryExecutionComponent> CreateAsync(
            ExecutionEnvironment executionEnvironment,
            CosmosQueryClient queryClient,
            string requestContinuation,
            Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback,
            DistinctQueryType distinctQueryType)
        {
            IDocumentQueryExecutionComponent distinctDocumentQueryExecutionComponent;
            switch (executionEnvironment)
            {
                case ExecutionEnvironment.Client:
                    distinctDocumentQueryExecutionComponent = await ClientDistinctDocumentQueryExecutionComponent.CreateAsync(
                        queryClient,
                        requestContinuation,
                        createSourceCallback,
                        distinctQueryType);
                    break;

                case ExecutionEnvironment.Compute:
                    distinctDocumentQueryExecutionComponent = await ComputeDistinctDocumentQueryExecutionComponent.CreateAsync(
                        queryClient,
                        requestContinuation,
                        createSourceCallback,
                        distinctQueryType);
                    break;

                default:
                    throw new ArgumentException($"Unknown {nameof(ExecutionEnvironment)}: {executionEnvironment}.");
            }

            return distinctDocumentQueryExecutionComponent;
        }

        /// <summary>
        /// Continuation token for distinct queries.
        /// </summary>
        private readonly struct DistinctContinuationToken
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
                distinctContinuationToken = default(DistinctContinuationToken);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                try
                {
                    distinctContinuationToken = JsonConvert.DeserializeObject<DistinctContinuationToken>(value);
                    return true;
                }
                catch (JsonException ex)
                {
                    DefaultTrace.TraceWarning($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)} Invalid continuation token {value} for Distinct~Component, exception: {ex.Message}");
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
