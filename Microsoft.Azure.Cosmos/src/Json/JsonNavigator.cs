//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Core.Utf8;

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
        /// <returns>A concrete JsonNavigator that can navigate the supplied buffer.</returns>
        public static IJsonNavigator Create(
            ReadOnlyMemory<byte> buffer)
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
                JsonSerializationFormat.Binary => new JsonBinaryNavigator(buffer),

                // or otherwise assume text format
                _ => new JsonTextNavigator(buffer),
            };
        }

        #region IJsonNavigator
        /// <inheritdoc />
        public abstract IJsonNavigatorNode GetRootNode();

        /// <inheritdoc />
        public abstract JsonNodeType GetNodeType(IJsonNavigatorNode node);

        /// <inheritdoc />
        public abstract Number64 GetNumberValue(IJsonNavigatorNode numberNode);

        /// <inheritdoc />
        public abstract bool TryGetBufferedStringValue(IJsonNavigatorNode stringNode, out Utf8Memory bufferedStringValue);

        /// <inheritdoc />
        public abstract UtfAnyString GetStringValue(IJsonNavigatorNode stringNode);

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
        public virtual void WriteNode(IJsonNavigatorNode node, IJsonWriter jsonWriter)
        {
            JsonNodeType nodeType = this.GetNodeType(node);
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

                case JsonNodeType.Number:
                    if (this.TryGetUInt64Value(node, out ulong uint64Value))
                    {
                        jsonWriter.WriteNumberValue(uint64Value);
                    }
                    else
                    {
                        Number64 value = this.GetNumberValue(node);
                        jsonWriter.WriteNumberValue(value);
                    }
                    break;

                case JsonNodeType.String:
                case JsonNodeType.FieldName:
                    bool fieldName = nodeType == JsonNodeType.FieldName;
                    if (this.TryGetBufferedStringValue(node, out Utf8Memory bufferedStringValue))
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
                        string value = this.GetStringValue(node);
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

                        foreach (IJsonNavigatorNode arrayItem in this.GetArrayItems(node))
                        {
                            this.WriteNode(arrayItem, jsonWriter);
                        }

                        jsonWriter.WriteArrayEnd();
                    }
                    break;

                case JsonNodeType.Object:
                    {
                        jsonWriter.WriteObjectStart();

                        foreach (ObjectProperty objectProperty in this.GetObjectProperties(node))
                        {
                            this.WriteNode(objectProperty.NameNode, jsonWriter);
                            this.WriteNode(objectProperty.ValueNode, jsonWriter);
                        }

                        jsonWriter.WriteObjectEnd();
                    }
                    break;

                case JsonNodeType.Int8:
                    {
                        sbyte value = this.GetInt8Value(node);
                        jsonWriter.WriteInt8Value(value);
                    }
                    break;

                case JsonNodeType.Int16:
                    {
                        short value = this.GetInt16Value(node);
                        jsonWriter.WriteInt16Value(value);
                    }
                    break;

                case JsonNodeType.Int32:
                    {
                        int value = this.GetInt32Value(node);
                        jsonWriter.WriteInt32Value(value);
                    }
                    break;

                case JsonNodeType.Int64:
                    {
                        long value = this.GetInt64Value(node);
                        jsonWriter.WriteInt64Value(value);
                    }
                    break;

                case JsonNodeType.UInt32:
                    {
                        uint value = this.GetUInt32Value(node);
                        jsonWriter.WriteUInt32Value(value);
                    }
                    break;

                case JsonNodeType.Float32:
                    {
                        float value = this.GetFloat32Value(node);
                        jsonWriter.WriteFloat32Value(value);
                    }
                    break;

                case JsonNodeType.Float64:
                    {
                        double value = this.GetFloat64Value(node);
                        jsonWriter.WriteFloat64Value(value);
                    }
                    break;

                case JsonNodeType.Binary:
                    {
                        ReadOnlyMemory<byte> value = this.GetBinaryValue(node);
                        jsonWriter.WriteBinaryValue(value.Span);
                    }
                    break;

                case JsonNodeType.Guid:
                    {
                        Guid value = this.GetGuidValue(node);
                        jsonWriter.WriteGuidValue(value);
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(JsonNodeType)}: {nodeType}.");
            }
        }

        /// <inheritdoc />
        public abstract IJsonReader CreateReader(IJsonNavigatorNode node);
        #endregion

        /// <summary>
        /// Attempts to read the specified number node as an unsigned 64-bit integer.
        /// </summary>
        /// <param name="numberNode">The number <see cref="IJsonNavigatorNode"/> to retrieve its value.</param>
        /// <param name="value">When this method returns, contains the value of the specified number node if it was an unsigned 64-bit integer; otherwise, the default value of <c>ulong</c>.</param>
        /// <returns><c>true</c> if the number node value is an unsigned 64-bit integer; otherwise, <c>false</c>.</returns>
        protected abstract bool TryGetUInt64Value(IJsonNavigatorNode numberNode, out ulong value);
    }
}
