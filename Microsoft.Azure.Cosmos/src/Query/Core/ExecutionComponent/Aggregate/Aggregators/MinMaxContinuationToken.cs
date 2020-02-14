// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Aggregate.Aggregators
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class MinMaxContinuationToken
    {
        private const string TypeName = "type";
        private const string ValueName = "value";

        private MinMaxContinuationToken(
            MinMaxContinuationTokenType type,
            CosmosElement value)
        {
            switch (type)
            {
                case MinMaxContinuationTokenType.MinValue:
                case MinMaxContinuationTokenType.MaxValue:
                case MinMaxContinuationTokenType.Undefined:
                    if (value != null)
                    {
                        throw new ArgumentException($"{nameof(value)} must be set with type: {type}.");
                    }
                    break;

                case MinMaxContinuationTokenType.Value:
                    if (value == null)
                    {
                        throw new ArgumentException($"{nameof(value)} must not be set with type: {type}.");
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(type)}: {type}.");
            }

            this.Type = type;
            this.Value = value;
        }

        public MinMaxContinuationTokenType Type { get; }
        public CosmosElement Value { get; }

        public static MinMaxContinuationToken CreateMinValueContinuationToken()
        {
            return new MinMaxContinuationToken(type: MinMaxContinuationTokenType.MinValue, value: null);
        }

        public static MinMaxContinuationToken CreateMaxValueContinuationToken()
        {
            return new MinMaxContinuationToken(type: MinMaxContinuationTokenType.MaxValue, value: null);
        }

        public static MinMaxContinuationToken CreateUndefinedValueContinuationToken()
        {
            return new MinMaxContinuationToken(type: MinMaxContinuationTokenType.Undefined, value: null);
        }

        public static MinMaxContinuationToken CreateValueContinuationToken(CosmosElement value)
        {
            return new MinMaxContinuationToken(type: MinMaxContinuationTokenType.Value, value: value);
        }

        public static CosmosElement ToCosmosElement(MinMaxContinuationToken minMaxContinuationToken)
        {
            if (minMaxContinuationToken == null)
            {
                throw new ArgumentNullException(nameof(minMaxContinuationToken));
            }

            Dictionary<string, CosmosElement> dictionary = new Dictionary<string, CosmosElement>();
            dictionary.Add(
                MinMaxContinuationToken.TypeName,
                EnumToCosmosString.ConvertEnumToCosmosString(minMaxContinuationToken.Type));
            if (minMaxContinuationToken.Value != null)
            {
                dictionary.Add(MinMaxContinuationToken.ValueName, minMaxContinuationToken.Value);
            }

            return CosmosObject.Create(dictionary);
        }

        public static TryCatch<MinMaxContinuationToken> TryCreateFromCosmosElement(CosmosElement cosmosElement)
        {
            if (!(cosmosElement is CosmosObject cosmosObject))
            {
                return TryCatch<MinMaxContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(MinMaxContinuationToken)} was not an object."));
            }

            if (!cosmosObject.TryGetValue(MinMaxContinuationToken.TypeName, out CosmosString typeValue))
            {
                return TryCatch<MinMaxContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(MinMaxContinuationToken)} is missing property: {MinMaxContinuationToken.TypeName}."));
            }

            if (!Enum.TryParse(typeValue.Value, out MinMaxContinuationTokenType minMaxContinuationTokenType))
            {
                return TryCatch<MinMaxContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(MinMaxContinuationToken)} has malformed '{MinMaxContinuationToken.TypeName}': {typeValue.Value}."));
            }

            CosmosElement value;
            if (minMaxContinuationTokenType == MinMaxContinuationTokenType.Value)
            {
                if (!cosmosObject.TryGetValue(MinMaxContinuationToken.ValueName, out value))
                {
                    return TryCatch<MinMaxContinuationToken>.FromException(
                        new MalformedContinuationTokenException($"{nameof(MinMaxContinuationToken)} is missing property: {MinMaxContinuationToken.ValueName}."));
                }
            }
            else
            {
                value = null;
            }

            return TryCatch<MinMaxContinuationToken>.FromResult(
                new MinMaxContinuationToken(minMaxContinuationTokenType, value));
        }

        private static class EnumToCosmosString
        {
            private static readonly CosmosString MinValueCosmosString = CosmosString.Create(MinMaxContinuationTokenType.MinValue.ToString());
            private static readonly CosmosString MaxValueCosmosString = CosmosString.Create(MinMaxContinuationTokenType.MaxValue.ToString());
            private static readonly CosmosString UndefinedCosmosString = CosmosString.Create(MinMaxContinuationTokenType.Undefined.ToString());
            private static readonly CosmosString ValueCosmosString = CosmosString.Create(MinMaxContinuationTokenType.Value.ToString());

            public static CosmosString ConvertEnumToCosmosString(MinMaxContinuationTokenType type)
            {
                switch (type)
                {
                    case MinMaxContinuationTokenType.MinValue:
                        return EnumToCosmosString.MinValueCosmosString;

                    case MinMaxContinuationTokenType.MaxValue:
                        return EnumToCosmosString.MaxValueCosmosString;

                    case MinMaxContinuationTokenType.Undefined:
                        return EnumToCosmosString.UndefinedCosmosString;

                    case MinMaxContinuationTokenType.Value:
                        return EnumToCosmosString.ValueCosmosString;

                    default:
                        throw new ArgumentOutOfRangeException($"Unknown {nameof(MinMaxContinuationTokenType)}: {type}");
                }
            }
        }

        public enum MinMaxContinuationTokenType
        {
            MinValue,
            MaxValue,
            Undefined,
            Value,
        }
    }
}
