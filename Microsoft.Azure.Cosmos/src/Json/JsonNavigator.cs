//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Base abstract class for JSON navigators.
    /// The navigator defines methods that allow random access to JSON document nodes.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    abstract partial class JsonNavigator : IJsonNavigator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonNavigator"/> class.
        /// </summary>
        protected JsonNavigator()
        {
        }

        /// <inheritdoc />
        public abstract JsonSerializationFormat SerializationFormat { get; }

        /// <summary>
        /// Creates a JsonNavigator that can navigate a supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer to navigate</param>
        /// <param name="jsonStringDictionary">The optional json string dictionary for binary encoding.</param>
        /// <returns>A concrete JsonNavigator that can navigate the supplied buffer.</returns>
        public static IJsonNavigator Create(
            ReadOnlyMemory<byte> buffer,
            JsonStringDictionary jsonStringDictionary = null)
        {
            if (buffer.IsEmpty)
            {
                throw new ArgumentOutOfRangeException($"{nameof(buffer)} can not be empty.");
            }

            // Examine the first buffer byte to determine the serialization format
            byte firstByte = buffer.Span[0];

            return ((JsonSerializationFormat)firstByte) switch
            {
                // Explicitly pick from the set of supported formats
                JsonSerializationFormat.Binary => new JsonBinaryNavigator(buffer, jsonStringDictionary),

                // or otherwise assume text format
                _ => new JsonTextNavigator(buffer),
            };
        }

        /// <inheritdoc />
        public abstract IJsonNavigatorNode GetRootNode();

        /// <inheritdoc />
        public abstract JsonNodeType GetNodeType(IJsonNavigatorNode node);

        /// <inheritdoc />
        public abstract Number64 GetNumber64Value(IJsonNavigatorNode numberNode);

        /// <inheritdoc />
        public abstract bool TryGetBufferedStringValue(IJsonNavigatorNode stringNode, out Utf8Memory bufferedStringValue);

        /// <inheritdoc />
        public abstract string GetStringValue(IJsonNavigatorNode stringNode);

        /// <inheritdoc />
        public abstract sbyte GetInt8Value(IJsonNavigatorNode numberNode);

        /// <inheritdoc />
        public abstract short GetInt16Value(IJsonNavigatorNode numberNode);

        /// <inheritdoc />
        public abstract int GetInt32Value(IJsonNavigatorNode numberNode);

        /// <inheritdoc />
        public abstract long GetInt64Value(IJsonNavigatorNode numberNode);

        /// <inheritdoc />
        public abstract float GetFloat32Value(IJsonNavigatorNode numberNode);

        /// <inheritdoc />
        public abstract double GetFloat64Value(IJsonNavigatorNode numberNode);

        /// <inheritdoc />
        public abstract uint GetUInt32Value(IJsonNavigatorNode numberNode);

        /// <inheritdoc />
        public abstract Guid GetGuidValue(IJsonNavigatorNode guidNode);

        /// <inheritdoc />
        public abstract ReadOnlyMemory<byte> GetBinaryValue(IJsonNavigatorNode binaryNode);

        /// <inheritdoc />
        public abstract bool TryGetBufferedBinaryValue(IJsonNavigatorNode binaryNode, out ReadOnlyMemory<byte> bufferedBinaryValue);

        /// <inheritdoc />
        public abstract int GetArrayItemCount(IJsonNavigatorNode arrayNode);

        /// <inheritdoc />
        public abstract IJsonNavigatorNode GetArrayItemAt(IJsonNavigatorNode arrayNode, int index);

        /// <inheritdoc />
        public abstract IEnumerable<IJsonNavigatorNode> GetArrayItems(IJsonNavigatorNode arrayNode);

        /// <inheritdoc />
        public abstract int GetObjectPropertyCount(IJsonNavigatorNode objectNode);

        /// <inheritdoc />
        public abstract bool TryGetObjectProperty(IJsonNavigatorNode objectNode, string propertyName, out ObjectProperty objectProperty);

        /// <inheritdoc />
        public abstract IEnumerable<ObjectProperty> GetObjectProperties(IJsonNavigatorNode objectNode);

        /// <inheritdoc />
        public abstract bool TryGetBufferedRawJson(IJsonNavigatorNode jsonNode, out ReadOnlyMemory<byte> bufferedRawJson);

        /// <inheritdoc />
        public virtual void WriteTo(IJsonNavigatorNode jsonNavigatorNode, IJsonWriter jsonWriter)
        {
            JsonNodeType nodeType = this.GetNodeType(jsonNavigatorNode);
            switch (nodeType)
            {
                case JsonNodeType.Null:
                    jsonWriter.WriteNullValue();
                    return;

                case JsonNodeType.False:
                    jsonWriter.WriteBoolValue(false);
                    return;

                case JsonNodeType.True:
                    jsonWriter.WriteBoolValue(true);
                    return;
            }

            bool sameEncoding = this.SerializationFormat == jsonWriter.SerializationFormat;
            if (sameEncoding && this.TryGetBufferedRawJson(jsonNavigatorNode, out ReadOnlyMemory<byte> bufferedRawJson))
            {
                // Token type doesn't make any difference other than whether it's a value or field name
                JsonTokenType tokenType = nodeType == JsonNodeType.FieldName ? JsonTokenType.FieldName : JsonTokenType.String;
                jsonWriter.WriteRawJsonToken(tokenType, bufferedRawJson.Span);
                return;
            }

            switch (nodeType)
            {
                case JsonNodeType.Number64:
                    {
                        Number64 value = this.GetNumber64Value(jsonNavigatorNode);
                        jsonWriter.WriteNumber64Value(value);
                    }
                    break;

                case JsonNodeType.String:
                case JsonNodeType.FieldName:
                    bool fieldName = nodeType == JsonNodeType.FieldName;
                    if (this.TryGetBufferedStringValue(jsonNavigatorNode, out Utf8Memory bufferedStringValue))
                    {
                        if (fieldName)
                        {
                            jsonWriter.WriteFieldName(bufferedStringValue.Span);
                        }
                        else
                        {
                            jsonWriter.WriteStringValue(bufferedStringValue.Span);
                        }
                    }
                    else
                    {
                        string value = this.GetStringValue(jsonNavigatorNode);
                        if (fieldName)
                        {
                            jsonWriter.WriteFieldName(value);
                        }
                        else
                        {
                            jsonWriter.WriteStringValue(value);
                        }
                    }
                    break;

                case JsonNodeType.Array:
                    {
                        jsonWriter.WriteArrayStart();

                        foreach (IJsonNavigatorNode arrayItem in this.GetArrayItems(jsonNavigatorNode))
                        {
                            this.WriteTo(arrayItem, jsonWriter);
                        }

                        jsonWriter.WriteArrayEnd();
                    }
                    break;

                case JsonNodeType.Object:
                    {
                        jsonWriter.WriteObjectStart();

                        foreach (ObjectProperty objectProperty in this.GetObjectProperties(jsonNavigatorNode))
                        {
                            this.WriteTo(objectProperty.NameNode, jsonWriter);
                            this.WriteTo(objectProperty.ValueNode, jsonWriter);
                        }

                        jsonWriter.WriteObjectEnd();
                    }
                    break;

                case JsonNodeType.Int8:
                    {
                        sbyte value = this.GetInt8Value(jsonNavigatorNode);
                        jsonWriter.WriteInt8Value(value);
                    }
                    break;

                case JsonNodeType.Int16:
                    {
                        short value = this.GetInt16Value(jsonNavigatorNode);
                        jsonWriter.WriteInt16Value(value);
                    }
                    break;

                case JsonNodeType.Int32:
                    {
                        int value = this.GetInt32Value(jsonNavigatorNode);
                        jsonWriter.WriteInt32Value(value);
                    }
                    break;

                case JsonNodeType.Int64:
                    {
                        long value = this.GetInt64Value(jsonNavigatorNode);
                        jsonWriter.WriteInt64Value(value);
                    }
                    break;

                case JsonNodeType.UInt32:
                    {
                        uint value = this.GetUInt32Value(jsonNavigatorNode);
                        jsonWriter.WriteUInt32Value(value);
                    }
                    break;

                case JsonNodeType.Float32:
                    {
                        float value = this.GetFloat32Value(jsonNavigatorNode);
                        jsonWriter.WriteFloat32Value(value);
                    }
                    break;

                case JsonNodeType.Float64:
                    {
                        double value = this.GetFloat64Value(jsonNavigatorNode);
                        jsonWriter.WriteFloat64Value(value);
                    }
                    break;

                case JsonNodeType.Binary:
                    {
                        ReadOnlyMemory<byte> value = this.GetBinaryValue(jsonNavigatorNode);
                        jsonWriter.WriteBinaryValue(value.Span);
                    }
                    break;

                case JsonNodeType.Guid:
                    {
                        Guid value = this.GetGuidValue(jsonNavigatorNode);
                        jsonWriter.WriteGuidValue(value);
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(JsonNodeType)}: {nodeType}.");
            }
        }
    }
}
