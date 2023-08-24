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
            public const string ArrayType = "array";
            public const string High = "high";
            public const string Low = "low";
            public const string ObjectType = "object";
            public const string Type = "type";
        }

        private SqlQueryResumeValue(CosmosElement element)
        {
            this.resumeValue = element;
        }

        private readonly CosmosElement resumeValue;

        public int CompareTo(CosmosElement cosmosElement)
        {
            switch (this.resumeValue)
            {
                case CosmosUndefined:
                case CosmosNull:
                case CosmosBoolean:
                case CosmosNumber:
                case CosmosString:
                    return ItemComparer.Instance.Compare(this.resumeValue, cosmosElement);

                case CosmosObject:
                {
                    CosmosObject cosmosObject = (CosmosObject)this.resumeValue;

                    if (!cosmosObject.TryGetValue(PropertyNames.Type, out CosmosString objectType)
                        || !cosmosObject.TryGetValue(PropertyNames.Low, out CosmosNumber64 lowValue)
                        || !cosmosObject.TryGetValue(PropertyNames.High, out CosmosNumber64 highValue))
                    {
                        throw new ArgumentException($"Incorrect Array / Object Resume Value. One or more of the required properties are missing.");
                    }

                    UInt128 hashValue = UInt128.Create((ulong)Number64.ToLong(lowValue.Value), (ulong)Number64.ToLong(highValue.Value));
                    if (string.Equals(objectType.Value, PropertyNames.ArrayType))
                    {
                        // If the order by result is also of array type, then compare the hash values
                        // For other types create an empty array and call CosmosElement comparer which
                        // will take care of ordering based on types.
                        if (cosmosElement is CosmosArray arrayResult)
                        {
                            return UInt128BinaryComparer.Singleton.Compare(hashValue, DistinctHash.GetHash(arrayResult));
                        }
                        else
                        {
                            return ItemComparer.Instance.Compare(CosmosArray.Empty, cosmosElement);
                        }
                    }
                    else if (string.Equals(objectType.Value, PropertyNames.ObjectType))
                    {
                        // If the order by result is also of object type, then compare the hash values
                        // For other types create an empty object and call CosmosElement comparer which
                        // will take care of ordering based on types.
                        if (cosmosElement is CosmosObject objectResult)
                        {
                            // same type so compare the hash values
                            return UInt128BinaryComparer.Singleton.Compare(hashValue, DistinctHash.GetHash(objectResult));
                        }
                        else
                        {
                            return ItemComparer.Instance.Compare(CosmosObject.Create(new Dictionary<string, CosmosElement>()), cosmosElement);
                        }
                    }
                    else
                    {
                        throw new ArgumentException($"Incorrect value for {PropertyNames.Type} property. Value is {objectType.Value}.");
                    }
                }

                default:
                    throw new ArgumentException($"Invalid {nameof(SqlQueryResumeValue)} type.");
            }
        }

        // Utility method that converts SqlQueryResumeValue to CosmosElement which can then be serialized to string
        public static CosmosElement ToCosmosElement(SqlQueryResumeValue resumeValue)
        {
            return resumeValue.resumeValue switch
            {
                CosmosUndefined => CosmosArray.Create(new List<CosmosElement>()),
                CosmosBoolean or CosmosNull or CosmosNumber or CosmosString or CosmosObject => resumeValue.resumeValue,
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
            switch (value.resumeValue)
            {
                case CosmosUndefined:
                    writer.WriteStartArray();
                    writer.WriteEndArray();
                    break;

                case CosmosNull:
                    writer.WriteNull();
                    break;

                case CosmosBoolean booleanValue:
                    serializer.Serialize(writer, booleanValue.Value);
                    break;

                case CosmosNumber numberValue:
                    serializer.Serialize(writer, numberValue.Value);
                    break;

                case CosmosString stringValue:
                    serializer.Serialize(writer, stringValue.Value.ToString());
                    break;

                case CosmosObject objectValue:
                    serializer.Serialize(writer, objectValue);
                    break;

                default:
                    throw new ArgumentException($"Invalid {nameof(SqlQueryResumeValue)} type.");
            }
        }

        // Visitor to convert resume values that are represented as CosmosElement to ResumeValue
        // This is the inverse of ToCosmosElement method
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

                return new SqlQueryResumeValue(CosmosUndefined.Create());
            }

            public SqlQueryResumeValue Visit(CosmosBinary cosmosBinary)
            {
                throw new NotSupportedException($"Converting {nameof(CosmosBinary)} to {nameof(SqlQueryResumeValue)} is not supported");
            }

            public SqlQueryResumeValue Visit(CosmosBoolean cosmosBoolean)
            {
                return new SqlQueryResumeValue(cosmosBoolean);
            }

            public SqlQueryResumeValue Visit(CosmosGuid cosmosGuid)
            {
                throw new NotSupportedException($"Converting {nameof(CosmosGuid)} to {nameof(SqlQueryResumeValue)} is not supported");
            }

            public SqlQueryResumeValue Visit(CosmosNull cosmosNull)
            {
                return new SqlQueryResumeValue(cosmosNull);
            }

            public SqlQueryResumeValue Visit(CosmosUndefined cosmosUndefined)
            {
                return new SqlQueryResumeValue(cosmosUndefined);
            }

            public SqlQueryResumeValue Visit(CosmosNumber cosmosNumber)
            {
                return new SqlQueryResumeValue(cosmosNumber);
            }

            public SqlQueryResumeValue Visit(CosmosObject cosmosObject)
            {
                // Validate if the object is in the expected format
                if (!cosmosObject.TryGetValue(PropertyNames.Type, out CosmosString objectType)
                    || !cosmosObject.TryGetValue(PropertyNames.Low, out CosmosNumber64 _)
                    || !cosmosObject.TryGetValue(PropertyNames.High, out CosmosNumber64 _))
                {
                    throw new ArgumentException($"Incorrect Array / Object Resume Value. One or more of the required properties are missing.");
                }

                if (!string.Equals(objectType.Value, PropertyNames.ArrayType) && !string.Equals(objectType.Value, PropertyNames.ObjectType))
                {
                    throw new ArgumentException($"Incorrect value for {PropertyNames.Type} property. Value is {objectType.Value}.");
                }

                return new SqlQueryResumeValue(cosmosObject);
            }

            public SqlQueryResumeValue Visit(CosmosString cosmosString)
            {
                return new SqlQueryResumeValue(cosmosString);
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
                UInt128 hashValue = DistinctHash.GetHash(cosmosArray);
                return new SqlQueryResumeValue(CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { PropertyNames.Type, CosmosString.Create(PropertyNames.ArrayType) },
                        { PropertyNames.Low, CosmosNumber64.Create((long)hashValue.GetLow()) },
                        { PropertyNames.High, CosmosNumber64.Create((long)hashValue.GetHigh()) }
                    }));
            }

            public SqlQueryResumeValue Visit(CosmosBinary cosmosBinary)
            {
                throw new NotSupportedException($"Converting {nameof(CosmosBinary)} to {nameof(SqlQueryResumeValue)} is not supported");
            }

            public SqlQueryResumeValue Visit(CosmosBoolean cosmosBoolean)
            {
                return new SqlQueryResumeValue(cosmosBoolean);
            }

            public SqlQueryResumeValue Visit(CosmosGuid cosmosGuid)
            {
                throw new NotSupportedException($"Converting {nameof(CosmosGuid)} to {nameof(SqlQueryResumeValue)} is not supported");
            }

            public SqlQueryResumeValue Visit(CosmosNull cosmosNull)
            {
                return new SqlQueryResumeValue(cosmosNull);
            }

            public SqlQueryResumeValue Visit(CosmosUndefined cosmosUndefined)
            {
                return new SqlQueryResumeValue(cosmosUndefined);
            }

            public SqlQueryResumeValue Visit(CosmosNumber cosmosNumber)
            {
                return new SqlQueryResumeValue(cosmosNumber);
            }

            public SqlQueryResumeValue Visit(CosmosObject cosmosObject)
            {
                UInt128 hashValue = DistinctHash.GetHash(cosmosObject);
                return new SqlQueryResumeValue(CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { PropertyNames.Type, CosmosString.Create(PropertyNames.ObjectType) },
                        { PropertyNames.Low, CosmosNumber64.Create((long)hashValue.GetLow()) },
                        { PropertyNames.High, CosmosNumber64.Create((long)hashValue.GetHigh()) }
                    }));
            }

            public SqlQueryResumeValue Visit(CosmosString cosmosString)
            {
                return new SqlQueryResumeValue(cosmosString);
            }
        }
    }
}
