// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal static class CosmosElementDeserializer
    {
        public static T Deserialize<T>(ReadOnlyMemory<byte> buffer)
        {
            TryCatch<T> tryDeserialize = CosmosElementDeserializer.Monadic.Deserialize<T>(buffer);
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
                TryCatch<object> tryAcceptVisitor = cosmosElement.Accept(Visitor.Singleton, typeof(T));
                if (tryAcceptVisitor.Failed)
                {
                    return TryCatch<T>.FromException(tryAcceptVisitor.Exception);
                }

                return TryCatch<T>.FromResult((T)tryAcceptVisitor.Result);
            }
        }

        public static bool TryDeserialize<T>(ReadOnlyMemory<byte> buffer, out T result)
        {
            TryCatch<T> tryDeserialize = CosmosElementDeserializer.Monadic.Deserialize<T>(buffer);
            return TryCatch<T>.ConvertToTryGet<T>(tryDeserialize, out result);
        }

        private sealed class Visitor : ICosmosElementVisitor<Type, TryCatch<object>>
        {
            public static readonly Visitor Singleton = new Visitor();

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
            }

            private static class BoxedValues
            {
                public static readonly object True = true;
                public static readonly object False = false;
                public static readonly object Null = null;
            }

            private Visitor()
            {
            }

            public TryCatch<object> Visit(CosmosArray cosmosArray, Type type)
            {
                bool isReadOnlyList = type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>));
                if (!isReadOnlyList)
                {
                    return TryCatch<object>.FromException(Visitor.Exceptions.ExpectedArray);
                }

                Type genericArgumentType = type.GenericTypeArguments.First();

                Type listType = typeof(List<>).MakeGenericType(genericArgumentType);
                IList list = (IList)Activator.CreateInstance(listType);

                foreach (CosmosElement arrayItem in cosmosArray)
                {
                    TryCatch<object> tryGetMaterializedArrayItem;
                    if (genericArgumentType == typeof(object))
                    {
                        Type dotNetType = arrayItem.Type switch
                        {
                            CosmosElementType.Array => typeof(IReadOnlyList<object>),
                            CosmosElementType.Boolean => typeof(bool),
                            CosmosElementType.Null => typeof(object),
                            CosmosElementType.Number => typeof(Number64),
                            CosmosElementType.Object => typeof(object),
                            CosmosElementType.String => typeof(string),
                            CosmosElementType.Guid => typeof(Guid),
                            CosmosElementType.Binary => typeof(ReadOnlyMemory<byte>),
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
                    return TryCatch<object>.FromException(Visitor.Exceptions.ExpectedBinary);
                }

                return TryCatch<object>.FromResult((object)cosmosBinary.Value);
            }

            public TryCatch<object> Visit(CosmosBoolean cosmosBoolean, Type type)
            {
                if (type != typeof(bool))
                {
                    return TryCatch<object>.FromException(Visitor.Exceptions.ExpectedBoolean);
                }

                return TryCatch<object>.FromResult(cosmosBoolean.Value ? Visitor.BoxedValues.True : Visitor.BoxedValues.False);
            }

            public TryCatch<object> Visit(CosmosGuid cosmosGuid, Type type)
            {
                if (type != typeof(Guid))
                {
                    return TryCatch<object>.FromException(Visitor.Exceptions.ExpectedGuid);
                }

                return TryCatch<object>.FromResult((object)cosmosGuid.Value);
            }

            public TryCatch<object> Visit(CosmosNull cosmosNull, Type type)
            {
                if (type.IsValueType && !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)))
                {
                    return TryCatch<object>.FromException(Visitor.Exceptions.ExpectedReferenceOrNullableType);
                }

                return TryCatch<object>.FromResult(default);
            }

            public TryCatch<object> Visit(CosmosNumber cosmosNumber, Type type)
            {
                if (type == typeof(Number64))
                {
                    return TryCatch<object>.FromResult((object)cosmosNumber.Value);
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

                            return TryCatch<object>.FromResult((object)(byte)value);
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

                            return TryCatch<object>.FromResult((object)value);
                        }

                    case TypeCode.Double:
                        {
                            if (!cosmosNumber.Value.IsDouble)
                            {
                                return TryCatch<object>.FromException(
                                    new CosmosElementWrongTypeException("Expected floating point type for double."));
                            }

                            double value = Number64.ToDouble(cosmosNumber.Value);
                            return TryCatch<object>.FromResult((object)value);
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

                            return TryCatch<object>.FromResult((object)(short)value);
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

                            return TryCatch<object>.FromResult((object)(int)value);
                        }

                    case TypeCode.Int64:
                        {
                            if (!cosmosNumber.Value.IsInteger)
                            {
                                return TryCatch<object>.FromException(
                                    new CosmosElementWrongTypeException("Expected integral type for long."));
                            }

                            long value = Number64.ToLong(cosmosNumber.Value);
                            return TryCatch<object>.FromResult((object)(long)value);
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

                            return TryCatch<object>.FromResult((object)(sbyte)value);
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

                            return TryCatch<object>.FromResult((object)(float)value);
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

                            return TryCatch<object>.FromResult((object)(ushort)value);
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

                            return TryCatch<object>.FromResult((object)(uint)value);
                        }

                    case TypeCode.UInt64:
                        {
                            if (!cosmosNumber.Value.IsInteger)
                            {
                                return TryCatch<object>.FromException(
                                    new CosmosElementWrongTypeException("Expected integral type for ulong."));
                            }

                            long value = Number64.ToLong(cosmosNumber.Value);
                            return TryCatch<object>.FromResult((object)(ulong)value);
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
                    return TryCatch<object>.FromException(Visitor.Exceptions.ExpectedString);
                }

                return TryCatch<object>.FromResult((object)cosmosString.Value);
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
