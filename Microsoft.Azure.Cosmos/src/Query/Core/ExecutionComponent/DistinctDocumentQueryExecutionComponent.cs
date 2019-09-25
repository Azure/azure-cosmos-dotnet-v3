//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.ExecutionComponent
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using QueryResult = Documents.QueryResult;

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
    internal sealed class DistinctDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        /// <summary>
        /// An DistinctMap that efficiently stores the documents that we have already seen.
        /// </summary>
        private readonly DistinctMap distinctMap;

        /// <summary>
        /// Initializes a new instance of the DistinctDocumentQueryExecutionComponent class.
        /// </summary>
        /// <param name="distinctQueryType">The type of distinct query.</param>
        /// <param name="distinctMapContinuationToken">The distinct map continuation token.</param>
        /// <param name="source">The source to drain from.</param>
        private DistinctDocumentQueryExecutionComponent(
            DistinctQueryType distinctQueryType,
            string distinctMapContinuationToken,
            IDocumentQueryExecutionComponent source)
            : base(source)
        {
            if (!((distinctQueryType == DistinctQueryType.Ordered) || (distinctQueryType == DistinctQueryType.Unordered)))
            {
                throw new ArgumentException("It doesn't make sense to create a distinct component of type None.");
            }

            this.distinctMap = DistinctMap.Create(
                distinctQueryType,
                distinctMapContinuationToken);
        }

        /// <summary>
        /// Creates an DistinctDocumentQueryExecutionComponent
        /// </summary>
        /// <param name="queryClient">The query client</param>
        /// <param name="requestContinuation">The continuation token.</param>
        /// <param name="createSourceCallback">The callback to create the source to drain from.</param>
        /// <param name="distinctQueryType">The type of distinct query.</param>
        /// <returns>A task to await on and in return </returns>
        public static async Task<DistinctDocumentQueryExecutionComponent> CreateAsync(
            CosmosQueryClient queryClient,
            string requestContinuation,
            Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback,
            DistinctQueryType distinctQueryType)
        {
            DistinctContinuationToken distinctContinuationToken;
            if (requestContinuation != null)
            {
                if (!DistinctContinuationToken.TryParse(requestContinuation, out distinctContinuationToken))
                {
                    throw queryClient.CreateBadRequestException($"Invalid {nameof(DistinctContinuationToken)}: {requestContinuation}");
                }
            }
            else
            {
                distinctContinuationToken = new DistinctContinuationToken(sourceToken: null, distinctMapToken: null);
            }

            return new DistinctDocumentQueryExecutionComponent(
                distinctQueryType,
                distinctContinuationToken.DistinctMapToken,
                await createSourceCallback(distinctContinuationToken.SourceToken));
        }

        /// <summary>
        /// Drains a page of results returning only distinct elements.
        /// </summary>
        /// <param name="maxElements">The maximum number of items to drain.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A page of distinct results.</returns>
        public override async Task<QueryResponseCore> DrainAsync(int maxElements, CancellationToken cancellationToken)
        {
            List<CosmosElement> distinctResults = new List<CosmosElement>();
            QueryResponseCore sourceResponse = await base.DrainAsync(maxElements, cancellationToken);

            if (!sourceResponse.IsSuccess)
            {
                return sourceResponse;
            }

            foreach (CosmosElement document in sourceResponse.CosmosElements)
            {
                if (this.distinctMap.Add(document, out UInt192? hash))
                {
                    distinctResults.Add(document);
                }
            }

            string updatedContinuationToken;
            if (!this.IsDone)
            {
                updatedContinuationToken = new DistinctContinuationToken(
                    sourceResponse.ContinuationToken,
                    this.distinctMap.GetContinuationToken()).ToString();
            }
            else
            {
                this.Source.Stop();
                updatedContinuationToken = null;
            }

            return QueryResponseCore.CreateSuccess(
                result: distinctResults,
                continuationToken: updatedContinuationToken,
                disallowContinuationTokenMessage: null,
                activityId: sourceResponse.ActivityId,
                requestCharge: sourceResponse.RequestCharge,
                queryMetricsText: sourceResponse.QueryMetricsText,
                queryMetrics: sourceResponse.QueryMetrics,
                requestStatistics: sourceResponse.RequestStatistics,
                responseLengthBytes: sourceResponse.ResponseLengthBytes);
        }

        /// <summary>
        /// Efficiently casts a object to a JToken.
        /// </summary>
        /// <param name="document">The document to cast.</param>
        /// <returns>The JToken from the object.</returns>
        private static JToken GetJTokenFromObject(object document)
        {
            QueryResult queryResult = document as QueryResult;
            if (queryResult != null)
            {
                // We wrap objects in QueryResults inorder to support other requests
                // But we didn't create a nice way to turn it back into a flat object
                return queryResult.Payload;
            }

            JToken jToken = document as JToken;
            if (jToken != null)
            {
                return jToken;
            }

            // JToken.FromObject does not honor DateParseHandling.None
            // The author does not plan on changing this:
            // https://github.com/JamesNK/Newtonsoft.Json/issues/862
            // Until we get our custom serializer to work we are going to have to live 
            // with datetime.ToString(some format) all hashing to the same value :(
            return JToken.FromObject(document);
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
