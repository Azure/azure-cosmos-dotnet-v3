// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Core.Utf8;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;
    using Newtonsoft.Json;

    internal class SqlQueryResumeValue : IComparable<CosmosElement>
    {
        private static class PropertyNames
        {
            public const string Type = "type";
            public const string ArrayType = "array";
            public const string ObjectType = "object";
            public const string Low = "low";
            public const string High = "high";
        }

        private class UndefinedResumeValue : SqlQueryResumeValue
        {
        }

        private class NullResumeValue : SqlQueryResumeValue
        {
        }

        private class BooleanResumeValue : SqlQueryResumeValue
        {
            public bool Value { get; }

            public BooleanResumeValue(bool value)
            {
                this.Value = value;
            }
        }

        private class NumberResumeValue : SqlQueryResumeValue
        {
            public Number64 Value { get; }

            public NumberResumeValue(Number64 value)
            {
                this.Value = value;
            }
        }

        private class StringResumeValue : SqlQueryResumeValue
        {
            public UtfAnyString Value { get; }

            public StringResumeValue(UtfAnyString value)
            {
                this.Value = value;
            }
        }

        private class ArrayResumeValue : SqlQueryResumeValue
        {
            public UInt128 HashValue { get; }

            public ArrayResumeValue(UInt128 hashValue)
            {
                this.HashValue = hashValue;
            }
        }

        private class ObjectResumeValue : SqlQueryResumeValue
        {
            public UInt128 HashValue { get; }

            public ObjectResumeValue(UInt128 hashValue)
            {
                this.HashValue = hashValue;
            }
        }

        public int CompareTo(CosmosElement cosmosElement)
        {
            switch (this)
            {
                case UndefinedResumeValue:
                    return ItemComparer.Instance.Compare(CosmosUndefined.Create(), cosmosElement);

                case NullResumeValue:
                    return ItemComparer.Instance.Compare(CosmosNull.Create(), cosmosElement);

                case BooleanResumeValue booleanValue:
                    return ItemComparer.Instance.Compare(CosmosBoolean.Create(booleanValue.Value), cosmosElement);

                case NumberResumeValue numberValue:
                    return ItemComparer.Instance.Compare(CosmosNumber64.Create(numberValue.Value), cosmosElement);

                case StringResumeValue stringValue:
                    return ItemComparer.Instance.Compare(CosmosString.Create(stringValue.Value), cosmosElement);

                case ArrayResumeValue arrayValue:
                    {
                        // If the order by result is also of array type, then compare the hash values
                        // For other types create an empty array and call CosmosElement comparer which
                        // will take care of ordering based on types.
                        if (cosmosElement is CosmosArray arrayResult)
                        {
                            return UInt128BinaryComparer.Singleton.Compare(arrayValue.HashValue, DistinctHash.GetHash(arrayResult));
                        }
                        else
                        {
                            return ItemComparer.Instance.Compare(CosmosArray.Empty, cosmosElement);
                        }
                    }

                case ObjectResumeValue objectValue:
                    {
                        // If the order by result is also of object type, then compare the hash values
                        // For other types create an empty object and call CosmosElement comparer which
                        // will take care of ordering based on types.
                        if (cosmosElement is CosmosObject objectResult)
                        {
                            // same type so compare the hash values
                            return UInt128BinaryComparer.Singleton.Compare(objectValue.HashValue, DistinctHash.GetHash(objectResult));
                        }
                        else
                        {
                            return ItemComparer.Instance.Compare(CosmosObject.Create(new Dictionary<string, CosmosElement>()), cosmosElement);
                        }
                    }

                default:
                    throw new ArgumentException($"Invalid {nameof(SqlQueryResumeValue)} type.");
            }
        }

        public static CosmosElement ToCosmosElement(SqlQueryResumeValue resumeValue)
        {
            return resumeValue switch
            {
                UndefinedResumeValue => CosmosArray.Create(new List<CosmosElement>()),
                NullResumeValue => CosmosNull.Create(),
                BooleanResumeValue booleanValue => CosmosBoolean.Create(booleanValue.Value),
                NumberResumeValue numberValue => CosmosNumber64.Create(numberValue.Value),
                StringResumeValue stringValue => CosmosString.Create(stringValue.Value),
                ArrayResumeValue arrayValue => CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { PropertyNames.Type, CosmosString.Create(PropertyNames.ArrayType) },
                        { PropertyNames.Low, CosmosNumber64.Create(arrayValue.HashValue.GetLow()) },
                        { PropertyNames.High, CosmosNumber64.Create(arrayValue.HashValue.GetHigh()) }
                    }),
                ObjectResumeValue objectValue => CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { PropertyNames.Type, CosmosString.Create(PropertyNames.ObjectType) },
                        { PropertyNames.Low, CosmosNumber64.Create(objectValue.HashValue.GetLow()) },
                        { PropertyNames.High, CosmosNumber64.Create(objectValue.HashValue.GetHigh()) }
                    }),
                _ => throw new ArgumentException($"Invalid {nameof(SqlQueryResumeValue)} type."),
            };
        }

        public static SqlQueryResumeValue FromCosmosElement(CosmosElement value)
        {
            return value.Accept(CosmosElementToResumeValueVisitor.Singleton);
        }

        public static SqlQueryResumeValue FromOrderByValue(CosmosElement orderByValue)
        {
            return orderByValue.Accept(OrderByValueToResumeValueVisitor.Singleton);
        }

        public static void Serialize(JsonWriter writer, SqlQueryResumeValue value, JsonSerializer serializer)
        {
            switch (value)
            {
                case UndefinedResumeValue:
                    writer.WriteStartArray();
                    writer.WriteEndArray();
                    break;

                case NullResumeValue:
                    writer.WriteNull();
                    break;

                case BooleanResumeValue booleanValue:
                    serializer.Serialize(writer, booleanValue.Value);
                    break;

                case NumberResumeValue numberValue:
                    serializer.Serialize(writer, numberValue.Value);
                    break;

                case StringResumeValue stringValue:
                    serializer.Serialize(writer, stringValue.Value.ToString());
                    break;

                case ArrayResumeValue arrayValue:
                    writer.WriteStartObject();
                    writer.WritePropertyName(SqlQueryResumeValue.PropertyNames.Type);
                    writer.WriteValue(SqlQueryResumeValue.PropertyNames.ArrayType);
                    writer.WritePropertyName(SqlQueryResumeValue.PropertyNames.Low);
                    writer.WriteValue(arrayValue.HashValue.GetLow());
                    writer.WritePropertyName(SqlQueryResumeValue.PropertyNames.High);
                    writer.WriteValue(arrayValue.HashValue.GetHigh());
                    writer.WriteEndObject();
                    break;

                case ObjectResumeValue objectValue:
                    writer.WriteStartObject();
                    writer.WritePropertyName(SqlQueryResumeValue.PropertyNames.Type);
                    writer.WriteValue(SqlQueryResumeValue.PropertyNames.ObjectType);
                    writer.WritePropertyName(SqlQueryResumeValue.PropertyNames.Low);
                    writer.WriteValue(objectValue.HashValue.GetLow());
                    writer.WritePropertyName(SqlQueryResumeValue.PropertyNames.High);
                    writer.WriteValue(objectValue.HashValue.GetHigh());
                    writer.WriteEndObject();
                    break;

                default:
                    throw new ArgumentException($"Invalid {nameof(SqlQueryResumeValue)} type.");
            }
        }

        private sealed class CosmosElementToResumeValueVisitor : ICosmosElementVisitor<SqlQueryResumeValue>
        {
            public static readonly CosmosElementToResumeValueVisitor Singleton = new CosmosElementToResumeValueVisitor();

            private CosmosElementToResumeValueVisitor()
            {
            }

            public SqlQueryResumeValue Visit(CosmosArray cosmosArray)
            {
                if (cosmosArray.Count != 0)
                {
                    throw new ArgumentException($"Only empty arrays can be converted to ResumeValue. Array has {cosmosArray.Count} elements.");
                }

                return new UndefinedResumeValue();
            }

            public SqlQueryResumeValue Visit(CosmosBinary cosmosBinary)
            {
                throw new NotSupportedException($"Converting {nameof(CosmosBinary)} to {nameof(SqlQueryResumeValue)} is not supported");
            }

            public SqlQueryResumeValue Visit(CosmosBoolean cosmosBoolean)
            {
                return new BooleanResumeValue(cosmosBoolean.Value);
            }

            public SqlQueryResumeValue Visit(CosmosGuid cosmosGuid)
            {
                throw new NotSupportedException($"Converting {nameof(CosmosGuid)} to {nameof(SqlQueryResumeValue)} is not supported");
            }

            public SqlQueryResumeValue Visit(CosmosNull cosmosNull)
            {
                return new NullResumeValue();
            }

            public SqlQueryResumeValue Visit(CosmosUndefined cosmosUndefined)
            {
                return new UndefinedResumeValue();
            }

            public SqlQueryResumeValue Visit(CosmosNumber cosmosNumber)
            {
                return new NumberResumeValue(cosmosNumber.Value);
            }

            public SqlQueryResumeValue Visit(CosmosObject cosmosObject)
            {
                if (!cosmosObject.TryGetValue(SqlQueryResumeValue.PropertyNames.Type, out CosmosString objectType)
                    || !cosmosObject.TryGetValue(SqlQueryResumeValue.PropertyNames.Low, out CosmosNumber64 lowValue)
                    || !cosmosObject.TryGetValue(SqlQueryResumeValue.PropertyNames.High, out CosmosNumber64 highValue))
                {
                    throw new ArgumentException($"Incorrect Array / Object Resume Value. One or more of the required properties are missing.");
                }

                if (string.Equals(objectType.Value, SqlQueryResumeValue.PropertyNames.ArrayType))
                {
                    return new ArrayResumeValue(
                        UInt128.Create(
                            (ulong)Number64.ToLong(lowValue.Value),
                            (ulong)Number64.ToLong(highValue.Value)));
                }
                else if (string.Equals(objectType.Value, SqlQueryResumeValue.PropertyNames.ObjectType))
                {
                    return new ObjectResumeValue(
                        UInt128.Create(
                            (ulong)Number64.ToLong(lowValue.Value),
                            (ulong)Number64.ToLong(highValue.Value)));
                }
                else
                {
                    throw new ArgumentException($"Incorrect value for {SqlQueryResumeValue.PropertyNames.Type} property. Value is {objectType.Value}.");
                }
            }

            public SqlQueryResumeValue Visit(CosmosString cosmosString)
            {
                return new StringResumeValue(cosmosString.Value);
            }
        }

        private sealed class OrderByValueToResumeValueVisitor : ICosmosElementVisitor<SqlQueryResumeValue>
        {
            public static readonly OrderByValueToResumeValueVisitor Singleton = new OrderByValueToResumeValueVisitor();

            private OrderByValueToResumeValueVisitor()
            {
            }

            public SqlQueryResumeValue Visit(CosmosArray cosmosArray)
            {
                return new ArrayResumeValue(DistinctHash.GetHash(cosmosArray));
            }

            public SqlQueryResumeValue Visit(CosmosBinary cosmosBinary)
            {
                throw new NotSupportedException($"Converting {nameof(CosmosBinary)} to {nameof(SqlQueryResumeValue)} is not supported");
            }

            public SqlQueryResumeValue Visit(CosmosBoolean cosmosBoolean)
            {
                return new BooleanResumeValue(cosmosBoolean.Value);
            }

            public SqlQueryResumeValue Visit(CosmosGuid cosmosGuid)
            {
                throw new NotSupportedException($"Converting {nameof(CosmosGuid)} to {nameof(SqlQueryResumeValue)} is not supported");
            }

            public SqlQueryResumeValue Visit(CosmosNull cosmosNull)
            {
                return new NullResumeValue();
            }

            public SqlQueryResumeValue Visit(CosmosUndefined cosmosUndefined)
            {
                return new UndefinedResumeValue();
            }

            public SqlQueryResumeValue Visit(CosmosNumber cosmosNumber)
            {
                return new NumberResumeValue(cosmosNumber.Value);
            }

            public SqlQueryResumeValue Visit(CosmosObject cosmosObject)
            {
                return new ObjectResumeValue(DistinctHash.GetHash(cosmosObject));
            }

            public SqlQueryResumeValue Visit(CosmosString cosmosString)
            {
                return new StringResumeValue(cosmosString.Value);
            }
        }

    }
}
