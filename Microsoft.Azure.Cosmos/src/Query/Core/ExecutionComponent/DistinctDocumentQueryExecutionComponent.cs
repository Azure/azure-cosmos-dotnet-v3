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
        /// The type of distinct query this component is serving.
        /// </summary>
        private readonly DistinctQueryType distinctQueryType;

        private readonly CosmosQueryClient queryClient;

        /// <summary>
        /// The hash of the last value added to the distinct map.
        /// </summary>
        private UInt192? lastHash;

        /// <summary>
        /// Initializes a new instance of the DistinctDocumentQueryExecutionComponent class.
        /// </summary>
        /// <param name="queryClient">The query client</param>
        /// <param name="distinctQueryType">The type of distinct query.</param>
        /// <param name="previousHash">The previous that distinct map saw.</param>
        /// <param name="source">The source to drain from.</param>
        private DistinctDocumentQueryExecutionComponent(
            CosmosQueryClient queryClient,
            DistinctQueryType distinctQueryType,
            UInt192? previousHash,
            IDocumentQueryExecutionComponent source)
            : base(source)
        {
            if (distinctQueryType == DistinctQueryType.None)
            {
                throw new ArgumentException("It doesn't make sense to create a distinct component of type None.");
            }

            this.queryClient = queryClient;
            this.distinctQueryType = distinctQueryType;
            this.distinctMap = DistinctMap.Create(distinctQueryType, previousHash);
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
            DistinctContinuationToken distinctContinuationToken = new DistinctContinuationToken(null, null);
            if (requestContinuation != null)
            {
                distinctContinuationToken = DistinctContinuationToken.Parse(queryClient, requestContinuation);
                if (distinctQueryType != DistinctQueryType.Ordered && distinctContinuationToken.LastHash != null)
                {
                    throw queryClient.CreateBadRequestException($"DistinctContinuationToken is malformed: {distinctContinuationToken}. DistinctContinuationToken can not have a 'lastHash', when the query type is not ordered (ex SELECT DISTINCT VALUE c.blah FROM c ORDER BY c.blah).");
                }
            }

            return new DistinctDocumentQueryExecutionComponent(
                queryClient,
                distinctQueryType,
                distinctContinuationToken.LastHash,
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
            QueryResponseCore cosmosQueryResponse = await base.DrainAsync(maxElements, cancellationToken);
            if (!cosmosQueryResponse.IsSuccess)
            {
                return cosmosQueryResponse;
            }

            foreach (CosmosElement document in cosmosQueryResponse.CosmosElements)
            {
                if (this.distinctMap.Add(document, out this.lastHash))
                {
                    distinctResults.Add(document);
                }
            }

            string updatedContinuationToken;
            if (!this.IsDone)
            {
                updatedContinuationToken = new DistinctContinuationToken(
                    this.lastHash,
                    cosmosQueryResponse.ContinuationToken).ToString();
            }
            else
            {
                this.Source.Stop();
                updatedContinuationToken = null;
            }

            string disallowContinuationTokenMessage = this.distinctQueryType == DistinctQueryType.Ordered ? null : Documents.RMResources.UnorderedDistinctQueryContinuationToken;
            return QueryResponseCore.CreateSuccess(
                result: distinctResults,
                continuationToken: updatedContinuationToken,
                disallowContinuationTokenMessage: disallowContinuationTokenMessage,
                activityId: cosmosQueryResponse.ActivityId,
                requestCharge: cosmosQueryResponse.RequestCharge,
                queryMetricsText: cosmosQueryResponse.QueryMetricsText,
                queryMetrics: cosmosQueryResponse.QueryMetrics,
                requestStatistics: cosmosQueryResponse.RequestStatistics,
                responseLengthBytes: cosmosQueryResponse.ResponseLengthBytes);
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
        private struct DistinctContinuationToken
        {
            /// <summary>
            /// Initializes a new instance of the DistinctContinuationToken struct.
            /// </summary>
            /// <param name="lastHash">The last hash.</param>
            /// <param name="sourceToken">The continuation token for the source context.</param>
            public DistinctContinuationToken(UInt192? lastHash, string sourceToken)
            {
                this.LastHash = lastHash;
                this.SourceToken = sourceToken;
            }

            /// <summary>
            /// Gets the previous hash.
            /// </summary>
            /// <remarks>The type is nullable, since only ordered distinct queries will have a previous hash.</remarks>
            [JsonProperty("lastHash")]
            [JsonConverter(typeof(NullableUInt192Serializer))]
            public UInt192? LastHash
            {
                get;
            }

            /// <summary>
            /// Gets he continuation token for the source context.
            /// </summary>
            [JsonProperty("sourceToken")]
            public string SourceToken
            {
                get;
            }

            /// <summary>
            /// Parses out the DistinctContinuationToken from a string.
            /// </summary>
            /// <param name="queryClient">The query client</param>
            /// <param name="value">The value to parse.</param>
            /// <returns>The parsed DistinctContinuationToken.</returns>
            public static DistinctContinuationToken Parse(CosmosQueryClient queryClient, string value)
            {
                DistinctContinuationToken result;
                if (!TryParse(value, out result))
                {
                    throw queryClient.CreateBadRequestException($"Invalid DistinctContinuationToken: {value}");
                }
                else
                {
                    return result;
                }
            }

            /// <summary>
            /// Tries to parse a DistinctContinuationToken from a string.
            /// </summary>
            /// <param name="value">The value to parse.</param>
            /// <param name="distinctContinuationToken">The output DistinctContinuationToken.</param>
            /// <returns>True if we successfully parsed the DistinctContinuationToken, else false.</returns>
            public static bool TryParse(string value, out DistinctContinuationToken distinctContinuationToken)
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

            /// <summary>
            /// Customer converter for nullable UInt192 so that we can serialize and deserialize the previous hash for distinct queries.
            /// </summary>
            private class NullableUInt192Serializer : JsonConverter
            {
                /// <summary>
                /// Gets whether or not you can convert the object type.
                /// </summary>
                /// <param name="objectType">The object type.</param>
                /// <returns>Whether or not you can convert the object type.</returns>
                public override bool CanConvert(Type objectType)
                {
                    return objectType == typeof(UInt192) || objectType == typeof(object);
                }

                /// <summary>
                /// Reads the nullable UInt192
                /// </summary>
                /// <param name="reader">The reader to read from.</param>
                /// <param name="objectType">The object type.</param>
                /// <param name="existingValue">The existing value.</param>
                /// <param name="serializer">The serializer.</param>
                /// <returns>The nullable UInt192.</returns>
                public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
                {
                    if (reader.Value == null)
                    {
                        return null;
                    }

                    return UInt192.Parse(reader.Value.ToString());
                }

                /// <summary>
                /// Writes the nullable UInt192
                /// </summary>
                /// <param name="writer">The writer.</param>
                /// <param name="value">The value.</param>
                /// <param name="serializer">The serializer.</param>
                public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
                {
                    if (value == null)
                    {
                        writer.WriteNull();
                    }
                    else
                    {
                        UInt192 uint192 = (UInt192)value;
                        JToken token = JToken.FromObject(uint192.ToString());
                        token.WriteTo(writer);
                    }
                }
            }
        }
    }
}
