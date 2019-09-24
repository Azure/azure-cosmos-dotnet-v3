//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.ExecutionComponent
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using ClientSideRequestStatistics = Documents.ClientSideRequestStatistics;

    /// <summary>
    /// Execution component that is able to aggregate local aggregates from multiple continuations and partitions.
    /// At a high level aggregates queries only return a "partial" aggregate.
    /// "partial" means that the result is only valid for that one continuation (and one partition).
    /// For example suppose you have the query "SELECT COUNT(1) FROM c" and you have a single partition collection, 
    /// then you will get one count for each continuation of the query.
    /// If you wanted the true result for this query, then you will have to take the sum of all continuations.
    /// The reason why we have multiple continuations is because for a long running query we have to break up the results into multiple continuations.
    /// Fortunately all the aggregates can be aggregated across continuations and partitions.
    /// </summary>
    internal sealed class AggregateDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private static readonly List<CosmosElement> EmptyResults = new List<CosmosElement>();

        /// <summary>
        /// This class does most of the work, since a query like:
        /// 
        /// SELECT VALUE AVG(c.age)
        /// FROM c
        /// 
        /// is really just an aggregation on a single grouping (the whole collection).
        /// </summary>
        private readonly SingleGroupAggregator singleGroupAggregator;

        /// <summary>
        /// We need to keep track of whether the projection has the 'VALUE' keyword.
        /// </summary>
        private readonly bool isValueAggregateQuery;

        private bool isDone;

        /// <summary>
        /// Initializes a new instance of the AggregateDocumentQueryExecutionComponent class.
        /// </summary>
        /// <param name="source">The source component that will supply the local aggregates from multiple continuations and partitions.</param>
        /// <param name="singleGroupAggregator">The single group aggregator that we will feed results into.</param>
        /// <param name="isValueAggregateQuery">Whether or not the query has the 'VALUE' keyword.</param>
        /// <remarks>This constructor is private since there is some async initialization that needs to happen in CreateAsync().</remarks>
        private AggregateDocumentQueryExecutionComponent(
            IDocumentQueryExecutionComponent source,
            SingleGroupAggregator singleGroupAggregator,
            bool isValueAggregateQuery)
            : base(source)
        {
            if (singleGroupAggregator == null)
            {
                throw new ArgumentNullException(nameof(singleGroupAggregator));
            }

            this.singleGroupAggregator = singleGroupAggregator;
            this.isValueAggregateQuery = isValueAggregateQuery;
        }

        public override bool IsDone => this.isDone;

        /// <summary>
        /// Creates a AggregateDocumentQueryExecutionComponent.
        /// </summary>
        /// <param name="queryClient">The query client.</param>
        /// <param name="aggregates">The aggregates.</param>
        /// <param name="aliasToAggregateType">The alias to aggregate type.</param>
        /// <param name="hasSelectValue">Whether or not the query has the 'VALUE' keyword.</param>
        /// <param name="requestContinuation">The continuation token to resume from.</param>
        /// <param name="createSourceCallback">The callback to create the source component that supplies the local aggregates.</param>
        /// <returns>The AggregateDocumentQueryExecutionComponent.</returns>
        public static async Task<AggregateDocumentQueryExecutionComponent> CreateAsync(
            CosmosQueryClient queryClient,
            AggregateOperator[] aggregates,
            IReadOnlyDictionary<string, AggregateOperator?> aliasToAggregateType,
            bool hasSelectValue,
            string requestContinuation,
            Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback)
        {
            string sourceContinuationToken;
            string singleGroupAggregatorContinuationToken;
            if (requestContinuation != null)
            {
                if (!AggregateContinuationToken.TryParse(requestContinuation, out AggregateContinuationToken aggregateContinuationToken))
                {
                    throw queryClient.CreateBadRequestException($"Malfomed {nameof(AggregateContinuationToken)}: '{requestContinuation}'");
                }

                sourceContinuationToken = aggregateContinuationToken.SourceContinuationToken;
                singleGroupAggregatorContinuationToken = aggregateContinuationToken.SingleGroupAggregatorContinuationToken;
            }
            else
            {
                sourceContinuationToken = null;
                singleGroupAggregatorContinuationToken = null;
            }

            return new AggregateDocumentQueryExecutionComponent(
                await createSourceCallback(sourceContinuationToken),
                SingleGroupAggregator.Create(
                    queryClient,
                    aggregates,
                    aliasToAggregateType,
                    hasSelectValue,
                    singleGroupAggregatorContinuationToken),
                (aggregates != null) && (aggregates.Count() == 1));
        }

        /// <summary>
        /// Drains at most 'maxElements' documents from the AggregateDocumentQueryExecutionComponent.
        /// </summary>
        /// <param name="maxElements">This value is ignored, since the aggregates are aggregated for you.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The aggregate result after all the continuations have been followed.</returns>
        /// <remarks>
        /// Note that this functions follows all continuations meaning that it won't return until all continuations are drained.
        /// This means that if you have a long running query this function will take a very long time to return.
        /// </remarks>
        public override async Task<QueryResponseCore> DrainAsync(int maxElements, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Draining aggregates is broken down into two stages
            QueryResponseCore response;
            if (!this.Source.IsDone)
            {
                // Stage 1:
                // Drain the aggregates fully from all continuations and all partitions
                QueryResponseCore sourceResponse = await base.DrainAsync(int.MaxValue, cancellationToken);
                if (!sourceResponse.IsSuccess)
                {
                    return sourceResponse;
                }

                foreach (CosmosElement element in sourceResponse.CosmosElements)
                {
                    RewrittenAggregateProjections rewrittenAggregateProjections = new RewrittenAggregateProjections(
                        this.isValueAggregateQuery,
                        element);
                    this.singleGroupAggregator.AddValues(rewrittenAggregateProjections.Payload);
                }

                if (this.Source.IsDone)
                {
                    response = this.GetFinalResponse();
                }
                else
                {
                    response = this.GetEmptyPage(sourceResponse);
                }
            }
            else
            {
                response = this.GetFinalResponse();
            }

            return response;
        }

        private QueryResponseCore GetFinalResponse()
        {
            List<CosmosElement> finalResult = new List<CosmosElement>();
            CosmosElement aggregationResult = this.singleGroupAggregator.GetResult();
            if (aggregationResult != null)
            {
                finalResult.Add(aggregationResult);
            }

            QueryResponseCore response = QueryResponseCore.CreateSuccess(
                result: finalResult,
                continuationToken: null,
                activityId: null,
                disallowContinuationTokenMessage: null,
                requestCharge: 0,
                queryMetricsText: null,
                queryMetrics: this.GetQueryMetrics(),
                requestStatistics: null,
                responseLengthBytes: 0);
            this.isDone = true;

            return response;
        }

        private QueryResponseCore GetEmptyPage(QueryResponseCore sourceResponse)
        {
            AggregateContinuationToken aggregateContinuationToken = AggregateContinuationToken.Create(
                    this.singleGroupAggregator.GetContinuationToken(),
                    sourceResponse.ContinuationToken);

            // We need to give empty pages until the results are fully drained.
            QueryResponseCore response = QueryResponseCore.CreateSuccess(
                result: EmptyResults,
                continuationToken: aggregateContinuationToken.ToString(),
                disallowContinuationTokenMessage: null,
                activityId: sourceResponse.ActivityId,
                requestCharge: sourceResponse.RequestCharge,
                queryMetricsText: sourceResponse.QueryMetricsText,
                queryMetrics: sourceResponse.QueryMetrics,
                requestStatistics: sourceResponse.RequestStatistics,
                responseLengthBytes: sourceResponse.ResponseLengthBytes);

            this.isDone = false;

            return response;
        }

        /// <summary>
        /// Struct for getting the payload out of the rewritten projection.
        /// </summary>
        private struct RewrittenAggregateProjections
        {
            public RewrittenAggregateProjections(bool isValueAggregateQuery, CosmosElement raw)
            {
                if (raw == null)
                {
                    throw new ArgumentNullException(nameof(raw));
                }

                if (isValueAggregateQuery)
                {
                    // SELECT VALUE [{"item": {"sum": SUM(c.blah), "count": COUNT(c.blah)}}]
                    if (!(raw is CosmosArray aggregates))
                    {
                        throw new ArgumentException($"{nameof(RewrittenAggregateProjections)} was not an array for a value aggregate query. Type is: {raw.Type}");
                    }

                    this.Payload = aggregates[0];
                }
                else
                {
                    if (!(raw is CosmosObject cosmosObject))
                    {
                        throw new ArgumentException($"{nameof(raw)} must not be an object.");
                    }

                    if (!cosmosObject.TryGetValue("payload", out CosmosElement cosmosPayload))
                    {
                        throw new InvalidOperationException($"Underlying object does not have an 'payload' field.");
                    }

                    // SELECT {"$1": {"item": {"sum": SUM(c.blah), "count": COUNT(c.blah)}}} AS payload
                    if (cosmosPayload == null)
                    {
                        throw new ArgumentException($"{nameof(RewrittenAggregateProjections)} does not have a 'payload' property.");
                    }

                    this.Payload = cosmosPayload;
                }
            }

            public CosmosElement Payload
            {
                get;
            }
        }

        private struct AggregateContinuationToken
        {
            private const string SingleGroupAggregatorContinuationTokenName = "SingleGroupAggregatorContinuationToken";
            private const string SourceContinuationTokenName = "SourceContinuationToken";

            private readonly CosmosObject rawCosmosObject;

            private AggregateContinuationToken(CosmosObject rawCosmosObject)
            {
                this.rawCosmosObject = rawCosmosObject;
            }

            public static AggregateContinuationToken Create(
                string singleGroupAggregatorContinuationToken,
                string sourceContinuationToken)
            {
                if (singleGroupAggregatorContinuationToken == null)
                {
                    throw new ArgumentNullException(nameof(singleGroupAggregatorContinuationToken));
                }

                if (sourceContinuationToken == null)
                {
                    throw new ArgumentNullException(nameof(sourceContinuationToken));
                }

                Dictionary<string, CosmosElement> dictionary = new Dictionary<string, CosmosElement>
                {
                    [AggregateContinuationToken.SingleGroupAggregatorContinuationTokenName] = CosmosString.Create(singleGroupAggregatorContinuationToken),
                    [AggregateContinuationToken.SourceContinuationTokenName] = CosmosString.Create(sourceContinuationToken)
                };

                CosmosObject rawCosmosObject = CosmosObject.Create(dictionary);
                return new AggregateContinuationToken(rawCosmosObject);
            }

            public static bool TryParse(string serializedContinuationToken, out AggregateContinuationToken aggregateContinuationToken)
            {
                if (serializedContinuationToken == null)
                {
                    throw new ArgumentNullException(nameof(serializedContinuationToken));
                }

                if (!CosmosElement.TryParse(serializedContinuationToken, out CosmosElement rawCosmosElement))
                {
                    aggregateContinuationToken = default(AggregateContinuationToken);
                    return false;
                }

                if (!(rawCosmosElement is CosmosObject rawAggregateContinuationToken))
                {
                    aggregateContinuationToken = default(AggregateContinuationToken);
                    return false;
                }

                CosmosElement rawSingleGroupAggregatorContinuationToken = rawAggregateContinuationToken[AggregateContinuationToken.SingleGroupAggregatorContinuationTokenName];
                if (!(rawSingleGroupAggregatorContinuationToken is CosmosString singleGroupAggregatorContinuationToken))
                {
                    aggregateContinuationToken = default(AggregateContinuationToken);
                    return false;
                }

                CosmosElement rawSourceContinuationToken = rawAggregateContinuationToken[AggregateContinuationToken.SourceContinuationTokenName];
                if (!(rawSourceContinuationToken is CosmosString sourceContinuationToken))
                {
                    aggregateContinuationToken = default(AggregateContinuationToken);
                    return false;
                }

                aggregateContinuationToken = new AggregateContinuationToken(rawAggregateContinuationToken);
                return true;
            }

            public override string ToString()
            {
                return this.rawCosmosObject.ToString();
            }

            public string SingleGroupAggregatorContinuationToken
            {
                get
                {
                    return (this.rawCosmosObject[AggregateContinuationToken.SingleGroupAggregatorContinuationTokenName] as CosmosString).Value;
                }
            }

            public string SourceContinuationToken
            {
                get
                {
                    return (this.rawCosmosObject[AggregateContinuationToken.SourceContinuationTokenName] as CosmosString).Value;
                }
            }
        }
    }
}
