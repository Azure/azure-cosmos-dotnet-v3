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
    using Newtonsoft.Json.Linq;

    // Class that represents the resume value of a query. Primarily used to represent the resume value for order by query
    // The actual value is saved as a CosmosElement. Only native JSON types are supported. C* types are not supported.
    // Objects and Arrays are represented by their UInt128 hash value. All other types are represented by their actual value.
    // Example for Object and Array:
    //       {"type":"array", "low": 1000000000, "high": 8888888888}
    //       {"type":"object", "low": 1000000000, "high": 8888888888}
    internal class SqlQueryResumeValue : IComparable<CosmosElement>
    {
        private static class PropertyNames
        {
            public const string ArrayType = "array";
            public const string High = "high";
            public const string Low = "low";
            public const string ObjectType = "object";
            public const string Type = "type";
        }

        private static readonly CosmosElement EmptyObject = CosmosObject.Create(new Dictionary<string, CosmosElement>());

        private class UndefinedResumeValue : SqlQueryResumeValue
        {
            private static readonly UndefinedResumeValue Singelton = new UndefinedResumeValue();

            private UndefinedResumeValue() 
            {
            }

            public static UndefinedResumeValue Create()
            {
                return Singelton;
            }
        }

        private class NullResumeValue : SqlQueryResumeValue
        {
            private static readonly NullResumeValue Singelton = new NullResumeValue();

            private NullResumeValue() 
            { 
            }

            public static NullResumeValue Create()
            {
                return Singelton;
            }
        }

        private class BooleanResumeValue : SqlQueryResumeValue
        {
            private static readonly BooleanResumeValue True = new BooleanResumeValue(true);
            private static readonly BooleanResumeValue False = new BooleanResumeValue(false);

            public bool Value { get; }

            private BooleanResumeValue(bool value)
            {
                this.Value = value;
            }

            public static BooleanResumeValue Create(bool value)
            {
                return value ? True : False;
            }
        }

        private class NumberResumeValue : SqlQueryResumeValue
        {
            public CosmosNumber Value { get; }

            private NumberResumeValue(CosmosNumber value)
            {
                this.Value = value;
            }

            public static NumberResumeValue Create(CosmosNumber value)
            {
                return new NumberResumeValue(value);
            }
        }

        private class StringResumeValue : SqlQueryResumeValue
        {
            public CosmosString Value { get; }

            private StringResumeValue(CosmosString value)
            {
                this.Value = value;
            }

            public static StringResumeValue Create(CosmosString value)
            {
                return new StringResumeValue(value);
            }
        }

        private class ArrayResumeValue : SqlQueryResumeValue
        {
            public UInt128 HashValue { get; }

            private ArrayResumeValue(UInt128 hashValue)
            {
                this.HashValue = hashValue;
            }

            public static ArrayResumeValue Create(UInt128 hashValue)
            {
                return new ArrayResumeValue(hashValue);
            }

            public static ArrayResumeValue Create(CosmosArray arrayValue)
            {
                return Create(DistinctHash.GetHash(arrayValue));
            }
        }

        private class ObjectResumeValue : SqlQueryResumeValue
        {
            public UInt128 HashValue { get; }

            private ObjectResumeValue(UInt128 hashValue)
            {
                this.HashValue = hashValue;
            }

            public static ObjectResumeValue Create(UInt128 hashValue)
            {
                return new ObjectResumeValue(hashValue);
            }

            public static ObjectResumeValue Create(CosmosObject objectValue)
            {
                return Create(DistinctHash.GetHash(objectValue));
            }
        }

        // Method to compare a value represented as CosmosElement with the resume value.
        // Object and Array needs special handling as the resume value is a UInt128 hash.
        public int CompareTo(CosmosElement cosmosElement)
        {
            // Convert ResumeValue to CosmosElement and invoke ItemComparer to compare the cosmoselements
            switch (this)
            {
                case UndefinedResumeValue:
                    return ItemComparer.Instance.Compare(CosmosUndefined.Create(), cosmosElement);

                case NullResumeValue:
                    return ItemComparer.Instance.Compare(CosmosNull.Create(), cosmosElement);

                case BooleanResumeValue booleanValue:
                    return ItemComparer.Instance.Compare(CosmosBoolean.Create(booleanValue.Value), cosmosElement);

                case NumberResumeValue numberValue:
                    return ItemComparer.Instance.Compare(numberValue.Value, cosmosElement);

                case StringResumeValue stringValue:
                    return ItemComparer.Instance.Compare(stringValue.Value, cosmosElement);

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
                        return ItemComparer.Instance.Compare(EmptyObject, cosmosElement);
                    }
                }

                default:
                    throw new ArgumentException($"Invalid {nameof(SqlQueryResumeValue)} type.");
            }
        }

        // Utility method that converts SqlQueryResumeValue to CosmosElement which can then be serialized to string
        public static CosmosElement ToCosmosElement(SqlQueryResumeValue resumeValue)
        {
            return resumeValue switch
            {
                UndefinedResumeValue => CosmosArray.Empty,
                NullResumeValue => CosmosNull.Create(),
                BooleanResumeValue booleanValue => CosmosBoolean.Create(booleanValue.Value),
                NumberResumeValue numberValue => numberValue.Value,
                StringResumeValue stringValue => stringValue.Value,
                ArrayResumeValue arrayValue => CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { PropertyNames.Type, CosmosString.Create(PropertyNames.ArrayType) },
                        { PropertyNames.Low, CosmosNumber64.Create((long)arrayValue.HashValue.GetLow()) },
                        { PropertyNames.High, CosmosNumber64.Create((long)arrayValue.HashValue.GetHigh()) }
                    }),
                ObjectResumeValue objectValue => CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { PropertyNames.Type, CosmosString.Create(PropertyNames.ObjectType) },
                        { PropertyNames.Low, CosmosNumber64.Create((long)objectValue.HashValue.GetLow()) },
                        { PropertyNames.High, CosmosNumber64.Create((long)objectValue.HashValue.GetHigh()) }
                    }),
                _ => throw new ArgumentException($"Invalid {nameof(SqlQueryResumeValue)} type."),
            };
        }

        public static SqlQueryResumeValue FromCosmosElement(CosmosElement value)
        {
            return value.Accept(CosmosElementToResumeValueVisitor.Singleton);
        }

        // Generates the SqlQueryResumeValue given an exact orderby value from the query.
        // The orderby value is provided as CosmosElement.
        public static SqlQueryResumeValue FromOrderByValue(CosmosElement orderByValue)
        {
            return orderByValue.Accept(OrderByValueToResumeValueVisitor.Singleton);
        }

        // Serializer that gets called when serializing SqlQueryResumeValue to send to backend. 
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
                    serializer.Serialize(writer, stringValue.Value);
                    break;

                case ArrayResumeValue arrayValue:
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyName(PropertyNames.Type);
                        writer.WriteValue(PropertyNames.ArrayType);
                        writer.WritePropertyName(PropertyNames.Low);
                        writer.WriteValue((long)arrayValue.HashValue.GetLow());
                        writer.WritePropertyName(PropertyNames.High);
                        writer.WriteValue((long)arrayValue.HashValue.GetHigh());
                        writer.WriteEndObject();
                    }
                    break;

                case ObjectResumeValue objectValue:
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyName(PropertyNames.Type);
                        writer.WriteValue(PropertyNames.ObjectType);
                        writer.WritePropertyName(PropertyNames.Low);
                        writer.WriteValue((long)objectValue.HashValue.GetLow());
                        writer.WritePropertyName(PropertyNames.High);
                        writer.WriteValue((long)objectValue.HashValue.GetHigh());
                        writer.WriteEndObject();
                    }
                    break;

                default:
                    throw new ArgumentException($"Invalid {nameof(SqlQueryResumeValue)} type.");
            }
        }

        // Visitor to verify if the number type is supported by resume value. C* number types are not supported
        public sealed class SupportedResumeNumberTypeVisitor : ICosmosNumberVisitor<bool>
        {
            public static readonly SupportedResumeNumberTypeVisitor Singleton = new SupportedResumeNumberTypeVisitor();

            private SupportedResumeNumberTypeVisitor()
            {
            }

            public bool Visit(CosmosNumber64 cosmosNumber64)
            {
                return true;
            }

            public bool Visit(CosmosInt8 cosmosInt8)
            {
                return false;
            }

            public bool Visit(CosmosInt16 cosmosInt16)
            {
                return false;
            }

            public bool Visit(CosmosInt32 cosmosInt32)
            {
                return false;
            }

            public bool Visit(CosmosInt64 cosmosInt64)
            {
                return false;
            }

            public bool Visit(CosmosUInt32 cosmosUInt32)
            {
                return false;
            }
            public bool Visit(CosmosFloat32 cosmosFloat32)
            {
                return false;
            }

            public bool Visit(CosmosFloat64 cosmosFloat64)
            {
                return false;
            }
        }

        // Visitor to convert resume values that are represented as CosmosElement to ResumeValue
        // This is the inverse of ToCosmosElement method. The input for this is from the Client continuation token.
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

                return UndefinedResumeValue.Create();
            }

            public SqlQueryResumeValue Visit(CosmosBinary cosmosBinary)
            {
                throw new NotSupportedException($"Converting {nameof(CosmosBinary)} to {nameof(SqlQueryResumeValue)} is not supported");
            }

            public SqlQueryResumeValue Visit(CosmosBoolean cosmosBoolean)
            {
                return BooleanResumeValue.Create(cosmosBoolean.Value);
            }

            public SqlQueryResumeValue Visit(CosmosGuid cosmosGuid)
            {
                throw new NotSupportedException($"Converting {nameof(CosmosGuid)} to {nameof(SqlQueryResumeValue)} is not supported");
            }

            public SqlQueryResumeValue Visit(CosmosNull cosmosNull)
            {
                return NullResumeValue.Create();
            }

            public SqlQueryResumeValue Visit(CosmosUndefined cosmosUndefined)
            {
                return UndefinedResumeValue.Create();
            }

            public SqlQueryResumeValue Visit(CosmosNumber cosmosNumber)
            {
                bool bSupportedType = cosmosNumber.Accept(SupportedResumeNumberTypeVisitor.Singleton);
                if (!bSupportedType)
                {
                    throw new NotSupportedException($"Extended number types are not supported in SqlQueryResumeValue.");
                }

                return NumberResumeValue.Create(cosmosNumber);
            }

            public SqlQueryResumeValue Visit(CosmosObject cosmosObject)
            {
                if (!cosmosObject.TryGetValue(PropertyNames.Type, out CosmosString objectType)
                    || !cosmosObject.TryGetValue(PropertyNames.Low, out CosmosNumber64 lowValue)
                    || !cosmosObject.TryGetValue(PropertyNames.High, out CosmosNumber64 highValue))
                {
                    throw new ArgumentException($"Incorrect Array / Object Resume Value. One or more of the required properties are missing.");
                }

                if (string.Equals(objectType.Value, PropertyNames.ArrayType))
                {
                    return ArrayResumeValue.Create(
                        UInt128.Create(
                            (ulong)Number64.ToLong(lowValue.Value),
                            (ulong)Number64.ToLong(highValue.Value)));
                }
                else if (string.Equals(objectType.Value, PropertyNames.ObjectType))
                {
                    return ObjectResumeValue.Create(
                        UInt128.Create(
                            (ulong)Number64.ToLong(lowValue.Value),
                            (ulong)Number64.ToLong(highValue.Value)));
                }
                else
                {
                    throw new ArgumentException($"Incorrect value for {PropertyNames.Type} property. Value is {objectType.Value}.");
                }
            }

            public SqlQueryResumeValue Visit(CosmosString cosmosString)
            {
                return StringResumeValue.Create(cosmosString);
            }
        }

        // Visitor to convert OrderBy values received from the backend to SqlQueryResumeValue
        // OrderBy values from backend are represented as CosmosElement
        private sealed class OrderByValueToResumeValueVisitor : ICosmosElementVisitor<SqlQueryResumeValue>
        {
            public static readonly OrderByValueToResumeValueVisitor Singleton = new OrderByValueToResumeValueVisitor();

            private OrderByValueToResumeValueVisitor()
            {
            }

            public SqlQueryResumeValue Visit(CosmosArray cosmosArray)
            {
                return ArrayResumeValue.Create(cosmosArray);
            }

            public SqlQueryResumeValue Visit(CosmosBinary cosmosBinary)
            {
                throw new NotSupportedException($"Converting {nameof(CosmosBinary)} to {nameof(SqlQueryResumeValue)} is not supported");
            }

            public SqlQueryResumeValue Visit(CosmosBoolean cosmosBoolean)
            {
                return BooleanResumeValue.Create(cosmosBoolean.Value);
            }

            public SqlQueryResumeValue Visit(CosmosGuid cosmosGuid)
            {
                throw new NotSupportedException($"Converting {nameof(CosmosGuid)} to {nameof(SqlQueryResumeValue)} is not supported");
            }

            public SqlQueryResumeValue Visit(CosmosNull cosmosNull)
            {
                return NullResumeValue.Create();
            }

            public SqlQueryResumeValue Visit(CosmosUndefined cosmosUndefined)
            {
                return UndefinedResumeValue.Create();
            }

            public SqlQueryResumeValue Visit(CosmosNumber cosmosNumber)
            {
                bool bSupportedType = cosmosNumber.Accept(SupportedResumeNumberTypeVisitor.Singleton);
                if (!bSupportedType)
                {
                    throw new NotSupportedException($"Extended number types are not supported in SqlQueryResumeValue.");
                }

                return NumberResumeValue.Create(cosmosNumber);
            }

            public SqlQueryResumeValue Visit(CosmosObject cosmosObject)
            {
                return ObjectResumeValue.Create(cosmosObject);
            }

            public SqlQueryResumeValue Visit(CosmosString cosmosString)
            {
                return StringResumeValue.Create(cosmosString);
            }
        }
    }
}
