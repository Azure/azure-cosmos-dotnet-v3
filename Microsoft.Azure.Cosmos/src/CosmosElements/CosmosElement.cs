//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#nullable enable

    using System;
    using System.Globalization;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    [Newtonsoft.Json.JsonConverter(typeof(CosmosElementJsonConverter))]
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    abstract class CosmosElement : IEquatable<CosmosElement>, IComparable<CosmosElement>
    {
        protected static readonly Newtonsoft.Json.JsonSerializer DefaultSerializer = new Newtonsoft.Json.JsonSerializer()
        {
            Culture = CultureInfo.InvariantCulture,
            DateParseHandling = Newtonsoft.Json.DateParseHandling.None,
        };

        protected CosmosElement()
        {
        }

        public override string ToString()
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            this.WriteTo(jsonWriter);

            return Utf8StringHelpers.ToString(jsonWriter.GetResult());
        }

        public override bool Equals(object obj)
        {
            return obj is CosmosElement cosmosElement && this.Equals(cosmosElement);
        }

        public abstract bool Equals(CosmosElement cosmosElement);

        public abstract override int GetHashCode();

        public int CompareTo(CosmosElement other)
        {
            int thisTypeOrder = this.Accept(CosmosElementToTypeOrder.Singleton);
            int otherTypeOrder = other.Accept(CosmosElementToTypeOrder.Singleton);

            if (thisTypeOrder != otherTypeOrder)
            {
                return thisTypeOrder.CompareTo(otherTypeOrder);
            }

            // The types are the same so dispatch to each compare operator
            return this.Accept(CosmosElementWithinTypeComparer.Singleton, other);
        }

        public abstract void WriteTo(IJsonWriter jsonWriter);

        public abstract void Accept(ICosmosElementVisitor cosmosElementVisitor);

        public abstract TResult Accept<TResult>(ICosmosElementVisitor<TResult> cosmosElementVisitor);

        public abstract TResult Accept<TArg, TResult>(ICosmosElementVisitor<TArg, TResult> cosmosElementVisitor, TArg input);

        public virtual T Materialize<T>()
        {
            Cosmos.Json.IJsonReader cosmosJsonReader = this.CreateReader();
            Newtonsoft.Json.JsonReader newtonsoftReader = new CosmosDBToNewtonsoftReader(cosmosJsonReader);

            return DefaultSerializer.Deserialize<T>(newtonsoftReader);
        }

        public virtual IJsonReader CreateReader()
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
            this.WriteTo(jsonWriter);

            ReadOnlyMemory<byte> buffer = jsonWriter.GetResult();

            Cosmos.Json.IJsonReader cosmosJsonReader = Cosmos.Json.JsonReader.Create(buffer);
            return cosmosJsonReader;
        }

        public static class Monadic
        {
            public static TryCatch<TCosmosElement> CreateFromBuffer<TCosmosElement>(ReadOnlyMemory<byte> buffer)
                where TCosmosElement : CosmosElement
            {
                if (buffer.IsEmpty)
                {
                    TryCatch<TCosmosElement>.FromException(
                        new ArgumentException($"{nameof(buffer)} must not be empty."));
                }

                CosmosElement unTypedCosmosElement;
                try
                {
                    IJsonNavigator jsonNavigator = JsonNavigator.Create(buffer);
                    IJsonNavigatorNode jsonNavigatorNode = jsonNavigator.GetRootNode();
                    unTypedCosmosElement = CosmosElement.Dispatch(jsonNavigator, jsonNavigatorNode);
                }
                catch (JsonParseException jpe)
                {
                    return TryCatch<TCosmosElement>.FromException(jpe);
                }

                if (!(unTypedCosmosElement is TCosmosElement typedCosmosElement))
                {
                    return TryCatch<TCosmosElement>.FromException(
                        new CosmosElementWrongTypeException(
                            message: $"buffer was incorrect cosmos element type: {unTypedCosmosElement.GetType()} when {typeof(TCosmosElement)} was requested."));
                }

                return TryCatch<TCosmosElement>.FromResult(typedCosmosElement);
            }

            public static TryCatch<CosmosElement> CreateFromBuffer(ReadOnlyMemory<byte> buffer)
            {
                return CosmosElement.Monadic.CreateFromBuffer<CosmosElement>(buffer);
            }

            public static TryCatch<TCosmosElement> Parse<TCosmosElement>(string serializedCosmosElement)
                where TCosmosElement : CosmosElement
            {
                if (serializedCosmosElement == null)
                {
                    throw new ArgumentNullException(nameof(serializedCosmosElement));
                }

                if (string.IsNullOrWhiteSpace(serializedCosmosElement))
                {
                    return TryCatch<TCosmosElement>.FromException(
                        new ArgumentException($"'{nameof(serializedCosmosElement)}' must not be null, empty, or whitespace."));
                }

                byte[] buffer = Encoding.UTF8.GetBytes(serializedCosmosElement);

                return CosmosElement.Monadic.CreateFromBuffer<TCosmosElement>(buffer);
            }

            public static TryCatch<CosmosElement> Parse(string serializedCosmosElement)
            {
                return CosmosElement.Monadic.Parse<CosmosElement>(serializedCosmosElement);
            }
        }

        public static TCosmosElement CreateFromBuffer<TCosmosElement>(ReadOnlyMemory<byte> buffer)
            where TCosmosElement : CosmosElement
        {
            TryCatch<TCosmosElement> tryCreateFromBuffer = CosmosElement.Monadic.CreateFromBuffer<TCosmosElement>(buffer);
            tryCreateFromBuffer.ThrowIfFailed();

            return tryCreateFromBuffer.Result;
        }

        public static CosmosElement CreateFromBuffer(ReadOnlyMemory<byte> buffer)
        {
            return CosmosElement.CreateFromBuffer<CosmosElement>(buffer);
        }

        public static bool TryCreateFromBuffer<TCosmosElement>(ReadOnlyMemory<byte> buffer, out TCosmosElement cosmosElement)
            where TCosmosElement : CosmosElement
        {
            TryCatch<TCosmosElement> tryCreateFromBuffer = CosmosElement.Monadic.CreateFromBuffer<TCosmosElement>(buffer);
            if (tryCreateFromBuffer.Failed)
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                cosmosElement = default;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                return false;
            }

            cosmosElement = tryCreateFromBuffer.Result;
            return true;
        }

        public static CosmosElement Parse(string json)
        {
            TryCatch<CosmosElement> tryParse = CosmosElement.Monadic.Parse(json);
            tryParse.ThrowIfFailed();

            return tryParse.Result;
        }

        public static TCosmosElement Parse<TCosmosElement>(string json)
            where TCosmosElement : CosmosElement
        {
            TryCatch<TCosmosElement> tryParse = CosmosElement.Monadic.Parse<TCosmosElement>(json);
            tryParse.ThrowIfFailed();

            return tryParse.Result;
        }

        public static bool TryParse<TCosmosElement>(string json, out TCosmosElement cosmosElement)
            where TCosmosElement : CosmosElement
        {
            TryCatch<TCosmosElement> tryParse = CosmosElement.Monadic.Parse<TCosmosElement>(json);
            if (tryParse.Failed)
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                cosmosElement = default;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                return false;
            }

            cosmosElement = tryParse.Result;
            return true;
        }

        public static CosmosElement Dispatch(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            JsonNodeType jsonNodeType = jsonNavigator.GetNodeType(jsonNavigatorNode);
            return jsonNodeType switch
            {
                JsonNodeType.Null => CosmosNull.Create(),
                JsonNodeType.False => CosmosBoolean.Create(false),
                JsonNodeType.True => CosmosBoolean.Create(true),
                JsonNodeType.Number => CosmosNumber64.Create(jsonNavigator, jsonNavigatorNode),
                JsonNodeType.FieldName => CosmosString.Create(jsonNavigator, jsonNavigatorNode),
                JsonNodeType.String => CosmosString.Create(jsonNavigator, jsonNavigatorNode),
                JsonNodeType.Array => CosmosArray.Create(jsonNavigator, jsonNavigatorNode),
                JsonNodeType.Object => CosmosObject.Create(jsonNavigator, jsonNavigatorNode),
                JsonNodeType.Int8 => CosmosInt8.Create(jsonNavigator, jsonNavigatorNode),
                JsonNodeType.Int16 => CosmosInt16.Create(jsonNavigator, jsonNavigatorNode),
                JsonNodeType.Int32 => CosmosInt32.Create(jsonNavigator, jsonNavigatorNode),
                JsonNodeType.Int64 => CosmosInt64.Create(jsonNavigator, jsonNavigatorNode),
                JsonNodeType.UInt32 => CosmosUInt32.Create(jsonNavigator, jsonNavigatorNode),
                JsonNodeType.Float32 => CosmosFloat32.Create(jsonNavigator, jsonNavigatorNode),
                JsonNodeType.Float64 => CosmosFloat64.Create(jsonNavigator, jsonNavigatorNode),
                JsonNodeType.Guid => CosmosGuid.Create(jsonNavigator, jsonNavigatorNode),
                JsonNodeType.Binary => CosmosBinary.Create(jsonNavigator, jsonNavigatorNode),
                _ => throw new ArgumentException($"Unknown {nameof(JsonNodeType)}: {jsonNodeType}")
            };
        }

        public static bool operator ==(CosmosElement a, CosmosElement b)
        {
            if (object.ReferenceEquals(a, b))
            {
                return true;
            }

            if ((a is null) || (b is null))
            {
                return false;
            }

            return a.Equals(b);
        }

        public static bool operator !=(CosmosElement a, CosmosElement b)
        {
            return !(a == b);
        }

        private sealed class CosmosElementToTypeOrder : ICosmosElementVisitor<int>
        {
            public static readonly CosmosElementToTypeOrder Singleton = new CosmosElementToTypeOrder();

            private CosmosElementToTypeOrder()
            {
            }

            public int Visit(CosmosUndefined cosmosUndefined)
            {
                return 0;
            }

            public int Visit(CosmosNull cosmosNull)
            {
                return 1;
            }

            public int Visit(CosmosBoolean cosmosBoolean)
            {
                return 2;
            }

            public int Visit(CosmosNumber cosmosNumber)
            {
                return 3;
            }

            public int Visit(CosmosString cosmosString)
            {
                return 4;
            }

            public int Visit(CosmosArray cosmosArray)
            {
                return 5;
            }

            public int Visit(CosmosObject cosmosObject)
            {
                return 6;
            }

            public int Visit(CosmosGuid cosmosGuid)
            {
                return 7;
            }

            public int Visit(CosmosBinary cosmosBinary)
            {
                return 8;
            }
        }

        private sealed class CosmosElementWithinTypeComparer : ICosmosElementVisitor<CosmosElement, int>
        {
            public static readonly CosmosElementWithinTypeComparer Singleton = new CosmosElementWithinTypeComparer();

            private CosmosElementWithinTypeComparer()
            {
            }

            public int Visit(CosmosUndefined cosmosUndefined, CosmosElement input)
            {
                return cosmosUndefined.CompareTo((CosmosUndefined)input);
            }

            public int Visit(CosmosArray cosmosArray, CosmosElement input)
            {
                return cosmosArray.CompareTo((CosmosArray)input);
            }

            public int Visit(CosmosBinary cosmosBinary, CosmosElement input)
            {
                return cosmosBinary.CompareTo((CosmosBinary)input);
            }

            public int Visit(CosmosBoolean cosmosBoolean, CosmosElement input)
            {
                return cosmosBoolean.CompareTo((CosmosBoolean)input);
            }

            public int Visit(CosmosGuid cosmosGuid, CosmosElement input)
            {
                return cosmosGuid.CompareTo((CosmosGuid)input);
            }

            public int Visit(CosmosNull cosmosNull, CosmosElement input)
            {
                return cosmosNull.CompareTo((CosmosNull)input);
            }

            public int Visit(CosmosNumber cosmosNumber, CosmosElement input)
            {
                return cosmosNumber.CompareTo((CosmosNumber)input);
            }

            public int Visit(CosmosObject cosmosObject, CosmosElement input)
            {
                return cosmosObject.CompareTo((CosmosObject)input);
            }

            public int Visit(CosmosString cosmosString, CosmosElement input)
            {
                return cosmosString.CompareTo((CosmosString)input);
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
