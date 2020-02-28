//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Aggregate.Aggregators
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
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

            if (!ItemComparer.IsMinOrMax(this.globalMinMax) && (!IsPrimitve(localMinMax) || !IsPrimitve(this.globalMinMax)))
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
            if ((this.globalMinMax == ItemComparer.MinValue) || (this.globalMinMax == ItemComparer.MaxValue))
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

            MinMaxContinuationToken minMaxContinuationToken;
            if (this.globalMinMax == ItemComparer.MinValue)
            {
                minMaxContinuationToken = MinMaxContinuationToken.CreateMinValueContinuationToken();
            }
            else if (this.globalMinMax == ItemComparer.MaxValue)
            {
                minMaxContinuationToken = MinMaxContinuationToken.CreateMaxValueContinuationToken();
            }
            else if (this.globalMinMax == Undefined)
            {
                minMaxContinuationToken = MinMaxContinuationToken.CreateUndefinedValueContinuationToken();
            }
            else
            {
                minMaxContinuationToken = MinMaxContinuationToken.CreateValueContinuationToken(this.globalMinMax);
            }

            CosmosElement minMaxContinuationTokenAsCosmosElement = MinMaxContinuationToken.ToCosmosElement(minMaxContinuationToken);
            minMaxContinuationTokenAsCosmosElement.WriteTo(jsonWriter);
        }

        public static TryCatch<IAggregator> TryCreateMinAggregator(CosmosElement continuationToken)
        {
            return MinMaxAggregator.TryCreate(isMinAggregation: true, continuationToken: continuationToken);
        }

        public static TryCatch<IAggregator> TryCreateMaxAggregator(CosmosElement continuationToken)
        {
            return MinMaxAggregator.TryCreate(isMinAggregation: false, continuationToken: continuationToken);
        }

        private static TryCatch<IAggregator> TryCreate(bool isMinAggregation, CosmosElement continuationToken)
        {
            CosmosElement globalMinMax;
            if (continuationToken != null)
            {
                TryCatch<MinMaxContinuationToken> tryCreateMinMaxContinuationToken = MinMaxContinuationToken.TryCreateFromCosmosElement(continuationToken);
                if (!tryCreateMinMaxContinuationToken.Succeeded)
                {
                    return TryCatch<IAggregator>.FromException(tryCreateMinMaxContinuationToken.Exception);
                }

                switch (tryCreateMinMaxContinuationToken.Result.Type)
                {
                    case MinMaxContinuationToken.MinMaxContinuationTokenType.MinValue:
                        globalMinMax = ItemComparer.MinValue;
                        break;

                    case MinMaxContinuationToken.MinMaxContinuationTokenType.MaxValue:
                        globalMinMax = ItemComparer.MaxValue;
                        break;

                    case MinMaxContinuationToken.MinMaxContinuationTokenType.Undefined:
                        globalMinMax = MinMaxAggregator.Undefined;
                        break;

                    case MinMaxContinuationToken.MinMaxContinuationTokenType.Value:
                        globalMinMax = tryCreateMinMaxContinuationToken.Result.Value;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException($"Unknown {nameof(MinMaxContinuationToken.MinMaxContinuationTokenType)}: {tryCreateMinMaxContinuationToken.Result.Type}");
                }
            }
            else
            {
                globalMinMax = isMinAggregation ? (CosmosElement)ItemComparer.MaxValue : (CosmosElement)ItemComparer.MinValue;
            }

            return TryCatch<IAggregator>.FromResult(
                new MinMaxAggregator(isMinAggregation: isMinAggregation, globalMinMax: globalMinMax));
        }

        private static bool IsPrimitve(CosmosElement cosmosElement)
        {
            if (cosmosElement == Undefined)
            {
                return false;
            }

            return cosmosElement.Accept(IsPrimitiveCosmosElementVisitor.Singleton);
        }

        private sealed class IsPrimitiveCosmosElementVisitor : ICosmosElementVisitor<bool>
        {
            public static readonly IsPrimitiveCosmosElementVisitor Singleton = new IsPrimitiveCosmosElementVisitor();

            private IsPrimitiveCosmosElementVisitor()
            {
            }

            public bool Visit(CosmosArray cosmosArray)
            {
                return false;
            }

            public bool Visit(CosmosBinary cosmosBinary)
            {
                return true;
            }

            public bool Visit(CosmosBoolean cosmosBoolean)
            {
                return true;
            }

            public bool Visit(CosmosGuid cosmosGuid)
            {
                return true;
            }

            public bool Visit(CosmosNull cosmosNull)
            {
                return true;
            }

            public bool Visit(CosmosNumber cosmosNumber)
            {
                return true;
            }

            public bool Visit(CosmosObject cosmosObject)
            {
                return false;
            }

            public bool Visit(CosmosString cosmosString)
            {
                return true;
            }
        }
    }
}
