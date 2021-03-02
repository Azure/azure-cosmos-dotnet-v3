// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal static class FeedRangeCosmosElementSerializer
    {
        private const string TypePropertyName = "type";
        private const string ValuePropertyName = "value";

        private const string MinPropertyName = "min";
        private const string MaxPropertyName = "max";

        private const string LogicalPartitionKey = "Logical Partition Key";
        private const string PhysicalPartitionKeyRangeId = "Physical Partition Key Range Id";
        private const string EffectivePartitionKeyRange = "Effective Partition Key Range";

        public static TryCatch<FeedRangeInternal> MonadicCreateFromCosmosElement(CosmosElement cosmosElement)
        {
            if (cosmosElement == null)
            {
                throw new ArgumentNullException(nameof(cosmosElement));
            }

            if (!(cosmosElement is CosmosObject cosmosObject))
            {
                return TryCatch<FeedRangeInternal>.FromException(
                    new FormatException($"Expected object for feed range: {cosmosElement}."));
            }

            if (!cosmosObject.TryGetValue(TypePropertyName, out CosmosString typeProperty))
            {
                return TryCatch<FeedRangeInternal>.FromException(
                    new FormatException($"expected string type property for feed range: {cosmosElement}."));
            }

            if (!cosmosObject.TryGetValue(ValuePropertyName, out CosmosElement valueProperty))
            {
                return TryCatch<FeedRangeInternal>.FromException(
                    new FormatException($"expected value property for feed range: {cosmosElement}."));
            }

            FeedRangeInternal feedRange;
            switch (typeProperty.Value)
            {
                case LogicalPartitionKey:
                    {
                        if (!(valueProperty is CosmosString stringValueProperty))
                        {
                            return TryCatch<FeedRangeInternal>.FromException(
                                new FormatException($"expected string value property for logical partition key feed range: {cosmosElement}."));
                        }

                        if (!PartitionKey.TryParseJsonString(stringValueProperty.Value, out PartitionKey partitionKey))
                        {
                            return TryCatch<FeedRangeInternal>.FromException(
                                new FormatException($"failed to parse logical partition key value: {stringValueProperty.Value}."));
                        }

                        feedRange = new FeedRangeLogicalPartitionKey(partitionKey);
                    }
                    break;

                case PhysicalPartitionKeyRangeId:
                    {
                        if (!(valueProperty is CosmosString stringValueProperty))
                        {
                            return TryCatch<FeedRangeInternal>.FromException(
                                new FormatException($"expected string value property for physical partition key feed range: {cosmosElement}."));
                        }

                        feedRange = new FeedRangePhysicalPartitionKeyRange(stringValueProperty.Value);
                    }
                    break;

                case EffectivePartitionKeyRange:
                    {
                        if (!(valueProperty is CosmosObject objectValueProperty))
                        {
                            return TryCatch<FeedRangeInternal>.FromException(
                                new FormatException($"expected object value property for effective partition key range feed range: {cosmosElement}."));
                        }

                        if (!objectValueProperty.TryGetValue(MinPropertyName, out CosmosString minPartitionKeyValue))
                        {
                            return TryCatch<FeedRangeInternal>.FromException(
                                new FormatException($"expected string value property for min effective partition key value: {cosmosElement}."));
                        }

                        if (!objectValueProperty.TryGetValue(MaxPropertyName, out CosmosString maxPartitionKeyValue))
                        {
                            return TryCatch<FeedRangeInternal>.FromException(
                                new FormatException($"expected string value property for max effective partition key value: {cosmosElement}."));
                        }

                        feedRange = new FeedRangeEpkRange(
                            startEpkInclusive: minPartitionKeyValue.Value,
                            endEpkExclusive: maxPartitionKeyValue.Value);
                    }
                    break;

                default:
                    throw new InvalidOperationException($"unexpected feed range type: {typeProperty.Value}");
            }

            return TryCatch<FeedRangeInternal>.FromResult(feedRange);
        }

        public static CosmosElement ToCosmosElement(FeedRangeInternal feedRange)
        {
            if (feedRange == null)
            {
                throw new ArgumentNullException(nameof(feedRange));
            }

            return feedRange.Accept(FeedRangeToCosmosElementTransformer.Singleton);
        }

        private sealed class FeedRangeToCosmosElementTransformer : IFeedRangeTransformer<CosmosElement>
        {
            public static readonly FeedRangeToCosmosElementTransformer Singleton = new FeedRangeToCosmosElementTransformer();

            private static readonly CosmosElement LogicalPartitionKeyCosmosElement = CosmosString.Create(LogicalPartitionKey);
            private static readonly CosmosElement PhysicalPartitionKeyRangeIdCosmosElement = CosmosString.Create(PhysicalPartitionKeyRangeId);
            private static readonly CosmosElement EffecitvePartitionKeyRangeIdCosmosElement = CosmosString.Create(EffectivePartitionKeyRange);

            private FeedRangeToCosmosElementTransformer()
            {
            }

            public CosmosElement Visit(FeedRangeLogicalPartitionKey feedRange)
            {
                return CosmosObject.Create(new Dictionary<string, CosmosElement>()
                {
                    { TypePropertyName, LogicalPartitionKeyCosmosElement },
                    { ValuePropertyName, CosmosString.Create(feedRange.PartitionKey.ToJsonString()) },
                });
            }

            public CosmosElement Visit(FeedRangePhysicalPartitionKeyRange feedRange)
            {
                return CosmosObject.Create(new Dictionary<string, CosmosElement>()
                {
                    { TypePropertyName, PhysicalPartitionKeyRangeIdCosmosElement },
                    { ValuePropertyName, CosmosString.Create(feedRange.PartitionKeyRangeId) },
                });
            }

            public CosmosElement Visit(FeedRangeEpkRange feedRange)
            {
                return CosmosObject.Create(new Dictionary<string, CosmosElement>()
                {
                    { TypePropertyName, EffecitvePartitionKeyRangeIdCosmosElement },
                    {
                        ValuePropertyName,
                        CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { MinPropertyName, CosmosString.Create(feedRange.StartEpkInclusive) },
                                { MaxPropertyName, CosmosString.Create(feedRange.EndEpkExclusive) },
                            })
                    }
                });
            }
        }
    }
}
