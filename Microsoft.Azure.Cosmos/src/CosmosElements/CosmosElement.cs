//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Runtime.ExceptionServices;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    [Newtonsoft.Json.JsonConverter(typeof(CosmosElementJsonConverter))]
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    abstract class CosmosElement
    {
        protected CosmosElement(CosmosElementType cosmosItemType)
        {
            this.Type = cosmosItemType;
        }

        public CosmosElementType Type { get; }

        public override string ToString()
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            this.WriteTo(jsonWriter);

            return Utf8StringHelpers.ToString(jsonWriter.GetResult());
        }

        public override bool Equals(object obj)
        {
            if (!(obj is CosmosElement cosmosElement))
            {
                return false;
            }

            return this.Equals(cosmosElement);
        }

        public bool Equals(CosmosElement cosmosElement)
        {
            return CosmosElementEqualityComparer.Value.Equals(this, cosmosElement);
        }

        public override int GetHashCode()
        {
            return CosmosElementEqualityComparer.Value.GetHashCode(this);
        }

        public abstract void WriteTo(IJsonWriter jsonWriter);

        public abstract void Accept(ICosmosElementVisitor cosmosElementVisitor);

        public abstract TResult Accept<TResult>(ICosmosElementVisitor<TResult> cosmosElementVisitor);

        public abstract TResult Accept<TArg, TResult>(ICosmosElementVisitor<TArg, TResult> cosmosElementVisitor, TArg input);

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
                cosmosElement = default;
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
                cosmosElement = default;
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
            CosmosElement item;
            switch (jsonNodeType)
            {
                case JsonNodeType.Null:
                    item = CosmosNull.Create();
                    break;

                case JsonNodeType.False:
                    item = CosmosBoolean.Create(false);
                    break;

                case JsonNodeType.True:
                    item = CosmosBoolean.Create(true);
                    break;

                case JsonNodeType.Number64:
                    item = CosmosNumber64.Create(jsonNavigator, jsonNavigatorNode);
                    break;

                case JsonNodeType.FieldName:
                case JsonNodeType.String:
                    item = CosmosString.Create(jsonNavigator, jsonNavigatorNode);
                    break;

                case JsonNodeType.Array:
                    item = CosmosArray.Create(jsonNavigator, jsonNavigatorNode);
                    break;

                case JsonNodeType.Object:
                    item = CosmosObject.Create(jsonNavigator, jsonNavigatorNode);
                    break;

                case JsonNodeType.Int8:
                    item = CosmosInt8.Create(jsonNavigator, jsonNavigatorNode);
                    break;

                case JsonNodeType.Int16:
                    item = CosmosInt16.Create(jsonNavigator, jsonNavigatorNode);
                    break;

                case JsonNodeType.Int32:
                    item = CosmosInt32.Create(jsonNavigator, jsonNavigatorNode);
                    break;

                case JsonNodeType.Int64:
                    item = CosmosInt64.Create(jsonNavigator, jsonNavigatorNode);
                    break;

                case JsonNodeType.UInt32:
                    item = CosmosUInt32.Create(jsonNavigator, jsonNavigatorNode);
                    break;

                case JsonNodeType.Float32:
                    item = CosmosFloat32.Create(jsonNavigator, jsonNavigatorNode);
                    break;

                case JsonNodeType.Float64:
                    item = CosmosFloat64.Create(jsonNavigator, jsonNavigatorNode);
                    break;

                case JsonNodeType.Guid:
                    item = CosmosGuid.Create(jsonNavigator, jsonNavigatorNode);
                    break;

                case JsonNodeType.Binary:
                    item = CosmosBinary.Create(jsonNavigator, jsonNavigatorNode);
                    break;

                default:
                    throw new ArgumentException($"Unknown {nameof(JsonNodeType)}: {jsonNodeType}");
            }

            return item;
        }
    }
}
