// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal abstract partial class AggregateDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private static readonly List<CosmosElement> EmptyResults = new List<CosmosElement>();

        private sealed class ComputeAggregateDocumentQueryExecutionComponent : AggregateDocumentQueryExecutionComponent
        {
            public ComputeAggregateDocumentQueryExecutionComponent(
                IDocumentQueryExecutionComponent source,
                SingleGroupAggregator singleGroupAggregator,
                bool isValueAggregateQuery)
                : base(source, singleGroupAggregator, isValueAggregateQuery)
            {
                // all the work is done in the base constructor.
            }

            public override async Task<QueryResponseCore> DrainAsync(
                int maxElements,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                cancellationToken.ThrowIfCancellationRequested();

                // Draining aggregates is broken down into two stages
                QueryResponseCore response;
                if (!this.Source.IsDone)
                {
                    // Stage 1:
                    // Drain the aggregates fully from all continuations and all partitions
                    // And return empty pages in the meantime.
                    QueryResponseCore sourceResponse = await this.Source.DrainAsync(int.MaxValue, cancellationToken);
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
                    // Stage 2:
                    // Return the final page after draining.
                    response = this.GetFinalResponse();
                }

                return response;
            }
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
                requestCharge: 0,
                activityId: null,
                responseLengthBytes: 0,
                disallowContinuationTokenMessage: null,
                continuationToken: null,
                diagnostics: QueryResponseCore.EmptyDiagnostics);
            this.isDone = true;

            return response;
        }

        private QueryResponseCore GetEmptyPage(QueryResponseCore sourceResponse)
        {
            if (!this.TryGetContinuationToken(out string updatedContinuationToken))
            {
                throw new InvalidOperationException("Failed to get source continuation token.");
            }

            // We need to give empty pages until the results are fully drained.
            QueryResponseCore response = QueryResponseCore.CreateSuccess(
                result: EmptyResults,
                requestCharge: sourceResponse.RequestCharge,
                activityId: sourceResponse.ActivityId,
                responseLengthBytes: sourceResponse.ResponseLengthBytes,
                disallowContinuationTokenMessage: null,
                continuationToken: updatedContinuationToken,
                diagnostics: sourceResponse.Diagnostics);

            this.isDone = false;

            return response;
        }

        public override bool TryGetContinuationToken(out string state)
        {
            if (!this.IsDone)
            {
                if (this.Source.TryGetContinuationToken(out string sourceState))
                {
                    AggregateContinuationToken aggregateContinuationToken = AggregateContinuationToken.Create(
                        this.singleGroupAggregator.GetContinuationToken(),
                        sourceState);
                    state = aggregateContinuationToken.ToString();
                    return true;
                }
                else
                {
                    state = null;
                    return false;
                }
            }
            else
            {
                state = null;
                return true;
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

            public static bool TryParse(
                string serializedContinuationToken,
                out AggregateContinuationToken aggregateContinuationToken)
            {
                if (serializedContinuationToken == null)
                {
                    throw new ArgumentNullException(nameof(serializedContinuationToken));
                }

                if (!CosmosElement.TryParse<CosmosObject>(serializedContinuationToken, out CosmosObject rawAggregateContinuationToken))
                {
                    aggregateContinuationToken = default;
                    return false;
                }

                CosmosElement rawSingleGroupAggregatorContinuationToken = rawAggregateContinuationToken[AggregateContinuationToken.SingleGroupAggregatorContinuationTokenName];
                if (!(rawSingleGroupAggregatorContinuationToken is CosmosString singleGroupAggregatorContinuationToken))
                {
                    aggregateContinuationToken = default;
                    return false;
                }

                CosmosElement rawSourceContinuationToken = rawAggregateContinuationToken[AggregateContinuationToken.SourceContinuationTokenName];
                if (!(rawSourceContinuationToken is CosmosString sourceContinuationToken))
                {
                    aggregateContinuationToken = default;
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
