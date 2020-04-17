//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Json;

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

        public static bool TryCreateFromBuffer<TCosmosElement>(ReadOnlyMemory<byte> buffer, out TCosmosElement cosmosElement)
            where TCosmosElement : CosmosElement
        {
            CosmosElement unTypedCosmosElement;
            try
            {
                unTypedCosmosElement = CreateFromBuffer(buffer);
            }
            catch (JsonParseException)
            {
                cosmosElement = default;
                return false;
            }

            if (!(unTypedCosmosElement is TCosmosElement typedCosmosElement))
            {
                cosmosElement = default;
                return false;
            }

            cosmosElement = typedCosmosElement;
            return true;
        }

        public static CosmosElement CreateFromBuffer(ReadOnlyMemory<byte> buffer)
        {
            IJsonNavigator jsonNavigator = JsonNavigator.Create(buffer);
            IJsonNavigatorNode jsonNavigatorNode = jsonNavigator.GetRootNode();

            return CosmosElement.Dispatch(jsonNavigator, jsonNavigatorNode);
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

        public static bool TryParse(
            string serializedCosmosElement,
            out CosmosElement cosmosElement)
        {
            if (string.IsNullOrWhiteSpace(serializedCosmosElement))
            {
                cosmosElement = default;
                return false;
            }

            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(serializedCosmosElement);
                cosmosElement = CosmosElement.CreateFromBuffer(buffer);
            }
            catch (JsonParseException)
            {
                cosmosElement = default;
            }

            return cosmosElement != default;
        }

        public static bool TryParse<TCosmosElement>(string serializedCosmosElement, out TCosmosElement cosmosElement)
            where TCosmosElement : CosmosElement
        {
            if (!CosmosElement.TryParse(serializedCosmosElement, out CosmosElement rawCosmosElement))
            {
                cosmosElement = default(TCosmosElement);
                return false;
            }

            if (!(rawCosmosElement is TCosmosElement typedCosmosElement))
            {
                cosmosElement = default(TCosmosElement);
                return false;
            }

            cosmosElement = typedCosmosElement;
            return true;
        }

        public static CosmosElement Parse(string json)
        {
            if (!CosmosElement.TryParse(json, out CosmosElement cosmosElement))
            {
                throw new ArgumentException($"Failed to parse json: {json}.");
            }

            return cosmosElement;
        }

        public static TCosmosElement Parse<TCosmosElement>(string json)
            where TCosmosElement : CosmosElement
        {
            if (!CosmosElement.TryParse(json, out TCosmosElement cosmosElement))
            {
                throw new ArgumentException($"Failed to parse json: {json}.");
            }

            return cosmosElement;
        }
    }
#if INTERNAL
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
