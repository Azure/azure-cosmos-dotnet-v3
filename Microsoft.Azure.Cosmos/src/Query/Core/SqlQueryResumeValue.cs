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

        private class PrimitiveResumeValue : SqlQueryResumeValue
        {
            public CosmosElement Value { get; }

            private PrimitiveResumeValue(CosmosElement value)
            {
                this.Value = value;
            }

            public static PrimitiveResumeValue Create(CosmosElement value)
            {
                return value switch
                {
                    CosmosNull or CosmosBoolean or CosmosNumber or CosmosString => new PrimitiveResumeValue(value),
                    _ => throw new ArgumentException("Non primitive value passed to PrimitiveResumeValue"),
                };
            }
        }

        private class ComplexResumeValue : SqlQueryResumeValue
        {
            public bool IsArray { get; }

            public UInt128 HashValue { get; }

            private ComplexResumeValue(bool isArray, UInt128 hashValue)
            {
                this.IsArray = isArray;
                this.HashValue = hashValue;
            }

            public static ComplexResumeValue Create(bool isArray, UInt128 hashValue)
            {
                return new ComplexResumeValue(isArray, hashValue);
            }

            public static ComplexResumeValue Create(CosmosArray arrayValue)
            {
                return Create(isArray: true, DistinctHash.GetHash(arrayValue));
            }

            public static ComplexResumeValue Create(CosmosObject objectValue)
            {
                return Create(isArray: false, DistinctHash.GetHash(objectValue));
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

                case PrimitiveResumeValue primitiveResumeValue:
                    return ItemComparer.Instance.Compare(primitiveResumeValue.Value, cosmosElement);

                case ComplexResumeValue complexResumeValue:
                {
                    if (complexResumeValue.IsArray)
                    {
                        // If the order by result is also of array type, then compare the hash values
                        // For other types create an empty array and call CosmosElement comparer which
                        // will take care of ordering based on types.
                        if (cosmosElement is CosmosArray arrayResult)
                        {
                            return UInt128BinaryComparer.Singleton.Compare(complexResumeValue.HashValue, DistinctHash.GetHash(arrayResult));
                        }
                        else
                        {
                            return ItemComparer.Instance.Compare(CosmosArray.Empty, cosmosElement);
                        }

                    }
                    else
                    {
                        // If the order by result is also of object type, then compare the hash values
                        // For other types create an empty object and call CosmosElement comparer which
                        // will take care of ordering based on types.
                        if (cosmosElement is CosmosObject objectResult)
                        {
                            // same type so compare the hash values
                            return UInt128BinaryComparer.Singleton.Compare(complexResumeValue.HashValue, DistinctHash.GetHash(objectResult));
                        }
                        else
                        {
                            return ItemComparer.Instance.Compare(EmptyObject, cosmosElement);
                        }
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
                PrimitiveResumeValue primiteResumeValue => primiteResumeValue.Value,
                ComplexResumeValue complexResumeValue => CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { PropertyNames.Type, CosmosString.Create(complexResumeValue.IsArray ? PropertyNames.ArrayType : PropertyNames.ObjectType) },
                        { PropertyNames.Low, CosmosNumber64.Create((long)complexResumeValue.HashValue.GetLow()) },
                        { PropertyNames.High, CosmosNumber64.Create((long)complexResumeValue.HashValue.GetHigh()) }
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

                case PrimitiveResumeValue primitiveResumeValue:
                    serializer.Serialize(writer, primitiveResumeValue.Value);
                    break;

                case ComplexResumeValue complexResumeValue:
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyName(PropertyNames.Type);
                        writer.WriteValue(complexResumeValue.IsArray ? PropertyNames.ArrayType : PropertyNames.ObjectType);
                        writer.WritePropertyName(PropertyNames.Low);
                        writer.WriteValue((long)complexResumeValue.HashValue.GetLow());
                        writer.WritePropertyName(PropertyNames.High);
                        writer.WriteValue((long)complexResumeValue.HashValue.GetHigh());
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
                return PrimitiveResumeValue.Create(cosmosBoolean);
            }

            public SqlQueryResumeValue Visit(CosmosGuid cosmosGuid)
            {
                throw new NotSupportedException($"Converting {nameof(CosmosGuid)} to {nameof(SqlQueryResumeValue)} is not supported");
            }

            public SqlQueryResumeValue Visit(CosmosNull cosmosNull)
            {
                return PrimitiveResumeValue.Create(cosmosNull);
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

                return PrimitiveResumeValue.Create(cosmosNumber);
            }

            public SqlQueryResumeValue Visit(CosmosObject cosmosObject)
            {
                if (!cosmosObject.TryGetValue(PropertyNames.Type, out CosmosString objectType)
                    || !cosmosObject.TryGetValue(PropertyNames.Low, out CosmosNumber64 lowValue)
                    || !cosmosObject.TryGetValue(PropertyNames.High, out CosmosNumber64 highValue))
                {
                    throw new ArgumentException($"Incorrect Array / Object Resume Value. One or more of the required properties are missing.");
                }

                UInt128 hashValue = UInt128.Create(
                            (ulong)Number64.ToLong(lowValue.Value),
                            (ulong)Number64.ToLong(highValue.Value));

                if (string.Equals(objectType.Value, PropertyNames.ArrayType))
                {
                    return ComplexResumeValue.Create(isArray: true, hashValue);
                }
                else if (string.Equals(objectType.Value, PropertyNames.ObjectType))
                {
                    return ComplexResumeValue.Create(isArray: false, hashValue);
                }
                else
                {
                    throw new ArgumentException($"Incorrect value for {PropertyNames.Type} property. Value is {objectType.Value}.");
                }
            }

            public SqlQueryResumeValue Visit(CosmosString cosmosString)
            {
                return PrimitiveResumeValue.Create(cosmosString);
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
                return ComplexResumeValue.Create(cosmosArray);
            }

            public SqlQueryResumeValue Visit(CosmosBinary cosmosBinary)
            {
                throw new NotSupportedException($"Converting {nameof(CosmosBinary)} to {nameof(SqlQueryResumeValue)} is not supported");
            }

            public SqlQueryResumeValue Visit(CosmosBoolean cosmosBoolean)
            {
                return PrimitiveResumeValue.Create(cosmosBoolean);
            }

            public SqlQueryResumeValue Visit(CosmosGuid cosmosGuid)
            {
                throw new NotSupportedException($"Converting {nameof(CosmosGuid)} to {nameof(SqlQueryResumeValue)} is not supported");
            }

            public SqlQueryResumeValue Visit(CosmosNull cosmosNull)
            {
                return PrimitiveResumeValue.Create(cosmosNull);
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

                return PrimitiveResumeValue.Create(cosmosNumber);
            }

            public SqlQueryResumeValue Visit(CosmosObject cosmosObject)
            {
                return ComplexResumeValue.Create(cosmosObject);
            }

            public SqlQueryResumeValue Visit(CosmosString cosmosString)
            {
                return PrimitiveResumeValue.Create(cosmosString);
            }
        }
    }
}
