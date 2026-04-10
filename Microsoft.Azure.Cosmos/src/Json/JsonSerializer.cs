// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal static class JsonSerializer
    {
        public static ReadOnlyMemory<byte> Serialize(
            object value,
            JsonSerializationFormat jsonSerializationFormat = JsonSerializationFormat.Text)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat);
            JsonSerializer.SerializeInternal(value, jsonWriter);
            return jsonWriter.GetResult();
        }

        public static void SerializeInternal(
            object value,
            IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException(nameof(jsonWriter));
            }

            switch (value)
            {
                case null:
                    jsonWriter.WriteNullValue();
                    break;

                case bool boolValue:
                    jsonWriter.WriteBoolValue(boolValue);
                    break;

                case string stringValue:
                    jsonWriter.WriteStringValue(stringValue);
                    break;

                case Number64 numberValue:
                    jsonWriter.WriteNumberValue(numberValue);
                    break;

                case sbyte signedByteValue:
                    jsonWriter.WriteInt8Value(signedByteValue);
                    break;

                case short shortValue:
                    jsonWriter.WriteInt16Value(shortValue);
                    break;

                case int intValue:
                    jsonWriter.WriteInt32Value(intValue);
                    break;

                case long longValue:
                    jsonWriter.WriteInt64Value(longValue);
                    break;

                case uint uintValue:
                    jsonWriter.WriteUInt32Value(uintValue);
                    break;

                case float floatValue:
                    jsonWriter.WriteFloat32Value(floatValue);
                    break;

                case double doubleValue:
                    jsonWriter.WriteFloat64Value(doubleValue);
                    break;

                case ReadOnlyMemory<byte> binaryValue:
                    jsonWriter.WriteBinaryValue(binaryValue.Span);
                    break;

                case Guid guidValue:
                    jsonWriter.WriteGuidValue(guidValue);
                    break;

                case IEnumerable enumerableValue:
                    jsonWriter.WriteArrayStart();

                    foreach (object arrayItem in enumerableValue)
                    {
                        JsonSerializer.SerializeInternal(arrayItem, jsonWriter);
                    }

                    jsonWriter.WriteArrayEnd();
                    break;

                case CosmosElement cosmosElementValue:
                    cosmosElementValue.WriteTo(jsonWriter);
                    break;

                case ValueType valueType:
                    throw new ArgumentOutOfRangeException($"Unable to serialize type: {valueType.GetType()}");

                default:
                    Type type = value.GetType();
                    PropertyInfo[] properties = type.GetProperties();

                    jsonWriter.WriteObjectStart();

                    foreach (PropertyInfo propertyInfo in properties)
                    {
                        jsonWriter.WriteFieldName(propertyInfo.Name);
                        object propertyValue = propertyInfo.GetValue(value);
                        JsonSerializer.SerializeInternal(propertyValue, jsonWriter);
                    }

                    jsonWriter.WriteObjectEnd();
                    break;
            }
        }

        public static T Deserialize<T>(ReadOnlyMemory<byte> buffer)
        {
            TryCatch<T> tryDeserialize = JsonSerializer.Monadic.Deserialize<T>(buffer);
            tryDeserialize.ThrowIfFailed();
            return tryDeserialize.Result;
        }

        public static class Monadic
        {
            public static TryCatch<T> Deserialize<T>(ReadOnlyMemory<byte> buffer)
            {
                TryCatch<CosmosElement> tryCreateFromBuffer = CosmosElement.Monadic.CreateFromBuffer(buffer);
                if (tryCreateFromBuffer.Failed)
                {
                    return TryCatch<T>.FromException(tryCreateFromBuffer.Exception);
                }

                CosmosElement cosmosElement = tryCreateFromBuffer.Result;
                TryCatch<object> tryAcceptVisitor = cosmosElement.Accept(DeserializationVisitor.Singleton, typeof(T));
                if (tryAcceptVisitor.Failed)
                {
                    return TryCatch<T>.FromException(tryAcceptVisitor.Exception);
                }

                if (!(tryAcceptVisitor.Result is T typedResult))
                {
                    Type type = typeof(T);
                    if ((tryAcceptVisitor.Result is null) && (!type.IsValueType || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))))
                    {
                        return TryCatch<T>.FromResult(default);
                    }

                    // string needs to be handle differently since JsonReader return UtfAnyString instead of string.
                    if (type == typeof(string))
                    {
                        return TryCatch<T>.FromResult((T)(object)tryAcceptVisitor.Result.ToString());
                    }

                    throw new InvalidOperationException("Could not cast to T.");
                }

                return TryCatch<T>.FromResult(typedResult);
            }
        }

        public static bool TryDeserialize<T>(ReadOnlyMemory<byte> buffer, out T result)
        {
            TryCatch<T> tryDeserialize = JsonSerializer.Monadic.Deserialize<T>(buffer);
            return TryCatch<T>.ConvertToTryGet<T>(tryDeserialize, out result);
        }

        private sealed class DeserializationVisitor : ICosmosElementVisitor<Type, TryCatch<object>>
        {
            public static readonly DeserializationVisitor Singleton = new DeserializationVisitor();

            private static class Exceptions
            {
                public static readonly CosmosElementWrongTypeException ExpectedArray = new CosmosElementWrongTypeException(
                    message: $"Expected return type of '{nameof(IReadOnlyList<object>)}'.");

                public static readonly CosmosElementWrongTypeException ExpectedBoolean = new CosmosElementWrongTypeException(
                    message: $"Expected return type of '{typeof(bool)}'.");

                public static readonly CosmosElementWrongTypeException ExpectedBinary = new CosmosElementWrongTypeException(
                    message: $"Expected return type of '{typeof(ReadOnlyMemory<byte>)}'.");

                public static readonly CosmosElementWrongTypeException ExpectedGuid = new CosmosElementWrongTypeException(
                    message: $"Expected return type of '{typeof(Guid)}'.");

                public static readonly CosmosElementWrongTypeException ExpectedNumber = new CosmosElementWrongTypeException(
                    message: "Expected return type of number.");

                public static readonly CosmosElementWrongTypeException ExpectedReferenceOrNullableType = new CosmosElementWrongTypeException(
                    message: "Expected return type to be a reference or nullable type.");

                public static readonly CosmosElementWrongTypeException ExpectedString = new CosmosElementWrongTypeException(
                    message: $"Expected return type of '{typeof(string)}'.");

                public static readonly CosmosElementWrongTypeException UnexpectedUndefined = new CosmosElementWrongTypeException(
                    message: $"Did not expect to encounter '{typeof(CosmosUndefined)}'.");
            }

            private static class BoxedValues
            {
                public static readonly object True = true;
                public static readonly object False = false;
                public static readonly object Null = null;
            }

            private DeserializationVisitor()
            {
            }

            public TryCatch<object> Visit(CosmosArray cosmosArray, Type type)
            {
                bool isReadOnlyList = type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>));
                if (!isReadOnlyList)
                {
                    return TryCatch<object>.FromException(DeserializationVisitor.Exceptions.ExpectedArray);
                }

                Type genericArgumentType = type.GenericTypeArguments.First();

                Type listType = typeof(List<>).MakeGenericType(genericArgumentType);
                IList list = (IList)Activator.CreateInstance(listType);

                foreach (CosmosElement arrayItem in cosmosArray)
                {
                    TryCatch<object> tryGetMaterializedArrayItem;
                    if (genericArgumentType == typeof(object))
                    {
                        Type dotNetType = arrayItem switch
                        {
                            CosmosArray _ => typeof(IReadOnlyList<object>),
                            CosmosBoolean _ => typeof(bool),
                            CosmosNull _ => typeof(object),
                            CosmosNumber _ => typeof(Number64),
                            CosmosObject _ => typeof(object),
                            CosmosString _ => typeof(string),
                            CosmosGuid _ => typeof(Guid),
                            CosmosBinary _ => typeof(ReadOnlyMemory<byte>),
                            CosmosUndefined _ => typeof(object),
                            _ => throw new ArgumentOutOfRangeException($"Unknown cosmos element type."),
                        };

                        tryGetMaterializedArrayItem = arrayItem.Accept(this, dotNetType);
                    }
                    else
                    {
                        tryGetMaterializedArrayItem = arrayItem.Accept(this, genericArgumentType);
                    }

                    if (tryGetMaterializedArrayItem.Failed)
                    {
                        return tryGetMaterializedArrayItem;
                    }

                    list.Add(tryGetMaterializedArrayItem.Result);
                }

                return TryCatch<object>.FromResult(list);
            }

            public TryCatch<object> Visit(CosmosBinary cosmosBinary, Type type)
            {
                if (type != typeof(ReadOnlyMemory<byte>))
                {
                    return TryCatch<object>.FromException(DeserializationVisitor.Exceptions.ExpectedBinary);
                }

                return TryCatch<object>.FromResult(cosmosBinary.Value);
            }

            public TryCatch<object> Visit(CosmosBoolean cosmosBoolean, Type type)
            {
                if (type != typeof(bool))
                {
                    return TryCatch<object>.FromException(DeserializationVisitor.Exceptions.ExpectedBoolean);
                }

                return TryCatch<object>.FromResult(cosmosBoolean.Value ? DeserializationVisitor.BoxedValues.True : DeserializationVisitor.BoxedValues.False);
            }

            public TryCatch<object> Visit(CosmosGuid cosmosGuid, Type type)
            {
                if (type != typeof(Guid))
                {
                    return TryCatch<object>.FromException(DeserializationVisitor.Exceptions.ExpectedGuid);
                }

                return TryCatch<object>.FromResult(cosmosGuid.Value);
            }

            public TryCatch<object> Visit(CosmosNull cosmosNull, Type type)
            {
                if (type.IsValueType && !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)))
                {
                    return TryCatch<object>.FromException(DeserializationVisitor.Exceptions.ExpectedReferenceOrNullableType);
                }

                return TryCatch<object>.FromResult(default);
            }

            public TryCatch<object> Visit(CosmosUndefined cosmosUndefined, Type type)
            {
                return TryCatch<object>.FromException(DeserializationVisitor.Exceptions.UnexpectedUndefined);
            }

            public TryCatch<object> Visit(CosmosNumber cosmosNumber, Type type)
            {
                if (type == typeof(Number64))
                {
                    return TryCatch<object>.FromResult(cosmosNumber.Value);
                }

                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Byte:
                        {
                            if (!cosmosNumber.Value.IsInteger)
                            {
                                return TryCatch<object>.FromException(
                                    new CosmosElementWrongTypeException("Expected integral type for byte."));
                            }

                            long value = Number64.ToLong(cosmosNumber.Value);
                            if ((value < byte.MinValue) || (value > byte.MaxValue))
                            {
                                return TryCatch<object>.FromException(
                                    new OverflowException($"{value} was out of range for byte."));
                            }

                            return TryCatch<object>.FromResult((byte)value);
                        }

                    case TypeCode.Decimal:
                        {
                            decimal value;
                            if (cosmosNumber.Value.IsDouble)
                            {
                                value = (decimal)Number64.ToDouble(cosmosNumber.Value);
                            }
                            else
                            {
                                value = Number64.ToLong(cosmosNumber.Value);
                            }

                            return TryCatch<object>.FromResult(value);
                        }

                    case TypeCode.Double:
                        {
                            if (!cosmosNumber.Value.IsDouble)
                            {
                                return TryCatch<object>.FromException(
                                    new CosmosElementWrongTypeException("Expected floating point type for double."));
                            }

                            double value = Number64.ToDouble(cosmosNumber.Value);
                            return TryCatch<object>.FromResult(value);
                        }

                    case TypeCode.Int16:
                        {
                            if (!cosmosNumber.Value.IsInteger)
                            {
                                return TryCatch<object>.FromException(
                                    new CosmosElementWrongTypeException("Expected integral type for short."));
                            }

                            long value = Number64.ToLong(cosmosNumber.Value);
                            if ((value < short.MinValue) || (value > short.MaxValue))
                            {
                                return TryCatch<object>.FromException(
                                    new OverflowException($"{value} was out of range for short."));
                            }

                            return TryCatch<object>.FromResult((short)value);
                        }

                    case TypeCode.Int32:
                        {
                            if (!cosmosNumber.Value.IsInteger)
                            {
                                return TryCatch<object>.FromException(
                                    new CosmosElementWrongTypeException("Expected integral type for int."));
                            }

                            long value = Number64.ToLong(cosmosNumber.Value);
                            if ((value < int.MinValue) || (value > int.MaxValue))
                            {
                                return TryCatch<object>.FromException(
                                    new OverflowException($"{value} was out of range for int."));
                            }

                            return TryCatch<object>.FromResult((int)value);
                        }

                    case TypeCode.Int64:
                        {
                            if (!cosmosNumber.Value.IsInteger)
                            {
                                return TryCatch<object>.FromException(
                                    new CosmosElementWrongTypeException("Expected integral type for long."));
                            }

                            long value = Number64.ToLong(cosmosNumber.Value);
                            return TryCatch<object>.FromResult(value);
                        }

                    case TypeCode.SByte:
                        {
                            if (!cosmosNumber.Value.IsInteger)
                            {
                                return TryCatch<object>.FromException(
                                    new CosmosElementWrongTypeException("Expected integral type for sbyte."));
                            }

                            long value = Number64.ToLong(cosmosNumber.Value);
                            if ((value < sbyte.MinValue) || (value > sbyte.MaxValue))
                            {
                                return TryCatch<object>.FromException(
                                    new OverflowException($"{value} was out of range for sbyte."));
                            }

                            return TryCatch<object>.FromResult((sbyte)value);
                        }

                    case TypeCode.Single:
                        {
                            if (!cosmosNumber.Value.IsDouble)
                            {
                                return TryCatch<object>.FromException(
                                    new CosmosElementWrongTypeException("Expected floating point type for float."));
                            }

                            double value = Number64.ToDouble(cosmosNumber.Value);
                            if ((value < float.MinValue) || (value > float.MaxValue))
                            {
                                return TryCatch<object>.FromException(
                                    new OverflowException($"{value} was out of range for float."));
                            }

                            return TryCatch<object>.FromResult((float)value);
                        }

                    case TypeCode.UInt16:
                        {
                            if (!cosmosNumber.Value.IsInteger)
                            {
                                return TryCatch<object>.FromException(
                                    new CosmosElementWrongTypeException("Expected integral type for ushort."));
                            }

                            long value = Number64.ToLong(cosmosNumber.Value);
                            if ((value < ushort.MinValue) || (value > ushort.MaxValue))
                            {
                                return TryCatch<object>.FromException(
                                    new OverflowException($"{value} was out of range for ushort."));
                            }

                            return TryCatch<object>.FromResult((ushort)value);
                        }

                    case TypeCode.UInt32:
                        {
                            if (!cosmosNumber.Value.IsInteger)
                            {
                                return TryCatch<object>.FromException(
                                    new CosmosElementWrongTypeException("Expected integral type for uint."));
                            }

                            long value = Number64.ToLong(cosmosNumber.Value);
                            if ((value < uint.MinValue) || (value > uint.MaxValue))
                            {
                                return TryCatch<object>.FromException(
                                    new OverflowException($"{value} was out of range for uint."));
                            }

                            return TryCatch<object>.FromResult((uint)value);
                        }

                    case TypeCode.UInt64:
                        {
                            if (!cosmosNumber.Value.IsInteger)
                            {
                                return TryCatch<object>.FromException(
                                    new CosmosElementWrongTypeException("Expected integral type for ulong."));
                            }

                            long value = Number64.ToLong(cosmosNumber.Value);
                            return TryCatch<object>.FromResult((ulong)value);
                        }

                    default:
                        throw new ArgumentOutOfRangeException($"Unknown {nameof(TypeCode)}: {Type.GetTypeCode(type)}.");
                }
            }

            public TryCatch<object> Visit(CosmosObject cosmosObject, Type type)
            {
                ConstructorInfo[] constructors = type.GetConstructors();
                if (constructors.Length == 0)
                {
                    return TryCatch<object>.FromException(
                        new CosmosElementNoPubliclyAccessibleConstructorException(
                            message: $"Could not find publicly accessible constructors for type: {type.FullName}."));
                }

                if (constructors.Length > 1)
                {
                    return TryCatch<object>.FromException(
                        new CosmosElementCouldNotDetermineWhichConstructorToUseException(
                            message: $"Could not determine which constructor to use for type: {type.FullName}."));
                }

                ConstructorInfo constructor = constructors.First();
                ParameterInfo[] parameters = constructor.GetParameters();
                List<object> parameterValues = new List<object>();
                foreach (ParameterInfo parameter in parameters)
                {
                    if (!cosmosObject.TryGetValue(parameter.Name, out CosmosElement rawParameterValue))
                    {
                        return TryCatch<object>.FromException(
                            new CosmosElementFailedToFindPropertyException(
                                message: $"Could not find property: '{parameter.Name}'."));
                    }

                    TryCatch<object> tryGetMaterializedParameterValue = rawParameterValue.Accept(this, parameter.ParameterType);
                    if (tryGetMaterializedParameterValue.Failed)
                    {
                        return TryCatch<object>.FromException(tryGetMaterializedParameterValue.Exception);
                    }

                    parameterValues.Add(tryGetMaterializedParameterValue.Result);
                }

                object instance;
                try
                {
                    instance = constructor.Invoke(parameterValues.ToArray());
                }
                catch (Exception ex)
                {
                    return TryCatch<object>.FromException(ex);
                }

                return TryCatch<object>.FromResult(instance);
            }

            public TryCatch<object> Visit(CosmosString cosmosString, Type type)
            {
                if (type != typeof(string))
                {
                    return TryCatch<object>.FromException(DeserializationVisitor.Exceptions.ExpectedString);
                }

                return TryCatch<object>.FromResult(cosmosString.Value.ToString());
            }
        }

        private sealed class ReadOnlyListWrapper<T> : IReadOnlyList<T>
        {
            private readonly IList<T> list;

            public ReadOnlyListWrapper(IList<T> list)
            {
                this.list = list ?? throw new ArgumentNullException(nameof(list));
            }

            public T this[int index] => this.list[index];

            public int Count => this.list.Count;

            public IEnumerator<T> GetEnumerator()
            {
                return this.list.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.list.GetEnumerator();
            }
        }
    }
}
