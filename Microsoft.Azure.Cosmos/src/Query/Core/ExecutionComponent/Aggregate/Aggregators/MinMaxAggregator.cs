//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Aggregate.Aggregators
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    /// <summary>
    /// Concrete implementation of IAggregator that can take the global min/max from the local min/max of multiple partitions and continuations.
    /// Let min/max_i,j be the min/max from the ith continuation in the jth partition, 
    /// then the min/max for the entire query is MIN/MAX(min/max_i,j for all i and j).
    /// </summary>
    internal sealed class MinMaxAggregator : IAggregator
    {
        private const string MinValueContinuationToken = "MIN_VALUE";
        private const string MaxValueContinuationToken = "MAX_VALUE";
        private const string UndefinedContinuationToken = "UNDEFINED";

        private static readonly CosmosElement Undefined = null;
        /// <summary>
        /// Whether or not the aggregation is a min or a max.
        /// </summary>
        private readonly bool isMinAggregation;

        /// <summary>
        /// The global max of all items seen.
        /// </summary>
        private CosmosElement globalMinMax;

        private MinMaxAggregator(bool isMinAggregation, CosmosElement globalMinMax)
        {
            this.isMinAggregation = isMinAggregation;
            this.globalMinMax = globalMinMax;
        }

        public void Aggregate(CosmosElement localMinMax)
        {
            // If the value became undefinded at some point then it should stay that way.
            if (this.globalMinMax == Undefined)
            {
                return;
            }

            if (localMinMax == Undefined)
            {
                // If we got an undefined in the pipeline then the whole thing becomes undefined.
                this.globalMinMax = Undefined;
                return;
            }

            // Check to see if we got the higher precision result 
            // and unwrap the object to get the actual item of interest
            if (localMinMax is CosmosObject cosmosObject)
            {
                if (cosmosObject.TryGetValue("count", out CosmosNumber countToken))
                {
                    // We know the object looks like: {"min": MIN(c.blah), "count": COUNT(c.blah)}
                    long count = Number64.ToLong(countToken.Value);
                    if (count == 0)
                    {
                        // Ignore the value since the continuation / partition had no results that matched the filter so min is undefined.
                        return;
                    }

                    if (!cosmosObject.TryGetValue("min", out CosmosElement min))
                    {
                        min = null;
                    }

                    if (!cosmosObject.TryGetValue("max", out CosmosElement max))
                    {
                        max = null;
                    }

                    if (min != null)
                    {
                        localMinMax = min;
                    }
                    else if (max != null)
                    {
                        localMinMax = max;
                    }
                    else
                    {
                        localMinMax = Undefined;
                    }
                }
            }

            if (!ItemComparer.IsMinOrMax(this.globalMinMax)
                && (!CosmosElementIsPrimitive(localMinMax) || !CosmosElementIsPrimitive(this.globalMinMax)))
            {
                // This means we are comparing non primitives which is undefined
                this.globalMinMax = Undefined;
                return;
            }

            // Finally do the comparision
            if (this.isMinAggregation)
            {
                if (ItemComparer.Instance.Compare(localMinMax, this.globalMinMax) < 0)
                {
                    this.globalMinMax = localMinMax;
                }
            }
            else
            {
                if (ItemComparer.Instance.Compare(localMinMax, this.globalMinMax) > 0)
                {
                    this.globalMinMax = localMinMax;
                }
            }
        }

        public CosmosElement GetResult()
        {
            CosmosElement result;
            if (this.globalMinMax == ItemComparer.MinValue || this.globalMinMax == ItemComparer.MaxValue)
            {
                // The filter did not match any documents.
                result = Undefined;
            }
            else
            {
                result = this.globalMinMax;
            }

            return result;
        }

        public string GetContinuationToken()
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            this.SerializeState(jsonWriter);
            return Utf8StringHelpers.ToString(jsonWriter.GetResult());
        }

        public void SerializeState(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException(nameof(jsonWriter));
            }

            if (this.globalMinMax == ItemComparer.MinValue)
            {
                jsonWriter.WriteStringValue(MinMaxAggregator.MinValueContinuationToken);
            }
            else if (this.globalMinMax == ItemComparer.MaxValue)
            {
                jsonWriter.WriteStringValue(MinMaxAggregator.MaxValueContinuationToken);
            }
            else if (this.globalMinMax == Undefined)
            {
                jsonWriter.WriteStringValue(MinMaxAggregator.UndefinedContinuationToken);
            }
            else
            {
                this.globalMinMax.WriteTo(jsonWriter);
            }
        }

        public static TryCatch<IAggregator> TryCreateMinAggregator(RequestContinuationToken continuationToken)
        {
            return MinMaxAggregator.TryCreate(isMinAggregation: true, continuationToken: continuationToken);
        }

        public static TryCatch<IAggregator> TryCreateMaxAggregator(RequestContinuationToken continuationToken)
        {
            return MinMaxAggregator.TryCreate(isMinAggregation: false, continuationToken: continuationToken);
        }

        private static TryCatch<IAggregator> TryCreate(bool isMinAggregation, RequestContinuationToken continuationToken)
        {
            if (continuationToken == null)
            {
                throw new ArgumentNullException(nameof(continuationToken));
            }

            CosmosElement globalMinMax;
            if (!continuationToken.IsNull)
            {
                if (!continuationToken.TryConvertToCosmosElement(out CosmosElement globalMinMax))
                {
                    return TryCatch<IAggregator>.FromException(
                            new MalformedContinuationTokenException($"Malformed continuation token: {continuationToken}"));
                }
                if (continuationToken == MinMaxAggregator.MaxValueContinuationToken)
                {
                    globalMinMax = ItemComparer.MaxValue;
                }
                else if (continuationToken == MinMaxAggregator.MinValueContinuationToken)
                {
                    globalMinMax = ItemComparer.MinValue;
                }
                else if (continuationToken == MinMaxAggregator.UndefinedContinuationToken)
                {
                    globalMinMax = MinMaxAggregator.Undefined;
                }
                else
                {
                    if (!CosmosElement.TryParse(continuationToken, out globalMinMax))
                    {
                        
                    }
                }
            }
            else
            {
                globalMinMax = isMinAggregation ? (CosmosElement)ItemComparer.MaxValue : (CosmosElement)ItemComparer.MinValue;
            }

            return TryCatch<IAggregator>.FromResult(
                new MinMaxAggregator(isMinAggregation: isMinAggregation, globalMinMax: globalMinMax));
        }

        private static bool CosmosElementIsPrimitive(CosmosElement cosmosElement)
        {
            if (cosmosElement == null)
            {
                return false;
            }

            CosmosElementType cosmosElementType = cosmosElement.Type;
            switch (cosmosElementType)
            {
                case CosmosElementType.Array:
                    return false;

                case CosmosElementType.Boolean:
                    return true;

                case CosmosElementType.Null:
                    return true;

                case CosmosElementType.Number:
                    return true;

                case CosmosElementType.Object:
                    return false;

                case CosmosElementType.String:
                    return true;

                case CosmosElementType.Guid:
                    return true;

                case CosmosElementType.Binary:
                    return true;

                default:
                    throw new ArgumentException($"Unknown {nameof(CosmosElementType)} : {cosmosElementType}.");
            }
        }
    }
}
