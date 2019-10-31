//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Aggregation;

    /// <summary>
    /// Aggregates all the projections for a single grouping.
    /// </summary>
    internal abstract class SingleGroupAggregator
    {
        /// <summary>
        /// Adds the payload for group by values 
        /// </summary>
        /// <param name="values"></param>
        public abstract void AddValues(CosmosElement values);

        /// <summary>
        /// Forms the final result of the grouping.
        /// </summary>
        public abstract CosmosElement GetResult();

        public abstract string GetContinuationToken();

        public static SingleGroupAggregator Create(
            CosmosQueryClient queryClient,
            AggregateOperator[] aggregates,
            IReadOnlyDictionary<string, AggregateOperator?> aggregateAliasToAggregateType,
            IReadOnlyList<string> orderedAliases,
            bool hasSelectValue,
            string continuationToken)
        {
            SingleGroupAggregator aggregateValues;
            if (hasSelectValue)
            {
                if (aggregates != null && aggregates.Any())
                {
                    // SELECT VALUE <AGGREGATE>
                    aggregateValues = SelectValueAggregateValues.Create(
                        aggregates[0],
                        continuationToken);
                }
                else
                {
                    // SELECT VALUE <NON AGGREGATE>
                    aggregateValues = SelectValueAggregateValues.Create(
                        aggregateOperator: null,
                        continuationToken: continuationToken);
                }
            }
            else
            {
                aggregateValues = SelectListAggregateValues.Create(
                    queryClient, 
                    aggregateAliasToAggregateType, 
                    orderedAliases, 
                    continuationToken);
            }

            return aggregateValues;
        }

        /// <summary>
        /// For SELECT VALUE queries there is only one value for each grouping.
        /// This class just helps maintain that and captures the first value across all continuations.
        /// </summary>
        private sealed class SelectValueAggregateValues : SingleGroupAggregator
        {
            private readonly AggregateValue aggregateValue;

            private SelectValueAggregateValues(AggregateValue aggregateValue)
            {
                if (aggregateValue == null)
                {
                    throw new ArgumentNullException(nameof(AggregateValue));
                }

                this.aggregateValue = aggregateValue;
            }

            public static SelectValueAggregateValues Create(AggregateOperator? aggregateOperator, string continuationToken)
            {
                AggregateValue aggregateValue = AggregateValue.Create(aggregateOperator, continuationToken);
                return new SelectValueAggregateValues(aggregateValue);
            }

            public override void AddValues(CosmosElement values)
            {
                this.aggregateValue.AddValue(values);
            }

            public override CosmosElement GetResult()
            {
                return this.aggregateValue.Result;
            }

            public override string GetContinuationToken()
            {
                return this.aggregateValue.GetContinuationToken();
            }

            public override string ToString()
            {
                return this.aggregateValue.ToString();
            }
        }

        /// <summary>
        /// For select list queries we need to create a dictionary of alias to group by value.
        /// For each grouping drained from the backend we merge it with the results here.
        /// At the end this class will form a JSON object with the correct aliases and grouping result.
        /// </summary>
        private sealed class SelectListAggregateValues : SingleGroupAggregator
        {
            private readonly IReadOnlyDictionary<string, AggregateValue> aliasToValue;
            private readonly IReadOnlyList<string> orderedAliases;

            private SelectListAggregateValues(
                IReadOnlyDictionary<string, AggregateValue> aliasToValue,
                IReadOnlyList<string> orderedAliases)
            {
                if (aliasToValue == null)
                {
                    throw new ArgumentNullException(nameof(aliasToValue));
                }

                if (orderedAliases == null)
                {
                    throw new ArgumentNullException(nameof(orderedAliases));
                }

                this.aliasToValue = aliasToValue;
                this.orderedAliases = orderedAliases;
            }

            public override CosmosElement GetResult()
            {
                List<KeyValuePair<string, CosmosElement>> aliasToElement = new List<KeyValuePair<string, CosmosElement>>();
                foreach (string alias in this.orderedAliases)
                {
                    AggregateValue aggregateValue = this.aliasToValue[alias];
                    if (aggregateValue.Result != null)
                    {
                        KeyValuePair<string, CosmosElement> kvp = new KeyValuePair<string, CosmosElement>(alias, aggregateValue.Result);
                        aliasToElement.Add(kvp);
                    }
                }

                return CosmosObject.Create(aliasToElement);
            }

            public override string GetContinuationToken()
            {
                Dictionary<string, CosmosElement> aliasToContinuationToken = new Dictionary<string, CosmosElement>();
                foreach (KeyValuePair<string, AggregateValue> kvp in this.aliasToValue)
                {
                    aliasToContinuationToken[kvp.Key] = CosmosString.Create(kvp.Value.GetContinuationToken());
                }

                CosmosObject cosmosObject = CosmosObject.Create(aliasToContinuationToken);
                return cosmosObject.ToString();
            }

            public static SelectListAggregateValues Create(
                CosmosQueryClient cosmosQueryClient,
                IReadOnlyDictionary<string, AggregateOperator?> aggregateAliasToAggregateType,
                IReadOnlyList<string> orderedAliases,
                string continuationToken)
            {
                CosmosObject aliasToContinuationToken;
                if (continuationToken != null)
                {
                    if (!CosmosElement.TryParse(continuationToken, out aliasToContinuationToken))
                    {
                        throw cosmosQueryClient.CreateBadRequestException(
                            $"{nameof(SelectListAggregateValues)} continuation token is malformed: {continuationToken}.");
                    }
                }
                else
                {
                    aliasToContinuationToken = null;
                }

                Dictionary<string, AggregateValue> groupingTable = new Dictionary<string, AggregateValue>();
                foreach (KeyValuePair<string, AggregateOperator?> aliasToAggregate in aggregateAliasToAggregateType)
                {
                    string alias = aliasToAggregate.Key;
                    AggregateOperator? aggregateOperator = aliasToAggregate.Value;
                    string aliasContinuationToken;
                    if (aliasToContinuationToken != null)
                    {
                        if (!(aliasToContinuationToken[alias] is CosmosString parsedAliasContinuationToken))
                        {
                            throw cosmosQueryClient.CreateBadRequestException(
                            $"{nameof(SelectListAggregateValues)} continuation token is malformed: {continuationToken}.");
                        }

                        aliasContinuationToken = parsedAliasContinuationToken.Value;
                    }
                    else
                    {
                        aliasContinuationToken = null;
                    }

                    groupingTable[alias] = AggregateValue.Create(aggregateOperator, aliasContinuationToken);
                }

                return new SelectListAggregateValues(groupingTable, orderedAliases);
            }

            public override void AddValues(CosmosElement values)
            {
                if (!(values is CosmosObject payload))
                {
                    throw new ArgumentException("values is not an object.");
                }

                foreach (KeyValuePair<string, AggregateValue> aliasAndValue in this.aliasToValue)
                {
                    string alias = aliasAndValue.Key;
                    AggregateValue aggregateValue = aliasAndValue.Value;
                    if (!payload.TryGetValue(alias, out CosmosElement payloadValue))
                    {
                        payloadValue = null;
                    }

                    aggregateValue.AddValue(payloadValue);
                }
            }

            public override string ToString()
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(this.aliasToValue);
            }
        }

        /// <summary>
        /// With a group by value we need to encapsulate the fact that we have:
        /// 1) aggregate group by values
        /// 2) scalar group by values.
        /// </summary>
        private abstract class AggregateValue
        {
            public abstract void AddValue(CosmosElement aggregateValue);

            public abstract CosmosElement Result { get; }

            public abstract string GetContinuationToken();

            public override string ToString()
            {
                return this.Result.ToString();
            }

            public static AggregateValue Create(AggregateOperator? aggregateOperator, string continuationToken)
            {
                AggregateValue value;
                if (aggregateOperator.HasValue)
                {
                    value = AggregateAggregateValue.Create(aggregateOperator.Value, continuationToken);
                }
                else
                {
                    value = ScalarAggregateValue.Create(continuationToken);
                }

                return value;
            }

            private sealed class AggregateAggregateValue : AggregateValue
            {
                private readonly IAggregator aggregator;

                public override CosmosElement Result => this.aggregator.GetResult();

                private AggregateAggregateValue(IAggregator aggregator)
                {
                    if (aggregator == null)
                    {
                        throw new ArgumentNullException(nameof(aggregator));
                    }

                    this.aggregator = aggregator;
                }

                public override void AddValue(CosmosElement aggregateValue)
                {
                    AggregateItem aggregateItem = new AggregateItem(aggregateValue);
                    this.aggregator.Aggregate(aggregateItem.Item);
                }

                public override string GetContinuationToken()
                {
                    return this.aggregator.GetContinuationToken();
                }

                public static AggregateAggregateValue Create(
                    AggregateOperator aggregateOperator,
                    string continuationToken)
                {
                    IAggregator aggregator;
                    switch (aggregateOperator)
                    {
                        case AggregateOperator.Average:
                            aggregator = AverageAggregator.Create(continuationToken);
                            break;

                        case AggregateOperator.Count:
                            aggregator = CountAggregator.Create(continuationToken);
                            break;

                        case AggregateOperator.Max:
                            aggregator = MinMaxAggregator.CreateMaxAggregator(continuationToken);
                            break;

                        case AggregateOperator.Min:
                            aggregator = MinMaxAggregator.CreateMinAggregator(continuationToken);
                            break;

                        case AggregateOperator.Sum:
                            aggregator = SumAggregator.Create(continuationToken);
                            break;

                        default:
                            throw new ArgumentException($"Unknown {nameof(AggregateOperator)}: {aggregateOperator}.");
                    }

                    return new AggregateAggregateValue(aggregator);
                }
            }

            private sealed class ScalarAggregateValue : AggregateValue
            {
                private CosmosElement value;
                private bool initialized;

                private ScalarAggregateValue(CosmosElement initialValue, bool initialized)
                {
                    this.value = initialValue;
                    this.initialized = initialized;
                }

                public override CosmosElement Result
                {
                    get
                    {
                        if (!this.initialized)
                        {
                            throw new InvalidOperationException($"{nameof(ScalarAggregateValue)} is not yet initialized.");
                        }

                        return this.value;
                    }
                }

                public override string GetContinuationToken()
                {
                    IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
                    jsonWriter.WriteObjectStart();
                    jsonWriter.WriteFieldName(nameof(this.initialized));
                    jsonWriter.WriteBoolValue(this.initialized);
                    if (this.value != null)
                    {
                        jsonWriter.WriteFieldName(nameof(this.value));
                        this.value.WriteTo(jsonWriter);
                    }
                    jsonWriter.WriteObjectEnd();

                    string continuationToken = Utf8StringHelpers.ToString(jsonWriter.GetResult());
                    return continuationToken;
                }

                public static ScalarAggregateValue Create(string continuationToken)
                {
                    CosmosElement value;
                    bool initialized;
                    if (continuationToken != null)
                    {
                        if (!CosmosElement.TryParse<CosmosObject>(
                            continuationToken,
                            out CosmosObject rawContinuationToken))
                        {
                            throw new ArgumentException($"Invalid {nameof(ScalarAggregateValue)}: {continuationToken}");
                        }

                        if (!rawContinuationToken.TryGetValue<CosmosBoolean>(
                            nameof(ScalarAggregateValue.initialized),
                            out CosmosBoolean rawInitialized))
                        {
                            throw new ArgumentException($"Invalid {nameof(ScalarAggregateValue)}: {continuationToken}");
                        }

                        value = rawContinuationToken[nameof(ScalarAggregateValue.value)];
                        initialized = rawInitialized.Value;
                    }
                    else
                    {
                        value = null;
                        initialized = false;
                    }

                    return new ScalarAggregateValue(value, initialized);
                }

                public override void AddValue(CosmosElement aggregateValue)
                {
                    if (!this.initialized)
                    {
                        this.value = aggregateValue;
                        this.initialized = true;
                    }
                }
            }
        }
    }
}
